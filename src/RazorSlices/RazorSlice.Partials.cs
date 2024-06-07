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

        return RenderChildSliceAsync(partial);
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
#pragma warning disable CA2000 // Dispose objects before losing scope: Disposed by RenderPartialAsyncImpl
        var slice = TSlice.CreateSlice();
#pragma warning restore CA2000
        return RenderChildSliceAsync(slice);
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
#pragma warning disable CA2000 // Dispose objects before losing scope: Disposed by RenderPartialAsyncImpl
        var slice = TSlice.CreateSlice(model);
#pragma warning restore CA2000
        return RenderChildSliceAsync(slice);
    }

    internal ValueTask<HtmlString> RenderChildSliceAsync(RazorSlice child)
    {
        Debug.WriteLine($"Rendering child slice of type '{child.GetType().Name}' from layout slice of type '{GetType().Name}'");

        CopySliceState(this, child);

        ValueTask renderPartialTask;

#pragma warning disable CA2012 // Use ValueTasks correctly: Completion handled by HandleSynchronousCompletion
        if (_pipeWriter is not null)
        {
            renderPartialTask = child.RenderToPipeWriterAsync(_pipeWriter, _htmlEncoder, CancellationToken, renderLayout: false);
        }
        else if (_textWriter is not null)
        {
            renderPartialTask = child.RenderToTextWriterAsync(_textWriter, _htmlEncoder, CancellationToken, renderLayout: false);
        }
#pragma warning restore CA2012
        else
        {
            throw new UnreachableException();
        }

        if (!renderPartialTask.HandleSynchronousCompletion())
        {
            // Go async
            return AwaitPartialTask(renderPartialTask);
        }

        return ValueTask.FromResult(HtmlString.Empty);
    }

    private static async ValueTask<HtmlString> AwaitPartialTask(ValueTask partialTask)
    {
        await partialTask;
        return HtmlString.Empty;
    }
}
