using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Http;

namespace RazorSlices.Tests;

public class RazorSliceIResultTests
{
    [Fact]
    public async Task ExecuteAsync_UsesDefaultHtmlContentType()
    {
        var slice = new TestSlice
        {
            HtmlEncoder = HtmlEncoder.Default
        };
        var httpContext = CreateHttpContext();

        await ((IResult)slice).ExecuteAsync(httpContext);

        Assert.Equal("text/html; charset=utf-8", httpContext.Response.ContentType);
    }

    [Fact]
    public async Task ExecuteAsync_UsesOverriddenContentType()
    {
        var slice = new TestSlice
        {
            ContentType = "application/xml; charset=utf-8",
            HtmlEncoder = HtmlEncoder.Default
        };
        var httpContext = CreateHttpContext();

        await ((IResult)slice).ExecuteAsync(httpContext);

        Assert.Equal("application/xml; charset=utf-8", httpContext.Response.ContentType);
    }

    private static HttpContext CreateHttpContext()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        return httpContext;
    }

    private sealed class TestSlice : RazorSlice
    {
        public override Task ExecuteAsync() => Task.CompletedTask;
    }
}
