using Microsoft.AspNetCore.Html;

namespace RazorSlices;

/// <summary>
/// A <see cref="RazorSlice"/> that serves as the layout for another <see cref="RazorSlice"/>.
/// </summary>
public abstract class RazorLayoutSlice : RazorSlice, IRazorLayoutSlice
{
    internal RazorSlice? ContentSlice { get; set; }

    RazorSlice? IRazorLayoutSlice.ContentSlice { get => ContentSlice; set => ContentSlice = value; }

    /// <summary>
    /// Renders the content of the <see cref="RazorSlice"/> using this slice for layout.
    /// </summary>
    /// <returns><see cref="HtmlString.Empty"/> just to allow for easy calling within <c>.cshtml</c> files, e.g. <c>@await RenderBodyAsync()</c>.</returns>
    protected ValueTask<HtmlString> RenderBodyAsync()
    {
        if (ContentSlice is not null)
        {
            return RenderChildSliceAsync(ContentSlice);
        }

        return ValueTask.FromResult(HtmlString.Empty);
    }

    /// <summary>
    /// Renders the content of the section with the specified name.
    /// </summary>
    /// <param name="sectionName">The section name.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> representing the rendering of the section.</returns>
    /// <exception cref="ArgumentException">Thrown when no section with name <paramref name="sectionName"/> has been defined by the slice being rendered.</exception>
    protected ValueTask<HtmlString> RenderSectionAsync(string sectionName)
    {
        ArgumentException.ThrowIfNullOrEmpty(sectionName);

        if (ContentSlice is not null)
        {
            var renderTask = ContentSlice.ExecuteSectionAsync(sectionName);
            if (!renderTask.HandleSynchronousCompletion())
            {
                return AwaitRenderTask(renderTask);
            }
        }

        return ValueTask.FromResult(HtmlString.Empty);
    }
}
