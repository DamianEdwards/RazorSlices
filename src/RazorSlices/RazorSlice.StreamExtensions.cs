using System.Buffers;
using System.IO.Pipelines;
using System.Text.Encodings.Web;

namespace RazorSlices;

/// <summary>
/// Extension methods for <see cref="RazorSlice" /> that enable rendering directly to a <see cref="Stream" />.
/// </summary>
public static class RazorSliceStreamExtensions
{
    /// <summary>
    /// Renders the template to the specified <see cref="Stream"/>.
    /// </summary>
    /// <param name="razorSlice">The <see cref="RazorSlice" /> instance.</param>
    /// <param name="stream">The <see cref="Stream"/> to render the template to.</param>
    /// <param name="htmlEncoder">An optional <see cref="HtmlEncoder"/> instance to use when rendering the template. If none is specified, <see cref="HtmlEncoder.Default"/> will be used.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask" /> that represents the template rendering.</returns>
    public static async ValueTask RenderAsync(this RazorSlice razorSlice, Stream stream, HtmlEncoder? htmlEncoder = null, CancellationToken cancellationToken = default)
    {
        var pipe = PipeWriter.Create(stream, new(MemoryPool<byte>.Shared));
        await razorSlice.RenderToPipeWriterAsync(pipe, htmlEncoder, cancellationToken);
        await pipe.FlushAsync(razorSlice.CancellationToken);
    }
}
