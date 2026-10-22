using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Http;

namespace RazorSlices;

/// <summary>An HTTP adapter for a minimal slice. The adapter is allocated only when explicitly requested.</summary>
public sealed class RazorSliceBaseResult : IResult, IStatusCodeHttpResult, IContentTypeHttpResult
{
    private readonly RazorSliceBase _slice;
    private readonly HtmlEncoder _encoder;

    internal RazorSliceBaseResult(RazorSliceBase slice, int statusCode, string contentType, HtmlEncoder? encoder)
    {
        _slice = slice;
        _encoder = encoder ?? (slice as RazorSlice)?.HtmlEncoder ?? HtmlEncoder.Default;
        StatusCode = statusCode;
        ContentType = contentType;
    }

    /// <summary>Gets the response status code.</summary>
    public int StatusCode { get; }
    int? IStatusCodeHttpResult.StatusCode => StatusCode;

    /// <summary>Gets the response content type.</summary>
    public string ContentType { get; }

    /// <inheritdoc />
    public async Task ExecuteAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        try
        {
            if (_slice is RazorSlice full) { full.HttpContext = httpContext; }
            httpContext.Response.StatusCode = StatusCode;
            httpContext.Response.ContentType = ContentType;
            await httpContext.Response.StartAsync(httpContext.RequestAborted);
        }
        catch (Exception error)
        {
            _slice.DisposeAfterFailure(error);
            throw;
        }
        await _slice.RenderAsync(httpContext.Response.BodyWriter, _encoder, httpContext.RequestAborted);
        await httpContext.Response.BodyWriter.FlushAsync(httpContext.RequestAborted);
    }
}

/// <summary>Opt-in HTTP integration for minimal templates.</summary>
public static class RazorSliceBaseResultExtensions
{
    /// <summary>Wraps a minimal template as an HTTP result without resolving request services.</summary>
    public static RazorSliceBaseResult AsResult(this RazorSliceBase slice, int statusCode = StatusCodes.Status200OK,
        string contentType = "text/html; charset=utf-8", HtmlEncoder? htmlEncoder = null)
    {
        ArgumentNullException.ThrowIfNull(slice);
        ArgumentNullException.ThrowIfNull(contentType);
        return new(slice, statusCode, contentType, htmlEncoder);
    }
}
