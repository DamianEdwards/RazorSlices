using System.Buffers;
using System.IO.Pipelines;
using System.Text;
using System.Text.Encodings.Web;

namespace RazorSlices.Tests;

public class BufferWriterHtmlWritingTests
{
    [Fact]
    public async ValueTask HtmlEncodeAndWrite_DoesNotEncodeTextThatRequiresNoEncoding()
    {
        var bufferStream = new MemoryStream(512);
        var pipeWriter = PipeWriter.Create(bufferStream);

        var text = "Hello, World!";
        pipeWriter.HtmlEncodeAndWrite(text.AsSpan(), HtmlEncoder.Default);
        await pipeWriter.FlushAsync();

        var writtenText = Encoding.UTF8.GetString(bufferStream.ToArray(), 0, (int)bufferStream.Length);

        Assert.Equal(text, writtenText);
    }

    [Fact]
    public async ValueTask HtmlEncodeAndWrite_EncodesSimpleText()
    {
        var bufferStream = new MemoryStream(512);
        var pipeWriter = PipeWriter.Create(bufferStream);

        var text = "Hello, <World>!";
        pipeWriter.HtmlEncodeAndWrite(text.AsSpan(), HtmlEncoder.Default);
        await pipeWriter.FlushAsync();

        var writtenText = Encoding.UTF8.GetString(bufferStream.ToArray(), 0, (int)bufferStream.Length);

        Assert.Equal("Hello, &lt;World&gt;!", writtenText);
    }

    [Fact]
    public async Task HtmlEncodeAndWrite_EncodesTextThatIsLargerThanEncodeBuffer()
    {
        var bufferStream = new MemoryStream(512);
        var pipeWriter = PipeWriter.Create(bufferStream);

        // Every character in this text must be HTML encoded so it will definitely exceed the max buffer size
        var text = new string('<', BufferSizes.MaxBufferSize);

        var encodeTask = Task.Run(() => pipeWriter.HtmlEncodeAndWrite(text.AsSpan(), HtmlEncoder.Default));

        var completedTask = await Task.WhenAny(encodeTask, Task.Delay(5000));

        Assert.Equal(encodeTask, completedTask);

        await pipeWriter.FlushAsync();

        var writtenText = Encoding.UTF8.GetString(bufferStream.ToArray(), 0, (int)bufferStream.Length);

        var expected = text.Replace("<", "&lt;");
        Assert.Equal(expected, writtenText);
    }

    [Fact]
    public async Task HtmlEncodeAndWrite_EncodesTextWhenRemainingBufferIsTooSmallToEncodeAnyRemainingChars()
    {
        var bufferStream = new MemoryStream(512);
        var pipeWriter = PipeWriter.Create(bufferStream);

        // This text can't be fully encoded in one cycle and results in the buffer being too small to encode even one of the remaining chars into on the second cycle
        // so specifically exercises the case where the buffer is too small to encode any of the remaining chars into and thus must write out the chars encoded thus
        // far before resetting the buffer and encoding the remaining chars
        var text = "{'antiForgery':'CfDJ8PXqQxmMPkhOiAitfXYwz3Qx8ObgK9ND-yZcc0Wf5Vtyo8pylpAtos8zwLNhJ-T3gJ4m2iX96lbH15gV0LIdcEBHU5xf3U95L_rlojOvp-0XIvZ8HI3CWIr6xUIKGhDpKaasa9hMAzjUZuFjYVdDkmE','formFieldName':'__RequestVerificationToken','antiForgery':'CfDJ8PXqQxmMPkhOiAitfXYwz3Qx8ObgK9ND-yZcc0Wf5Vtyo8pylpAtos8zwLNhJ-T3gJ4m2iX96lbH15gV0LIdcEBHU5xf3U95L_rlojOvp-0XIvZ8HI3CWIr6xUIKGhDpKaasa9hMAzjUZuFjYVdDkmE','formFieldName':'__RequestVerificationToken'}";

        var encodeTask = Task.Run(() => pipeWriter.HtmlEncodeAndWrite(text.AsSpan(), HtmlEncoder.Default));

        var completedTask = await Task.WhenAny(encodeTask, Task.Delay(5000));

        Assert.Equal(encodeTask, completedTask);

        await pipeWriter.FlushAsync();

        var writtenText = Encoding.UTF8.GetString(bufferStream.ToArray(), 0, (int)bufferStream.Length);

        Assert.Equal("{&#x27;antiForgery&#x27;:&#x27;CfDJ8PXqQxmMPkhOiAitfXYwz3Qx8ObgK9ND-yZcc0Wf5Vtyo8pylpAtos8zwLNhJ-T3gJ4m2iX96lbH15gV0LIdcEBHU5xf3U95L_rlojOvp-0XIvZ8HI3CWIr6xUIKGhDpKaasa9hMAzjUZuFjYVdDkmE&#x27;,&#x27;formFieldName&#x27;:&#x27;__RequestVerificationToken&#x27;,&#x27;antiForgery&#x27;:&#x27;CfDJ8PXqQxmMPkhOiAitfXYwz3Qx8ObgK9ND-yZcc0Wf5Vtyo8pylpAtos8zwLNhJ-T3gJ4m2iX96lbH15gV0LIdcEBHU5xf3U95L_rlojOvp-0XIvZ8HI3CWIr6xUIKGhDpKaasa9hMAzjUZuFjYVdDkmE&#x27;,&#x27;formFieldName&#x27;:&#x27;__RequestVerificationToken&#x27;}", writtenText);
    }
}