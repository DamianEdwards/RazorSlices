using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Internal;

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
    /// Implemented by the generated template class.
    /// </summary>
    /// <remarks>
    /// This method should not be called directly. Call
    /// <see cref="RenderAsync(IBufferWriter{byte}, Func{CancellationToken, ValueTask}?, HtmlEncoder?)"/> or
    /// <see cref="RenderAsync(TextWriter, HtmlEncoder?)"/> instead to render the template.
    /// </remarks>
    /// <returns>A <see cref="Task"/> representing the execution of the template.</returns>
    public abstract Task ExecuteAsync();

    /// <summary>
    /// Renders the template to the specified <see cref="IBufferWriter{T}"/>.
    /// </summary>
    /// <param name="bufferWriter">The <see cref="IBufferWriter{T}"/> to render the template to.</param>
    /// <param name="flushAsync">An optional delegate that flushes the <see cref="IBufferWriter{T}"/>.</param>
    /// <param name="htmlEncoder">An optional <see cref="HtmlEncoder"/> instance to use when rendering the template. If none is specified, <see cref="HtmlEncoder.Default"/> will be used.</param>
    /// <returns>A <see cref="ValueTask"/> representing the rendering of the template.</returns>
    [MemberNotNull(nameof(_bufferWriter))]
    public ValueTask RenderAsync(IBufferWriter<byte> bufferWriter, Func<CancellationToken, ValueTask>? flushAsync = null, HtmlEncoder? htmlEncoder = null)
    {
        ArgumentNullException.ThrowIfNull(bufferWriter);

        // TODO: Render via layout if LayoutAttribute is set

        _bufferWriter = bufferWriter;
        _textWriter = null;
        _outputFlush = flushAsync;
        _htmlEncoder = htmlEncoder ?? _htmlEncoder;

        var executeTask = ExecuteAsync();

        if (executeTask.IsCompletedSuccessfully)
        {
            return ValueTask.CompletedTask;
        }
        return new ValueTask(executeTask);

        // TODO: Should we explicitly flush here if flushAsync is not null?
    }

    // TODO: Add a RenderAsync overload that renders to a Stream

    /// <summary>
    /// Renders the template to the specified <see cref="TextWriter"/>.
    /// </summary>
    /// <param name="textWriter">The <see cref="TextWriter"/> to render the template to.</param>
    /// <param name="htmlEncoder">An optional <see cref="HtmlEncoder"/> instance to use when rendering the template. If none is specified, <see cref="HtmlEncoder.Default"/> will be used.</param>
    /// <returns>A <see cref="ValueTask"/> representing the rendering of the template.</returns>
    [MemberNotNull(nameof(_textWriter), nameof(_outputFlush))]
    public ValueTask RenderAsync(TextWriter textWriter, HtmlEncoder? htmlEncoder = null)
    {
        ArgumentNullException.ThrowIfNull(textWriter);

        // TODO: Render via layout if LayoutAttribute is set

        _bufferWriter = null;
        _textWriter = textWriter;
        _outputFlush = (_) =>
        {
            var flushTask = textWriter.FlushAsync();
            if (flushTask.IsCompletedSuccessfully)
            {
                return ValueTask.CompletedTask;
            }
            return AwaitOutputFlushTask(flushTask);
        };
        _htmlEncoder = htmlEncoder ?? _htmlEncoder;

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
    /// Indicates whether <see cref="FlushAsync(CancellationToken)"/> will actually flush the underlying output during rendering.
    /// </summary>
    protected bool CanFlush => _outputFlush is not null;

    /// <summary>
    /// Attempts to flush the underlying output the template is being rendered to. Check <see cref="CanFlush"/> to determine if
    /// the output will actually be flushed or not before calling this method.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    protected ValueTask<HtmlString> FlushAsync(CancellationToken cancellationToken = default)
    {
        if (!CanFlush || _outputFlush is null)
        {
            return ValueTask.FromResult(HtmlString.Empty);
        }

        var flushTask = _outputFlush(cancellationToken);

        if (flushTask.IsCompletedSuccessfully)
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
    /// <param name="name"></param>
    /// <param name="section"></param>
    /// <exception cref="InvalidOperationException"></exception>
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
    /// <exception cref="ArgumentException"></exception>
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
    /// <typeparam name="TValue">The type of the value being written.</typeparam>
    protected void WriteLiteral<TValue>(TValue? value) => WriteLiteral(value?.ToString());

    /// <summary>
    /// Writes the string representation of the provided object to the output without HTML encoding it.
    /// </summary>
    /// <remarks>
    /// You generally shouldn't call this method directly. The Razor compiler will emit the appropriate calls to this method for
    /// all blocks of HTML in your .cshtml file.
    /// </remarks>
    /// <param name="value">The value to write to the output.</param>
    protected void WriteLiteral(object? value) => WriteLiteral(value?.ToString());

    /// <summary>
    /// Writes a buffer of UTF8 bytes to the output without HTML encoding it.
    /// </summary>
    /// <remarks>
    /// You generally shouldn't call this method directly. The Razor compiler will emit the appropriate calls to this method for
    /// all blocks of HTML in your .cshtml file.
    /// NOTE: We'd need a tweak to the Razor compiler to to have it support emitting <see cref="WriteLiteral(ReadOnlySpan{byte})"/> calls with UTF8 string literals.
    /// </remarks>
    /// <param name="value">The value to write to the output.</param>
    protected void WriteLiteral(ReadOnlySpan<byte> value)
    {
        if (value.Length == 0)
        {
            return;
        }

        _bufferWriter?.Write(value);
        _textWriter?.Write(Encoding.Unicode.GetString(value));
    }

    /// <summary>
    /// Writes the specified <see cref="HtmlString"/> value to the output without HTML encoding it again.
    /// </summary>
    /// <param name="htmlString">The <see cref="HtmlString"/> value to write to the output.</param>
    protected void Write(HtmlString htmlString)
    {
        if (htmlString != HtmlString.Empty)
        {
            WriteLiteral(htmlString.Value);
        }
    }

    /// <summary>
    /// Writes the specified <see cref="IHtmlContent"/> value to the output.
    /// </summary>
    /// <param name="htmlContent">The <see cref="IHtmlContent"/> value to write to the output.</param>
    protected void Write(IHtmlContent htmlContent)
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

    /// <summary>
    /// Writes the specified value to the output after HTML encoding it.
    /// </summary>
    /// <param name="value">The value to write to the output.</param>
    protected void Write(string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            _bufferWriter?.HtmlEncodeAndWrite(value, _htmlEncoder);
            _textWriter?.Write(_htmlEncoder.Encode(value));
        }
    }

    /// <summary>
    /// Writes the specified object to the output after HTML encoding the result of calling <see cref="object.ToString"/> on it.
    /// </summary>
    /// <typeparam name="TValue">The object type.</typeparam>
    /// <param name="value">The object to write to the output.</param>
    protected void Write<TValue>(TValue? value) => Write(value?.ToString());

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
