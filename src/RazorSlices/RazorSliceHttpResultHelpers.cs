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
        //var startTask = httpContext.Response.StartAsync();
        //if (!startTask.IsCompletedSuccessfully)
        //{
        //    AwaitStartTask(startTask, slice, httpContext, effectiveHtmlEncoder);
        //}

#pragma warning disable CA2012 // Use ValueTasks correctly: The ValueTask is observed in code below
        var renderTask = slice.RenderToPipeWriterAsync(httpContext.Response.BodyWriter, effectiveHtmlEncoder, httpContext.RequestAborted);
#pragma warning restore CA2012

        if (renderTask.HandleSynchronousCompletion())
        {
            return httpContext.Response.BodyWriter.FlushAsync(httpContext.RequestAborted).GetAsTask();
        }

        return AwaitRenderTaskAndFlushResponse(renderTask, httpContext.Response.BodyWriter, httpContext.RequestAborted);
    }

    //private static async Task AwaitStartTask(ValueTask renderTask, PipeWriter responseBodyWriter, CancellationToken cancellationToken)
    //{
    //    await renderTask;
    //    await responseBodyWriter.FlushAsync(cancellationToken);
    //}

    private static async Task AwaitRenderTaskAndFlushResponse(ValueTask renderTask, PipeWriter responseBodyWriter, CancellationToken cancellationToken)
    {
        await renderTask;
        await responseBodyWriter.FlushAsync(cancellationToken);
    }
}