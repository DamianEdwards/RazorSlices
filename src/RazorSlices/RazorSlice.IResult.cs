using System.IO.Pipelines;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace RazorSlices;

public abstract partial class RazorSlice : IResult, IStatusCodeHttpResult, IContentTypeHttpResult
{
    /// <summary>
    /// Gets or sets the HTTP status code. Defaults to <see cref="StatusCodes.Status200OK"/>
    /// </summary>
    public int StatusCode { get; set; } = StatusCodes.Status200OK;

    int? IStatusCodeHttpResult.StatusCode { get => StatusCode; }

    /// <summary>
    /// Gets the content type: <c>text/html; charset=utf-8</c>
    /// </summary>
    public string ContentType => "text/html; charset=utf-8";

    /// <summary>
    /// Gets or sets the <see cref="System.Text.Encodings.Web.HtmlEncoder" /> instance to use when rendering the template. If
    /// <c>null</c> the template will use <see cref="HtmlEncoder.Default" />.
    /// </summary>
    public HtmlEncoder? HtmlEncoder { get; set; }

    /// <inheritdoc />
    Task IResult.ExecuteAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        HttpContext = httpContext;

        // The HtmlEncoder property can be set on the instance to avoid the overhead of RequestServices if desired
        var effectiveHtmlEncoder = HtmlEncoder ?? httpContext.RequestServices.GetService<HtmlEncoder>();

        httpContext.Response.StatusCode = StatusCode;
        httpContext.Response.ContentType = ContentType;

        // Force the response to start before rendering the slice
        var startTask = httpContext.Response.StartAsync();

        if (!startTask.IsCompletedSuccessfully)
        {
            // Go async
            return AwaitStartTaskAndRender(startTask, this, httpContext, effectiveHtmlEncoder);
        }

#pragma warning disable CA2012 // Use ValueTasks correctly: The ValueTask is observed in code below
        var renderTask = RenderAsync(httpContext.Response.BodyWriter, effectiveHtmlEncoder, httpContext.RequestAborted);
#pragma warning restore CA2012

        if (!renderTask.HandleSynchronousCompletion())
        {
            // Go async
            return AwaitRenderTaskAndFlushResponse(renderTask, httpContext.Response.BodyWriter, httpContext.RequestAborted);
        }

        return httpContext.Response.BodyWriter.FlushAsync(httpContext.RequestAborted).GetAsTask();
    }

    private static async Task AwaitStartTaskAndRender(Task startTask, RazorSlice slice, HttpContext httpContext, HtmlEncoder? htmlEncoder)
    {
        await startTask;
        await slice.RenderAsync(httpContext.Response.BodyWriter, htmlEncoder, httpContext.RequestAborted);
        await httpContext.Response.BodyWriter.FlushAsync(httpContext.RequestAborted);
    }

    private static async Task AwaitRenderTaskAndFlushResponse(ValueTask renderTask, PipeWriter responseBodyWriter, CancellationToken cancellationToken)
    {
        await renderTask;
        await responseBodyWriter.FlushAsync(cancellationToken);
    }
}
