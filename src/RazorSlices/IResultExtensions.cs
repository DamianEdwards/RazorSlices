using System.Text.Encodings.Web;
using RazorSlices;

namespace Microsoft.AspNetCore.Http;

#if NET10_0_OR_GREATER
/// <summary>
/// Extensions methods for <see cref="Results"/> to render Razor slices.
/// </summary>
public static class ResultsExtensions
{
    extension(Results)
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
    extension(TypedResults)
    {
        /// <summary>
        /// Render a <see cref="RazorSlices.RazorSlice" /> template to the response.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Use this method to return a slice that you have already created using its generated proxy, e.g.:
        /// <example>
        /// <code language="csharp">
        /// app.MapGet("/", () =&gt; Results.RazorSlice(Slices.HomePage.Create(new HomeModel { Message = "Hello from RazorSlices!" })));
        /// </code>
        /// </example>
        /// </para>
        /// <para>
        /// This method sets the <see cref="RazorSlice.HtmlEncoder"/> property to <see cref="HtmlEncoder.Default"/>, if it's not already set, to avoid looking it
        /// up from the DI container and paying the cost of the request services scope. If you need to use a custom <see cref="HtmlEncoder"/>,
        /// set it on the slice before passing it to this method, or return the slice directly without using this method to have the <see cref="HtmlEncoder"/>
        /// resolved from <see cref="HttpContext.RequestServices"/>, e.g.:
        /// <example>
        /// <code language="csharp">
        /// app.MapGet("/", () =&gt; Slices.HomePage.Create(new HomeModel { Message = "Hello from RazorSlices!" }));
        /// </code>
        /// </example>
        /// </para>
        /// </remarks>
        /// <param name="razorSlice">The Razor slice to render.</param>
        /// <param name="statusCode">The HTTP status code to set on the response.</param>
        /// <returns>The Razor slice with the specified status code.</returns>
        public static RazorSlice RazorSlice(RazorSlice razorSlice, int statusCode = StatusCodes.Status200OK)
        {
            // Set the default HtmlEncoder if it's not set to avoid looking it up from the DI container and paying the cost of the request services scope.
            razorSlice.HtmlEncoder ??= HtmlEncoder.Default;
            razorSlice.StatusCode = statusCode;
            return razorSlice;
        }

        /// <summary>
        /// Render a <see cref="RazorSlices.RazorSlice" /> template to the response.
        /// </summary>
        /// <remarks>
        /// This method sets the <see cref="RazorSlice.HtmlEncoder"/> property to <see cref="HtmlEncoder.Default"/>, if it's not already set, to avoid looking it
        /// up from the DI container and paying the cost of the request services scope. If you need to use a custom <see cref="HtmlEncoder"/>,
        /// use the <see cref="RazorSlice"/> overload and set <see cref="RazorSlice.HtmlEncoder"/> before passing it in. Alternatively, return the slice directly
        /// without using these methods to have the <see cref="HtmlEncoder"/> resolved from <see cref="HttpContext.RequestServices"/>, e.g.:
        /// <example>
        /// <code language="csharp">
        /// app.MapGet("/", () =&gt; Slices.HomePage.Create());
        /// </code>
        /// </example>
        /// </remarks>
        /// <typeparam name="TSliceProxy">The type of the Razor slice proxy.</typeparam>
        /// <param name="statusCode">The HTTP status code to set on the response.</param>
        /// <returns>A <see cref="RazorSlices.RazorSlice"/> that can be rendered to the response.</returns>
        public static RazorSlice RazorSlice<TSliceProxy>(int statusCode = StatusCodes.Status200OK)
            where TSliceProxy : IRazorSliceProxy
        {
            var razorSlice = TSliceProxy.CreateSlice();

            // Set the default HtmlEncoder if it's not set to avoid looking it up from the DI container and paying the cost of the request services scope.
            razorSlice.HtmlEncoder ??= HtmlEncoder.Default;
            razorSlice.StatusCode = statusCode;
            return razorSlice;
        }

        /// <summary>
        /// Render a <see cref="RazorSlices.RazorSlice{TModel}" /> template to the response.
        /// </summary>
        /// <remarks>
        /// This method sets the <see cref="RazorSlice.HtmlEncoder"/> property to <see cref="HtmlEncoder.Default"/>, if it's not already set, to avoid looking it
        /// up from the DI container and paying the cost of the request services scope. If you need to use a custom <see cref="HtmlEncoder"/>,
        /// use the <see cref="RazorSlice"/> overload and set <see cref="RazorSlice.HtmlEncoder"/> before passing it in. Alternatively, return the slice directly
        /// without using these methods to have the <see cref="HtmlEncoder"/> resolved from <see cref="HttpContext.RequestServices"/>, e.g.:
        /// <example>
        /// <code language="csharp">
        /// app.MapGet("/", () =&gt; Slices.HomePage.Create(new HomeModel { Message = "Hello from RazorSlices!" }));
        /// </code>
        /// </example>
        /// </remarks>
        /// <typeparam name="TSliceProxy">The type of the Razor slice proxy.</typeparam>
        /// <typeparam name="TModel">The type of the model.</typeparam>
        /// <param name="model">The model to pass to the Razor slice.</param>
        /// <param name="statusCode">The HTTP status code to set on the response.</param>
        /// <returns>A <see cref="RazorSlices.RazorSlice{TModel}"/> that can be rendered to the response.</returns>
        public static RazorSlice<TModel> RazorSlice<TSliceProxy, TModel>(TModel model, int statusCode = StatusCodes.Status200OK)
            where TSliceProxy : IRazorSliceProxy<TModel>
        {
            var razorSlice = TSliceProxy.CreateSlice(model);

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
        var razorSlice = TSliceProxy.CreateSlice();

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
        var razorSlice = TSliceProxy.CreateSlice(model);

        // Set the default HtmlEncoder if it's not set to avoid looking it up from the DI container and paying the cost of the request services scope.
        razorSlice.HtmlEncoder ??= HtmlEncoder.Default;
        razorSlice.StatusCode = statusCode;
        return razorSlice;
    }
}
