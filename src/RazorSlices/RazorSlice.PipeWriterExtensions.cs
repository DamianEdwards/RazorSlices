using System.Buffers;
using System.IO.Pipelines;
using System.Text.Encodings.Web;

namespace RazorSlices;

/// <summary>
/// Extension methods for <see cref="RazorSlice" /> that enable rendering directly to a <see cref="PipeWriter" />.
/// </summary>
public static class RazorSlicePipeWriterExtensions
{
    /// <summary>
    /// Renders the template to the specified <see cref="PipeWriter"/>.
    /// </summary>
    /// <param name="razorSlice">The <see cref="RazorSlice" /> instance.</param>
    /// <param name="pipeWriter">The <see cref="PipeWriter"/> to render the template to.</param>
    /// <param name="htmlEncoder">An optional <see cref="HtmlEncoder"/> instance to use when rendering the template. If none is specified, <see cref="HtmlEncoder.Default"/> will be used.</param>
    /// <returns>A <see cref="ValueTask" /> that represents the template rendering.</returns>
    public static ValueTask RenderToPipeWriterAsync(this RazorSlice razorSlice, PipeWriter pipeWriter, HtmlEncoder? htmlEncoder = null)
        => razorSlice.RenderAsync(pipeWriter, GetFlushWrapper(pipeWriter), htmlEncoder);

    /// <summary>
    /// Renders the template to the specified <see cref="Stream"/>.
    /// </summary>
    /// <param name="razorSlice">The <see cref="RazorSlice" /> instance.</param>
    /// <param name="stream">The <see cref="Stream"/> to render the template to.</param>
    /// <param name="htmlEncoder">An optional <see cref="HtmlEncoder"/> instance to use when rendering the template. If none is specified, <see cref="HtmlEncoder.Default"/> will be used.</param>
    /// <returns>A <see cref="ValueTask" /> that represents the template rendering.</returns>
    public static async ValueTask RenderAsync(this RazorSlice razorSlice, Stream stream, HtmlEncoder? htmlEncoder = null)
    {
        var pipe = PipeWriter.Create(stream, new(MemoryPool<byte>.Shared));
        await RenderToPipeWriterAsync(razorSlice, pipe, htmlEncoder);
        await pipe.FlushAsync();
        pipe.Complete();
    }

    private static Func<CancellationToken, ValueTask> GetFlushWrapper(PipeWriter pipeWriter)
        => ct => AsValueTask(pipeWriter.FlushAsync(ct));

    private static ValueTask AsValueTask<TResult>(ValueTask<TResult> valueTask)
    {
        if (valueTask.IsCompletedSuccessfully)
        {
            var _ = valueTask.GetAwaiter().GetResult();
            return default;
        }

        return new ValueTask(valueTask.AsTask());
    }
}
