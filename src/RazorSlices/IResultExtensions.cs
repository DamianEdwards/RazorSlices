using Microsoft.AspNetCore.Http.HttpResults;
using RazorSlices;

namespace Microsoft.AspNetCore.Http;

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
#pragma warning disable CA2000 // Dispose objects before losing scope: IResult will get disposed by ASP.NET Core
        var razorSlice = TSliceProxy.Create();
#pragma warning restore CA2000 // Dispose objects before losing scope

        if (razorSlice is RazorSliceHttpResult razorSliceHttpResult)
        {
            razorSliceHttpResult.StatusCode = statusCode;
            return razorSliceHttpResult;
        }

        return WrapRazorSliceWithHttpResult(razorSlice, statusCode);
    }

    /// <summary>
    /// Render a <see cref="RazorSlices.RazorSlice{TModel}" /> template to the response.
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
#pragma warning disable CA2000 // Dispose objects before losing scope: IResult will get disposed by ASP.NET Core
        var razorSlice = TSliceProxy.Create(model);
#pragma warning restore CA2000 // Dispose objects before losing scope
        
        if (razorSlice is RazorSliceHttpResult<TModel> razorSliceHttpResult)
        {
            razorSliceHttpResult.StatusCode = statusCode;
            return razorSliceHttpResult;
        }

        return WrapRazorSliceWithHttpResult(razorSlice, statusCode);
    }

    private static RazorSliceHttpResultWrapper WrapRazorSliceWithHttpResult(RazorSlice razorSlice, int statusCode)
    {
        return new RazorSliceHttpResultWrapper(razorSlice)
        {
            StatusCode = statusCode
        };
    }
}
