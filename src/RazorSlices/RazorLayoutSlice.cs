using Microsoft.AspNetCore.Html;

namespace RazorSlices;

/// <summary>
/// A <see cref="RazorSlice"/> that serves as the layout for another <see cref="RazorSlice"/>.
/// </summary>
public abstract class RazorLayoutSlice : RazorSlice, IRazorLayoutSlice
{
    internal Func<Task>? ContentRenderer { get; set; }
    internal Func<string, Task>? SectionContentRenderer { get; set; }

    Func<Task>? IRazorLayoutSlice.ContentRenderer { set => ContentRenderer = value; }
    Func<string, Task>? IRazorLayoutSlice.SectionContentRenderer { set => SectionContentRenderer = value; }

    /// <summary>
    /// Renders the content of the <see cref="RazorSlice"/> using this slice for layout.
    /// </summary>
    /// <returns><see cref="HtmlString.Empty"/> just to allow for easy calling within <c>.cshtml</c> files, e.g. <c>@await RenderContentAsync()</c>.</returns>
    protected ValueTask<HtmlString> RenderContentAsync()
    {
        if (ContentRenderer is not null)
        {
#pragma warning disable CA2012 // Use ValueTasks correctly: Completion handled by HandleSynchronousCompletion
            var renderTask = ContentRenderer();
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
    /// <param name="sectionName">The section name.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> representing the rendering of the section.</returns>
    /// <exception cref="ArgumentException">Thrown when no section with name <paramref name="sectionName"/> has been defined by the slice being rendered.</exception>
    protected ValueTask<HtmlString> RenderSectionContentAsync(string sectionName)
    {
        ArgumentException.ThrowIfNullOrEmpty(sectionName);

        if (SectionContentRenderer is not null)
        {
#pragma warning disable CA2012 // Use ValueTasks correctly: Completion handled by HandleSynchronousCompletion
            var renderTask = SectionContentRenderer(sectionName);
            if (!renderTask.HandleSynchronousCompletion())
            {
                return AwaitRenderTask(renderTask);
            }
#pragma warning restore CA2012
        }

        return ValueTask.FromResult(HtmlString.Empty);
    }

    private static async ValueTask<HtmlString> AwaitRenderTask(Task renderTask)
    {
        await renderTask;
        return HtmlString.Empty;
    }
}
