using System.Globalization;
using System.Numerics;
using Microsoft.AspNetCore.Html;

namespace RazorSlices;

public abstract partial class RazorSlice
{
    /// <summary>
    /// Writes a <see cref="char"/> value to the output.
    /// </summary>
    /// <remarks>
    /// <para>
    /// You generally shouldn't call this method directly. The Razor compiler will emit the appropriate calls to this method for
    /// all matching Razor expressions in your .cshtml file.
    /// </para>
    /// <para>
    /// To manually write out a <see cref="char"/> value, use <see cref="WriteChar(char?, ReadOnlySpan{char}, IFormatProvider?, bool)"/>
    /// instead, e.g. <c>@WriteChar('の')</c>
    /// </para>
    /// </remarks>
    /// <param name="value"></param>
    protected void Write(char? value)
    {
        if (value.HasValue)
        {
            WriteSpanFormattable(value.Value);
        }
    }

    /// <summary>
    /// Writes a <see cref="byte"/> value to the output.
    /// </summary>
    /// <remarks>
    /// <para>
    /// You generally shouldn't call this method directly. The Razor compiler will emit the appropriate calls to this method for
    /// all matching Razor expressions in your .cshtml file.
    /// </para>
    /// <para>
    /// To manually write out a <see cref="byte"/> value, use <see cref="WriteNumber(byte?, ReadOnlySpan{char}, IFormatProvider?, bool)"/>
    /// instead, e.g. <c>@WriteNumber(rgb.Red, "G", CultureInfo.InvariantCulture)</c>
    /// </para>
    /// </remarks>
    /// <param name="value"></param>
    protected void Write(byte? value)
    {
        if (value.HasValue)
        {
            WriteSpanFormattable(value.Value);
        }
    }

    /// <summary>
    /// Writes a <see cref="short"/> value to the output.
    /// </summary>
    /// <remarks>
    /// <para>
    /// You generally shouldn't call this method directly. The Razor compiler will emit the appropriate calls to this method for
    /// all matching Razor expressions in your .cshtml file.
    /// </para>
    /// <para>
    /// To manually write out a <see cref="short"/> value, use <see cref="WriteNumber(short?, ReadOnlySpan{char}, IFormatProvider?, bool)"/>
    /// instead, e.g. <c>@WriteNumber(item.Qty, "G", CultureInfo.InvariantCulture)</c>
    /// </para>
    /// </remarks>
    /// <param name="value"></param>
    protected void Write(short? value)
    {
        if (value.HasValue)
        {
            WriteSpanFormattable(value.Value);
        }
    }

    /// <summary>
    /// Writes a <see cref="ushort"/> value to the output.
    /// </summary>
    /// <remarks>
    /// <para>
    /// You generally shouldn't call this method directly. The Razor compiler will emit the appropriate calls to this method for
    /// all matching Razor expressions in your .cshtml file.
    /// </para>
    /// <para>
    /// To manually write out a <see cref="ushort"/> value, use <see cref="WriteNumber(ushort?, ReadOnlySpan{char}, IFormatProvider?, bool)"/>
    /// instead, e.g. <c>@WriteNumber(item.Qty, "G", CultureInfo.InvariantCulture)</c>
    /// </para>
    /// </remarks>
    /// <param name="value"></param>
    protected void Write(ushort? value)
    {
        if (value.HasValue)
        {
            WriteSpanFormattable(value.Value);
        }
    }

    /// <summary>
    /// Writes an <see cref="int"/> value to the output.
    /// </summary>
    /// <remarks>
    /// <para>
    /// You generally shouldn't call this method directly. The Razor compiler will emit the appropriate calls to this method for
    /// all matching Razor expressions in your .cshtml file.
    /// </para>
    /// <para>
    /// To manually write out a <see cref="int"/> value, use <see cref="WriteNumber(int?, ReadOnlySpan{char}, IFormatProvider?, bool)"/>
    /// instead, e.g. <c>@WriteNumber(item.Qty, "G", CultureInfo.InvariantCulture)</c>
    /// </para>
    /// </remarks>
    /// <param name="value"></param>
    protected void Write(int? value)
    {
        if (value.HasValue)
        {
            WriteSpanFormattable(value.Value);
        }
    }

    /// <summary>
    /// Writes a <see cref="uint"/> value to the output.
    /// </summary>
    /// <remarks>
    /// <para>
    /// You generally shouldn't call this method directly. The Razor compiler will emit the appropriate calls to this method for
    /// all matching Razor expressions in your .cshtml file.
    /// </para>
    /// <para>
    /// To manually write out a <see cref="uint"/> value, use <see cref="WriteNumber(uint?, ReadOnlySpan{char}, IFormatProvider?, bool)"/>
    /// instead, e.g. <c>@WriteNumber(item.Qty, "G", CultureInfo.InvariantCulture)</c>
    /// </para>
    /// </remarks>
    /// <param name="value"></param>
    protected void Write(uint? value)
    {
        if (value.HasValue)
        {
            WriteSpanFormattable(value.Value);
        }
    }

    /// <summary>
    /// Writes a <see cref="long"/> value to the output.
    /// </summary>
    /// <remarks>
    /// <para>
    /// You generally shouldn't call this method directly. The Razor compiler will emit the appropriate calls to this method for
    /// all matching Razor expressions in your .cshtml file.
    /// </para>
    /// <para>
    /// To manually write out a <see cref="long"/> value, use <see cref="WriteNumber(long?, ReadOnlySpan{char}, IFormatProvider?, bool)"/>
    /// instead, e.g. <c>@WriteNumber(events.Count, "E", CultureInfo.InvariantCulture)</c>
    /// </para>
    /// </remarks>
    /// <param name="value"></param>
    protected void Write(long? value)
    {
        if (value.HasValue)
        {
            WriteSpanFormattable(value.Value);
        }
    }

    /// <summary>
    /// Writes a <see cref="ulong"/> value to the output.
    /// </summary>
    /// <remarks>
    /// <para>
    /// You generally shouldn't call this method directly. The Razor compiler will emit the appropriate calls to this method for
    /// all matching Razor expressions in your .cshtml file.
    /// </para>
    /// <para>
    /// To manually write out a <see cref="ulong"/> value, use <see cref="WriteNumber(ulong?, ReadOnlySpan{char}, IFormatProvider?, bool)"/>
    /// instead, e.g. <c>@WriteNumber(events.Count, "E", CultureInfo.InvariantCulture)</c>
    /// </para>
    /// </remarks>
    /// <param name="value"></param>
    protected void Write(ulong? value)
    {
        if (value.HasValue)
        {
            WriteSpanFormattable(value.Value);
        }
    }

    /// <summary>
    /// Writes a <see cref="BigInteger"/> value to the output.
    /// </summary>
    /// <remarks>
    /// <para>
    /// You generally shouldn't call this method directly. The Razor compiler will emit the appropriate calls to this method for
    /// all matching Razor expressions in your .cshtml file.
    /// </para>
    /// <para>
    /// To manually write out a <see cref="BigInteger"/> value, use <see cref="WriteNumber(BigInteger?, ReadOnlySpan{char}, IFormatProvider?, bool)"/>
    /// instead, e.g. <c>@WriteNumber(events.Count, "E", CultureInfo.InvariantCulture)</c>
    /// </para>
    /// </remarks>
    /// <param name="value"></param>
    protected void Write(BigInteger? value)
    {
        if (value.HasValue)
        {
            WriteSpanFormattable(value.Value);
        }
    }

    /// <summary>
    /// Writes a <see cref="Half"/> value to the output.
    /// </summary>
    /// <remarks>
    /// <para>
    /// You generally shouldn't call this method directly. The Razor compiler will emit the appropriate calls to this method for
    /// all matching Razor expressions in your .cshtml file.
    /// </para>
    /// <para>
    /// To manually write out a <see cref="Half"/> value, use <see cref="WriteNumber(Half?, ReadOnlySpan{char}, IFormatProvider?, bool)"/>
    /// instead, e.g. <c>@WriteNumber(matrix.A, "C")</c>
    /// </para>
    /// </remarks>
    /// <param name="value"></param>
    protected void Write(Half? value)
    {
        if (value.HasValue)
        {
            WriteSpanFormattable(value.Value);
        }
    }

    /// <summary>
    /// Writes a <see cref="float"/> value to the output.
    /// </summary>
    /// <remarks>
    /// <para>
    /// You generally shouldn't call this method directly. The Razor compiler will emit the appropriate calls to this method for
    /// all matching Razor expressions in your .cshtml file.
    /// </para>
    /// <para>
    /// To manually write out a <see cref="float"/> value, use <see cref="WriteNumber(float?, ReadOnlySpan{char}, IFormatProvider?, bool)"/>
    /// instead, e.g. <c>@WriteNumber(item.Price, "C")</c>
    /// </para>
    /// </remarks>
    /// <param name="value"></param>
    protected void Write(float? value)
    {
        if (value.HasValue)
        {
            WriteSpanFormattable(value.Value);
        }
    }

    /// <summary>
    /// Writes a <see cref="double"/> value to the output.
    /// </summary>
    /// <remarks>
    /// <para>
    /// You generally shouldn't call this method directly. The Razor compiler will emit the appropriate calls to this method for
    /// all matching Razor expressions in your .cshtml file.
    /// </para>
    /// <para>
    /// To manually write out a <see cref="double"/> value, use <see cref="WriteNumber(double?, ReadOnlySpan{char}, IFormatProvider?, bool)"/>
    /// instead, e.g. <c>@WriteNumber(item.Price, "C")</c>
    /// </para>
    /// </remarks>
    /// <param name="value"></param>
    protected void Write(double? value)
    {
        if (value.HasValue)
        {
            WriteSpanFormattable(value.Value);
        }
    }

    /// <summary>
    /// Writes a <see cref="decimal"/> value to the output.
    /// </summary>
    /// <remarks>
    /// <para>
    /// You generally shouldn't call this method directly. The Razor compiler will emit the appropriate calls to this method for
    /// all matching Razor expressions in your .cshtml file.
    /// </para>
    /// <para>
    /// To manually write out a <see cref="decimal"/> value, use <see cref="WriteNumber(decimal?, ReadOnlySpan{char}, IFormatProvider?, bool)"/>
    /// instead, e.g. <c>@WriteNumber(item.Price, "C")</c>
    /// </para>
    /// </remarks>
    /// <param name="value"></param>
    protected void Write(decimal? value)
    {
        if (value.HasValue)
        {
            WriteSpanFormattable(value.Value);
        }
    }

    /// <summary>
    /// Writes a <see cref="DateTime"/> value to the output.
    /// </summary>
    /// <remarks>
    /// <para>
    /// You generally shouldn't call this method directly. The Razor compiler will emit the appropriate calls to this method for
    /// all matching Razor expressions in your .cshtml file.
    /// </para>
    /// <para>
    /// To manually write out a <see cref="DateTime"/> value, use <see cref="WriteDate(DateTime?, ReadOnlySpan{char}, IFormatProvider?, bool)"/>
    /// instead, e.g. <c>@WriteDate(todo.DueBy, "yyyy-MM-dd")</c>
    /// </para>
    /// </remarks>
    /// <param name="value"></param>
    protected void Write(DateTime? value)
    {
        if (value.HasValue)
        {
            WriteSpanFormattable(value.Value);
        }
    }

    /// <summary>
    /// Writes a <see cref="DateTimeOffset"/> value to the output.
    /// </summary>
    /// <remarks>
    /// <para>
    /// You generally shouldn't call this method directly. The Razor compiler will emit the appropriate calls to this method for
    /// all matching Razor expressions in your .cshtml file.
    /// </para>
    /// <para>
    /// To manually write out a <see cref="DateTimeOffset"/> value, use <see cref="WriteDate(DateTimeOffset?, ReadOnlySpan{char}, IFormatProvider?, bool)"/>
    /// instead, e.g. <c>@WriteDate(todo.DueBy, "yyyy-MM-dd")</c>
    /// </para>
    /// </remarks>
    /// <param name="value"></param>
    protected void Write(DateTimeOffset? value)
    {
        if (value.HasValue)
        {
            WriteSpanFormattable(value.Value);
        }
    }

    /// <summary>
    /// Writes a <see cref="TimeSpan"/> value to the output.
    /// </summary>
    /// <remarks>
    /// <para>
    /// You generally shouldn't call this method directly. The Razor compiler will emit the appropriate calls to this method for
    /// all matching Razor expressions in your .cshtml file.
    /// </para>
    /// <para>
    /// To manually write out a <see cref="TimeSpan"/> value, use <see cref="WriteTimeSpan(TimeSpan?, ReadOnlySpan{char}, IFormatProvider?, bool)"/>
    /// instead, e.g. <c>@WriteTimeSpan(appointment.Duration, "HH:mm:ss")</c>
    /// </para>
    /// </remarks>
    /// <param name="value"></param>
    protected void Write(TimeSpan? value)
    {
        if (value.HasValue)
        {
            WriteSpanFormattable(value.Value);
        }
    }

    /// <summary>
    /// Writes a <see cref="DateOnly"/> value to the output.
    /// </summary>
    /// <remarks>
    /// <para>
    /// You generally shouldn't call this method directly. The Razor compiler will emit the appropriate calls to this method for
    /// all matching Razor expressions in your .cshtml file.
    /// </para>
    /// <para>
    /// To manually write out a <see cref="DateOnly"/> value, use <see cref="WriteDate(DateOnly?, ReadOnlySpan{char}, IFormatProvider?, bool)"/>
    /// instead, e.g. <c>@WriteDate(todo.DueBy, "yyyy-MM-dd")</c>
    /// </para>
    /// </remarks>
    /// <param name="value"></param>
    protected void Write(DateOnly? value)
    {
        if (value.HasValue)
        {
            WriteSpanFormattable(value.Value);
        }
    }

    /// <summary>
    /// Writes a <see cref="TimeOnly"/> value to the output.
    /// </summary>
    /// <remarks>
    /// <para>
    /// You generally shouldn't call this method directly. The Razor compiler will emit the appropriate calls to this method for
    /// all matching Razor expressions in your .cshtml file.
    /// </para>
    /// <para>
    /// To manually write out a <see cref="TimeOnly"/> value, use <see cref="WriteTime(TimeOnly?, ReadOnlySpan{char}, IFormatProvider?, bool)"/>
    /// instead, e.g. <c>@WriteTime(appointment.StartsAt, "HH:mm")</c>
    /// </para>
    /// </remarks>
    /// <param name="value"></param>
    protected void Write(TimeOnly? value)
    {
        if (value.HasValue)
        {
            WriteSpanFormattable(value.Value);
        }
    }

    /// <summary>
    /// Writes a <see cref="Guid"/> value to the output.
    /// </summary>
    /// <remarks>
    /// <para>
    /// You generally shouldn't call this method directly. The Razor compiler will emit the appropriate calls to this method for
    /// all matching Razor expressions in your .cshtml file.
    /// </para>
    /// <para>
    /// To manually write out a <see cref="Guid"/> value, use <see cref="WriteGuid(Guid?, ReadOnlySpan{char}, IFormatProvider?, bool)"/>
    /// instead, e.g. <c>@WriteGuid(item.Id, "D")</c>
    /// </para>
    /// </remarks>
    /// <param name="value"></param>
    protected void Write(Guid? value)
    {
        if (value.HasValue)
        {
            WriteSpanFormattable(value.Value);
        }
    }

    /// <summary>
    /// Writes a <see cref="Version"/> value to the output.
    /// </summary>
    /// <remarks>
    /// <para>
    /// You generally shouldn't call this method directly. The Razor compiler will emit the appropriate calls to this method for
    /// all matching Razor expressions in your .cshtml file.
    /// </para>
    /// <para>
    /// To manually write out a <see cref="Version"/> value, use <see cref="WriteVersion(Version?, ReadOnlySpan{char}, IFormatProvider?, bool)"/>
    /// instead, e.g. <c>@WriteVersion(assembly.Version)</c>
    /// </para>
    /// </remarks>
    /// <param name="value"></param>
    protected void Write(Version? value)
    {
        if (value is not null)
        {
            WriteSpanFormattable(value);
        }
    }


    /// <summary>
    /// Writes a <see cref="char"/> value to the output.
    /// </summary>
    /// <param name="value">The value to write to the output.</param>
    /// <param name="format">The format to use when writing the value to the output. Defaults to the default format for the value's type if not provided.</param>
    /// <param name="formatProvider">The <see cref="IFormatProvider" /> to use when writing the value to the output. Defaults to <see cref="CultureInfo.CurrentCulture"/> if <c>null</c>.</param>
    /// <param name="htmlEncode">Whether to HTML encode the value or not. Defaults to <c>true</c>.</param>
    /// <returns><see cref="HtmlString.Empty"/> to allow for easy calling via a Razor expression, e.g. <c>@WriteChar('の')</c></returns>
    protected HtmlString WriteChar(char? value, ReadOnlySpan<char> format = default, IFormatProvider? formatProvider = null, bool htmlEncode = true)
    {
        if (value.HasValue)
        {
            WriteSpanFormattable(value.Value, format, formatProvider, htmlEncode);
        }
        return HtmlString.Empty;
    }

    /// <summary>
    /// Writes a <see cref="byte"/> value to the output.
    /// </summary>
    /// <param name="value">The value to write to the output.</param>
    /// <param name="format">The format to use when writing the value to the output. Defaults to the default format for the value's type if not provided.</param>
    /// <param name="formatProvider">The <see cref="IFormatProvider" /> to use when writing the value to the output. Defaults to <see cref="CultureInfo.CurrentCulture"/> if <c>null</c>.</param>
    /// <param name="htmlEncode">Whether to HTML encode the value or not. Defaults to <c>true</c>.</param>
    /// <returns><see cref="HtmlString.Empty"/> to allow for easy calling via a Razor expression, e.g. <c>@WriteNumber(rgb.Red, "G", CultureInfo.InvariantCulture)</c></returns>
    protected HtmlString WriteNumber(byte? value, ReadOnlySpan<char> format = default, IFormatProvider? formatProvider = null, bool htmlEncode = true)
    {
        if (value.HasValue)
        {
            WriteSpanFormattable(value.Value, format, formatProvider, htmlEncode);
        }
        return HtmlString.Empty;
    }

    /// <summary>
    /// Writes a <see cref="short"/> value to the output.
    /// </summary>
    /// <param name="value">The value to write to the output.</param>
    /// <param name="format">The format to use when writing the value to the output. Defaults to the default format for the value's type if not provided.</param>
    /// <param name="formatProvider">The <see cref="IFormatProvider" /> to use when writing the value to the output. Defaults to <see cref="CultureInfo.CurrentCulture"/> if <c>null</c>.</param>
    /// <param name="htmlEncode">Whether to HTML encode the value or not. Defaults to <c>true</c>.</param>
    /// <returns><see cref="HtmlString.Empty"/> to allow for easy calling via a Razor expression, e.g. <c>@WriteNumber(item.Qty, "G", CultureInfo.InvariantCulture)</c></returns>
    protected HtmlString WriteNumber(short? value, ReadOnlySpan<char> format = default, IFormatProvider? formatProvider = null, bool htmlEncode = true)
    {
        if (value.HasValue)
        {
            WriteSpanFormattable(value.Value, format, formatProvider, htmlEncode);
        }
        return HtmlString.Empty;
    }

    /// <summary>
    /// Writes a <see cref="ushort"/> value to the output.
    /// </summary>
    /// <param name="value">The value to write to the output.</param>
    /// <param name="format">The format to use when writing the value to the output. Defaults to the default format for the value's type if not provided.</param>
    /// <param name="formatProvider">The <see cref="IFormatProvider" /> to use when writing the value to the output. Defaults to <see cref="CultureInfo.CurrentCulture"/> if <c>null</c>.</param>
    /// <param name="htmlEncode">Whether to HTML encode the value or not. Defaults to <c>true</c>.</param>
    /// <returns><see cref="HtmlString.Empty"/> to allow for easy calling via a Razor expression, e.g. <c>@WriteNumber(item.Qty, "G", CultureInfo.InvariantCulture)</c></returns>
    protected HtmlString WriteNumber(ushort? value, ReadOnlySpan<char> format = default, IFormatProvider? formatProvider = null, bool htmlEncode = true)
    {
        if (value.HasValue)
        {
            WriteSpanFormattable(value.Value, format, formatProvider, htmlEncode);
        }
        return HtmlString.Empty;
    }

    /// <summary>
    /// Writes an <see cref="int"/> value to the output.
    /// </summary>
    /// <param name="value">The value to write to the output.</param>
    /// <param name="format">The format to use when writing the value to the output. Defaults to the default format for the value's type if not provided.</param>
    /// <param name="formatProvider">The <see cref="IFormatProvider" /> to use when writing the value to the output. Defaults to <see cref="CultureInfo.CurrentCulture"/> if <c>null</c>.</param>
    /// <param name="htmlEncode">Whether to HTML encode the value or not. Defaults to <c>true</c>.</param>
    /// <returns><see cref="HtmlString.Empty"/> to allow for easy calling via a Razor expression, e.g. <c>@WriteNumber(item.Qty, "G", CultureInfo.InvariantCulture)</c></returns>
    protected HtmlString WriteNumber(int? value, ReadOnlySpan<char> format = default, IFormatProvider? formatProvider = null, bool htmlEncode = true)
    {
        if (value.HasValue)
        {
            WriteSpanFormattable(value.Value, format, formatProvider, htmlEncode);
        }
        return HtmlString.Empty;
    }

    /// <summary>
    /// Writes a <see cref="uint"/> value to the output.
    /// </summary>
    /// <param name="value">The value to write to the output.</param>
    /// <param name="format">The format to use when writing the value to the output. Defaults to the default format for the value's type if not provided.</param>
    /// <param name="formatProvider">The <see cref="IFormatProvider" /> to use when writing the value to the output. Defaults to <see cref="CultureInfo.CurrentCulture"/> if <c>null</c>.</param>
    /// <param name="htmlEncode">Whether to HTML encode the value or not. Defaults to <c>true</c>.</param>
    /// <returns><see cref="HtmlString.Empty"/> to allow for easy calling via a Razor expression, e.g. <c>@WriteNumber(item.Qty, "G", CultureInfo.InvariantCulture)</c></returns>
    protected HtmlString WriteNumber(uint? value, ReadOnlySpan<char> format = default, IFormatProvider? formatProvider = null, bool htmlEncode = true)
    {
        if (value.HasValue)
        {
            WriteSpanFormattable(value.Value, format, formatProvider, htmlEncode);
        }
        return HtmlString.Empty;
    }

    /// <summary>
    /// Writes a <see cref="long"/> value to the output.
    /// </summary>
    /// <param name="value">The value to write to the output.</param>
    /// <param name="format">The format to use when writing the value to the output. Defaults to the default format for the value's type if not provided.</param>
    /// <param name="formatProvider">The <see cref="IFormatProvider" /> to use when writing the value to the output. Defaults to <see cref="CultureInfo.CurrentCulture"/> if <c>null</c>.</param>
    /// <param name="htmlEncode">Whether to HTML encode the value or not. Defaults to <c>true</c>.</param>
    /// <returns><see cref="HtmlString.Empty"/> to allow for easy calling via a Razor expression, e.g. <c>@WriteNumber(events.Count, "E", CultureInfo.InvariantCulture)</c></returns>
    protected HtmlString WriteNumber(long? value, ReadOnlySpan<char> format = default, IFormatProvider? formatProvider = null, bool htmlEncode = true)
    {
        if (value.HasValue)
        {
            WriteSpanFormattable(value.Value, format, formatProvider, htmlEncode);
        }
        return HtmlString.Empty;
    }

    /// <summary>
    /// Writes a <see cref="ulong"/> value to the output.
    /// </summary>
    /// <param name="value">The value to write to the output.</param>
    /// <param name="format">The format to use when writing the value to the output. Defaults to the default format for the value's type if not provided.</param>
    /// <param name="formatProvider">The <see cref="IFormatProvider" /> to use when writing the value to the output. Defaults to <see cref="CultureInfo.CurrentCulture"/> if <c>null</c>.</param>
    /// <param name="htmlEncode">Whether to HTML encode the value or not. Defaults to <c>true</c>.</param>
    /// <returns><see cref="HtmlString.Empty"/> to allow for easy calling via a Razor expression, e.g. <c>@WriteNumber(events.Count, "E", CultureInfo.InvariantCulture)</c></returns>
    protected HtmlString WriteNumber(ulong? value, ReadOnlySpan<char> format = default, IFormatProvider? formatProvider = null, bool htmlEncode = true)
    {
        if (value.HasValue)
        {
            WriteSpanFormattable(value.Value, format, formatProvider, htmlEncode);
        }
        return HtmlString.Empty;
    }

    /// <summary>
    /// Writes a <see cref="BigInteger"/> value to the output.
    /// </summary>
    /// <param name="value">The value to write to the output.</param>
    /// <param name="format">The format to use when writing the value to the output. Defaults to the default format for the value's type if not provided.</param>
    /// <param name="formatProvider">The <see cref="IFormatProvider" /> to use when writing the value to the output. Defaults to <see cref="CultureInfo.CurrentCulture"/> if <c>null</c>.</param>
    /// <param name="htmlEncode">Whether to HTML encode the value or not. Defaults to <c>true</c>.</param>
    /// <returns><see cref="HtmlString.Empty"/> to allow for easy calling via a Razor expression, e.g. <c>@WriteNumber(events.Count, "E", CultureInfo.InvariantCulture)</c></returns>
    protected HtmlString WriteNumber(BigInteger? value, ReadOnlySpan<char> format = default, IFormatProvider? formatProvider = null, bool htmlEncode = true)
    {
        if (value.HasValue)
        {
            WriteSpanFormattable(value.Value, format, formatProvider, htmlEncode);
        }
        return HtmlString.Empty;
    }

    /// <summary>
    /// Writes a <see cref="Half"/> value to the output.
    /// </summary>
    /// <param name="value">The value to write to the output.</param>
    /// <param name="format">The format to use when writing the value to the output. Defaults to the default format for the value's type if not provided.</param>
    /// <param name="formatProvider">The <see cref="IFormatProvider" /> to use when writing the value to the output. Defaults to <see cref="CultureInfo.CurrentCulture"/> if <c>null</c>.</param>
    /// <param name="htmlEncode">Whether to HTML encode the value or not. Defaults to <c>true</c>.</param>
    /// <returns><see cref="HtmlString.Empty"/> to allow for easy calling via a Razor expression, e.g. <c>@WriteNumber(matrix.A, "C")</c></returns>
    protected HtmlString WriteNumber(Half? value, ReadOnlySpan<char> format = default, IFormatProvider? formatProvider = null, bool htmlEncode = true)
    {
        if (value.HasValue)
        {
            WriteSpanFormattable(value.Value, format, formatProvider, htmlEncode);
        }
        return HtmlString.Empty;
    }

    /// <summary>
    /// Writes a <see cref="float"/> value to the output.
    /// </summary>
    /// <param name="value">The value to write to the output.</param>
    /// <param name="format">The format to use when writing the value to the output. Defaults to the default format for the value's type if not provided.</param>
    /// <param name="formatProvider">The <see cref="IFormatProvider" /> to use when writing the value to the output. Defaults to <see cref="CultureInfo.CurrentCulture"/> if <c>null</c>.</param>
    /// <param name="htmlEncode">Whether to HTML encode the value or not. Defaults to <c>true</c>.</param>
    /// <returns><see cref="HtmlString.Empty"/> to allow for easy calling via a Razor expression, e.g. <c>@WriteNumber(item.Price, "C")</c></returns>
    protected HtmlString WriteNumber(float? value, ReadOnlySpan<char> format = default, IFormatProvider? formatProvider = null, bool htmlEncode = true)
    {
        if (value.HasValue)
        {
            WriteSpanFormattable(value.Value, format, formatProvider, htmlEncode);
        }
        return HtmlString.Empty;
    }

    /// <summary>
    /// Writes a <see cref="double"/> value to the output.
    /// </summary>
    /// <param name="value">The value to write to the output.</param>
    /// <param name="format">The format to use when writing the value to the output. Defaults to the default format for the value's type if not provided.</param>
    /// <param name="formatProvider">The <see cref="IFormatProvider" /> to use when writing the value to the output. Defaults to <see cref="CultureInfo.CurrentCulture"/> if <c>null</c>.</param>
    /// <param name="htmlEncode">Whether to HTML encode the value or not. Defaults to <c>true</c>.</param>
    /// <returns><see cref="HtmlString.Empty"/> to allow for easy calling via a Razor expression, e.g. <c>@WriteNumber(item.Price, "C")</c></returns>
    protected HtmlString WriteNumber(double? value, ReadOnlySpan<char> format = default, IFormatProvider? formatProvider = null, bool htmlEncode = true)
    {
        if (value.HasValue)
        {
            WriteSpanFormattable(value.Value, format, formatProvider, htmlEncode);
        }
        return HtmlString.Empty;
    }

    /// <summary>
    /// Writes a <see cref="decimal"/> value to the output.
    /// </summary>
    /// <param name="value">The value to write to the output.</param>
    /// <param name="format">The format to use when writing the value to the output. Defaults to the default format for the value's type if not provided.</param>
    /// <param name="formatProvider">The <see cref="IFormatProvider" /> to use when writing the value to the output. Defaults to <see cref="CultureInfo.CurrentCulture"/> if <c>null</c>.</param>
    /// <param name="htmlEncode">Whether to HTML encode the value or not. Defaults to <c>true</c>.</param>
    /// <returns><see cref="HtmlString.Empty"/> to allow for easy calling via a Razor expression, e.g. <c>@WriteNumber(item.Price, "C")</c></returns>
    protected HtmlString WriteNumber(decimal? value, ReadOnlySpan<char> format = default, IFormatProvider? formatProvider = null, bool htmlEncode = true)
    {
        if (value.HasValue)
        {
            WriteSpanFormattable(value.Value, format, formatProvider, htmlEncode);
        }
        return HtmlString.Empty;
    }

    /// <summary>
    /// Writes a <see cref="DateTime"/> value to the output.
    /// </summary>
    /// <param name="value">The value to write to the output.</param>
    /// <param name="format">The format to use when writing the value to the output. Defaults to the default format for the value's type if not provided.</param>
    /// <param name="formatProvider">The <see cref="IFormatProvider" /> to use when writing the value to the output. Defaults to <see cref="CultureInfo.CurrentCulture"/> if <c>null</c>.</param>
    /// <param name="htmlEncode">Whether to HTML encode the value or not. Defaults to <c>true</c>.</param>
    /// <returns><see cref="HtmlString.Empty"/> to allow for easy calling via a Razor expression, e.g. <c>@WriteDate(todo.DueBy, "yyyy-MM-dd")</c></returns>
    protected HtmlString WriteDate(DateTime? value, ReadOnlySpan<char> format = default, IFormatProvider? formatProvider = null, bool htmlEncode = true)
    {
        if (value.HasValue)
        {
            WriteSpanFormattable(value.Value, format, formatProvider, htmlEncode);
        }
        return HtmlString.Empty;
    }

    /// <summary>
    /// Writes a <see cref="DateTimeOffset"/> value to the output.
    /// </summary>
    /// <param name="value">The value to write to the output.</param>
    /// <param name="format">The format to use when writing the value to the output. Defaults to the default format for the value's type if not provided.</param>
    /// <param name="formatProvider">The <see cref="IFormatProvider" /> to use when writing the value to the output. Defaults to <see cref="CultureInfo.CurrentCulture"/> if <c>null</c>.</param>
    /// <param name="htmlEncode">Whether to HTML encode the value or not. Defaults to <c>true</c>.</param>
    /// <returns><see cref="HtmlString.Empty"/> to allow for easy calling via a Razor expression, e.g. <c>@WriteDate(todo.DueBy, "yyyy-MM-dd")</c></returns>
    protected HtmlString WriteDate(DateTimeOffset? value, ReadOnlySpan<char> format = default, IFormatProvider? formatProvider = null, bool htmlEncode = true)
    {
        if (value.HasValue)
        {
            WriteSpanFormattable(value.Value, format, formatProvider, htmlEncode);
        }
        return HtmlString.Empty;
    }

    /// <summary>
    /// Writes a <see cref="TimeSpan"/> value to the output.
    /// </summary>
    /// <param name="value">The value to write to the output.</param>
    /// <param name="format">The format to use when writing the value to the output. Defaults to the default format for the value's type if not provided.</param>
    /// <param name="formatProvider">The <see cref="IFormatProvider" /> to use when writing the value to the output. Defaults to <see cref="CultureInfo.CurrentCulture"/> if <c>null</c>.</param>
    /// <param name="htmlEncode">Whether to HTML encode the value or not. Defaults to <c>true</c>.</param>
    /// <returns><see cref="HtmlString.Empty"/> to allow for easy calling via a Razor expression, e.g. <c>@WriteTimeSpan(appointment.Duration, "HH:mm:ss")</c></returns>
    protected HtmlString WriteTimeSpan(TimeSpan? value, ReadOnlySpan<char> format = default, IFormatProvider? formatProvider = null, bool htmlEncode = true)
    {
        if (value.HasValue)
        {
            WriteSpanFormattable(value.Value, format, formatProvider, htmlEncode);
        }
        return HtmlString.Empty;
    }

    /// <summary>
    /// Writes a <see cref="DateOnly"/> value to the output.
    /// </summary>
    /// <param name="value">The value to write to the output.</param>
    /// <param name="format">The format to use when writing the value to the output. Defaults to the default format for the value's type if not provided.</param>
    /// <param name="formatProvider">The <see cref="IFormatProvider" /> to use when writing the value to the output. Defaults to <see cref="CultureInfo.CurrentCulture"/> if <c>null</c>.</param>
    /// <param name="htmlEncode">Whether to HTML encode the value or not. Defaults to <c>true</c>.</param>
    /// <returns><see cref="HtmlString.Empty"/> to allow for easy calling via a Razor expression, e.g. <c>@WriteDate(todo.DueBy, "yyyy-MM-dd")</c></returns>
    protected HtmlString WriteDate(DateOnly? value, ReadOnlySpan<char> format = default, IFormatProvider? formatProvider = null, bool htmlEncode = true)
    {
        if (value.HasValue)
        {
            WriteSpanFormattable(value.Value, format, formatProvider, htmlEncode);
        }
        return HtmlString.Empty;
    }

    /// <summary>
    /// Writes a <see cref="TimeOnly"/> value to the output.
    /// </summary>
    /// <param name="value">The value to write to the output.</param>
    /// <param name="format">The format to use when writing the value to the output. Defaults to the default format for the value's type if not provided.</param>
    /// <param name="formatProvider">The <see cref="IFormatProvider" /> to use when writing the value to the output. Defaults to <see cref="CultureInfo.CurrentCulture"/> if <c>null</c>.</param>
    /// <param name="htmlEncode">Whether to HTML encode the value or not. Defaults to <c>true</c>.</param>
    /// <returns><see cref="HtmlString.Empty"/> to allow for easy calling via a Razor expression, e.g. <c>@WriteTime(appointment.StartsAt, "HH:mm")</c></returns>
    protected HtmlString WriteTime(TimeOnly? value, ReadOnlySpan<char> format = default, IFormatProvider? formatProvider = null, bool htmlEncode = true)
    {
        if (value.HasValue)
        {
            WriteSpanFormattable(value.Value, format, formatProvider, htmlEncode);
        }
        return HtmlString.Empty;
    }

    /// <summary>
    /// Writes a <see cref="Guid"/> value to the output.
    /// </summary>
    /// <param name="value">The value to write to the output.</param>
    /// <param name="format">The format to use when writing the value to the output. Defaults to the default format for the value's type if not provided.</param>
    /// <param name="formatProvider">The <see cref="IFormatProvider" /> to use when writing the value to the output. Defaults to <see cref="CultureInfo.CurrentCulture"/> if <c>null</c>.</param>
    /// <param name="htmlEncode">Whether to HTML encode the value or not. Defaults to <c>true</c>.</param>
    /// <returns><see cref="HtmlString.Empty"/> to allow for easy calling via a Razor expression, e.g. <c>@WriteGuid(item.Id, "D")</c></returns>
    protected HtmlString WriteGuid(Guid? value, ReadOnlySpan<char> format = default, IFormatProvider? formatProvider = null, bool htmlEncode = true)
    {
        if (value.HasValue)
        {
            WriteSpanFormattable(value.Value, format, formatProvider, htmlEncode);
        }
        return HtmlString.Empty;
    }

    /// <summary>
    /// Writes a <see cref="Version"/> value to the output.
    /// </summary>
    /// <param name="value">The value to write to the output.</param>
    /// <param name="format">The format to use when writing the value to the output. Defaults to the default format for the value's type if not provided.</param>
    /// <param name="formatProvider">The <see cref="IFormatProvider" /> to use when writing the value to the output. Defaults to <see cref="CultureInfo.CurrentCulture"/> if <c>null</c>.</param>
    /// <param name="htmlEncode">Whether to HTML encode the value or not. Defaults to <c>true</c>.</param>
    /// <returns><see cref="HtmlString.Empty"/> to allow for easy calling via a Razor expression, e.g. <c>@WriteVersion(assembly.Version)</c></returns>
    protected HtmlString WriteVersion(Version? value, ReadOnlySpan<char> format = default, IFormatProvider? formatProvider = null, bool htmlEncode = true)
    {
        if (value is not null)
        {
            WriteSpanFormattable(value, format, formatProvider, htmlEncode);
        }
        return HtmlString.Empty;
    }


    private bool TryWriteFormattableValue<T>(T? value)
    {
        if (value is null)
        {
            return false;
        }
        if (value is char charValue)
        {
            WriteSpanFormattable(charValue, default, null);
            return true;
        }
        if (value is byte byteValue)
        {
            WriteSpanFormattable(byteValue, default, null);
            return true;
        }
        if (value is short shortValue)
        {
            WriteSpanFormattable(shortValue, default, null);
            return true;
        }
        if (value is ushort ushortValue)
        {
            WriteSpanFormattable(ushortValue, default, null);
            return true;
        }
        if (value is int intValue)
        {
            WriteSpanFormattable(intValue, default, null);
            return true;
        }
        if (value is uint uintValue)
        {
            WriteSpanFormattable(uintValue, default, null);
            return true;
        }
        if (value is long longValue)
        {
            WriteSpanFormattable(longValue, default, null);
            return true;
        }
        if (value is ulong ulongValue)
        {
            WriteSpanFormattable(ulongValue, default, null);
            return true;
        }
        if (value is BigInteger BigIntegerValue)
        {
            WriteSpanFormattable(BigIntegerValue, default, null);
            return true;
        }
        if (value is Half HalfValue)
        {
            WriteSpanFormattable(HalfValue, default, null);
            return true;
        }
        if (value is float floatValue)
        {
            WriteSpanFormattable(floatValue, default, null);
            return true;
        }
        if (value is double doubleValue)
        {
            WriteSpanFormattable(doubleValue, default, null);
            return true;
        }
        if (value is decimal decimalValue)
        {
            WriteSpanFormattable(decimalValue, default, null);
            return true;
        }
        if (value is DateTime DateTimeValue)
        {
            WriteSpanFormattable(DateTimeValue, default, null);
            return true;
        }
        if (value is DateTimeOffset DateTimeOffsetValue)
        {
            WriteSpanFormattable(DateTimeOffsetValue, default, null);
            return true;
        }
        if (value is TimeSpan TimeSpanValue)
        {
            WriteSpanFormattable(TimeSpanValue, default, null);
            return true;
        }
        if (value is DateOnly DateOnlyValue)
        {
            WriteSpanFormattable(DateOnlyValue, default, null);
            return true;
        }
        if (value is TimeOnly TimeOnlyValue)
        {
            WriteSpanFormattable(TimeOnlyValue, default, null);
            return true;
        }
        if (value is Guid GuidValue)
        {
            WriteSpanFormattable(GuidValue, default, null);
            return true;
        }
        if (value is Version VersionValue)
        {
            WriteSpanFormattable(VersionValue, default, null);
            return true;
        }
        return false;
    }
}
