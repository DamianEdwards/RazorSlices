using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Internal;

namespace RazorSlices;

/// <summary>
/// Base class for a Razor Slice template. Inherit from this class or <see cref="RazorSlice{TModel}"/> in your <c>.cshtml</c> files using the <c>@inherit</c> directive.
/// </summary>
/// <seealso cref="RazorSlice{TModel}"/>
public abstract partial class RazorSlice : IDisposable
{
    private const int _autoFlushThreshold = 1_024 * 16; // Auto-flush after each slice rendering if unflushed bytes is over 16 KB

    private IServiceProvider? _serviceProvider;
    private HtmlEncoder _htmlEncoder = HtmlEncoder.Default;
    private PipeWriter? _pipeWriter;
    private TextWriter? _textWriter;
    private Utf8PipeTextWriter? _utf8BufferTextWriter;

    /// <summary>
    /// Gets or sets the <see cref="IServiceProvider"/> used to resolve services for injectable properties.
    /// </summary>
    public IServiceProvider? ServiceProvider
    {
        // Ensure service provider can be lazily initialized from HttpContext.RequestServices so we don't pay the scope cost
        // if it's not needed.
        get => _serviceProvider ??= HttpContext?.RequestServices;
        set => _serviceProvider = value;
    }

    /// <summary>
    /// Gets or sets the <see cref="HttpContext"/> associated with the current request.
    /// </summary>
    public HttpContext? HttpContext { get; set; }

    /// <summary>
    /// A token to monitor for cancellation requests.
    /// </summary>
    public CancellationToken CancellationToken { get; protected set; }

    /// <summary>
    /// Gets or sets a delegate used to initialize the template class before <see cref="ExecuteAsync"/> is called.
    /// </summary>
    internal Action<RazorSlice, IServiceProvider?, HttpContext?>? Initialize { get; set; }

    /// <summary>
    /// Implemented by the generated template class.
    /// </summary>
    /// <remarks>
    /// This method should not be called directly. Call
    /// <see cref="RenderAsync(PipeWriter, HtmlEncoder?, CancellationToken)"/> or
    /// <see cref="RenderAsync(TextWriter, HtmlEncoder?, CancellationToken)"/> instead to render the template.
    /// </remarks>
    /// <returns>A <see cref="Task"/> representing the execution of the template.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public abstract Task ExecuteAsync();

    internal Task ExecuteAsyncImpl()
    {
        if (Initialize is not null)
        {
            Initialize(this, _serviceProvider, HttpContext);
        }

        return ExecuteAsync();
    }

    /// <summary>
    /// Renders the template to the specified <see cref="PipeWriter"/>.
    /// </summary>
    /// <param name="pipeWriter">The <see cref="PipeWriter"/> to render the template to.</param>
    /// <param name="htmlEncoder">An optional <see cref="HtmlEncoder"/> instance to use when rendering the template. If none is specified, <see cref="HtmlEncoder.Default"/> will be used.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the rendering of the template.</returns>
    public ValueTask RenderAsync(PipeWriter pipeWriter, HtmlEncoder? htmlEncoder = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pipeWriter);

        var pipe = FlushTrackingPipeWriter.Create(pipeWriter);

        ValueTask renderTask;
        try
        {
            renderTask = RenderToPipeWriterAsync(pipe, htmlEncoder, cancellationToken);
        }
        catch (Exception)
        {
            FlushTrackingPipeWriter.Return(pipe);
            throw;
        }

        if (!renderTask.IsCompletedSuccessfully)
        {
            // Go async
            return AwaitRenderTaskAndReturnPipe(renderTask, pipe);
        }

        FlushTrackingPipeWriter.Return(pipe);

        return ValueTask.CompletedTask;
    }

    private static async ValueTask AwaitRenderTaskAndReturnPipe(ValueTask renderTask, PipeWriter pipe)
    {
        try
        {
            await renderTask;
        }
        finally
        {
            FlushTrackingPipeWriter.Return(pipe);
        }
    }

    /// <summary>
    /// Renders the template to the specified <see cref="TextWriter"/>.
    /// </summary>
    /// <param name="textWriter">The <see cref="TextWriter"/> to render the template to.</param>
    /// <param name="htmlEncoder">An optional <see cref="HtmlEncoder"/> instance to use when rendering the template. If none is specified, <see cref="HtmlEncoder.Default"/> will be used.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the rendering of the template.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="textWriter"/> is <c>null</c>.</exception>
    public ValueTask RenderAsync(TextWriter textWriter, HtmlEncoder? htmlEncoder = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(textWriter);

        return RenderToTextWriterAsync(textWriter, htmlEncoder, cancellationToken);
    }

    [MemberNotNull(nameof(_pipeWriter))]
    private ValueTask RenderToPipeWriterAsync(PipeWriter pipeWriter, HtmlEncoder? htmlEncoder, CancellationToken cancellationToken, bool renderLayout = true)
    {
        Debug.WriteLine($"Rendering slice of type '{GetType().Name}' to a PipeWriter");

        _pipeWriter = pipeWriter;
        _textWriter = null;
        _htmlEncoder = htmlEncoder ?? _htmlEncoder;
        CancellationToken = cancellationToken;

        if (renderLayout && this is IUsesLayout useLayout)
        {
            return RenderViaLayout(RenderToPipeWriterAsync, useLayout, _pipeWriter, htmlEncoder, cancellationToken);
        }

        var executeTask = ExecuteAsyncImpl();

        if (!executeTask.HandleSynchronousCompletion())
        {
            // Go async
            return AwaitExecuteTaskFlushAndDispose(this, executeTask);
        }

        Dispose();

        return AutoFlush().GetAsValueTask();
    }

    [MemberNotNull(nameof(_textWriter))]
    private ValueTask RenderToTextWriterAsync(TextWriter textWriter, HtmlEncoder? htmlEncoder, CancellationToken cancellationToken)
    {
        Debug.WriteLine($"Rendering slice of type '{GetType().Name}' to a TextWriter");

        _pipeWriter = null;
        _textWriter = textWriter;
        _htmlEncoder = htmlEncoder ?? _htmlEncoder;
        CancellationToken = cancellationToken;

        if (this is IUsesLayout useLayout)
        {
            return RenderViaLayout(RenderToTextWriterAsync, useLayout, textWriter, htmlEncoder, cancellationToken);
        }

        var executeTask = ExecuteAsyncImpl();

        if (!executeTask.IsCompletedSuccessfully)
        {
            // Go async
            return AwaitExecuteTaskFlushAndDispose(this, executeTask);
        }

        Dispose();

        // REVIEW: call AutoFlush here?

        return ValueTask.CompletedTask;
    }

    private static ValueTask RenderToPipeWriterAsync(RazorSlice razorSlice, PipeWriter pipeWriter, HtmlEncoder? htmlEncoder, CancellationToken cancellationToken)
        => razorSlice.RenderToPipeWriterAsync(pipeWriter, htmlEncoder, cancellationToken);

    private static ValueTask RenderToTextWriterAsync(RazorSlice razorSlice, TextWriter textWriter, HtmlEncoder? htmlEncoder, CancellationToken cancellationToken)
        => razorSlice.RenderToTextWriterAsync(textWriter, htmlEncoder, cancellationToken);

    private ValueTask RenderViaLayout<TWriter>(Func<RazorSlice, TWriter, HtmlEncoder?, CancellationToken, ValueTask> render, IUsesLayout usesLayout, TWriter writer, HtmlEncoder? htmlEncoder, CancellationToken cancellationToken)
    {
        var layoutSlice = usesLayout.CreateLayoutImpl();

        if (layoutSlice is IRazorLayoutSlice razorLayoutSlice and RazorSlice)
        {
            razorLayoutSlice.ContentSlice = this;
            CopySliceState(this, (RazorSlice)razorLayoutSlice);

            return render(layoutSlice, writer, htmlEncoder, cancellationToken);
        }

        throw new InvalidOperationException($"Layout slices must inherit from {nameof(RazorLayoutSlice)} or {nameof(RazorLayoutSlice)}<TModel>.");
    }

    internal static void CopySliceState(RazorSlice source, RazorSlice destination)
    {
        destination.HttpContext = source.HttpContext;
        // Avoid setting the service provider directly from our ServiceProvider property so it can be lazily initialized from HttpContext.RequestServices
        // only if needed
        destination.ServiceProvider = source._serviceProvider;
    }

    internal static async ValueTask<HtmlString> AwaitRenderTask(Task renderTask)
    {
        await renderTask;
        return HtmlString.Empty;
    }

    private static FlushResult _noFlushResult = new(false, false);

    private ValueTask<FlushResult> AutoFlush()
    {
        Debug.Assert(_pipeWriter is null || _pipeWriter.CanGetUnflushedBytes, "PipeWriter must support unflushed bytes to auto-flush.");

        if (_pipeWriter is not null && _pipeWriter.UnflushedBytes >= _autoFlushThreshold)
        {
            Debug.WriteLine($"Auto-flushing slice of type '{GetType().Name}' to a PipeWriter");
            return _pipeWriter.FlushAsync(CancellationToken);
        }

        return ValueTask.FromResult(_noFlushResult);
    }

    private static async ValueTask AwaitOutputFlushTask(Task flushTask)
    {
        await flushTask;
    }

    private static async ValueTask AwaitExecuteTaskFlushAndDispose(RazorSlice slice, Task executeTask)
    {
        await executeTask;
        await slice.AutoFlush();
        slice.Dispose();
    }

    /// <summary>
    /// Attempts to flush the underlying output the template is being rendered to.
    /// </summary>
    /// <returns>A <see cref="ValueTask"/> representing the flush operation.</returns>
    protected ValueTask<HtmlString> FlushAsync()
    {
        if (_pipeWriter is not null)
        {
            var pipeWriterFlushTask = _pipeWriter.FlushAsync(CancellationToken);
            if (!pipeWriterFlushTask.IsCompletedSuccessfully)
            {
                // Go async
                return AwaitPipeWriterFlushAsyncTask(pipeWriterFlushTask);
            }

            return ValueTask.FromResult(HtmlString.Empty);
        }
        else if (_textWriter is not null)
        {
            var textWriterFlushTask = _textWriter.FlushAsync(CancellationToken);
            if (!textWriterFlushTask.IsCompletedSuccessfully)
            {
                // Go async
                return AwaitTextWriterFlushAsyncTask(textWriterFlushTask);
            }

            return ValueTask.FromResult(HtmlString.Empty);
        }

        throw new UnreachableException();
    }

    private static async ValueTask<HtmlString> AwaitPipeWriterFlushAsyncTask(ValueTask<FlushResult> flushTask)
    {
        await flushTask;
        return HtmlString.Empty;
    }

    private static async ValueTask<HtmlString> AwaitTextWriterFlushAsyncTask(Task flushTask)
    {
        await flushTask;
        return HtmlString.Empty;
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
        Debug.WriteLine($"Disposing slice of type '{GetType().Name}'");

        if (this is IRazorLayoutSlice { ContentSlice: { } contentSlice })
        {
            Debug.WriteLine($"Disposing content slice of type '{contentSlice.GetType().Name}'");
            contentSlice.Dispose();
        }
        ReturnPooledObjects();
        GC.SuppressFinalize(this);

        Debug.WriteLine($"Disposed slice of type '{GetType().Name}'");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ReturnPooledObjects()
    {
        if (_utf8BufferTextWriter is not null)
        {
            Utf8PipeTextWriter.Return(_utf8BufferTextWriter);
        }
    }
}
