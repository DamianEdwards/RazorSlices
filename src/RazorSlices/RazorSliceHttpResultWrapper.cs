using Microsoft.AspNetCore.Http;

namespace RazorSlices;

internal sealed class RazorSliceHttpResultWrapper(RazorSlice razorSlice) : IRazorSliceHttpResult, IDisposable
{
    public int StatusCode { get; set; } = StatusCodes.Status200OK;

    int? IStatusCodeHttpResult.StatusCode => StatusCode;

    public string ContentType => "text/html; charset=utf-8";

    /// <inheritdoc />
    Task IResult.ExecuteAsync(HttpContext httpContext) => RazorSliceHttpResultHelpers.ExecuteAsync(razorSlice, httpContext, null, StatusCode, ContentType);

    public void Dispose()
    {
        razorSlice.Dispose();
    }
}
