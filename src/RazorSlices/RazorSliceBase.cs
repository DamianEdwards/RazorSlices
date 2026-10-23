using System.ComponentModel;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Html;

namespace RazorSlices;

/// <summary>
/// Minimal streaming Razor template with HTTP-result support, but without dependency injection, helpers, layouts, or partials.
/// Use <see cref="RazorSlice"/> for the normal, feature-rich template base.
/// </summary>
public abstract partial class RazorSliceBase : IDisposable
{
    private protected PipeWriter? _pipeWriter;
    private protected TextWriter? _textWriter;
    private protected HtmlEncoder _htmlEncoder = HtmlEncoder.Default;
    private bool _disposed;

    /// <summary>Gets whether this slice has been disposed.</summary>
    protected bool IsDisposed => _disposed;

    /// <summary>Gets the token for the current render.</summary>
    public CancellationToken CancellationToken { get; protected set; }

    /// <summary>Implemented by the Razor SDK. Call RenderAsync instead.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public abstract Task ExecuteAsync();

    /// <summary>Renders to a text writer using the specified encoder and cancellation token.</summary>
    public ValueTask RenderAsync(TextWriter textWriter, HtmlEncoder? htmlEncoder = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(textWriter);
        return RenderToTextWriterAsync(textWriter, htmlEncoder, cancellationToken);
    }

    /// <summary>Renders to a pipe writer using the specified encoder and cancellation token.</summary>
    public ValueTask RenderAsync(PipeWriter pipeWriter, HtmlEncoder? htmlEncoder = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pipeWriter);
        var pipe = FlushTrackingPipeWriter.Create(pipeWriter);
        if (ReferenceEquals(pipe, pipeWriter))
        {
            return RenderToPipeWriterAsync(pipe, htmlEncoder, cancellationToken);
        }
        ValueTask render;
        try
        {
            render = RenderToPipeWriterAsync(pipe, htmlEncoder, cancellationToken);
        }
        catch
        {
            FlushTrackingPipeWriter.Return(pipe);
            throw;
        }
        if (!render.IsCompletedSuccessfully)
        {
            return AwaitRenderAndReturnPipe(render, pipe);
        }
#pragma warning disable CA1849 // Already completed successfully; consume the ValueTask source.
        render.GetAwaiter().GetResult();
#pragma warning restore CA1849
        FlushTrackingPipeWriter.Return(pipe);
        return ValueTask.CompletedTask;
    }

    private static async ValueTask AwaitRenderAndReturnPipe(ValueTask render, PipeWriter pipe)
    {
        try { await render; }
        finally { FlushTrackingPipeWriter.Return(pipe); }
    }

    internal ValueTask RenderToTextWriterAsync(TextWriter writer, HtmlEncoder? encoder, CancellationToken token, bool renderLayout = true, bool disposeSlice = true)
    {
        _textWriter = writer;
        _pipeWriter = null;
        _htmlEncoder = encoder ?? _htmlEncoder;
        CancellationToken = token;
        return RenderCoreAsync(renderLayout, disposeSlice);
    }

    internal ValueTask RenderToPipeWriterAsync(PipeWriter writer, HtmlEncoder? encoder, CancellationToken token, bool renderLayout = true, bool disposeSlice = true)
    {
        _pipeWriter = writer;
        _textWriter = null;
        _htmlEncoder = encoder ?? _htmlEncoder;
        CancellationToken = token;
        return RenderCoreAsync(renderLayout, disposeSlice);
    }

    /// <summary>
    /// Executes the rendering pipeline after the output, encoder, and token have been established.
    /// Overrides must preserve asynchronous completion, cleanup, and exception propagation.
    /// </summary>
    /// <param name="renderLayout">Whether higher-level bases may render a layout.</param>
    /// <param name="disposeSlice">Whether this render owns disposal of the slice.</param>
    protected virtual ValueTask RenderCoreAsync(bool renderLayout, bool disposeSlice)
    {
        try
        {
            var execute = ExecuteAsync();
            if (!execute.HandleSynchronousCompletion())
            {
                return AwaitExecute(this, execute, disposeSlice);
            }
        }
        catch (Exception error)
        {
            DisposeAfterFailure(error);
            throw;
        }
        if (disposeSlice) { Dispose(); }
        return AutoFlush();
    }

    private static async ValueTask AwaitExecute(RazorSliceBase slice, Task execute, bool dispose)
    {
        try
        {
            await execute;
            await slice.AutoFlush();
        }
        catch (Exception error)
        {
            if (dispose) { slice.DisposeAfterFailure(error); }
            throw;
        }
        if (dispose) { slice.Dispose(); }
    }

    private protected ValueTask AutoFlush()
    {
        Debug.Assert(_pipeWriter is null || _pipeWriter.CanGetUnflushedBytes);
        return _pipeWriter is not null && _pipeWriter.UnflushedBytes >= 16_384
            ? _pipeWriter.FlushAsync(CancellationToken).GetAsValueTask()
            : ValueTask.CompletedTask;
    }

    internal void EnsureRendering()
    {
        if (_disposed || (_pipeWriter is null && _textWriter is null))
        {
            throw new InvalidOperationException("Helpers can only be invoked during an active render.");
        }
    }

    internal void DisposeAfterFailure(Exception failure)
    {
        if (!_disposed)
        {
            try { Dispose(); }
            catch (Exception cleanup) { throw new AggregateException(failure, cleanup); }
        }
    }

    /// <summary>Flushes the current output without buffering the template.</summary>
    protected internal ValueTask<HtmlString> FlushAsync()
    {
        if (_pipeWriter is not null)
        {
            var flush = _pipeWriter.FlushAsync(CancellationToken);
            if (!flush.IsCompletedSuccessfully) { return AwaitFlush(flush); }
            flush.GetAwaiter().GetResult();
            return ValueTask.FromResult(HtmlString.Empty);
        }
        if (_textWriter is not null)
        {
            var flush = _textWriter.FlushAsync(CancellationToken);
            return flush.HandleSynchronousCompletion() ? ValueTask.FromResult(HtmlString.Empty) : AwaitFlush(flush);
        }
        throw new UnreachableException();
    }

    private static async ValueTask<HtmlString> AwaitFlush(ValueTask<FlushResult> flush)
    {
        await flush;
        return HtmlString.Empty;
    }

    private static async ValueTask<HtmlString> AwaitFlush(Task flush)
    {
        await flush;
        return HtmlString.Empty;
    }

    /// <summary>Disposes the slice. Overrides must call base.Dispose().</summary>
    public virtual void Dispose()
    {
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
