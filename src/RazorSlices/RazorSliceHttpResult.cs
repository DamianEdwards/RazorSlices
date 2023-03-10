using System.IO.Pipelines;
using System.Text.Encodings.Web;
using RazorSlices;

namespace Microsoft.AspNetCore.Http.HttpResults;

/// <summary>
/// A <see cref="RazorSlice" /> template that is also an <see cref="IResult" /> so it can be directly returned from
/// a route handler delegate. When executed it will render the template to the response.
/// </summary>
public abstract class RazorSliceHttpResult : RazorSlice, IResult
#if NET7_0_OR_GREATER
    , IStatusCodeHttpResult, IContentTypeHttpResult
#endif
{
    /// <summary>
    /// Gets or sets the HTTP status code. Defaults to <see cref="StatusCodes.Status200OK"/>
    /// </summary>
    public int StatusCode { get; set; } = StatusCodes.Status200OK;

#if NET7_0_OR_GREATER
    int? IStatusCodeHttpResult.StatusCode => StatusCode;
#endif

    /// <summary>
    /// Gets the content type: <c>text/html; charset=utf-8</c>
    /// </summary>
#pragma warning disable CA1822 // Mark members as static
    public string ContentType => "text/html; charset=utf-8";
#pragma warning restore CA1822 // Mark members as static

    /// <summary>
    /// Gets or sets the <see cref="System.Text.Encodings.Web.HtmlEncoder" /> instance to use when rendering the template. If
    /// <c>null</c> the template will use <see cref="HtmlEncoder.Default" />.
    /// </summary>
    public HtmlEncoder? HtmlEncoder { get; set; }

    /// <inheritdoc />
    Task IResult.ExecuteAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        httpContext.Response.StatusCode = StatusCode;
        httpContext.Response.ContentType = ContentType;
        httpContext.Response.RegisterForDispose(this);

        var renderTask = this.RenderToPipeWriterAsync(httpContext.Response.BodyWriter, HtmlEncoder);

        if (renderTask.IsCompletedSuccessfully)
        {
            renderTask.GetAwaiter().GetResult();
            return httpContext.Response.BodyWriter.FlushAsync().AsTask();
        }

        return AwaitRenderTaskAndFlushResponse(renderTask, httpContext.Response.BodyWriter);
    }

    private static async Task AwaitRenderTaskAndFlushResponse(ValueTask renderTask, PipeWriter responseBodyWriter)
    {
        await renderTask;
        await responseBodyWriter.FlushAsync();
    }
}
