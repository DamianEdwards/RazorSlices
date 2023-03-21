using System.Buffers;
using System.Buffers.Text;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Html;

namespace RazorSlices;

public abstract partial class RazorSlice
{
    // TODO: Add overloads for other values supported by Utf8Formatter.TryFormat, e.g. unsigned numeric types
    // TODO: Review this for HtmlEncoding requirements

    /// <summary>
    /// Write the specified <see cref="DateTime"/> value to the output.
    /// </summary>
    /// <param name="value">The value to write to the output.</param>
    protected void Write(DateTime? value) => WriteDateTime(value, default);

    /// <summary>
    /// Write the specified <see cref="DateTime"/> value to the output.
    /// </summary>
    /// <param name="value">The value to write to the output.</param>
    protected void Write(DateTime value) => WriteDateTime(value, default);

    /// <summary>
    /// Write the specified <see cref="DateTimeOffset"/> value to the output.
    /// </summary>
    /// <param name="value">The value to write to the output.</param>
    protected void Write(DateTimeOffset? value) => WriteDateTime(value, default);

    /// <summary>
    /// Write the specified <see cref="DateTimeOffset"/> value to the output.
    /// </summary>
    /// <param name="value">The value to write to the output.</param>
    protected void Write(DateTimeOffset value) => WriteDateTime(value, default);

    /// <summary>
    /// Write the specified <see cref="TimeSpan"/> value to the output.
    /// </summary>
    /// <remarks>
    /// <para>
    /// You generally shouldn't call this method directly. The Razor compiler will emit the appropriate calls to this method for
    /// any Razor write expression for a <see cref="TimeSpan"/> value in your .cshtml file, e.g. <c>@item.Qty</c>.
    /// </para>
    /// <para>
    /// To specify a format, call <see cref="WriteTimeSpan(TimeSpan?, StandardFormat)"/> instead, or use <see cref="TimeSpan.ToString(string?)"/>,
    /// e.g. <c>@item.Duration?.ToString("G")</c>
    /// </para>
    /// </remarks>
    /// <param name="value">The value to write to the output.</param>
    protected void Write(TimeSpan? value) => WriteTimeSpan(value, default);

    /// <summary>
    /// Write the specified <see cref="TimeSpan"/> value to the output.
    /// </summary>
    /// <remarks>
    /// <para>
    /// You generally shouldn't call this method directly. The Razor compiler will emit the appropriate calls to this method for
    /// any Razor write expression for a <see cref="TimeSpan"/> value in your .cshtml file, e.g. <c>@item.Qty</c>.
    /// </para>
    /// <para>
    /// To specify a format, call <see cref="WriteTimeSpan(TimeSpan, StandardFormat)"/> instead, or use <see cref="TimeSpan.ToString(string?)"/>,
    /// e.g. <c>@item.Duration.ToString("G")</c>
    /// </para>
    /// </remarks>
    /// <param name="value">The value to write to the output.</param>
    protected void Write(TimeSpan value) => WriteTimeSpan(value, default);

    /// <summary>
    /// Write the specified <see cref="byte"/> value to the output.
    /// </summary>
    /// <remarks>
    /// <para>
    /// You generally shouldn't call this method directly. The Razor compiler will emit the appropriate calls to this method for
    /// any Razor write expression for a <see cref="byte"/> value in your .cshtml file, e.g. <c>@item.Qty</c>.
    /// </para>
    /// <para>
    /// To specify a format, call <see cref="WriteNumeric(byte?, StandardFormat)"/> instead, or use <see cref="byte.ToString(string?)"/>,
    /// e.g. <c>@item.Qty?.ToString("G")</c>
    /// </para>
    /// </remarks>
    /// <param name="value">The value to write to the output.</param>
    protected void Write(byte? value) => WriteNumeric(value, default);

    /// <summary>
    /// Write the specified <see cref="byte"/> value to the output.
    /// </summary>
    /// <remarks>
    /// <para>
    /// You generally shouldn't call this method directly. The Razor compiler will emit the appropriate calls to this method for
    /// any Razor write expression for a <see cref="byte"/> value in your .cshtml file, e.g. <c>@item.Qty</c>.
    /// </para>
    /// <para>
    /// To specify a format, call <see cref="WriteNumeric(byte, StandardFormat)"/> instead, or use <see cref="byte.ToString(string?)"/>,
    /// e.g. <c>@item.Qty.ToString("G")</c>
    /// </para>
    /// </remarks>
    /// <param name="value">The value to write to the output.</param>
    protected void Write(byte value) => WriteNumeric(value, default);

    /// <summary>
    /// Write the specified <see cref="short"/> value to the output.
    /// </summary>
    /// <remarks>
    /// <para>
    /// You generally shouldn't call this method directly. The Razor compiler will emit the appropriate calls to this method for
    /// any Razor write expression for a <see cref="short"/> value in your .cshtml file, e.g. <c>@item.Qty</c>.
    /// </para>
    /// <para>
    /// To specify a format, call <see cref="WriteNumeric(short?, StandardFormat)"/> instead, or use <see cref="short.ToString(string?)"/>,
    /// e.g. <c>@item.Qty?.ToString("G")</c>
    /// </para>
    /// </remarks>
    /// <param name="value">The value to write to the output.</param>
    protected void Write(short? value) => WriteNumeric(value, default);

    /// <summary>
    /// Write the specified <see cref="short"/> value to the output.
    /// </summary>
    /// <remarks>
    /// <para>
    /// You generally shouldn't call this method directly. The Razor compiler will emit the appropriate calls to this method for
    /// any Razor write expression for a <see cref="short"/> value in your .cshtml file, e.g. <c>@item.Qty</c>.
    /// </para>
    /// <para>
    /// To specify a format, call <see cref="WriteNumeric(short, StandardFormat)"/> instead, or use <see cref="short.ToString(string?)"/>,
    /// e.g. <c>@item.Qty.ToString("G")</c>
    /// </para>
    /// </remarks>
    /// <param name="value">The value to write to the output.</param>
    protected void Write(short value) => WriteNumeric(value, default);

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
    /// Write the specified <see cref="long"/> value to the output.
    /// </summary>
    /// <param name="value">The value to write to the output.</param>
    protected void Write(long? value) => WriteNumeric(value, default);

    /// <summary>
    /// Write the specified <see cref="long"/> value to the output.
    /// </summary>
    /// <param name="value">The value to write to the output.</param>
    protected void Write(long value) => WriteNumeric(value, default);

    /// <summary>
    /// Write the specified <see cref="double"/> value to the output.
    /// </summary>
    /// <param name="value">The value to write to the output.</param>
    protected void Write(double? value) => WriteNumeric(value, default);

    /// <summary>
    /// Write the specified <see cref="double"/> value to the output.
    /// </summary>
    /// <param name="value">The value to write to the output.</param>
    protected void Write(double value) => WriteNumeric(value, default);

    /// <summary>
    /// Write the specified <see cref="decimal"/> value to the output.
    /// </summary>
    /// <param name="value">The value to write to the output.</param>
    protected void Write(decimal? value) => WriteNumeric(value, default);

    /// <summary>
    /// Write the specified <see cref="decimal"/> value to the output.
    /// </summary>
    /// <param name="value">The value to write to the output.</param>
    protected void Write(decimal value) => WriteNumeric(value, default);

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

        WriteFormattable(formattable);
    }

    /// <summary>
    /// Write the specified <see cref="ISpanFormattable"/> value to the output with the specified format.
    /// </summary>
    /// <param name="formattable">The value to write to the output.</param>
    /// <param name="format">The format to use when writing the value to the output.</param>
    /// <returns><see cref="HtmlString.Empty"/> to allow for easy calling via a Razor expression, e.g. <c>@WriteFormattable(item.DueBy, "d")</c></returns>
    protected HtmlString WriteFormattable(ISpanFormattable? formattable, ReadOnlySpan<char> format = default)
    {
        if (formattable is null)
        {
            return HtmlString.Empty;
        }

        _bufferWriter?.HtmlEncodeAndWrite(formattable, _htmlEncoder, format);
        _textWriter?.HtmlEncodeAndWrite(formattable, _htmlEncoder, format);

        return HtmlString.Empty;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value">The value to write to the output.</param>
    /// <param name="format">The format to use when writing the value.</param>
    /// <returns><see cref="HtmlString.Empty"/> to allow for easy calling via a Razor expression, e.g. <c>@WriteDateTime(item.DueBy, StandardFormat.Parse("G"))</c></returns>
    protected HtmlString WriteDateTime(DateTime? value, StandardFormat format)
    {
        if (value.HasValue)
        {
            return WriteDateTime(value.Value, format);
        }
        return HtmlString.Empty;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value">The value to write to the output.</param>
    /// <param name="format">The format to use when writing the value.</param>
    /// <returns><see cref="HtmlString.Empty"/> to allow for easy calling via a Razor expression, e.g. <c>@WriteDateTime(item.DueBy, StandardFormat.Parse("G"))</c></returns>
    protected HtmlString WriteDateTime(DateTime value, StandardFormat format)
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
                WriteDateTimeSlow(value, format, _bufferWriter);
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
    private static void WriteDateTimeSlow(DateTime value, StandardFormat format, IBufferWriter<byte> bufferWriter)
    {
        var lengthHint = 64;
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

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value">The value to write to the output.</param>
    /// <param name="format">The format to use when writing the value.</param>
    /// <returns><see cref="HtmlString.Empty"/> to allow for easy calling via a Razor expression, e.g. <c>@WriteDateTime(item.DueBy, StandardFormat.Parse("G"))</c></returns>
    protected HtmlString WriteDateTime(DateTimeOffset? value, StandardFormat format)
    {
        if (value.HasValue)
        {
            return WriteDateTime(value.Value, format);
        }
        return HtmlString.Empty;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value">The value to write to the output.</param>
    /// <param name="format">The format to use when writing the value.</param>
    /// <returns><see cref="HtmlString.Empty"/> to allow for easy calling via a Razor expression, e.g. <c>@WriteDateTime(item.DueBy, StandardFormat.Parse("G"))</c></returns>
    protected HtmlString WriteDateTime(DateTimeOffset value, StandardFormat format)
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
                WriteDateTimeSlow(value, format, _bufferWriter);
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
    private static void WriteDateTimeSlow(DateTimeOffset value, StandardFormat format, IBufferWriter<byte> bufferWriter)
    {
        var lengthHint = 64;
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

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value">The value to write to the output.</param>
    /// <param name="format">The format to use when writing the value.</param>
    /// <returns><see cref="HtmlString.Empty"/> to allow for easy calling via a Razor expression, e.g. <c>@WriteTimeSpan(entry.Duration, StandardFormat.Parse("G"))</c></returns>
    protected HtmlString WriteTimeSpan(TimeSpan? value, StandardFormat format)
    {
        if (value.HasValue)
        {
            return WriteTimeSpan(value.Value, format);
        }
        return HtmlString.Empty;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value">The value to write to the output.</param>
    /// <param name="format">The format to use when writing the value.</param>
    /// <returns><see cref="HtmlString.Empty"/> to allow for easy calling via a Razor expression, e.g. <c>@WriteTimeSpan(entry.Duration, StandardFormat.Parse("G"))</c></returns>
    protected HtmlString WriteTimeSpan(TimeSpan value, StandardFormat format)
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
                WriteTimeSpanSlow(value, format, _bufferWriter);
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
    private static void WriteTimeSpanSlow(TimeSpan value, StandardFormat format, IBufferWriter<byte> bufferWriter)
    {
        var lengthHint = 64;
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

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value">The value to write to the output.</param>
    /// <param name="format">The format to use when writing the value.</param>
    /// <returns><see cref="HtmlString.Empty"/> to allow for easy calling via a Razor expression, e.g. <c>@WriteNumeric(item.Qty, StandardFormat.Parse("G"))</c></returns>
    protected HtmlString WriteNumeric(byte? value, StandardFormat format)
    {
        if (value.HasValue)
        {
            return WriteNumeric(value.Value, format);
        }
        return HtmlString.Empty;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value">The value to write to the output.</param>
    /// <param name="format">The format to use when writing the value.</param>
    /// <returns><see cref="HtmlString.Empty"/> to allow for easy calling via a Razor expression, e.g. <c>@WriteNumeric(item.Qty, StandardFormat.Parse("G"))</c></returns>
    protected HtmlString WriteNumeric(byte value, StandardFormat format)
        => WriteByte(value, format);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value">The value to write to the output.</param>
    /// <param name="format">The format to use when writing the value.</param>
    /// <returns></returns>
    protected HtmlString WriteNumeric(short? value, StandardFormat format)
    {
        if (value.HasValue)
        {
            return WriteNumeric(value.Value, format);
        }
        return HtmlString.Empty;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value">The value to write to the output.</param>
    /// <param name="format">The format to use when writing the value.</param>
    /// <returns><see cref="HtmlString.Empty"/> to allow for easy calling via a Razor expression, e.g. <c>@WriteNumeric(item.Qty, StandardFormat.Parse("G"))</c></returns>
    protected HtmlString WriteNumeric(short value, StandardFormat format)
        => WriteInt16(value, format);

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
    /// 
    /// </summary>
    /// <param name="value"></param>
    /// <param name="format"></param>
    /// <returns><see cref="HtmlString.Empty"/> to allow for easy calling via a Razor expression, e.g. <c>@WriteNumeric(item.Qty, StandardFormat.Parse("G"))</c></returns>
    protected HtmlString WriteNumeric(long? value, StandardFormat format)
    {
        if (value.HasValue)
        {
            return WriteNumeric(value.Value, format);
        }
        return HtmlString.Empty;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    /// <param name="format"></param>
    /// <returns><see cref="HtmlString.Empty"/> to allow for easy calling via a Razor expression, e.g. <c>@WriteNumeric(item.Qty, StandardFormat.Parse("G"))</c></returns>
    protected HtmlString WriteNumeric(int value, StandardFormat format) =>
        WriteInt32(value, format);

    /// <summary>
    /// Write the specified <see cref="int"/> value to the output using the specified <see cref="StandardFormat"/>.
    /// </summary>
    /// <param name="value">The value to write to the output.</param>
    /// <param name="format">The format to use when writing the value.</param>
    /// <returns><see cref="HtmlString.Empty"/> to allow for easy calling via a Razor expression, e.g. <c>@WriteNumeric(item.Qty, StandardFormat.Parse("G"))</c></returns>
    protected HtmlString WriteNumeric(long value, StandardFormat format) =>
        WriteInt64(value, format);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value">The value to write to the output.</param>
    /// <param name="format">The format to use when writing the value.</param>
    /// <returns><see cref="HtmlString.Empty"/> to allow for easy calling via a Razor expression, e.g. <c>@WriteNumeric(item.Qty, StandardFormat.Parse("G"))</c></returns>
    protected HtmlString WriteNumeric(float? value, StandardFormat format)
    {
        if (value.HasValue)
        {
            return WriteNumeric(value.Value, format);
        }
        return HtmlString.Empty;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value">The value to write to the output.</param>
    /// <param name="format">The format to use when writing the value.</param>
    /// <returns><see cref="HtmlString.Empty"/> to allow for easy calling via a Razor expression, e.g. <c>@WriteNumeric(item.Qty, StandardFormat.Parse("G"))</c></returns>
    protected HtmlString WriteNumeric(float value, StandardFormat format)
        => WriteSingle(value, format);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value">The value to write to the output.</param>
    /// <param name="format">The format to use when writing the value.</param>
    /// <returns><see cref="HtmlString.Empty"/> to allow for easy calling via a Razor expression, e.g. <c>@WriteNumeric(item.Qty, StandardFormat.Parse("G"))</c></returns>
    protected HtmlString WriteNumeric(double? value, StandardFormat format)
    {
        if (value.HasValue)
        {
            return WriteNumeric(value.Value, format);
        }
        return HtmlString.Empty;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value">The value to write to the output.</param>
    /// <param name="format">The format to use when writing the value.</param>
    /// <returns><see cref="HtmlString.Empty"/> to allow for easy calling via a Razor expression, e.g. <c>@WriteNumeric(item.Qty, StandardFormat.Parse("G"))</c></returns>
    protected HtmlString WriteNumeric(double value, StandardFormat format)
        => WriteDouble(value, format);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value">The value to write to the output.</param>
    /// <param name="format">The format to use when writing the value.</param>
    /// <returns><see cref="HtmlString.Empty"/> to allow for easy calling via a Razor expression, e.g. <c>@WriteNumeric(item.Qty, StandardFormat.Parse("G"))</c></returns>
    protected HtmlString WriteNumeric(decimal? value, StandardFormat format)
    {
        if (value.HasValue)
        {
            return WriteNumeric(value.Value, format);
        }
        return HtmlString.Empty;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value">The value to write to the output.</param>
    /// <param name="format">The format to use when writing the value.</param>
    /// <returns><see cref="HtmlString.Empty"/> to allow for easy calling via a Razor expression, e.g. <c>@WriteNumeric(item.Qty, StandardFormat.Parse("G"))</c></returns>
    protected HtmlString WriteNumeric(decimal value, StandardFormat format)
        => WriteDecimal(value, format);

    private HtmlString WriteDecimal(decimal value, StandardFormat format)
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
                WriteDecimalSlow(value, format, _bufferWriter);
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
    private static void WriteDecimalSlow(decimal value, StandardFormat format, IBufferWriter<byte> bufferWriter)
    {
        var lengthHint = 64;
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

    private HtmlString WriteSingle(float value, StandardFormat format)
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
                WriteSingleSlow(value, format, _bufferWriter);
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
    private static void WriteSingleSlow(float value, StandardFormat format, IBufferWriter<byte> bufferWriter)
    {
        var lengthHint = 64;
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

    private HtmlString WriteDouble(double value, StandardFormat format)
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
                WriteDoubleSlow(value, format, _bufferWriter);
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
    private static void WriteDoubleSlow(double value, StandardFormat format, IBufferWriter<byte> bufferWriter)
    {
        var lengthHint = 64;
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

    private HtmlString WriteByte(byte value, StandardFormat format)
    {
        if (_bufferWriter is not null)
        {
            // Try to format the number into a fixed-length span first
            var span = _bufferWriter.GetSpan(3);

            if (Utf8Formatter.TryFormat(value, span, out var bytesWritten, format))
            {
                _bufferWriter.Advance(bytesWritten);
            }
            else
            {
                WriteByteSlow(value, format, _bufferWriter);
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
    private static void WriteByteSlow(byte value, StandardFormat format, IBufferWriter<byte> bufferWriter)
    {
        var lengthHint = 6;
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

    private HtmlString WriteInt16(short value, StandardFormat format)
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
                WriteInt16Slow(value, format, _bufferWriter);
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
    private static void WriteInt16Slow(short value, StandardFormat format, IBufferWriter<byte> bufferWriter)
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

    private HtmlString WriteInt32(int value, StandardFormat format)
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
                WriteInt32Slow(value, format, _bufferWriter);
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
    private static void WriteInt32Slow(long value, StandardFormat format, IBufferWriter<byte> bufferWriter)
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

    private HtmlString WriteInt64(long value, StandardFormat format)
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
                WriteInt64Slow(value, format, _bufferWriter);
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
    private static void WriteInt64Slow(long value, StandardFormat format, IBufferWriter<byte> bufferWriter)
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
    private static int GetFormattedLengthHint(long value, StandardFormat format)
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
