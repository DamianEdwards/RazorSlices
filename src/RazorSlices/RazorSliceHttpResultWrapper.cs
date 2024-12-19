using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace RazorSlices;

internal sealed class RazorSliceHttpResultWrapper(RazorSlice razorSlice) : RazorSliceHttpResult, IResult
{
    public override Task ExecuteAsync() => razorSlice.ExecuteAsync();

    /// <inheritdoc />
    Task IResult.ExecuteAsync(HttpContext httpContext)
    {
        razorSlice.HttpContext = httpContext;
        return RazorSliceHttpResultHelpers.ExecuteAsync(razorSlice, httpContext, HtmlEncoder, StatusCode, ContentType);
    }
}

internal sealed class RazorSliceHttpResultWrapper<TModel>(RazorSlice<TModel> razorSlice) : RazorSliceHttpResult<TModel>, IResult
{
    public override Task ExecuteAsync() => razorSlice.ExecuteAsync();

    /// <inheritdoc />
    Task IResult.ExecuteAsync(HttpContext httpContext)
    {
        razorSlice.HttpContext = httpContext;
        return RazorSliceHttpResultHelpers.ExecuteAsync(razorSlice, httpContext, HtmlEncoder, StatusCode, ContentType);
    }
}
