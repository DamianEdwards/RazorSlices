using System.Diagnostics;
using Microsoft.AspNetCore.Html;

namespace RazorSlices;

public partial class RazorSlice
{
    /// <summary>
    /// Renders a Razor Slice template inline.
    /// </summary>
    /// <remarks>
    /// Call from a <c>.cshtml</c> file using the <c>await</c> keyword, e.g:
    /// <example>
    /// <code>
    /// @await RenderPartialAsync(MyPartial.Create())
    /// </code>
    /// </example>
    /// </remarks>
    /// <param name="partial">The template instance to render.</param>
    /// <returns>A <see cref="ValueTask"/> representing the rendering of the template.</returns>
    protected internal ValueTask<HtmlString> RenderPartialAsync(RazorSlice partial)
    {
        ArgumentNullException.ThrowIfNull(partial);

        return RenderPartialAsyncImpl(partial);
    }

    /// <summary>
    /// Renders a Razor Slice template inline.
    /// </summary>
    /// <remarks>
    /// Call from a <c>.cshtml</c> file using the <c>await</c> keyword in an explicit Razor expression, e.g:
    /// <example>
    /// <code>
    /// @(await RenderPartialAsync&lt;MyPartial&gt;())
    /// </code>
    /// </example>
    /// </remarks>
    /// <typeparam name="TSlice">The slice proxy type.</typeparam>
    /// <returns>A <see cref="ValueTask"/> representing the rendering of the tmeplate.</returns>
    protected internal ValueTask<HtmlString> RenderPartialAsync<TSlice>()
        where TSlice : IRazorSliceProxy
    {
        var slice = TSlice.CreateSlice();
        return RenderPartialAsyncImpl(slice);
    }

    /// <summary>
    /// Renders a Razor Slice template inline with the given model.
    /// </summary>
    /// <remarks>
    /// Call from a <c>.cshtml</c> file using the <c>await</c> keyword in an explicit Razor expression, e.g:
    /// <example>
    /// <code>
    /// @(await RenderPartialAsync&lt;TodoRow, Todo&gt;(todo))
    /// </code>
    /// </example>
    /// </remarks>
    /// <typeparam name="TSlice">The slice proxy type.</typeparam>
    /// <typeparam name="TModel">The model type.</typeparam>
    /// <returns>A <see cref="ValueTask"/> representing the rendering of the tmeplate.</returns>
    protected internal ValueTask<HtmlString> RenderPartialAsync<TSlice, TModel>(TModel model)
        where TSlice : IRazorSliceProxy
    {
        var slice = TSlice.CreateSlice(model);
        return RenderPartialAsyncImpl(slice);
    }

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
            throw new UnreachableException();
        }

        if (renderPartialTask.HandleSynchronousCompletion())
        {
            return ValueTask.FromResult(HtmlString.Empty);
        }

        return AwaitPartialTask(renderPartialTask);
    }

    private static async ValueTask<HtmlString> AwaitPartialTask(ValueTask partialTask)
    {
        await partialTask;
        return HtmlString.Empty;
    }
}
