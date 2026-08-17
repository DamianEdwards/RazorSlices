using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace RazorSlices.Tests;

public class TextWriterHtmlExtensionsTests
{
    [Fact]
    public void WriteUtf8_WritesAsciiTextCorrectly()
    {
        var writer = new StringWriter();
        var utf8Bytes = Encoding.UTF8.GetBytes("Hello, World!");

        writer.WriteUtf8(utf8Bytes);

        Assert.Equal("Hello, World!", writer.ToString());
    }

    [Fact]
    public void WriteUtf8_WritesMultiByteUtf8TextCorrectly()
    {
        var writer = new StringWriter();
        var text = "Héllo, Wörld! 日本語 🎉";
        var utf8Bytes = Encoding.UTF8.GetBytes(text);

        writer.WriteUtf8(utf8Bytes);

        Assert.Equal(text, writer.ToString());
    }

    [Fact]
    public void WriteUtf8_WritesEmptySpanCorrectly()
    {
        var writer = new StringWriter();

        writer.WriteUtf8(ReadOnlySpan<byte>.Empty);

        Assert.Equal("", writer.ToString());
    }

    [Fact]
    public void WriteUtf8_WritesHtmlCharsWithoutEncoding()
    {
        var writer = new StringWriter();
        var text = "<div class=\"test\">&amp;</div>";
        var utf8Bytes = Encoding.UTF8.GetBytes(text);

        writer.WriteUtf8(utf8Bytes);

        Assert.Equal(text, writer.ToString());
    }

    [Fact]
    public void HtmlEncodeAndWriteUtf8_EncodesHtmlCharacters()
    {
        var writer = new StringWriter();
        var utf8Bytes = Encoding.UTF8.GetBytes("<script>alert('xss')</script>");

        writer.HtmlEncodeAndWriteUtf8(utf8Bytes, HtmlEncoder.Default);

        Assert.Equal("&lt;script&gt;alert(&#x27;xss&#x27;)&lt;/script&gt;", writer.ToString());
    }

    [Fact]
    public void HtmlEncodeAndWriteUtf8_WritesTextThatRequiresNoEncoding()
    {
        var writer = new StringWriter();
        var text = "Hello, World!";
        var utf8Bytes = Encoding.UTF8.GetBytes(text);

        writer.HtmlEncodeAndWriteUtf8(utf8Bytes, HtmlEncoder.Default);

        Assert.Equal(text, writer.ToString());
    }

    [Fact]
    public void HtmlEncodeAndWriteUtf8_DoesNotEncodeWithNullEncoder()
    {
        var writer = new StringWriter();
        var text = "Hello, <World>!";
        var utf8Bytes = Encoding.UTF8.GetBytes(text);

        writer.HtmlEncodeAndWriteUtf8(utf8Bytes, NullHtmlEncoder.Default);

        Assert.Equal(text, writer.ToString());
    }

    [Fact]
    public void HtmlEncodeAndWriteUtf8_HandlesMultiByteUtf8Characters()
    {
        var writer = new StringWriter();
        var text = "Héllo <Wörld> 日本語";
        var utf8Bytes = Encoding.UTF8.GetBytes(text);

        writer.HtmlEncodeAndWriteUtf8(utf8Bytes, HtmlEncoder.Default);

        var result = writer.ToString();
        Assert.Contains("&lt;", result);
        Assert.Contains("&gt;", result);
        Assert.Contains("H&#xE9;llo", result);
    }

    [Fact]
    public void WriteUtf8_WritesLargeUtf8TextCorrectly()
    {
        var writer = new StringWriter();
        var text = new string('a', 10_000) + "日本語" + new string('b', 10_000);
        var utf8Bytes = Encoding.UTF8.GetBytes(text);

        writer.WriteUtf8(utf8Bytes);

        Assert.Equal(text, writer.ToString());
    }

    [Fact]
    public void HtmlEncodeAndWriteUtf8_EncodesLargeUtf8Text()
    {
        var writer = new StringWriter();
        var text = new string('<', 1_000);
        var utf8Bytes = Encoding.UTF8.GetBytes(text);

        writer.HtmlEncodeAndWriteUtf8(utf8Bytes, HtmlEncoder.Default);

        var expected = string.Concat(Enumerable.Repeat("&lt;", 1_000));
        Assert.Equal(expected, writer.ToString());
    }

    private enum SampleStatus
    {
        Open = 0,
        Posted = 1
    }

    [Fact]
    public void HtmlEncodeAndWriteSpanFormattable_WritesOnlyTheFormattedChars_Enum()
    {
        var writer = new StringWriter();

        writer.HtmlEncodeAndWriteSpanFormattable(SampleStatus.Open, HtmlEncoder.Default);

        Assert.Equal("Open", writer.ToString());
    }

    [Fact]
    public void HtmlEncodeAndWriteSpanFormattable_WritesOnlyTheFormattedChars_Int()
    {
        var writer = new StringWriter();

        writer.HtmlEncodeAndWriteSpanFormattable(42, HtmlEncoder.Default);

        Assert.Equal("42", writer.ToString());
    }

    [Fact]
    public void HtmlEncodeAndWriteSpanFormattable_DoesNotWriteBufferPadding()
    {
        // The rented encode buffer comes from ArrayPool<char>.Shared, whose smallest bucket is 16.
        // Writing the whole buffer instead of the encoded length appended 12 chars of whatever the
        // previous renter left behind.
        var writer = new StringWriter();

        writer.HtmlEncodeAndWriteSpanFormattable(SampleStatus.Open, HtmlEncoder.Default);

        Assert.Equal(4, writer.ToString().Length);
        Assert.DoesNotContain('\0', writer.ToString());
    }

    [Fact]
    public void HtmlEncodeAndWriteSpanFormattable_DoesNotLeakPreviousRenterContent()
    {
        // Dirty the shared pool with a long value first, then write a short one. Without the fix
        // the short write carried the tail of the long one into the output.
        var warmup = new StringWriter();
        warmup.HtmlEncodeAndWriteSpanFormattable(1234567890123456789L, HtmlEncoder.Default);

        var writer = new StringWriter();
        writer.HtmlEncodeAndWriteSpanFormattable(SampleStatus.Posted, HtmlEncoder.Default);

        Assert.Equal("Posted", writer.ToString());
    }

    [Fact]
    public void HtmlEncodeAndWriteSpanFormattable_StillEncodesHtml()
    {
        var writer = new StringWriter();

        writer.HtmlEncodeAndWriteSpanFormattable(new HtmlishFormattable(), HtmlEncoder.Default);

        Assert.Equal("a&lt;b&gt;c", writer.ToString());
    }

    private readonly struct HtmlishFormattable : ISpanFormattable
    {
        public string ToString(string? format, IFormatProvider? formatProvider) => "a<b>c";

        public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
        {
            const string value = "a<b>c";
            if (destination.Length < value.Length)
            {
                charsWritten = 0;
                return false;
            }
            value.AsSpan().CopyTo(destination);
            charsWritten = value.Length;
            return true;
        }
    }
}
