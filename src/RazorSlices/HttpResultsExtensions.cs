using Microsoft.AspNetCore.Http.HttpResults;

namespace Microsoft.AspNetCore.Http;

/// <summary>
/// Extension methods for creating Razor Slice <see cref="IResult"/> instances.
/// </summary>
public static class HttpResultsExtensions
{
    /// <summary>
    /// Creates and returns a new instance of a <see cref="RazorSliceHttpResult" /> based on the provided name.
    /// </summary>
    /// <param name="resultExtensions">The <see cref="IResultExtensions"/>.</param>
    /// <param name="sliceName">The project-relative path to the template .cshtml file, e.g. /Slices/MyTemplate.cshtml<c></c></param>
    /// <param name="statusCode">The HTTP status code to return. Defaults to <see cref="StatusCodes.Status200OK"/>.</param>
    /// <returns>The <see cref="RazorSliceHttpResult" /> instance.</returns>
    public static IResult RazorSlice(
#pragma warning disable IDE0060 // Remove unused parameter
        this IResultExtensions resultExtensions,
#pragma warning restore IDE0060 // Remove unused parameter
        string sliceName, int statusCode = 200) =>
        RazorSlices.RazorSlice.CreateHttpResult(sliceName, statusCode);

    /// <summary>
    /// Creates and returns a new instance of a <see cref="RazorSliceHttpResult" /> based on the provided name.
    /// </summary>
    /// <param name="resultExtensions">The <see cref="IResultExtensions"/>.</param>
    /// <param name="sliceName">The project-relative path to the template .cshtml file, e.g. /Slices/MyTemplate.cshtml<c></c></param>
    /// <param name="serviceProvider">The <see cref="IServiceProvider" /> to use when setting the template's <c>@inject</c> properties.</param>
    /// <param name="statusCode">The HTTP status code to return. Defaults to <see cref="StatusCodes.Status200OK"/>.</param>
    /// <returns>The <see cref="RazorSliceHttpResult" /> instance.</returns>
    public static IResult RazorSlice(
#pragma warning disable IDE0060 // Remove unused parameter
        this IResultExtensions resultExtensions,
#pragma warning restore IDE0060 // Remove unused parameter
        string sliceName, IServiceProvider serviceProvider, int statusCode = 200) =>
        RazorSlices.RazorSlice.CreateHttpResult(sliceName, serviceProvider, statusCode);

    /// <summary>
    /// Creates and returns a new instance of a <see cref="RazorSliceHttpResult" /> based on the provided name.
    /// </summary>
    /// <param name="resultExtensions">The <see cref="IResultExtensions"/>.</param>
    /// <param name="sliceName">The project-relative path to the template .cshtml file, e.g. /Slices/MyTemplate.cshtml<c></c></param>
    /// <param name="model">The model to use for the template instance.</param>
    /// <param name="statusCode">The HTTP status code to return. Defaults to <see cref="StatusCodes.Status200OK"/>.</param>
    /// <typeparam name="TModel">The model type of the template.</typeparam>
    /// <returns>The <see cref="RazorSliceHttpResult" /> instance.</returns>
    public static IResult RazorSlice<TModel>(
#pragma warning disable IDE0060 // Remove unused parameter
        this IResultExtensions resultExtensions,
#pragma warning restore IDE0060 // Remove unused parameter
        string sliceName, TModel model, int statusCode = 200) =>
        RazorSlices.RazorSlice.CreateHttpResult(sliceName, model, statusCode);

    /// <summary>
    /// Creates and returns a new instance of a <see cref="RazorSliceHttpResult" /> based on the provided name.
    /// </summary>
    /// <param name="resultExtensions">The <see cref="IResultExtensions"/>.</param>
    /// <param name="sliceName">The project-relative path to the template .cshtml file, e.g. /Slices/MyTemplate.cshtml<c></c></param>
    /// <param name="model">The model to use for the template instance.</param>
    /// <param name="serviceProvider">The <see cref="IServiceProvider" /> to use when setting the template's <c>@inject</c> properties.</param>
    /// <param name="statusCode">The HTTP status code to return. Defaults to <see cref="StatusCodes.Status200OK"/>.</param>
    /// <typeparam name="TModel">The model type of the template.</typeparam>
    /// <returns>The <see cref="RazorSliceHttpResult" /> instance.</returns>
    public static IResult RazorSlice<TModel>(
#pragma warning disable IDE0060 // Remove unused parameter
        this IResultExtensions resultExtensions,
#pragma warning restore IDE0060 // Remove unused parameter
        string sliceName, TModel model, IServiceProvider serviceProvider, int statusCode = 200) =>
        RazorSlices.RazorSlice.CreateHttpResult(sliceName, model, serviceProvider, statusCode);
}
