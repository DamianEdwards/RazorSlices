using System.ComponentModel;
using Microsoft.AspNetCore.Html;

namespace RazorSlices;

/// <summary>
/// Base for SDK-compiled helper files. Helpers contain stateless methods implementing a public interface,
/// not a model, layout, injected properties, or a top-level slice body.
/// </summary>
public abstract class RazorSliceHelpers
{
    private RazorSlice? _owner;

    private RazorSliceBase Owner => (_owner ?? throw new InvalidOperationException("The helper is not bound to an active render.")).GetActiveHelperSlice();

    internal void Bind(RazorSlice owner)
    {
        _owner = owner;
    }

    internal void Unbind()
    {
        _owner = null;
    }

    /// <summary>Gets the caller context for invoking other helper facades.</summary>
    protected IHelperContext Helpers => _owner ?? throw new InvalidOperationException("The helper is not bound to an active render.");

    /// <summary>Gets the calling render's cancellation token.</summary>
    protected CancellationToken CancellationToken => Owner.CancellationToken;

    /// <summary>Required by the Razor SDK; helper bodies must be declared in @functions instead.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public abstract Task ExecuteAsync();

    /// <summary>Writes an unencoded literal.</summary>
    protected void WriteLiteral(string? value)
    {
        Owner.WriteLiteral(value);
    }

#if !RAZOR_SLICES_DISABLE_UTF8_LITERAL_OVERLOAD
    /// <summary>Writes an unencoded UTF-8 literal.</summary>
    protected void WriteLiteral(ReadOnlySpan<byte> value)
    {
        Owner.WriteLiteral(value);
    }
#endif

    /// <summary>Writes an unencoded value.</summary>
    protected void WriteLiteral<T>(T? value)
    {
        Owner.WriteLiteral(value);
    }

    /// <summary>Writes an encoded value.</summary>
    protected void Write<T>(T? value)
    {
        Owner.Write(value);
    }

    /// <summary>Writes an encoded string.</summary>
    protected void Write(string? value)
    {
        Owner.Write(value);
    }

    /// <summary>Writes encoded characters.</summary>
    protected void Write(ReadOnlySpan<char> value)
    {
        Owner.Write(value);
    }

    /// <summary>Writes encoded UTF-8 text.</summary>
    protected void Write(ReadOnlySpan<byte> value)
    {
        Owner.Write(value);
    }

    /// <summary>Writes encoded UTF-8 text.</summary>
    protected void Write(ReadOnlyMemory<byte> value)
    {
        Owner.Write(value.Span);
    }

    /// <summary>Writes encoded UTF-8 text.</summary>
    protected void Write(byte[]? value)
    {
        Owner.Write(value.AsSpan());
    }

    /// <summary>Writes pre-encoded HTML.</summary>
    protected void Write(HtmlString value)
    {
        Owner.Write(value);
    }

    /// <summary>Writes pre-encoded HTML.</summary>
    protected void Write(IHtmlContent? value)
    {
        Owner.Write(value);
    }

    /// <summary>Writes a Boolean.</summary>
    protected void Write(bool value)
    {
        Owner.WriteBool(value);
    }

    /// <summary>Writes a nullable Boolean.</summary>
    protected void Write(bool? value)
    {
        Owner.WriteBool(value);
    }

    /// <summary>Writes an integer.</summary>
    protected void Write(int value)
    {
        Owner.WriteUtf8SpanFormattable(value);
    }

    /// <summary>Writes a long integer.</summary>
    protected void Write(long value)
    {
        Owner.WriteUtf8SpanFormattable(value);
    }

    /// <summary>Writes a decimal.</summary>
    protected void Write(decimal value)
    {
        Owner.WriteUtf8SpanFormattable(value);
    }

    /// <summary>Writes a double.</summary>
    protected void Write(double value)
    {
        Owner.WriteUtf8SpanFormattable(value);
    }

    /// <summary>Writes a single-precision number.</summary>
    protected void Write(float value)
    {
        Owner.WriteUtf8SpanFormattable(value);
    }

    /// <summary>Writes a byte.</summary>
    protected void Write(byte value)
    {
        Owner.WriteUtf8SpanFormattable(value);
    }

    /// <summary>Writes a signed byte.</summary>
    protected void Write(sbyte value)
    {
        Owner.WriteUtf8SpanFormattable(value);
    }

    /// <summary>Writes a short integer.</summary>
    protected void Write(short value)
    {
        Owner.WriteUtf8SpanFormattable(value);
    }

    /// <summary>Writes an unsigned short integer.</summary>
    protected void Write(ushort value)
    {
        Owner.WriteUtf8SpanFormattable(value);
    }

    /// <summary>Writes an unsigned integer.</summary>
    protected void Write(uint value)
    {
        Owner.WriteUtf8SpanFormattable(value);
    }

    /// <summary>Writes an unsigned long integer.</summary>
    protected void Write(ulong value)
    {
        Owner.WriteUtf8SpanFormattable(value);
    }

    /// <summary>Writes a character.</summary>
    protected void Write(char value)
    {
        Owner.WriteUtf8SpanFormattable(value);
    }

    /// <summary>Writes a half-precision number.</summary>
    protected void Write(Half value)
    {
        Owner.WriteUtf8SpanFormattable(value);
    }

    /// <summary>Writes a large integer.</summary>
    protected void Write(System.Numerics.BigInteger value)
    {
        Owner.WriteUtf8SpanFormattable(value);
    }

    /// <summary>Writes a date and time.</summary>
    protected void Write(DateTime value)
    {
        Owner.WriteUtf8SpanFormattable(value);
    }

    /// <summary>Writes a date and time with an offset.</summary>
    protected void Write(DateTimeOffset value)
    {
        Owner.WriteUtf8SpanFormattable(value);
    }

    /// <summary>Writes a time interval.</summary>
    protected void Write(TimeSpan value)
    {
        Owner.WriteUtf8SpanFormattable(value);
    }

    /// <summary>Writes a date.</summary>
    protected void Write(DateOnly value)
    {
        Owner.WriteUtf8SpanFormattable(value);
    }

    /// <summary>Writes a time.</summary>
    protected void Write(TimeOnly value)
    {
        Owner.WriteUtf8SpanFormattable(value);
    }

    /// <summary>Writes a unique identifier.</summary>
    protected void Write(Guid value)
    {
        Owner.WriteUtf8SpanFormattable(value);
    }

    /// <summary>Writes a nullable formattable without boxing.</summary>
    protected void Write<T>(T? value) where T : struct, IUtf8SpanFormattable
    {
        if (value.HasValue)
        {
            Owner.WriteUtf8SpanFormattable(value.Value);
        }
    }
    /// <summary>Writes pre-encoded HTML.</summary>
    protected HtmlString WriteHtml(string? value)
    {
        return Owner.WriteHtml(value);
    }

    /// <summary>Writes pre-encoded HTML.</summary>
    protected HtmlString WriteHtml(HtmlString value)
    {
        return Owner.WriteHtml(value);
    }

    /// <summary>Writes pre-encoded HTML content.</summary>
    protected HtmlString WriteHtml<T>(T value) where T : IHtmlContent
    {
        return Owner.WriteHtml(value);
    }

    /// <summary>Writes a formatted value.</summary>
    protected HtmlString WriteSpanFormattable<T>(T value, ReadOnlySpan<char> format = default, IFormatProvider? formatProvider = null, bool htmlEncode = true)
        where T : ISpanFormattable
    {
        return Owner.WriteSpanFormattable(value, format, formatProvider, htmlEncode);
    }

    /// <summary>Writes a UTF-8 formatted value.</summary>
    protected HtmlString WriteUtf8SpanFormattable<T>(T value, ReadOnlySpan<char> format = default, IFormatProvider? formatProvider = null, bool htmlEncode = true)
        where T : IUtf8SpanFormattable
    {
        return Owner.WriteUtf8SpanFormattable(value, format, formatProvider, htmlEncode);
    }

    /// <summary>Begins a conditional attribute.</summary>
    protected void BeginWriteAttribute(string name, string prefix, int prefixOffset, string suffix, int suffixOffset, int count)
    {
        Owner.BeginWriteAttribute(name, prefix, prefixOffset, suffix, suffixOffset, count);
    }

    /// <summary>Writes a conditional attribute value.</summary>
    protected void WriteAttributeValue<T>(string prefix, int prefixOffset, T? value, int valueOffset, int valueLength, bool isLiteral)
    {
        Owner.WriteAttributeValue(prefix, prefixOffset, value, valueOffset, valueLength, isLiteral);
    }

    /// <summary>Writes a conditional Boolean attribute value.</summary>
    protected void WriteAttributeValue(string prefix, int prefixOffset, bool value, int valueOffset, int valueLength, bool isLiteral)
    {
        Owner.WriteAttributeValue(prefix, prefixOffset, value, valueOffset, valueLength, isLiteral);
    }

    /// <summary>Writes a conditional nullable Boolean attribute value.</summary>
    protected void WriteAttributeValue(string prefix, int prefixOffset, bool? value, int valueOffset, int valueLength, bool isLiteral)
    {
        Owner.WriteAttributeValue(prefix, prefixOffset, value, valueOffset, valueLength, isLiteral);
    }

    /// <summary>Ends a conditional attribute.</summary>
    protected void EndWriteAttribute()
    {
        Owner.EndWriteAttribute();
    }

    /// <summary>Flushes the caller's output.</summary>
    protected ValueTask<HtmlString> FlushAsync()
    {
        return Owner.FlushAsync();
    }
}
