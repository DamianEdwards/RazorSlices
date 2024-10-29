using System.Buffers;
using System.Diagnostics;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace RazorSlices.Tests;

public class BufferWriterHtmlWritingTests
{
    private static readonly TimeSpan _timeout = Debugger.IsAttached ? Timeout.InfiniteTimeSpan : TimeSpan.FromMilliseconds(5000);

    [Fact]
    public void HtmlEncodeAndWrite_DoesNotEncodeTextWhenPassedNullEncoder()
    {
        var bufferWriter = new ArrayBufferWriter<byte>();

        var text = "Hello, World<test>!";
        bufferWriter.HtmlEncodeAndWrite(text.AsSpan(), NullHtmlEncoder.Default);

        var writtenText = Encoding.UTF8.GetString(bufferWriter.WrittenSpan);

        Assert.Equal(text, writtenText);
    }

    [Fact]
    public void HtmlEncodeAndWrite_DoesNotEncodeTextThatRequiresNoEncoding()
    {
        var bufferWriter = new ArrayBufferWriter<byte>();

        var text = "Hello, World!";
        bufferWriter.HtmlEncodeAndWrite(text.AsSpan(), HtmlEncoder.Default);

        var writtenText = Encoding.UTF8.GetString(bufferWriter.WrittenSpan);

        Assert.Equal(text, writtenText);
    }

    [Fact]
    public void HtmlEncodeAndWrite_EncodesSimpleText()
    {
        var bufferWriter = new ArrayBufferWriter<byte>();

        var text = "Hello, <World>!";
        bufferWriter.HtmlEncodeAndWrite(text.AsSpan(), HtmlEncoder.Default);

        var writtenText = Encoding.UTF8.GetString(bufferWriter.WrittenSpan);

        Assert.Equal("Hello, &lt;World&gt;!", writtenText);
    }

    [Fact]
    public async Task HtmlEncodeAndWrite_EncodesTextThatIsLessThanSmallEncodeBuffer()
    {
        var bufferWriter = new ArrayBufferWriter<byte>();

        // Create a string with no encodable chars that's half the small threshold then add a small string that requires encoding
        var text = new string('a', BufferSizes.SmallTextWriteCharSize / 2);
        text += "<encode>";

        var encodeTask = Task.Run(() => bufferWriter.HtmlEncodeAndWrite(text.AsSpan(), HtmlEncoder.Default));

        var completedTask = await Task.WhenAny(encodeTask, Task.Delay(_timeout));

        Assert.Equal(encodeTask, completedTask);

        var writtenText = Encoding.UTF8.GetString(bufferWriter.WrittenSpan);

        var expected = text.Replace("<", "&lt;").Replace(">", "&gt;");
        Assert.Equal(expected, writtenText);
    }


    [Fact]
    public async Task HtmlEncodeAndWrite_EncodesTextThatIsLessThanSmallEncodeBufferAndButRequiresMultiplePasses()
    {
        var bufferWriter = new ArrayBufferWriter<byte>();

        // Create a string with no encodable chars that's 5 chars smaller than the threshold then add a two chars that require encoding
        // This should result in the first encode phase being successful as the encoded chars will fit in the small buffer but there will
        // not be enough space left in the buffer to encode the remaining char, forcing a buffer reset or growth.
        var text = new string('a', BufferSizes.SmallTextWriteCharSize - 5);
        text += "<>";

        var encodeTask = Task.Run(() => bufferWriter.HtmlEncodeAndWrite(text.AsSpan(), HtmlEncoder.Default));

        var completedTask = await Task.WhenAny(encodeTask, Task.Delay(_timeout));

        Assert.Equal(encodeTask, completedTask);

        var writtenText = Encoding.UTF8.GetString(bufferWriter.WrittenSpan);

        var expected = text.Replace("<", "&lt;").Replace(">", "&gt;");
        Assert.Equal(expected, writtenText);
    }
    [Fact]
    public async Task HtmlEncodeAndWrite_EncodesTextThatIsLargerThanEncodeBuffer()
    {
        var bufferWriter = new ArrayBufferWriter<byte>();

        // Every character in this text must be HTML encoded so it will definitely exceed the max buffer size
        var text = new string('<', BufferSizes.MaxBufferSize);

        var encodeTask = Task.Run(() => bufferWriter.HtmlEncodeAndWrite(text.AsSpan(), HtmlEncoder.Default));

        var completedTask = await Task.WhenAny(encodeTask, Task.Delay(_timeout));

        Assert.Equal(encodeTask, completedTask);        

        var writtenText = Encoding.UTF8.GetString(bufferWriter.WrittenSpan);

        var expected = text.Replace("<", "&lt;");
        Assert.Equal(expected, writtenText);
    }
}