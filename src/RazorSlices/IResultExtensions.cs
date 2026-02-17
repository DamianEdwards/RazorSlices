using System.Text.Encodings.Web;
using RazorSlices;

namespace Microsoft.AspNetCore.Http;

#if NET10_0_OR_GREATER
/// <summary>
/// Extensions methods for <see cref="Results"/> to render Razor slices.
/// </summary>
public static class ResultsExtensions
{
    extension (Results)
    {
        /// <summary>
        /// Render a <see cref="RazorSlices.RazorSlice" /> template to the response.
        /// </summary>
        /// <typeparam name="TSliceProxy"></typeparam>
        /// <param name="statusCode">The HTTP status code to set on the response.</param>
        /// <returns>An <see cref="RazorSlices.RazorSlice"/> that can be rendered to the response.</returns>
        public static RazorSlice RazorSlice<TSliceProxy>(int statusCode = StatusCodes.Status200OK)
            where TSliceProxy : IRazorSliceProxy
            => TypedResultsExtensions.RazorSlice<TSliceProxy>(statusCode);

        /// <summary>
        /// Render a <see cref="RazorSlices.RazorSlice{TModel}" /> template to the response.
        /// </summary>
        /// <typeparam name="TSliceProxy"></typeparam>
        /// <typeparam name="TModel"></typeparam>
        /// <param name="model">The model to pass to the Razor slice.</param>
        /// <param name="statusCode">The HTTP status code to set on the response.</param>
        /// <returns>An <see cref="RazorSlices.RazorSlice{TModel}"/> that can be rendered to the response.</returns>
        public static RazorSlice<TModel> RazorSlice<TSliceProxy, TModel>(TModel model, int statusCode = StatusCodes.Status200OK)
            where TSliceProxy : IRazorSliceProxy<TModel>
            => TypedResultsExtensions.RazorSlice<TSliceProxy, TModel>(model, statusCode);
    }
}

/// <summary>
/// Extensions methods for <see cref="TypedResults"/> to render Razor slices.
/// </summary>
public static class TypedResultsExtensions
{
    extension (TypedResults)
    {
        /// <summary>
        /// Render a <see cref="RazorSlices.RazorSlice" /> template to the response.
        /// </summary>
        /// <typeparam name="TSliceProxy"></typeparam>
        /// <param name="statusCode">The HTTP status code to set on the response.</param>
        /// <returns>An <see cref="RazorSlices.RazorSlice"/> that can be rendered to the response.</returns>
        public static RazorSlice RazorSlice<TSliceProxy>(int statusCode = StatusCodes.Status200OK)
            where TSliceProxy : IRazorSliceProxy
        {
#pragma warning disable CA2000 // Dispose objects before losing scope: IResult will get disposed by ASP.NET Core
            var razorSlice = TSliceProxy.CreateSlice();
#pragma warning restore CA2000 // Dispose objects before losing scope

            // Set the default HtmlEncoder if it's not set to avoid looking it up from the DI container and paying the cost of the request services scope.
            razorSlice.HtmlEncoder ??= HtmlEncoder.Default;
            razorSlice.StatusCode = statusCode;
            return razorSlice;
        }

        /// <summary>
        /// Render a <see cref="RazorSlices.RazorSlice{TModel}" /> template to the response.
        /// </summary>
        /// <typeparam name="TSliceProxy"></typeparam>
        /// <typeparam name="TModel"></typeparam>
        /// <param name="model">The model to pass to the Razor slice.</param>
        /// <param name="statusCode">The HTTP status code to set on the response.</param>
        /// <returns>An <see cref="RazorSlices.RazorSlice{TModel}"/> that can be rendered to the response.</returns>
        public static RazorSlice<TModel> RazorSlice<TSliceProxy, TModel>(TModel model, int statusCode = StatusCodes.Status200OK)
            where TSliceProxy : IRazorSliceProxy<TModel>
        {
    #pragma warning disable CA2000 // Dispose objects before losing scope: IResult will get disposed by ASP.NET Core
            var razorSlice = TSliceProxy.CreateSlice(model);
    #pragma warning restore CA2000 // Dispose objects before losing scope
            
            // Set the default HtmlEncoder if it's not set to avoid looking it up from the DI container and paying the cost of the request services scope.
            razorSlice.HtmlEncoder ??= HtmlEncoder.Default;
            razorSlice.StatusCode = statusCode;
            return razorSlice;
        }
    }
}
#endif

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
    /// <param name="statusCode">The HTTP status code to set on the response.</param>
    /// <returns>An <see cref="RazorSlices.RazorSlice"/> that can be rendered to the response.</returns>
#if NET10_0_OR_GREATER
    [Obsolete("For .NET 10.0 and later, call static extension method on Results type directly: Results.RazorSlice<TSliceProxy>(..)", error: true)]
#endif
    public static RazorSlice RazorSlice<TSliceProxy>(this IResultExtensions _, int statusCode = StatusCodes.Status200OK)
        where TSliceProxy : IRazorSliceProxy
    {
#pragma warning disable CA2000 // Dispose objects before losing scope: IResult will get disposed by ASP.NET Core
        var razorSlice = TSliceProxy.CreateSlice();
#pragma warning restore CA2000 // Dispose objects before losing scope

        // Set the default HtmlEncoder if it's not set to avoid looking it up from the DI container and paying the cost of the request services scope.
        razorSlice.HtmlEncoder ??= HtmlEncoder.Default;
        razorSlice.StatusCode = statusCode;
        return razorSlice;
    }

    /// <summary>
    /// Render a <see cref="RazorSlices.RazorSlice{TModel}" /> template to the response.
    /// </summary>
    /// <typeparam name="TSliceProxy"></typeparam>
    /// <typeparam name="TModel"></typeparam>
    /// <param name="_"></param>
    /// <param name="model"></param>
    /// <param name="statusCode">The HTTP status code to set on the response.</param>
    /// <returns>An <see cref="RazorSlices.RazorSlice{TModel}"/> that can be rendered to the response.</returns>
#if NET10_0_OR_GREATER
    [Obsolete("For .NET 10.0 and later, call static extension method on Results type directly: Results.RazorSlice<TSliceProxy, TModel>(..)", error: true)]
#endif
    public static RazorSlice<TModel> RazorSlice<TSliceProxy, TModel>(this IResultExtensions _, TModel model, int statusCode = StatusCodes.Status200OK)
        where TSliceProxy : IRazorSliceProxy<TModel>
    {
#pragma warning disable CA2000 // Dispose objects before losing scope: IResult will get disposed by ASP.NET Core
        var razorSlice = TSliceProxy.CreateSlice(model);
#pragma warning restore CA2000 // Dispose objects before losing scope
        
        // Set the default HtmlEncoder if it's not set to avoid looking it up from the DI container and paying the cost of the request services scope.
        razorSlice.HtmlEncoder ??= HtmlEncoder.Default;
        razorSlice.StatusCode = statusCode;
        return razorSlice;
    }
}
