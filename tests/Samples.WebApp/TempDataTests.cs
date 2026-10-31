using System.Collections;
using System.Net;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Mvc.ViewFeatures.Infrastructure;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace RazorSlices.Samples.WebApp.Tests;

public class TempDataTests
{
    [Fact]
    public async Task CookieFlashMessageIsSavedBeforeRedirectAndConsumedBeforeRendering()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using var redirect = await client.PostAsync("/tempdata", null);
        Assert.Equal(HttpStatusCode.Redirect, redirect.StatusCode);
        Assert.Equal("/tempdata", redirect.Headers.Location?.OriginalString);
        Assert.Contains(redirect.Headers.GetValues("Set-Cookie"),
            cookie => cookie.StartsWith(CookieTempDataProvider.CookieName, StringComparison.Ordinal));

        using var page = await client.GetAsync("/tempdata");
        page.EnsureSuccessStatusCode();
        Assert.Contains("Changes saved. &lt;This message is HTML encoded.&gt;",
            await page.Content.ReadAsStringAsync());
        Assert.True(page.Headers.Contains("Set-Cookie"));
        Assert.Contains("No flash message.", await client.GetStringAsync("/tempdata"));
    }

    [Fact]
    public async Task KeepPreservesMessageForTheNextRequest()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var redirect = await client.PostAsync("/tempdata", null);
        Assert.Equal(HttpStatusCode.Redirect, redirect.StatusCode);
        Assert.Contains("Changes saved.", await client.GetStringAsync("/tempdata?keep=true"));
        Assert.Contains("Changes saved.", await client.GetStringAsync("/tempdata"));
        Assert.Contains("No flash message.", await client.GetStringAsync("/tempdata"));
    }

    [Fact]
    public async Task AsyncHandlerFinishesBeforeSaveAndReturnsTheSameResult()
    {
        var provider = new RecordingProvider();
        var expected = TypedResults.Redirect("/next");
        await using var app = await CreateApp(provider, app =>
            app.MapGet("/", async (TempData data) =>
            {
                data["message"] = "before await";
                await Task.Yield();
                Assert.Equal(0, provider.SaveCount);
                data["message"] = "after await";
                return expected;
            }).WithTempData().AddEndpointFilter(async (context, next) =>
            {
                var result = await next(context);
                Assert.Same(expected, result);
                Assert.Equal(0, provider.SaveCount);
                return result;
            }));
        using var client = app.GetTestClient();
        using var response = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(1, provider.SaveCount);
        Assert.Equal("after await", provider.SavedValues["message"]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnusedTempDataDoesNotResolveTheFactoryOrLoadOrSave(bool bindParameter)
    {
        var provider = new RecordingProvider();
        await using var app = await CreateApp(provider, app =>
        {
            if (bindParameter) { app.MapGet("/", (TempData data) => TypedResults.NoContent()).WithTempData(); }
            else { app.MapGet("/", () => TypedResults.NoContent()).WithTempData(); }
        }, registerServices: false);
        using var client = app.GetTestClient();
        using var response = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(0, provider.LoadCount);
        Assert.Equal(0, provider.SaveCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RedirectsDoNotImplicitlyRetainReadValuesButPeekDoes(bool peek)
    {
        var provider = new RecordingProvider();
        await using var app = await CreateApp(provider, app =>
            app.MapGet("/", (TempData data) =>
            {
                data["message"] = "hello";
                Assert.Equal("hello", peek ? data.Peek("message") : data["message"]);
                return TypedResults.Redirect("/next");
            }).WithTempData());
        using var client = app.GetTestClient();
        using var response = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(peek, provider.SavedValues.ContainsKey("message"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HandlerFailuresCloseAccessAndDoNotSave(bool cancel)
    {
        var provider = new RecordingProvider();
        TempData? captured = null;
        await using var app = await CreateApp(provider, app =>
            app.MapGet("/", async Task<IResult> (TempData data) =>
            {
                captured = data;
                data["message"] = "not saved";
                await Task.Yield();
                if (cancel) { throw new OperationCanceledException(); }
                throw new InvalidOperationException("handler failed");
            }).WithTempData());
        using var client = app.GetTestClient();
        if (cancel) { await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GetAsync("/")); }
        else { await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetAsync("/")); }
        Assert.Equal(0, provider.SaveCount);
        Assert.NotNull(captured);
        AssertClosed(captured);
    }

    [Fact]
    public async Task BoundParameterRequiresFilterEvenIfTheHandlerDoesNotAccessIt()
    {
        var provider = new RecordingProvider();
        var handlerRan = false;
        await using var app = await CreateApp(provider, app =>
            app.MapGet("/", (TempData data) =>
            {
                handlerRan = true;
                return TypedResults.NoContent();
            }));
        using var client = app.GetTestClient();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetAsync("/"));
        Assert.Contains(".WithTempData()", error.Message);
        Assert.False(handlerRan);
        Assert.Equal(0, provider.LoadCount);
        Assert.Equal(0, provider.SaveCount);
    }

    [Fact]
    public async Task AccessWithoutRegisteredServicesFailsClearly()
    {
        await using var app = await CreateApp(new RecordingProvider(), app =>
            app.MapGet("/", (TempData data) => TypedResults.Text((string?)data["message"] ?? "missing"))
                .WithTempData(), registerServices: false);
        using var client = app.GetTestClient();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetAsync("/"));
        Assert.Contains("AddRazorSliceTempData()", error.Message);
    }

    [Fact]
    public async Task SessionProviderEstablishesANewSessionBeforeResultExecution()
    {
        await using var app = await CreateApp(new RecordingProvider(), app =>
        {
            app.UseSession();
            var group = app.MapGroup("/session").WithTempData();
            group.MapGet("/set", (TempData data) =>
            {
                data["message"] = "session flash";
                return TypedResults.NoContent();
            });
            group.MapGet("/read", (TempData data) =>
                TypedResults.Text((string?)data["message"] ?? "missing"));
        }, configureServices: services =>
        {
            services.AddDistributedMemoryCache();
            services.AddSession();
            services.AddSingleton<ITempDataProvider, SessionStateTempDataProvider>();
        });
        using var client = app.GetTestClient();
        using var response = await client.GetAsync("/session/set");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        Assert.StartsWith(".AspNetCore.Session=", cookie);
        client.DefaultRequestHeaders.Add("Cookie", cookie.Split(';')[0]);
        Assert.Equal("session flash", await client.GetStringAsync("/session/read"));
        Assert.Equal("missing", await client.GetStringAsync("/session/read"));
    }

    [Fact]
    public async Task ParameterCannotBeAccessedBeforeTheFilterActivates()
    {
        var provider = new RecordingProvider();
        await using var app = await CreateApp(provider, app =>
            app.MapGet("/", (TempData data) => TypedResults.NoContent())
                .AddEndpointFilter((context, next) =>
                {
                    AssertClosed(context.GetArgument<TempData>(0));
                    return next(context);
                }).WithTempData());
        using var client = app.GetTestClient();
        using var response = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(0, provider.LoadCount);
    }

    [Fact]
    public async Task CapturedDictionaryViewsAndEnumeratorsAreClosedBeforeResultExecution()
    {
        var provider = new RecordingProvider();
        await using var app = await CreateApp(provider, app =>
            app.MapGet("/", (TempData data) =>
            {
                data["message"] = "hello";
                var keys = data.Keys;
                var values = data.Values;
                var entries = data.GetEnumerator();
                var keyEnumerator = keys.GetEnumerator();
                var valueEnumerator = values.GetEnumerator();
                Assert.True(entries.MoveNext());
                Assert.True(keyEnumerator.MoveNext());
                Assert.True(valueEnumerator.MoveNext());
                Assert.Equal("hello", entries.Current.Value);
                data.Keep();
                return new CheckingResult(() =>
                {
                    Assert.Equal(1, provider.SaveCount);
                    AssertClosed(data);
                    Assert.Throws<InvalidOperationException>(() => keys.Count);
                    Assert.Throws<InvalidOperationException>(() => values.Count);
                    Assert.Throws<InvalidOperationException>(() => keys.GetEnumerator());
                    Assert.Throws<InvalidOperationException>(() => values.GetEnumerator());
                    Assert.Throws<InvalidOperationException>(() => keys.Contains("message"));
                    Assert.Throws<InvalidOperationException>(() => values.CopyTo(new object?[1], 0));
                    AssertEnumeratorClosed(entries);
                    AssertEnumeratorClosed(keyEnumerator);
                    AssertEnumeratorClosed(valueEnumerator);
                });
            }).WithTempData());
        using var client = app.GetTestClient();
        using var response = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("hello", provider.SavedValues["message"]);
    }

    [Fact]
    public async Task EnumerationPreservesMvcConsumptionSemantics()
    {
        var provider = new RecordingProvider();
        await using var app = await CreateApp(provider, app =>
            app.MapGet("/", (TempData data) =>
            {
                data["message"] = "hello";
                foreach (var entry in data) { Assert.Equal("hello", entry.Value); }
                return TypedResults.NoContent();
            }).WithTempData());
        using var client = app.GetTestClient();
        using var response = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(provider.SavedValues);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ResponseStartingInsideHandlerRejectsAccessAndSaving(bool accessAfterStart)
    {
        var provider = new RecordingProvider();
        TempData? captured = null;
        await using var app = await CreateApp(provider, app =>
            app.MapGet("/", (TempData data, HttpContext context) =>
            {
                captured = data;
                data["message"] = "too late";
                context.Features.Set<IHttpResponseFeature>(new StartedResponseFeature());
                if (accessAfterStart) { _ = data["message"]; }
                return TypedResults.NoContent();
            }).WithTempData());
        using var client = app.GetTestClient();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetAsync("/"));
        Assert.Contains("response", error.Message);
        Assert.Equal(0, provider.SaveCount);
        Assert.NotNull(captured);
        AssertClosed(captured);
    }

    [Fact]
    public async Task GroupAndEndpointFiltersShareOneDictionaryAndSaveOnce()
    {
        var provider = new RecordingProvider();
        await using var app = await CreateApp(provider, app =>
        {
            var group = app.MapGroup("/group").WithTempData();
            group.MapGet("/", (TempData first, TempData second) =>
            {
                Assert.Same(first, second);
                first["message"] = "shared";
                Assert.Equal("shared", second.Peek("message"));
                return TypedResults.NoContent();
            }).WithTempData().WithTempData();
        });
        using var client = app.GetTestClient();
        using var response = await client.GetAsync("/group/");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(1, provider.LoadCount);
        Assert.Equal(1, provider.SaveCount);
    }

    [Fact]
    public async Task SaveFailurePropagatesAndClosesAccessWithoutExecutingResult()
    {
        var provider = new RecordingProvider { FailSave = true };
        TempData? captured = null;
        var resultExecuted = false;
        await using var app = await CreateApp(provider, app =>
            app.MapGet("/", (TempData data) =>
            {
                captured = data;
                data["message"] = "cannot save";
                return new CheckingResult(() => resultExecuted = true);
            }).WithTempData());
        using var client = app.GetTestClient();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetAsync("/"));
        Assert.Equal("save failed", error.Message);
        Assert.False(resultExecuted);
        Assert.NotNull(captured);
        AssertClosed(captured);
    }

    [Fact]
    public async Task ExistingServicesArePreservedAndFactorySharesRequestDictionary()
    {
        var provider = new RecordingProvider();
        var serializer = new TestSerializer();
        var dictionaryFactory = new TempDataDictionaryFactory(provider);
        await using var app = await CreateApp(provider, app =>
            app.MapGet("/", (TempData data, HttpContext context) =>
            {
                var raw = dictionaryFactory.GetTempData(context);
                raw["message"] = "shared with MVC";
                Assert.Equal("shared with MVC", data.Peek("message"));
                return TypedResults.NoContent();
            }).WithTempData(), configureServices: services =>
            {
                services.AddSingleton<TempDataSerializer>(serializer);
                services.AddSingleton<ITempDataDictionaryFactory>(dictionaryFactory);
            });
        Assert.Same(provider, app.Services.GetRequiredService<ITempDataProvider>());
        Assert.Same(serializer, app.Services.GetRequiredService<TempDataSerializer>());
        Assert.Same(dictionaryFactory, app.Services.GetRequiredService<ITempDataDictionaryFactory>());
        using var client = app.GetTestClient();
        using var response = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("shared with MVC", provider.SavedValues["message"]);
    }

    [Fact]
    public void DefaultSerializerRoundTripsStringsAndNullAndRejectsOtherTypes()
    {
        using var services = new ServiceCollection().AddRazorSliceTempData().BuildServiceProvider();
        var serializer = services.GetRequiredService<TempDataSerializer>();
        var values = new Dictionary<string, object?> { ["message"] = "<hello>", ["nothing"] = null };
        var roundTrip = serializer.Deserialize(serializer.Serialize(values));
        Assert.Equal("<hello>", roundTrip["message"]);
        Assert.Null(roundTrip["nothing"]);
        Assert.Throws<InvalidOperationException>(() => serializer.Serialize(
            new Dictionary<string, object?> { ["unsupported"] = 42 }));
        Assert.Null(typeof(TempData).GetMethod("Save", BindingFlags.Public | BindingFlags.Instance));
        Assert.Null(typeof(TempData).GetMethod("Load", BindingFlags.Public | BindingFlags.Instance));
    }

    private static void AssertClosed(TempData data)
    {
        Assert.Throws<InvalidOperationException>(() => data["message"]);
        Assert.Throws<InvalidOperationException>(() => data["message"] = "invalid");
        Assert.Throws<InvalidOperationException>(() => data.Peek("message"));
        Assert.Throws<InvalidOperationException>(() => data.Keep());
        Assert.Throws<InvalidOperationException>(() => data.Keep("message"));
        Assert.Throws<InvalidOperationException>(() => data.Count);
        Assert.Throws<InvalidOperationException>(() => data.IsReadOnly);
        Assert.Throws<InvalidOperationException>(() => data.Keys);
        Assert.Throws<InvalidOperationException>(() => data.Values);
        Assert.Throws<InvalidOperationException>(() => data.ContainsKey("message"));
        Assert.Throws<InvalidOperationException>(() => data.TryGetValue("message", out _));
        Assert.Throws<InvalidOperationException>(() => data.Add("message", "invalid"));
        Assert.Throws<InvalidOperationException>(() => data.Remove("message"));
        Assert.Throws<InvalidOperationException>(() => data.Clear());
        Assert.Throws<InvalidOperationException>(() => data.Add(new("message", "invalid")));
        Assert.Throws<InvalidOperationException>(() => data.Contains(new("message", "invalid")));
        Assert.Throws<InvalidOperationException>(() => data.Remove(new KeyValuePair<string, object?>("message", "invalid")));
        Assert.Throws<InvalidOperationException>(() => data.CopyTo(new KeyValuePair<string, object?>[1], 0));
        Assert.Throws<InvalidOperationException>(() => data.GetEnumerator());
        Assert.Throws<InvalidOperationException>(() => ((IEnumerable)data).GetEnumerator());
    }

    private static void AssertEnumeratorClosed<T>(IEnumerator<T> enumerator)
    {
        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
        Assert.Throws<InvalidOperationException>(() => enumerator.Current);
        Assert.Throws<InvalidOperationException>(() => ((IEnumerator)enumerator).Current);
        Assert.Throws<InvalidOperationException>(() => enumerator.Reset());
        enumerator.Dispose();
    }

    private static async Task<WebApplication> CreateApp(
        RecordingProvider provider, Action<WebApplication> map, bool registerServices = true,
        Action<IServiceCollection>? configureServices = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        if (registerServices)
        {
            builder.Services.AddSingleton<ITempDataProvider>(provider);
            builder.Services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
            configureServices?.Invoke(builder.Services);
            builder.Services.AddRazorSliceTempData();
        }
        var app = builder.Build();
        map(app);
        await app.StartAsync();
        return app;
    }

    private sealed class CheckingResult(Action check) : IResult
    {
        public Task ExecuteAsync(HttpContext context)
        {
            check();
            context.Response.StatusCode = StatusCodes.Status204NoContent;
            return Task.CompletedTask;
        }
    }

    private sealed class StartedResponseFeature : HttpResponseFeature
    {
        public override bool HasStarted => true;
    }

    private sealed class RecordingProvider : ITempDataProvider
    {
        public int LoadCount { get; private set; }
        public int SaveCount { get; private set; }
        public bool FailSave { get; init; }
        public IDictionary<string, object?> SavedValues { get; private set; } = new Dictionary<string, object?>();

        public IDictionary<string, object?> LoadTempData(HttpContext context)
        {
            LoadCount++;
            return new Dictionary<string, object?>();
        }

        public void SaveTempData(HttpContext context, IDictionary<string, object?> values)
        {
            if (FailSave) { throw new InvalidOperationException("save failed"); }
            SaveCount++;
            SavedValues = new Dictionary<string, object?>(values);
        }
    }

    private sealed class TestSerializer : TempDataSerializer
    {
        public override IDictionary<string, object?> Deserialize(byte[] unprotectedData) => throw new NotSupportedException();
        public override byte[] Serialize(IDictionary<string, object?> values) => throw new NotSupportedException();
    }
}
