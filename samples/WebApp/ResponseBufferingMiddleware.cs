namespace RazorSlices.Samples.WebApp;

using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

internal sealed class ResponseBufferingMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        // Save the original response body stream
        var originalBodyStream = context.Response.Body;

        try
        {
            // Create a memory stream to buffer the response
            using var memoryStream = new MemoryStream();
            context.Response.Body = memoryStream;

            // Call the next middleware
            await next(context);

            // If no exception occurs, write the buffered response to the original body
            memoryStream.Seek(0, SeekOrigin.Begin);
            await memoryStream.CopyToAsync(originalBodyStream);
        }
        catch
        {
            // Clear the existing response
            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;

            // Rethrow to let the Developer Exception Page middleware handle it
            throw;
        }
        finally
        {
            // Restore the original body stream
            context.Response.Body = originalBodyStream;
        }
    }
}

internal static class ResponseBufferingMiddlewareExtensions
{
    public static IApplicationBuilder UseResponseBuffering(this IApplicationBuilder app)
    {
        return app.UseMiddleware<ResponseBufferingMiddleware>();
    }
}

