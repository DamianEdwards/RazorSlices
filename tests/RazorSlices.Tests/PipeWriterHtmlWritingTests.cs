using System.Buffers;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace RazorSlices.Tests;

public class PipeWriterHtmlWritingTests
{
    private static readonly TimeSpan _timeout = Debugger.IsAttached ? Timeout.InfiniteTimeSpan : TimeSpan.FromMilliseconds(5000);

    [Fact]
    public async ValueTask HtmlEncodeAndWrite_DoesNotEncodeTextWhenPassedNullEncoder()
    {
        var bufferStream = new MemoryStream(512);
        var pipeWriter = PipeWriter.Create(bufferStream);

        var text = "Hello, World<test>!";
        pipeWriter.HtmlEncodeAndWrite(text.AsSpan(), NullHtmlEncoder.Default);
        await pipeWriter.FlushAsync();

        var writtenText = Encoding.UTF8.GetString(bufferStream.ToArray(), 0, (int)bufferStream.Length);

        Assert.Equal(text, writtenText);
    }

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
    public async Task HtmlEncodeAndWrite_EncodesTextThatIsLessThanSmallEncodeBuffer()
    {
        var bufferStream = new MemoryStream(512);
        var pipeWriter = PipeWriter.Create(bufferStream);

        // Create a string with no encodable chars that's half the small threshold then add a small string that requires encoding
        var text = new string('a', BufferSizes.SmallTextWriteCharSize / 2);
        text += "<encode>";

        var encodeTask = Task.Run(() => pipeWriter.HtmlEncodeAndWrite(text.AsSpan(), HtmlEncoder.Default));

        var completedTask = await Task.WhenAny(encodeTask, Task.Delay(_timeout));

        Assert.Equal(encodeTask, completedTask);

        await pipeWriter.FlushAsync();

        var writtenText = Encoding.UTF8.GetString(bufferStream.ToArray(), 0, (int)bufferStream.Length);

        var expected = text.Replace("<", "&lt;").Replace(">", "&gt;");
        Assert.Equal(expected, writtenText);
    }


    [Fact]
    public async Task HtmlEncodeAndWrite_EncodesTextThatIsLessThanSmallEncodeBufferAndButRequiresMultiplePasses()
    {
        var bufferStream = new MemoryStream(512);
        var pipeWriter = PipeWriter.Create(bufferStream);

        // Create a string with no encodable chars that's 5 chars smaller than the threshold then add a two chars that require encoding
        // This should result in the first encode phase being successful as the encoded chars will fit in the small buffer but there will
        // not be enough space left in the buffer to encode the remaining char, forcing a buffer reset or growth.
        var text = new string('a', BufferSizes.SmallTextWriteCharSize - 5);
        text += "<>";

        var encodeTask = Task.Run(() => pipeWriter.HtmlEncodeAndWrite(text.AsSpan(), HtmlEncoder.Default));

        var completedTask = await Task.WhenAny(encodeTask, Task.Delay(_timeout));

        Assert.Equal(encodeTask, completedTask);

        await pipeWriter.FlushAsync();

        var writtenText = Encoding.UTF8.GetString(bufferStream.ToArray(), 0, (int)bufferStream.Length);

        var expected = text.Replace("<", "&lt;").Replace(">", "&gt;");
        Assert.Equal(expected, writtenText);
    }
    [Fact]
    public async Task HtmlEncodeAndWrite_EncodesTextThatIsLargerThanEncodeBuffer()
    {
        var bufferStream = new MemoryStream(512);
        var pipeWriter = PipeWriter.Create(bufferStream);

        // Every character in this text must be HTML encoded so it will definitely exceed the max buffer size
        var text = new string('<', BufferSizes.MaxBufferSize);

        var encodeTask = Task.Run(() => pipeWriter.HtmlEncodeAndWrite(text.AsSpan(), HtmlEncoder.Default));

        var completedTask = await Task.WhenAny(encodeTask, Task.Delay(_timeout));

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

        var completedTask = await Task.WhenAny(encodeTask, Task.Delay(_timeout));

        Assert.Equal(encodeTask, completedTask);

        await pipeWriter.FlushAsync();

        var writtenText = Encoding.UTF8.GetString(bufferStream.ToArray(), 0, (int)bufferStream.Length);

        Assert.Equal("{&#x27;antiForgery&#x27;:&#x27;CfDJ8PXqQxmMPkhOiAitfXYwz3Qx8ObgK9ND-yZcc0Wf5Vtyo8pylpAtos8zwLNhJ-T3gJ4m2iX96lbH15gV0LIdcEBHU5xf3U95L_rlojOvp-0XIvZ8HI3CWIr6xUIKGhDpKaasa9hMAzjUZuFjYVdDkmE&#x27;,&#x27;formFieldName&#x27;:&#x27;__RequestVerificationToken&#x27;,&#x27;antiForgery&#x27;:&#x27;CfDJ8PXqQxmMPkhOiAitfXYwz3Qx8ObgK9ND-yZcc0Wf5Vtyo8pylpAtos8zwLNhJ-T3gJ4m2iX96lbH15gV0LIdcEBHU5xf3U95L_rlojOvp-0XIvZ8HI3CWIr6xUIKGhDpKaasa9hMAzjUZuFjYVdDkmE&#x27;,&#x27;formFieldName&#x27;:&#x27;__RequestVerificationToken&#x27;}", writtenText);
    }
}