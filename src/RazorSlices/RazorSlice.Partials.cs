using System.Diagnostics;
using Microsoft.AspNetCore.Html;

namespace RazorSlices;

public partial class RazorSlice
{
    /// <summary>
    /// Renders a template inline.
    /// </summary>
    /// <param name="partial">The template instance to render.</param>
    /// <returns>A <see cref="ValueTask"/> representing the rendering of the template.</returns>
    protected internal ValueTask<HtmlString> RenderPartialAsync(RazorSlice partial)
    {
        ArgumentNullException.ThrowIfNull(partial);

        return RenderPartialAsyncImpl(partial);
    }

#if NET7_0_OR_GREATER
    /// <summary>
    /// Renders a template inline.
    /// </summary>
    /// <typeparam name="TSlice"></typeparam>
    /// <returns></returns>
    protected internal ValueTask<HtmlString> RenderPartialAsync<TSlice>()
        where TSlice : IRazorSliceProxy
    {
        var slice = TSlice.Create();
        return RenderPartialAsyncImpl(slice);
    }

    /// <summary>
    /// Renders a template inline.
    /// </summary>
    /// <typeparam name="TSlice"></typeparam>
    /// <typeparam name="TModel"></typeparam>
    /// <returns></returns>
    protected internal ValueTask<HtmlString> RenderPartialAsync<TSlice, TModel>(TModel model)
        where TSlice : IRazorSliceProxy<TModel>
    {
        var slice = TSlice.Create(model);
        return RenderPartialAsyncImpl(slice);
    }
#endif

    private ValueTask<HtmlString> RenderPartialAsyncImpl(RazorSlice partial)
    {
        partial.HttpContext = HttpContext;
        // Avoid setting the service provider directly from our ServiceProvider property so it can be lazily initialized from HttpContext.RequestServices
        // only if needed
        partial.ServiceProvider = _serviceProvider;

        ValueTask renderPartialTask = default;

#pragma warning disable CA2012 // Use ValueTasks correctly: Completion handled by HandleSynchronousCompletion
        if (_bufferWriter is not null)
        {
            renderPartialTask = partial.RenderToBufferWriterAsync(_bufferWriter, _outputFlush, _htmlEncoder, CancellationToken);
        }
        else if (_textWriter is not null)
        {
            renderPartialTask = partial.RenderToTextWriterAsync(_textWriter, _htmlEncoder, CancellationToken);
        }
#pragma warning restore CA2012
        else
        {
#if NET7_0_OR_GREATER
            throw new UnreachableException();
#else
            Debug.Fail("Unreachable");
            throw new InvalidOperationException();
#endif
        }

        if (renderPartialTask.HandleSynchronousCompletion())
        {
            return ValueTask.FromResult(HtmlString.Empty);
        }

        return AwaitPartialTask(partial, renderPartialTask);
    }

    private static async ValueTask<HtmlString> AwaitPartialTask(RazorSlice partial, ValueTask partialTask)
    {
        await partialTask;
        return HtmlString.Empty;
    }
}
