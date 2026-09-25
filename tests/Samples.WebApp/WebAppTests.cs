using System.Net;
using System.Net.Mime;
using System.Diagnostics;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace RazorSlices.Samples.WebApp.Tests;

public class WebAppTests
{
    [Theory]
    [InlineData("/favicon.svg")]
    [InlineData("/favicon.ico")]
    public async Task Favicon_IsServed(string path)
    {
        using var waf = new WebApplicationFactory<Program>();
        using var httpClient = waf.CreateClient();

        using var response = await httpClient.GetAsync(path);

        response.EnsureSuccessStatusCode();
        Assert.NotEmpty(await response.Content.ReadAsByteArrayAsync());
    }

    [Theory]
    [MemberData(nameof(EndpointDetails))]
    public async Task WafHosted_EndpointsRenderOK(string path, string shouldContain, string expectedMediaType)
    {
        var waf = new WebApplicationFactory<Program>();
        using var httpClient = waf.CreateClient();

        var response = await httpClient.GetAsync(path);

        response.EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(expectedMediaType, response.Content.Headers.ContentType?.MediaType);
        Assert.Contains(shouldContain, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task AttributeRendering_RendersConditionalAttributes()
    {
        var waf = new WebApplicationFactory<Program>();
        using var httpClient = waf.CreateClient();

        var html = await httpClient.GetStringAsync("/attribute-rendering");

        Assert.Contains(
            """
            <div>False</div>
            <div>Null</div>
            <div class="">Empty</div>
            <div class="false">False String</div>
            <div class="active">String</div>
            <input type="checkbox" checked="checked" name="true" />
            <input type="checkbox" name="false" />
            <input type="checkbox" name="null" />
            """,
            html.ReplaceLineEndings());
    }

    [Fact]
    public async Task Streaming_FlushesInitialMessageAndCountdownUpdates()
    {
        using var waf = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder
                .UseEnvironment("Development")
                .UseSetting("ENABLE_RESPONSE_BUFFERING", "true"));
        using var httpClient = waf.CreateClient();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var stopwatch = Stopwatch.StartNew();

        using var response = await httpClient.GetAsync("/streaming", HttpCompletionOption.ResponseHeadersRead, timeout.Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(MediaTypeNames.Text.Html, response.Content.Headers.ContentType?.MediaType);

        using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
        using var reader = new StreamReader(stream);

        var initialHtml = await ReadUntilAsync("</noscript>");
        Assert.Contains("""<p id="countdown" role="status">Counting down... <span id="countdown-value">10</span></p>""", initialHtml);
        Assert.DoesNotContain("<script>", initialHtml);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5), "The initial message should arrive before the countdown finishes.");

        for (var count = 9; count >= 0; count--)
        {
            var script = await ReadUntilAsync("</script>");
            Assert.Contains($"document.getElementById('countdown-value').textContent = '{count}';", script);
            Assert.DoesNotContain("Countdown complete!", script);
            Assert.True(stopwatch.Elapsed >= TimeSpan.FromSeconds(10 - count) - TimeSpan.FromMilliseconds(250),
                "Countdown updates should be delayed by one second each.");
        }

        var completionScript = await ReadUntilAsync("</script>");
        Assert.Contains("document.getElementById('countdown').textContent = 'Countdown complete!';", completionScript);
        Assert.True(stopwatch.Elapsed >= TimeSpan.FromSeconds(10.75), "Zero should be visible before completion.");
        Assert.Contains("</html>", await reader.ReadToEndAsync(timeout.Token));

        async Task<string> ReadUntilAsync(string marker)
        {
            using var chunkTimeout = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);
            chunkTimeout.CancelAfter(TimeSpan.FromSeconds(5));
            var html = new StringBuilder();
            while (true)
            {
                var line = await reader.ReadLineAsync(chunkTimeout.Token);
                Assert.NotNull(line);
                html.AppendLine(line);
                if (line.Contains(marker, StringComparison.Ordinal))
                {
                    return html.ToString();
                }
            }
        }
    }

    public static object[][] EndpointDetails => [
        ["/", "Todos", MediaTypeNames.Text.Html],
        ["/1", "Wash the dishes", MediaTypeNames.Text.Html],
        ["/nested", "Nested : Test", MediaTypeNames.Text.Html],
        ["/encoding", "{&#x27;antiForgery&#x27;", MediaTypeNames.Text.Html],
        ["/unicode", "🐻", MediaTypeNames.Text.Html],
        ["/streaming", "Countdown complete!", MediaTypeNames.Text.Html],
        ["/templated", "This is from a partial with a templated model", MediaTypeNames.Text.Html],
        ["/library", "This slice was loaded from a referenced Razor Class Library!", MediaTypeNames.Text.Html],
        ["/render-to-string", "htmlString", MediaTypeNames.Application.Json],
        ["/render-to-stringbuilder", "htmlString", MediaTypeNames.Application.Json],
        ["/lorem", "Lorem Ipsum (Static)", MediaTypeNames.Text.Html],
        ["/lorem-static", "Lorem Ipsum (Static)", MediaTypeNames.Text.Html],
        ["/lorem-dynamic", "Lorem Ipsum (Dynamic: 3 paragraphs)", MediaTypeNames.Text.Html],
        ["/lorem-dynamic?paraCount=12&paraLength=6", "Lorem Ipsum (Dynamic: 12 paragraphs)", MediaTypeNames.Text.Html],
        ["/lorem-formattable", "Lorem Ipsum (Formattable: 3 paragraphs)", MediaTypeNames.Text.Html],
        ["/lorem-htmlcontent", "Lorem Ipsum (IHtmlContent)", MediaTypeNames.Text.Html],
        ["/lorem-htmlcontent?encode=true", "&lt;p&gt;", MediaTypeNames.Text.Html],
        ["/lorem-injectableproperties", "Lorem Ipsum (Dependency-injected properties)", MediaTypeNames.Text.Html],
        ["/lorem-stream", "Lorem Ipsum (Static)", MediaTypeNames.Text.Html]
    ];
}
