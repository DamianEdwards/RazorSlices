using Microsoft.AspNetCore.Html;

namespace RazorSlices;

/// <summary>
/// A <see cref="RazorSlice{TModel}"/> that serves as the layout for another <see cref="RazorSlice"/>.
/// </summary>
/// <typeparam name="TModel">The layout model type.</typeparam>
public abstract class RazorLayoutSlice<TModel> : RazorSlice<TModel>, IRazorLayoutSlice
{
    internal RazorSlice? ContentSlice { get; set; }

    RazorSlice? IRazorLayoutSlice.ContentSlice { get => ContentSlice; set => ContentSlice = value; }

    /// <summary>
    /// Renders the <see cref="RazorSlice"/> that is using this <see cref="RazorSlice"/> as layout.
    /// </summary>
    /// <returns></returns>
    protected ValueTask<HtmlString> RenderBodyAsync()
    {
        if (ContentSlice is not null)
        {
#pragma warning disable CA2012 // Use ValueTasks correctly: Completion handled by HandleSynchronousCompletion
            var renderTask = ContentSlice.ExecuteAsyncImpl();
            if (!renderTask.HandleSynchronousCompletion())
            {
                return AwaitRenderTask(renderTask);
            }
#pragma warning restore CA2012
        }

        return ValueTask.FromResult(HtmlString.Empty);
    }

    /// <summary>
    /// Renders the content of the section with the specified name.
    /// </summary>
    /// <remarks>
    /// Call this method from layouts to render a template section.
    /// </remarks>
    /// <param name="sectionName">The section name.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> representing the rendering of the section.</returns>
    /// <exception cref="ArgumentException">Thrown when no section with name <paramref name="sectionName"/> has been defined by the slice being rendered.</exception>
    protected ValueTask<HtmlString> RenderSectionAsync(string sectionName)
    {
        ArgumentException.ThrowIfNullOrEmpty(sectionName);

        if (ContentSlice is not null)
        {
#pragma warning disable CA2012 // Use ValueTasks correctly: Completion handled by HandleSynchronousCompletion
            var renderTask = ContentSlice.ExecuteSectionAsync(sectionName);
            if (!renderTask.HandleSynchronousCompletion())
            {
                return AwaitRenderTask(renderTask);
            }
#pragma warning restore CA2012
        }

        return ValueTask.FromResult(HtmlString.Empty);
    }
}
