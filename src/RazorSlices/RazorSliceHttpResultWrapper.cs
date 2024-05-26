using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace RazorSlices;

internal sealed class RazorSliceHttpResultWrapper(RazorSlice razorSlice) : IRazorSliceHttpResult, IDisposable
{
    public int StatusCode { get; set; } = StatusCodes.Status200OK;

    int? IStatusCodeHttpResult.StatusCode => StatusCode;

    public string ContentType => "text/html; charset=utf-8";

    /// <inheritdoc />
    Task IResult.ExecuteAsync(HttpContext httpContext) => RazorSliceHttpResult.ExecuteAsync(razorSlice, httpContext, null, StatusCode, ContentType);

    public void Dispose()
    {
        razorSlice.Dispose();
    }
}
