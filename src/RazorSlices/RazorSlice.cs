﻿using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Internal;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace RazorSlices;

/// <summary>
/// Base class for a Razor Slice template.
/// </summary>
public abstract partial class RazorSlice : IDisposable
{
    private HtmlEncoder _htmlEncoder = HtmlEncoder.Default;
    private IBufferWriter<byte>? _bufferWriter;
    private TextWriter? _textWriter;
    private Utf8BufferTextWriter? _utf8BufferTextWriter;
    private Func<CancellationToken, ValueTask>? _outputFlush;
    private Dictionary<string, Func<Task>>? _sectionWriters;

    /// <summary>
    /// A token to monitor for cancellation requests.
    /// </summary>
    public CancellationToken CancellationToken { get; private set; }

    /// <summary>
    /// Implemented by the generated template class.
    /// </summary>
    /// <remarks>
    /// This method should not be called directly. Call
    /// <see cref="RenderAsync(IBufferWriter{byte}, Func{CancellationToken, ValueTask}?, HtmlEncoder?, CancellationToken)"/> or
    /// <see cref="RenderAsync(TextWriter, HtmlEncoder?, CancellationToken)"/> instead to render the template.
    /// </remarks>
    /// <returns>A <see cref="Task"/> representing the execution of the template.</returns>
    public abstract Task ExecuteAsync();

    /// <summary>
    /// Renders the template to the specified <see cref="IBufferWriter{T}"/>.
    /// </summary>
    /// <param name="bufferWriter">The <see cref="IBufferWriter{T}"/> to render the template to.</param>
    /// <param name="flushAsync">An optional delegate that flushes the <see cref="IBufferWriter{T}"/>.</param>
    /// <param name="htmlEncoder">An optional <see cref="HtmlEncoder"/> instance to use when rendering the template. If none is specified, <see cref="HtmlEncoder.Default"/> will be used.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the rendering of the template.</returns>
    [MemberNotNull(nameof(_bufferWriter))]
    public ValueTask RenderAsync(IBufferWriter<byte> bufferWriter, Func<CancellationToken, ValueTask>? flushAsync = null, HtmlEncoder? htmlEncoder = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bufferWriter);

        // TODO: Render via layout if LayoutAttribute is set

        _bufferWriter = bufferWriter;
        _textWriter = null;
        _outputFlush = flushAsync;
        _htmlEncoder = htmlEncoder ?? _htmlEncoder;

        CancellationToken = cancellationToken;

        var executeTask = ExecuteAsync();

        if (executeTask.HandleSynchronousCompletion())
        {
            return ValueTask.CompletedTask;
        }
        return new ValueTask(executeTask);

        // TODO: Should we explicitly flush here if flushAsync is not null?
    }

    /// <summary>
    /// Renders the template to the specified <see cref="TextWriter"/>.
    /// </summary>
    /// <param name="textWriter">The <see cref="TextWriter"/> to render the template to.</param>
    /// <param name="htmlEncoder">An optional <see cref="HtmlEncoder"/> instance to use when rendering the template. If none is specified, <see cref="HtmlEncoder.Default"/> will be used.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the rendering of the template.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="textWriter"/> is <c>null</c>.</exception>
    [MemberNotNull(nameof(_textWriter), nameof(_outputFlush))]
    public ValueTask RenderAsync(TextWriter textWriter, HtmlEncoder? htmlEncoder = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(textWriter);

        // TODO: Render via layout if LayoutAttribute is set

        _bufferWriter = null;
        _textWriter = textWriter;
        _outputFlush = (ct) =>
        {
#if NET8_0_OR_GREATER
            var flushTask = textWriter.FlushAsync(ct);
#else
            var flushTask = textWriter.FlushAsync();
#endif
            if (flushTask.IsCompletedSuccessfully)
            {
                return ValueTask.CompletedTask;
            }
            return AwaitOutputFlushTask(flushTask);
        };
        _htmlEncoder = htmlEncoder ?? _htmlEncoder;

        CancellationToken = cancellationToken;

        var executeTask = ExecuteAsync();

        if (executeTask.IsCompletedSuccessfully)
        {
            return ValueTask.CompletedTask;
        }
        return new ValueTask(executeTask);
    }

    private static async ValueTask AwaitOutputFlushTask(Task flushTask)
    {
        await flushTask;
    }

    /// <summary>
    /// Indicates whether <see cref="FlushAsync"/> will actually flush the underlying output during rendering.
    /// </summary>
    protected bool CanFlush => _outputFlush is not null;

    /// <summary>
    /// Attempts to flush the underlying output the template is being rendered to. Check <see cref="CanFlush"/> to determine if
    /// the output will actually be flushed or not before calling this method.
    /// </summary>
    /// <returns>A <see cref="ValueTask"/> representing the flush operation.</returns>
    protected ValueTask<HtmlString> FlushAsync()
    {
        if (!CanFlush || _outputFlush is null)
        {
            return ValueTask.FromResult(HtmlString.Empty);
        }

#pragma warning disable CA2012 // Use ValueTasks correctly: The ValueTask is observed in code below
        var flushTask = _outputFlush(CancellationToken);
#pragma warning restore CA2012

        if (flushTask.HandleSynchronousCompletion())
        {
            return ValueTask.FromResult(HtmlString.Empty);
        }

        return AwaitFlushAsyncTask(flushTask);
    }

    private static async ValueTask<HtmlString> AwaitFlushAsyncTask(ValueTask flushTask)
    {
        await flushTask;
        return HtmlString.Empty;
    }

    /// <summary>
    /// Defines a section that can be rendered on-demand via <see cref="RenderSectionAsync(string, bool)"/>.
    /// </summary>
    /// <remarks>
    /// You generally shouldn't call this method directly. The Razor compiler will emit the appropriate calls to this method
    /// for each use of the <c>@section</c> directive in your .cshtml file.
    /// </remarks>
    /// <param name="name">The name of the section.</param>
    /// <param name="section">The section delegate.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> or <paramref name="section"/> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when a section with this name is already defined.</exception>
    protected virtual void DefineSection(string name, Func<Task> section)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(section);

        _sectionWriters ??= new();

        if (_sectionWriters.ContainsKey(name))
        {
            throw new InvalidOperationException("Section already defined.");
        }
        _sectionWriters[name] = section;
    }

    /// <summary>
    /// Renders the section with the specified name.
    /// </summary>
    /// <param name="sectionName">The section name.</param>
    /// <param name="required">Whether the section is required or not.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> representing the rendering of the section.</returns>
    /// <exception cref="ArgumentException">Thrown when no section with name <paramref name="sectionName"/> has been defined by the slice being rendered.</exception>
    /// <exception cref="NotImplementedException"></exception>
    protected ValueTask<HtmlString> RenderSectionAsync(string sectionName, bool required)
    {
        var sectionDefined = _sectionWriters?.ContainsKey(sectionName) != true;
        if (required && !sectionDefined)
        {
            throw new ArgumentException($"The section '{sectionName}' has not been declared by the slice being rendered.");
        }
        else if (!required && !sectionDefined)
        {
            return ValueTask.FromResult(HtmlString.Empty);
        }

        throw new NotImplementedException("Haven't implemented layouts yet, but will!");
    }

    /// <summary>
    /// Writes a string value to the output without HTML encoding it.
    /// </summary>
    /// <remarks>
    /// You generally shouldn't call this method directly. The Razor compiler will emit the appropriate calls to this method for
    /// all blocks of HTML in your .cshtml file.
    /// </remarks>
    /// <param name="value">The value to write to the output.</param>
    protected void WriteLiteral(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        _bufferWriter?.WriteHtml(value.AsSpan());
        _textWriter?.Write(value);
    }

    /// <summary>
    /// Writes the string representation of the provided object to the output without HTML encoding it.
    /// </summary>
    /// <remarks>
    /// You generally shouldn't call this method directly. The Razor compiler will emit the appropriate calls to this method for
    /// all blocks of HTML in your .cshtml file.
    /// </remarks>
    /// <param name="value">The value to write to the output.</param>
    protected void WriteLiteral<T>(T? value)
    {
#if NET8_0_OR_GREATER
        if (value is IUtf8SpanFormattable)
        {
            WriteUtf8SpanFormattable((IUtf8SpanFormattable)(object)value, htmlEncode: false);
            return;
        }
#endif
        if (value is ISpanFormattable)
        {
            WriteSpanFormattable((ISpanFormattable)(object)value, htmlEncode: false);
            return;
        }
        WriteLiteral(value?.ToString());
    }

    /// <summary>
    /// Writes a buffer of UTF8 bytes to the output without HTML encoding it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// You generally shouldn't call this method directly. The Razor compiler will emit the appropriate calls to this method for
    /// all blocks of HTML in your .cshtml file.
    /// </para>
    /// <para>
    /// NOTE: We'd need a tweak to the Razor compiler to to have it support emitting <see cref="WriteLiteral(ReadOnlySpan{byte})"/> calls with UTF8 string literals
    ///       i.e. https://github.com/dotnet/razor/issues/8429
    /// </para>
    /// </remarks>
    /// <param name="value">The value to write to the output.</param>
    protected void WriteLiteral(ReadOnlySpan<byte> value)
    {
        if (value.Length == 0)
        {
            return;
        }

        _bufferWriter?.Write(value);
        _textWriter?.WriteUtf8(value);
    }

    /// <summary>
    /// Writes a <see cref="bool"/> value to the output.
    /// </summary>
    /// <remarks>
    /// <para>
    /// You generally shouldn't call this method directly. The Razor compiler will emit the appropriate calls to this method for
    /// all matching Razor expressions in your .cshtml file.
    /// </para>
    /// <para>
    /// To manually write out a value, use <see cref="WriteBool"/> instead, e.g. <c>@WriteBool(todo.Complete)</c>
    /// </para>
    /// </remarks>
    /// <param name="value"></param>
    protected void Write(bool? value) => WriteBool(value);

    /// <summary>
    /// Writes a buffer of UTF8 bytes to the output after HTML encoding it.
    /// </summary>
    /// <remarks>
    /// You generally shouldn't call this method directly. The Razor compiler will emit the appropriate calls to this method for
    /// all matching Razor expressions in your .cshtml file.
    /// </remarks>
    /// <param name="value">The value to write to the output.</param>
    protected void Write(byte[] value) => Write(value.AsSpan());

    /// <summary>
    /// Writes a buffer of UTF8 bytes to the output after HTML encoding it.
    /// </summary>
    /// <remarks>
    /// You generally shouldn't call this method directly. The Razor compiler will emit the appropriate calls to this method for
    /// all matching Razor expressions in your .cshtml file.
    /// </remarks>
    /// <param name="value">The value to write to the output.</param>
    protected void Write(ReadOnlySpan<byte> value)
    {
        if (value.Length == 0)
        {
            return;
        }

        _bufferWriter?.HtmlEncodeAndWriteUtf8(value, _htmlEncoder);
        _textWriter?.HtmlEncodeAndWriteUtf8(value, _htmlEncoder);
    }

    /// <summary>
    /// Writes the specified <see cref="HtmlString"/> value to the output without HTML encoding it again.
    /// </summary>
    /// <remarks>
    /// <para>
    /// You generally shouldn't call this method directly. The Razor compiler will emit the appropriate calls to this method for
    /// all matching Razor expressions in your .cshtml file.
    /// </para>
    /// <para>
    /// To manually write out a value, use <see cref="WriteHtml{T}(T)"/> instead,
    /// e.g. <c>@WriteHtml(myCustomHtmlString)</c>
    /// </para>
    /// </remarks>
    /// <param name="htmlString">The <see cref="HtmlString"/> value to write to the output.</param>
    protected void Write(HtmlString htmlString)
    {
        if (htmlString is not null && htmlString != HtmlString.Empty)
        {
            WriteHtml(htmlString);
        }
    }

    /// <summary>
    /// Writes the specified <see cref="IHtmlContent"/> value to the output without HTML encoding it again.
    /// </summary>
    /// <param name="htmlContent">The <see cref="IHtmlContent"/> value to write to the output.</param>
    protected void Write(IHtmlContent? htmlContent)
    {
        if (htmlContent is not null)
        {
            WriteHtml(htmlContent);
        }
    }

    /// <summary>
    /// Writes the specified value to the output after HTML encoding it.
    /// </summary>
    /// <param name="value">The value to write to the output.</param>
    protected void Write(string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            Write(value.AsSpan());
        }
    }

    /// <summary>
    /// Writes the specified value to the output after HTML encoding it.
    /// </summary>
    /// <param name="value">The value to write to the output.</param>
    protected void Write(ReadOnlySpan<char> value)
    {
        if (value.Length > 0)
        {
            _bufferWriter?.HtmlEncodeAndWrite(value, _htmlEncoder);
            _textWriter?.HtmlEncodeAndWrite(value, _htmlEncoder);
        }
    }

    /// <summary>
    /// Writes the specified <typeparamref name="T"/> to the output.
    /// </summary>
    /// <remarks>
    /// You generally shouldn't call this method directly. The Razor compiler will emit calls to the most appropriate overload of
    /// the <c>Write</c> method for all Razor expressions in your .cshtml file, e.g. <c>@someVariable</c>.
    /// </remarks>
    /// <param name="value">The <typeparamref name="T"/> to write to the output.</param>
    protected void Write<T>(T? value)
    {
        WriteValue(value);
    }

    /// <summary>
    /// Writes a <see cref="bool"/> value to the output.
    /// </summary>
    /// <param name="value">The value to write to the output.</param>
    /// <returns><see cref="HtmlString.Empty"/> to allow for easy calling via a Razor expression, e.g. <c>@WriteBool(todo.Completed)</c></returns>
    protected HtmlString WriteBool(bool? value)
    {
        if (value.HasValue)
        {
            _bufferWriter?.Write(value.Value);
            _textWriter?.Write(value.Value);
        }
        return HtmlString.Empty;
    }

    /// <summary>
    /// Write the specified <see cref="ISpanFormattable"/> value to the output with the specified format and optional <see cref="IFormatProvider" />.
    /// </summary>
    /// <param name="formattable">The value to write to the output.</param>
    /// <param name="format">The format to use when writing the value to the output. Defaults to the default format for the value's type if not provided.</param>
    /// <param name="formatProvider">The <see cref="IFormatProvider" /> to use when writing the value to the output. Defaults to <see cref="CultureInfo.CurrentCulture"/> if <c>null</c>.</param>
    /// <param name="htmlEncode">Whether to HTML encode the value or not. Defaults to <c>true</c>.</param>
    /// <returns><see cref="HtmlString.Empty"/> to allow for easy calling via a Razor expression, e.g. <c>@WriteSpanFormattable(item.DueBy, "d")</c></returns>
    protected HtmlString WriteSpanFormattable<T>(T? formattable, ReadOnlySpan<char> format = default, IFormatProvider? formatProvider = null, bool htmlEncode = true)
        where T : ISpanFormattable
    {
        if (formattable is not null)
        {
            var htmlEncoder = htmlEncode ? _htmlEncoder : NullHtmlEncoder.Default;
            _bufferWriter?.HtmlEncodeAndWriteSpanFormattable(formattable, htmlEncoder, format, formatProvider);
            _textWriter?.HtmlEncodeAndWriteSpanFormattable(formattable, htmlEncoder, format, formatProvider);
        }

        return HtmlString.Empty;
    }

#if NET8_0_OR_GREATER
    /// <summary>
    /// Write the specified <see cref="IUtf8SpanFormattable"/> value to the output with the specified format and optional <see cref="IFormatProvider" />.
    /// </summary>
    /// <param name="formattable">The value to write to the output.</param>
    /// <param name="format">The format to use when writing the value to the output. Defaults to the default format for the value's type if not provided.</param>
    /// <param name="formatProvider">The <see cref="IFormatProvider" /> to use when writing the value to the output. Defaults to <see cref="CultureInfo.CurrentCulture"/> if <c>null</c>.</param>
    /// <param name="htmlEncode">Whether to HTML encode the value or not. Defaults to <c>true</c>.</param>
    /// <returns><see cref="HtmlString.Empty"/> to allow for easy calling via a Razor expression, e.g. <c>@WriteUtf8SpanFormattable(item.DueBy, "d")</c></returns>
    protected HtmlString WriteUtf8SpanFormattable<T>(T? formattable, ReadOnlySpan<char> format = default, IFormatProvider? formatProvider = null, bool htmlEncode = true)
        where T : IUtf8SpanFormattable
    {
        if (formattable is not null)
        {
            var htmlEncoder = htmlEncode ? _htmlEncoder : NullHtmlEncoder.Default;
            _bufferWriter?.HtmlEncodeAndWriteUtf8SpanFormattable(formattable, htmlEncoder, format, formatProvider);
            _textWriter?.HtmlEncodeAndWriteUtf8SpanFormattable(formattable, htmlEncoder, format, formatProvider);
        }

        return HtmlString.Empty;
    }
#endif

    /// <summary>
    /// Writes the specified <see cref="IHtmlContent"/> value to the output.
    /// </summary>
    /// <param name="htmlContent">The <see cref="IHtmlContent"/> value to write to the output.</param>
    /// <returns><see cref="HtmlString.Empty"/> to allow for easy calling via a Razor expression, e.g. <c>@WriteHtmlContent(myCustomHtmlContent)</c></returns>
    protected HtmlString WriteHtml<T>(T htmlContent)
        where T : IHtmlContent
    {
        if (htmlContent is not null)
        {
            if (_bufferWriter is not null)
            {
                _utf8BufferTextWriter ??= Utf8BufferTextWriter.Get(_bufferWriter);
                htmlContent.WriteTo(_utf8BufferTextWriter, _htmlEncoder);
            }
            if (_textWriter is not null)
            {
                htmlContent.WriteTo(_textWriter, _htmlEncoder);
            }
        }

        return HtmlString.Empty;
    }

    /// <summary>
    /// Writes the specified <see cref="HtmlString"/> value to the output.
    /// </summary>
    /// <param name="htmlString">The <see cref="HtmlString"/> value to write to the output.</param>
    /// <returns><see cref="HtmlString.Empty"/> to allow for easy calling via a Razor expression, e.g. <c>@WriteHtml(myCustomHtmlContent)</c></returns>
    protected HtmlString WriteHtml(HtmlString htmlString)
    {
        if (htmlString is not null && htmlString != HtmlString.Empty)
        {
            _bufferWriter?.WriteHtml(htmlString.Value);
            _textWriter?.Write(htmlString.Value);
        }

        return HtmlString.Empty;
    }

    private void WriteValue<T>(T value)
    {
        if (value is null)
        {
            return;
        }

        // Dispatch to the most appropriately typed method
        if (TryWriteFormattableValue(value))
        {
            return;
        }

        if (value is string)
        {
            Write((string)(object)value);
        }
        else if (value is byte[])
        {
            Write(((byte[])(object)value).AsSpan());
        }
        // Handle derived types (this currently results in value types being boxed)
#if NET8_0_OR_GREATER
        else if (value is IUtf8SpanFormattable)
        {
            WriteUtf8SpanFormattable((IUtf8SpanFormattable)(object)value, default, null);
        }
#endif
        else if (value is ISpanFormattable)
        {
            WriteSpanFormattable((ISpanFormattable)(object)value, default, null);
        }
        else if (value is HtmlString)
        {
            WriteHtml((HtmlString)(object)value);
        }
        else if (value is IHtmlContent)
        {
            WriteHtml((IHtmlContent)(object)value);
        }
#if NET8_0_OR_GREATER
        else if (value is Enum)
        {
            WriteSpanFormattable((Enum)(object)value);
        }
#endif
        // Fallback to ToString()
        else
        {
            Write(value?.ToString());
        }
    }

    /// <summary>
    /// Disposes the instance. Overriding implementations should ensure they call <c>base.Dispose()</c> after performing their
    /// custom dispose logic, e.g.:
    /// <code>
    /// public override void Dispose()
    /// {
    ///     // Custom dispose logic here...
    ///     base.Dispose();
    /// }
    /// </code>
    /// </summary>
    public virtual void Dispose()
    {
        ReturnPooledObjects();
        GC.SuppressFinalize(this);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ReturnPooledObjects()
    {
        if (_utf8BufferTextWriter is not null)
        {
            Utf8BufferTextWriter.Return(_utf8BufferTextWriter);
        }
    }
}
