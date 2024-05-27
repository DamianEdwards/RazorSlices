using Microsoft.AspNetCore.Html;

namespace RazorSlices;

public abstract class RazorLayoutSlice<TModel> : RazorSlice<TModel>, IRazorLayoutSlice
{
    internal Func<Task>? ContentRenderer { get; set; }

    Func<Task>? IRazorLayoutSlice.ContentRenderer { set => ContentRenderer = value; }

    protected ValueTask<HtmlString> RenderContentAsync()
    {
        if (ContentRenderer is not null)
        {
#pragma warning disable CA2012 // Use ValueTasks correctly: Completion handled by HandleSynchronousCompletion
            var renderTask = ContentRenderer();
            if (!renderTask.HandleSynchronousCompletion())
            {
                return AwaitContentTask(renderTask);
            }
#pragma warning restore CA2012
        }

        return ValueTask.FromResult(HtmlString.Empty);
    }

    private static async ValueTask<HtmlString> AwaitContentTask(Task renderTask)
    {
        await renderTask;
        return HtmlString.Empty;
    }
}
