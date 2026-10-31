using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

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
                services.AddSingleton<ITempDataProvider>(provider)));
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

    internal static KeyValuePair<string, string> ReadToken(string html)
    {
        var match = Assert.Single(Regex.Matches(
            html, """<input type="hidden" name="([^"]+)" value="([^"]+)" />""").Cast<Match>());
        return new(WebUtility.HtmlDecode(match.Groups[1].Value), WebUtility.HtmlDecode(match.Groups[2].Value));
    }

    private sealed class RecordingProvider : ITempDataProvider
    {
        public int LoadCount { get; private set; }
        public int SaveCount { get; private set; }
        public IDictionary<string, object?> LoadTempData(HttpContext context)
        {
            LoadCount++;
            return new Dictionary<string, object?>();
        }
        public void SaveTempData(HttpContext context, IDictionary<string, object?> values) => SaveCount++;
    }
}
