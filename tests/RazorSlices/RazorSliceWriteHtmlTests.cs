using System.IO.Pipelines;
using System.Text;
using System.Text.Encodings.Web;

namespace RazorSlices.Tests;

public class RazorSliceWriteHtmlTests
{
    [Fact]
    public async Task WriteHtmlString_WritesRawHtmlToTextWriter()
    {
        var writer = new StringWriter();
        var slice = new WriteHtmlStringSlice("<span>&</span>");

        await slice.RenderAsync(writer, HtmlEncoder.Default);

        Assert.Equal("<span>&</span>", writer.ToString());
    }

    [Fact]
    public async Task WriteHtmlString_WritesRawHtmlToPipeWriter()
    {
        using var stream = new MemoryStream();
        var pipeWriter = PipeWriter.Create(stream);
        var slice = new WriteHtmlStringSlice("<span>&</span>");

        await slice.RenderAsync(pipeWriter, HtmlEncoder.Default);
        await pipeWriter.FlushAsync();
        await pipeWriter.CompleteAsync();

        Assert.Equal("<span>&</span>", Encoding.UTF8.GetString(stream.ToArray()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task WriteHtmlString_DoesNotWriteNullOrEmptyString(string? html)
    {
        var writer = new StringWriter();
        var slice = new WriteHtmlStringSlice(html);

        await slice.RenderAsync(writer, HtmlEncoder.Default);

        Assert.Equal("", writer.ToString());
    }

    private sealed class WriteHtmlStringSlice : RazorSlice
    {
        private readonly string? _html;

        public WriteHtmlStringSlice(string? html)
        {
            _html = html;
        }

        public override Task ExecuteAsync()
        {
            WriteHtml(_html);
            return Task.CompletedTask;
        }
    }
}
