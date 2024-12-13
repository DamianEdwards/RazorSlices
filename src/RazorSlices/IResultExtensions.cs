using System.Text.Encodings.Web;
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
    /// <returns>An <see cref="RazorSliceHttpResult"/> that can be rendered to the response.</returns>
    public static RazorSliceHttpResult RazorSlice<TSliceProxy>(this IResultExtensions _, int statusCode = StatusCodes.Status200OK)
        where TSliceProxy : IRazorSliceProxy
    {
#pragma warning disable CA2000 // Dispose objects before losing scope: IResult will get disposed by ASP.NET Core
        var razorSlice = TSliceProxy.CreateSlice();
#pragma warning restore CA2000 // Dispose objects before losing scope

        if (razorSlice is RazorSliceHttpResult razorSliceHttpResult)
        {
            // Set the default HtmlEncoder if it's not set to avoid looking it up from the DI container and paying the cost of the request services scope.
            razorSliceHttpResult.HtmlEncoder ??= HtmlEncoder.Default;
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
    /// <returns>An <see cref="RazorSliceHttpResult{TModel}"/> that can be rendered to the response.</returns>
    public static RazorSliceHttpResult<TModel> RazorSlice<TSliceProxy, TModel>(this IResultExtensions _, TModel model, int statusCode = StatusCodes.Status200OK)
        where TSliceProxy : IRazorSliceProxy
    {
#pragma warning disable CA2000 // Dispose objects before losing scope: IResult will get disposed by ASP.NET Core
        var razorSlice = TSliceProxy.CreateSlice(model);
#pragma warning restore CA2000 // Dispose objects before losing scope
        
        if (razorSlice is RazorSliceHttpResult<TModel> razorSliceHttpResult)
        {
            // Set the default HtmlEncoder if it's not set to avoid looking it up from the DI container and paying the cost of the request services scope.
            razorSliceHttpResult.HtmlEncoder ??= HtmlEncoder.Default;
            razorSliceHttpResult.StatusCode = statusCode;
            return razorSliceHttpResult;
        }

        return WrapRazorSliceWithHttpResult(razorSlice, statusCode);
    }

    private static RazorSliceHttpResultWrapper WrapRazorSliceWithHttpResult(RazorSlice razorSlice, int statusCode)
        => new(razorSlice)
        {
            StatusCode = statusCode
        };

    private static RazorSliceHttpResultWrapper<TModel> WrapRazorSliceWithHttpResult<TModel>(RazorSlice<TModel> razorSlice, int statusCode)
        => new(razorSlice)
        {
            Model = razorSlice.Model,
            StatusCode = statusCode
        };
}
