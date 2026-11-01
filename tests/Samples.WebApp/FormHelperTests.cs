using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace RazorSlices.Samples.WebApp.Tests;

public class FormHelperTests
{
    [Theory]
    [InlineData("__RequestVerificationToken")]
    [InlineData("token\"<&")]
    public async Task FormPostsWithCookieAndConfiguredEncodedTokenField(string fieldName)
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
                services.Configure<AntiforgeryOptions>(options => options.FormFieldName = fieldName)));
        using var client = factory.CreateClient(new()
        {
            BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false
        });
        using var form = await client.GetAsync("/tempdata");
        form.EnsureSuccessStatusCode();
        Assert.Contains(form.Headers.GetValues("Set-Cookie"),
            cookie => cookie.StartsWith(".AspNetCore.Antiforgery.", StringComparison.Ordinal));
        var html = await form.Content.ReadAsStringAsync();
        var token = ReadToken(html);
        Assert.Equal(fieldName, token.Key);
        Assert.NotEmpty(token.Value);
        if (fieldName.Contains('"')) { Assert.Contains("""name="token&quot;&lt;&amp;" """, html); }
        using var content = new FormUrlEncodedContent([token]);
        using var response = await client.PostAsync("/tempdata", content);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("Changes saved.", await client.GetStringAsync("/tempdata"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingOrInvalidTokenIsRejectedBeforeTempDataAccess(bool invalidToken)
    {
        var provider = new RecordingProvider();
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
                services.AddSingleton<IRazorSliceTempDataProvider>(provider)));
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        var token = ReadToken(await client.GetStringAsync("/tempdata"));
        var loadCount = provider.LoadCount;
        var saveCount = provider.SaveCount;
        using var content = new FormUrlEncodedContent(invalidToken
            ? [new(token.Key, "invalid-token")] : Array.Empty<KeyValuePair<string, string>>());
        using var response = await client.PostAsync("/tempdata", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(loadCount, provider.LoadCount);
        Assert.Equal(saveCount, provider.SaveCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TokenWithoutMatchingCookieIsRejected(bool differentCookie)
    {
        using var factory = new WebApplicationFactory<Program>();
        using var firstClient = factory.CreateClient();
        using var secondClient = factory.CreateClient(new() { AllowAutoRedirect = false });
        var token = ReadToken(await firstClient.GetStringAsync("/tempdata"));
        if (differentCookie) { await secondClient.GetStringAsync("/tempdata"); }
        using var content = new FormUrlEncodedContent([token]);
        using var response = await secondClient.PostAsync("/tempdata", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("No flash message.", await secondClient.GetStringAsync("/tempdata"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HelperFailsClearlyWhenPreparationIsMissing(bool hasHttpContext)
    {
        using var slice = Slices.TempDataDemo.Create(null);
        if (hasHttpContext) { slice.HttpContext = new DefaultHttpContext(); }
        var error = await Assert.ThrowsAsync<InvalidOperationException>(async () => await slice.RenderAsync());
        Assert.Equal(hasHttpContext
            ? "Prepare antiforgery tokens with .WithAntiforgery() before rendering the form."
            : "Rendering an antiforgery token requires an HttpContext.", error.Message);
    }

    [Fact]
    public async Task FromFormBindingRequiresMiddlewareEvenWithTheEndpointFilter()
    {
        await using var app = await CreateFormBindingApp(useMiddleware: false);
        using var client = app.GetTestClient();
        using var page = await client.GetAsync("/forms");
        var token = ReadToken(await page.Content.ReadAsStringAsync());
        SetAntiforgeryCookie(client, page);
        using var content = new FormUrlEncodedContent(
            [new("message", "hello"), token]);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => client.PostAsync("/forms", content));
        Assert.Contains("app.UseAntiforgery()", error.Message);
    }

    [Fact]
    public async Task FromFormBindingAcceptsMatchingTokenAndCookieWithMiddleware()
    {
        await using var app = await CreateFormBindingApp(useMiddleware: true);
        using var client = app.GetTestClient();
        using var page = await client.GetAsync("/forms");
        page.EnsureSuccessStatusCode();
        var token = ReadToken(await page.Content.ReadAsStringAsync());
        SetAntiforgeryCookie(client, page);
        using var content = new FormUrlEncodedContent([token, new("message", "posted value")]);
        using var response = await client.PostAsync("/forms", content);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("posted value", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FromFormBindingRejectsMissingOrInvalidTokensWithMiddleware(bool invalidToken)
    {
        var handlerCalls = 0;
        await using var app = await CreateFormBindingApp(useMiddleware: true, () => handlerCalls++);
        using var client = app.GetTestClient();
        using var page = await client.GetAsync("/forms");
        var token = ReadToken(await page.Content.ReadAsStringAsync());
        SetAntiforgeryCookie(client, page);
        var fields = new List<KeyValuePair<string, string>> { new("message", "must not reach handler") };
        if (invalidToken) { fields.Add(new(token.Key, "invalid")); }
        using var content = new FormUrlEncodedContent(fields);
        using var response = await client.PostAsync("/forms", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, handlerCalls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FromFormBindingRejectsTokenWithoutMatchingCookieWithMiddleware(bool differentCookie)
    {
        var handlerCalls = 0;
        await using var app = await CreateFormBindingApp(useMiddleware: true, () => handlerCalls++);
        using var first = app.GetTestClient();
        using var second = app.GetTestClient();
        var token = ReadToken(await first.GetStringAsync("/forms"));
        if (differentCookie)
        {
            using var otherPage = await second.GetAsync("/forms");
            SetAntiforgeryCookie(second, otherPage);
        }
        using var content = new FormUrlEncodedContent([token, new("message", "must not reach handler")]);
        using var response = await second.PostAsync("/forms", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, handlerCalls);
    }

    private static async Task<WebApplication> CreateFormBindingApp(bool useMiddleware, Action? handlerCalled = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        builder.Services.AddAntiforgery();
        var app = builder.Build();
        app.UseRouting();
        if (useMiddleware) { app.UseAntiforgery(); }
        var forms = app.MapGroup("/forms").WithAntiforgery();
        forms.MapGet("", RazorSlice () => Slices.TempDataDemo.Create(null));
        forms.MapPost("", ([FromForm] string message) =>
        {
            handlerCalled?.Invoke();
            return TypedResults.Text(message);
        });
        await app.StartAsync();
        return app;
    }

    private static void SetAntiforgeryCookie(HttpClient client, HttpResponseMessage page)
    {
        var cookie = Assert.Single(page.Headers.GetValues("Set-Cookie"),
            value => value.StartsWith(".AspNetCore.Antiforgery.", StringComparison.Ordinal));
        client.DefaultRequestHeaders.Add("Cookie", cookie.Split(';')[0]);
    }

    internal static KeyValuePair<string, string> ReadToken(string html)
    {
        var match = Assert.Single(Regex.Matches(
            html, """<input type="hidden" name="([^"]+)" value="([^"]+)" />""").Cast<Match>());
        return new(WebUtility.HtmlDecode(match.Groups[1].Value), WebUtility.HtmlDecode(match.Groups[2].Value));
    }

    private sealed class RecordingProvider : IRazorSliceTempDataProvider
    {
        public int LoadCount { get; private set; }
        public int SaveCount { get; private set; }
        public byte[] Load(HttpContext context)
        {
            LoadCount++;
            return [];
        }
        public void Save(HttpContext context, byte[] payload) => SaveCount++;
    }
}
