using System.Text.Encodings.Web;
using RazorSlices;

namespace Microsoft.AspNetCore.Http.HttpResults;

/// <summary>
/// A <see cref="RazorSlice{TModel}" /> template that is also an <see cref="IResult" /> so it can be directly returned from
/// a route handler delegate. When executed it will render the template to the response.
/// </summary>
public abstract class RazorSliceHttpResult<TModel> : RazorSlice<TModel>, IRazorSliceHttpResult
{
    /// <summary>
    /// Gets or sets the HTTP status code. Defaults to <see cref="StatusCodes.Status200OK"/>
    /// </summary>
    public int StatusCode { get; set; } = StatusCodes.Status200OK;

    int? IRazorSliceHttpResult.StatusCode { get => StatusCode; set => StatusCode = value ?? StatusCodes.Status200OK; }

    /// <summary>
    /// Gets the content type: <c>text/html; charset=utf-8</c>
    /// </summary>
    public string ContentType => "text/html; charset=utf-8";

    /// <summary>
    /// Gets or sets the <see cref="System.Text.Encodings.Web.HtmlEncoder" /> instance to use when rendering the template. If
    /// <c>null</c> the template will use <see cref="HtmlEncoder.Default" />.
    /// </summary>
    public HtmlEncoder? HtmlEncoder { get; set; }

    /// <inheritdoc/>
    Task IResult.ExecuteAsync(HttpContext httpContext)
    {
        HttpContext = httpContext;
        return RazorSliceHttpResultHelpers.ExecuteAsync(this, httpContext, HtmlEncoder, StatusCode, ContentType);
    }
}

///// <summary>
///// A <see cref="RazorSlice{TModel, TLayout}" /> template that is also an <see cref="IResult" /> so it can be directly returned from
///// a route handler delegate. When executed it will render the template to the response.
///// </summary>
//public abstract class RazorSliceHttpResult<TModel, TLayout> : RazorSliceHttpResult<TModel>, IUseLayout
//    where TLayout : IRazorSliceProxy
//{
//    RazorSlice IUseLayout.CreateLayout() => TLayout.Create();
//}

///// <summary>
///// A <see cref="RazorSlice{TModel, TLayout, TLayoutModel}" /> template that is also an <see cref="IResult" /> so it can be directly returned from
///// a route handler delegate. When executed it will render the template to the response.
///// </summary>
//public abstract class RazorSliceHttpResult<TModel, TLayout, TLayoutModel> : RazorSliceHttpResult<TModel, TLayout>, IUseLayout
//    where TLayout : IRazorSliceProxy
//{
//    RazorSlice IUseLayout.CreateLayout() => TLayout.Create(LayoutModel);

//    /// <summary>
//    /// Gets the layout model.
//    /// </summary>
//    protected abstract TLayoutModel LayoutModel { get; }
//}
