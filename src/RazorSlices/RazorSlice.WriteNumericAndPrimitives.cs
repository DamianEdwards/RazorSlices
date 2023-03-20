using System.Buffers;
using System.Buffers.Text;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Html;

namespace RazorSlices;

public abstract partial class RazorSlice
{
    // TODO: Add overloads for other values supported by Utf8Formatter.TryFormat, e.g. numeric types, DateTime, etc.
    // TODO: Review this for HtmlEncoding requirements

    /// <summary>
    /// Write the specified <see cref="Nullable{T}"/> value to the output.
    /// </summary>
    /// <remarks>
    /// <para>
    /// You generally shouldn't call this method directly. The Razor compiler will emit the appropriate calls to this method for
    /// any Razor write expression for an <see cref="int"/> value in your .cshtml file, e.g. <c>@item.Qty</c>.
    /// </para>
    /// <para>
    /// To specify a format, call <see cref="WriteNumeric(int?, StandardFormat)"/> instead, or use <see cref="int.ToString(string?)"/>,
    /// e.g. <c>@item.Qty?.ToString("G")</c>
    /// </para>
    /// </remarks>
    /// <param name="value">The value to write to the output.</param>
    protected void Write(int? value) => WriteNumeric(value, default);

    /// <summary>
    /// Write the specified <see cref="int"/> value to the output.
    /// </summary>
    /// <remarks>
    /// <para>
    /// You generally shouldn't call this method directly. The Razor compiler will emit the appropriate calls to this method for
    /// any Razor write expression for an <see cref="int"/> value in your .cshtml file, e.g. <c>@item.Qty</c>.
    /// </para>
    /// <para>
    /// To specify a format, call <see cref="WriteNumeric(int, StandardFormat)"/> instead, or use <see cref="int.ToString(string?)"/>,
    /// e.g. <c>@item.Qty.ToString("G")</c>
    /// </para>
    /// </remarks>
    /// <param name="value">The value to write to the output.</param>
    protected void Write(int value) => WriteNumeric(value, default);

    /// <summary>
    /// Write the specified <see cref="ISpanFormattable"/> value to the output.
    /// </summary>
    /// <param name="formattable">The value to write to the output.</param>
    protected void Write(ISpanFormattable? formattable)
    {
        if (formattable is null)
        {
            return;
        }

        _bufferWriter?.HtmlEncodeAndWrite(formattable, _htmlEncoder);
        _textWriter?.HtmlEncodeAndWrite(formattable, _htmlEncoder);
    }

    /// <summary>
    /// Write the specified <see cref="Nullable{T}"/> value to the output using the specified <see cref="StandardFormat"/>.
    /// </summary>
    /// <param name="value">The value to write to the output.</param>
    /// <param name="format">The format to use when writing the value.</param>
    /// <returns><see cref="HtmlString.Empty"/> to allow for easy calling via a Razor expression, e.g. <c>@WriteNumeric(item.Qty, StandardFormat.Parse("G"))</c></returns>
    protected HtmlString WriteNumeric(int? value, StandardFormat format)
    {
        if (value.HasValue)
        {
            return WriteNumeric(value.Value, format);
        }
        return HtmlString.Empty;
    }

    /// <summary>
    /// Write the specified <see cref="int"/> value to the output using the specified <see cref="StandardFormat"/>.
    /// </summary>
    /// <param name="value">The value to write to the output.</param>
    /// <param name="format">The format to use when writing the value.</param>
    /// <returns><see cref="HtmlString.Empty"/> to allow for easy calling via a Razor expression, e.g. <c>@WriteNumeric(item.Qty, StandardFormat.Parse("G"))</c></returns>
    protected HtmlString WriteNumeric(int value, StandardFormat format)
    {
        if (_bufferWriter is not null)
        {
            // Try to format the number into a fixed-length span first
            var span = _bufferWriter.GetSpan(32);

            if (Utf8Formatter.TryFormat(value, span, out var bytesWritten, format))
            {
                _bufferWriter.Advance(bytesWritten);
            }
            else
            {
                WriteNumericSlow(value, format, _bufferWriter);
            }
        }
        if (_textWriter is not null)
        {
            if (format.IsDefault)
            {
                _textWriter.Write(value);
            }
            else
            {
                _textWriter.Write(format.ToString(), value);
            }
        }
        return HtmlString.Empty;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void WriteNumericSlow(int value, StandardFormat format, IBufferWriter<byte> bufferWriter)
    {
        var lengthHint = GetFormattedLengthHint(value, format);
        int actualLength;

        while (true)
        {
            var span = bufferWriter.GetSpan(lengthHint);

            if (Utf8Formatter.TryFormat(value, span, out var bytesWritten, format))
            {
                actualLength = bytesWritten;
                break;
            }

            lengthHint *= 2;
        }
        bufferWriter.Advance(actualLength);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetFormattedLengthHint(int value, StandardFormat format)
    {
        var logBase = format.Symbol switch
        {
            'X' => 16,
            _ => 10
        };
        var maxDigits = Math.Max(1, (int)Math.Ceiling(Math.Log(value, logBase)));
        // If default allow 1 extra char for potential negative sign, otherwise allow double as a reasonable quick guess for
        // formatting chars, precision, etc. Caller will continue doubling until output fits anyway.
        // Possible this could be rewritten to calculate approximate or even exact formatted length but not clear if it's
        // worthwhile
        return format.IsDefault ? maxDigits + 1 : maxDigits * 2;
    }
}
