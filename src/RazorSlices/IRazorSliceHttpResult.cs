using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Http;

namespace RazorSlices;

/// <summary>
/// Represents an <see cref="IResult"/> that renders a <see cref="RazorSlice"/> template to the response.
/// </summary>
public interface IRazorSliceHttpResult : IResult, IStatusCodeHttpResult, IContentTypeHttpResult, IDisposable
{
    /// <summary>
    /// Gets or sets the HTTP status code. Defaults to <see cref="StatusCodes.Status200OK"/>
    /// </summary>
    public new int? StatusCode { get; set; }

    int? IStatusCodeHttpResult.StatusCode => StatusCode;

    /// <summary>
    /// Gets the content type: <c>text/html; charset=utf-8</c>
    /// </summary>
    public new string? ContentType => "text/html; charset=utf-8";

    /// <summary>
    /// Gets or sets the <see cref="System.Text.Encodings.Web.HtmlEncoder" /> instance to use when rendering the template. If
    /// <c>null</c> the template will use <see cref="HtmlEncoder.Default" />.
    /// </summary>
    public HtmlEncoder? HtmlEncoder { get; set; }

    /// <inheritdoc />
    Task IResult.ExecuteAsync(HttpContext httpContext)
    {
        var razorSlice = (RazorSlice)this;
        razorSlice.HttpContext = httpContext;
        return RazorSliceHttpResultHelpers.ExecuteAsync(razorSlice, httpContext, HtmlEncoder, StatusCode ?? StatusCodes.Status200OK, ContentType ?? "text/html; charset=utf-8");
    }
}
