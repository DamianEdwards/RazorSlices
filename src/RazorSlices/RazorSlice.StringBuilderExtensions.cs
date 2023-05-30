using System.Text;
using System.Text.Encodings.Web;

namespace RazorSlices;

/// <summary>
/// Extension methods for <see cref="RazorSlice" /> that allow rendering to a <see cref="StringBuilder" /> or string.
/// </summary>
public static class RazorSliceStringBuilderExtensions
{
    /// <summary>
    /// Renders the template to the specified <see cref="StringBuilder"/>.
    /// </summary>
    /// <param name="slice">The <see cref="RazorSlice" /> instance.</param>
    /// <param name="stringBuilder">The <see cref="StringBuilder"/> to render the template to.</param>
    /// <param name="htmlEncoder">An optional <see cref="HtmlEncoder"/> instance to use when rendering the template. If none is specified, <see cref="HtmlEncoder.Default"/> will be used.</param>
    /// <param name="cancellationToken"></param>
    /// <returns>A <see cref="ValueTask"/> representing the rendering of the template.</returns>
    public static ValueTask RenderAsync(this RazorSlice slice, StringBuilder stringBuilder, HtmlEncoder? htmlEncoder = null, CancellationToken cancellationToken = default)
    {
        using var stringWriter = new StringWriter(stringBuilder);
        return slice.RenderAsync(stringWriter, htmlEncoder, cancellationToken);
    }

    /// <summary>
    /// Renders the template to a string.
    /// </summary>
    /// <param name="slice">The <see cref="RazorSlice" /> instance.</param>
    /// <param name="htmlEncoder">An optional <see cref="HtmlEncoder"/> instance to use when rendering the template. If none is specified, <see cref="HtmlEncoder.Default"/> will be used.</param>
    /// <param name="cancellationToken"></param>
    /// <returns>The template rendered to a <see cref="string"/>.</returns>
    public static ValueTask<string> RenderAsync(this RazorSlice slice, HtmlEncoder? htmlEncoder = null, CancellationToken cancellationToken = default)
    {
        var sb = new StringBuilder();
        var task = slice.RenderAsync(sb, htmlEncoder, cancellationToken);

        if (task.IsCompletedSuccessfully)
        {
#pragma warning disable CA1849 // Call async methods when in an async method: task is already completed
            task.GetAwaiter().GetResult();
#pragma warning restore CA1849
            return ValueTask.FromResult(sb.ToString());
        }

        return AwaitRenderTask(task, sb);
    }

    private static async ValueTask<string> AwaitRenderTask(ValueTask task, StringBuilder sb)
    {
        await task;
        return sb.ToString();
    }
}
