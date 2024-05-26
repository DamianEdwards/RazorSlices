using RazorSlices;

namespace Microsoft.AspNetCore.Http.HttpResults;

/// <summary>
/// RazorSlices IResult extensions.
/// </summary>
public static class RazorSlicesExtensions
{
    /// <summary>
    /// Render a <see cref="RazorSlices.RazorSlice" /> template to the response.
    /// </summary>
    /// <typeparam name="TSliceProxy"></typeparam>
    /// <param name="_"></param>
    /// <param name="statusCode"></param>
    /// <returns>An <see cref="IRazorSliceHttpResult"/> that can be rendered to the response.</returns>
    public static IRazorSliceHttpResult RazorSlice<TSliceProxy>(this IResultExtensions _, int statusCode = StatusCodes.Status200OK)
        where TSliceProxy : IRazorSliceProxy
    {
        var razorSlice = TSliceProxy.Create();

        return HandleRazorSlice(razorSlice, statusCode);
    }

    /// <summary>
    /// Render a <see cref="RazorSlices.RazorSlice" /> template to the response.
    /// </summary>
    /// <typeparam name="TSliceProxy"></typeparam>
    /// <typeparam name="TModel"></typeparam>
    /// <param name="_"></param>
    /// <param name="model"></param>
    /// <param name="statusCode"></param>
    /// <returns>An <see cref="IRazorSliceHttpResult"/> that can be rendered to the response.</returns>
    public static IRazorSliceHttpResult RazorSlice<TSliceProxy, TModel>(this IResultExtensions _, TModel model, int statusCode = StatusCodes.Status200OK)
        where TSliceProxy : IRazorSliceProxy
    {
        var razorSlice = TSliceProxy.Create(model);

        return HandleRazorSlice(razorSlice, statusCode);
    }

    private static IRazorSliceHttpResult HandleRazorSlice(RazorSlice razorSlice, int statusCode)
    {
        if (razorSlice is RazorSliceHttpResult razorSliceHttpResult)
        {
            razorSliceHttpResult.StatusCode = statusCode;
            return razorSliceHttpResult;
        }

        return WrapRazorSlice(razorSlice, statusCode);
    }

    private static IRazorSliceHttpResult HandleRazorSlice<TModel>(RazorSlice<TModel> razorSlice, int statusCode)
    {
        if (razorSlice is RazorSliceHttpResult<TModel> razorSliceHttpResult)
        {
            razorSliceHttpResult.StatusCode = statusCode;
            return razorSliceHttpResult;
        }

        return WrapRazorSlice(razorSlice, statusCode);
    }

    private static RazorSliceHttpResultWrapper WrapRazorSlice(RazorSlice razorSlice, int statusCode)
    {
        return new RazorSliceHttpResultWrapper(razorSlice)
        {
            StatusCode = statusCode
        };
    }
}
