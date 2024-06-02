using System.IO.Pipelines;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using RazorSlices;

internal static class RazorSliceHttpResultHelpers
{

    /// <inheritdoc />
    internal static Task ExecuteAsync(RazorSlice slice, HttpContext httpContext, HtmlEncoder? htmlEncoder, int statusCode, string contentType)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        // The HtmlEncoder property can be set on the instance to avoid the overhead of RequestServices if desired
        var effectiveHtmlEncoder = htmlEncoder ?? httpContext.RequestServices.GetService<HtmlEncoder>();

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = contentType;

        // Force the response to start before rendering the slice
        var startTask = httpContext.Response.StartAsync();

        if (!startTask.IsCompletedSuccessfully)
        {
            // Go async
            return AwaitStartTaskAndRender(startTask, slice, httpContext, effectiveHtmlEncoder);
        }

#pragma warning disable CA2012 // Use ValueTasks correctly: The ValueTask is observed in code below
        var renderTask = slice.RenderAsync(httpContext.Response.BodyWriter, effectiveHtmlEncoder, httpContext.RequestAborted);
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