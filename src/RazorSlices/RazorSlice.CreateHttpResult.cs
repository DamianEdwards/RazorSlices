using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace RazorSlices;

public abstract partial class RazorSlice
{
    /// <summary>
    /// Creates and returns a new instance of a <see cref="RazorSliceHttpResult" /> based on the provided name.
    /// </summary>
    /// <param name="sliceName">The project-relative path to the template .cshtml file, e.g. /Slices/MyTemplate.cshtml<c></c></param>
    /// <param name="statusCode">The HTTP status code to return. Defaults to <see cref="StatusCodes.Status200OK"/>.</param>
    /// <returns>The <see cref="RazorSliceHttpResult" /> instance.</returns>
    public static RazorSliceHttpResult CreateHttpResult(string sliceName, int statusCode = StatusCodes.Status200OK)
    {
        var result = (RazorSliceHttpResult)Create(ResolveSliceFactory(sliceName));
        result.StatusCode = statusCode;
        return result;
    }

    /// <summary>
    /// Creates and returns a new instance of a <see cref="RazorSliceHttpResult" /> based on the provided name.
    /// </summary>
    /// <param name="sliceName">The project-relative path to the template .cshtml file, e.g. /Slices/MyTemplate.cshtml<c></c></param>
    /// <param name="serviceProvider"></param>
    /// <param name="statusCode">The HTTP status code to return. Defaults to <see cref="StatusCodes.Status200OK"/>.</param>
    /// <returns>The <see cref="RazorSliceHttpResult" /> instance.</returns>
    public static RazorSliceHttpResult CreateHttpResult(string sliceName, IServiceProvider serviceProvider, int statusCode = StatusCodes.Status200OK)
    {
        var result = (RazorSliceHttpResult)Create(ResolveSliceWithServiceFactory(sliceName), serviceProvider);
        result.StatusCode = statusCode;
        return result;
    }

    /// <summary>
    /// Creates and returns a new instance of a <see cref="RazorSliceHttpResult" /> using the provided <see cref="SliceFactory" /> delegate.
    /// </summary>
    /// <param name="sliceFactory">The <see cref="SliceFactory" /> delegate.</param>
    /// <param name="statusCode">The HTTP status code to return. Defaults to <see cref="StatusCodes.Status200OK"/>.</param>
    /// <returns>The <see cref="RazorSliceHttpResult" /> instance.</returns>
    public static RazorSliceHttpResult CreateHttpResult(SliceFactory sliceFactory, int statusCode = StatusCodes.Status200OK)
    {
        var result = (RazorSliceHttpResult)sliceFactory();
        result.StatusCode = statusCode;
        return result;
    }

    /// <summary>
    /// Creates and returns a new instance of a <see cref="RazorSliceHttpResult{TModel}" /> based on the provided name.
    /// </summary>
    /// <param name="sliceName">The project-relative path to the template .cshtml file, e.g. /Slices/MyTemplate.cshtml<c></c></param>
    /// <param name="model">The model to use for the template instance.</param>
    /// <param name="statusCode">The HTTP status code to return. Defaults to <see cref="StatusCodes.Status200OK"/>.</param>
    /// <typeparam name="TModel">The model type of the template.</typeparam>
    /// <returns>The <see cref="RazorSliceHttpResult{TModel}" /> instance.</returns>
    public static RazorSliceHttpResult<TModel> CreateHttpResult<TModel>(string sliceName, TModel model, int statusCode = StatusCodes.Status200OK)
    {
        var result = (RazorSliceHttpResult<TModel>)Create(sliceName, model);
        result.StatusCode = statusCode;
        return result;
    }

    /// <summary>
    /// Creates and returns a new instance of a <see cref="RazorSliceHttpResult{TModel}" /> based on the provided name.
    /// </summary>
    /// <param name="sliceName">The project-relative path to the template .cshtml file, e.g. /Slices/MyTemplate.cshtml<c></c></param>
    /// <param name="model">The model to use for the template instance.</param>
    /// <param name="serviceProvider"></param>
    /// <param name="statusCode">The HTTP status code to return. Defaults to <see cref="StatusCodes.Status200OK"/>.</param>
    /// <typeparam name="TModel">The model type of the template.</typeparam>
    /// <returns>The <see cref="RazorSliceHttpResult{TModel}" /> instance.</returns>
    public static RazorSliceHttpResult<TModel> CreateHttpResult<TModel>(string sliceName, TModel model, IServiceProvider serviceProvider, int statusCode = StatusCodes.Status200OK)
    {
        var result = (RazorSliceHttpResult<TModel>)Create(ResolveSliceWithServiceFactory<TModel>(sliceName), model, serviceProvider);
        result.StatusCode = statusCode;
        return result;
    }

    /// <summary>
    /// Creates and returns a new instance of a <see cref="RazorSliceHttpResult{TModel}" /> based on the provided name.
    /// </summary>
    /// <param name="sliceFactory">The <see cref="SliceFactory" /> delegate.</param>
    /// <param name="model">The model to use for the template instance.</param>
    /// <param name="statusCode">The HTTP status code to return. Defaults to <see cref="StatusCodes.Status200OK"/>.</param>
    /// <typeparam name="TModel">The model type of the template.</typeparam>
    /// <returns>The <see cref="RazorSliceHttpResult{TModel}" /> instance.</returns>
    public static RazorSliceHttpResult<TModel> CreateHttpResult<TModel>(SliceFactory<TModel> sliceFactory, TModel model, int statusCode = StatusCodes.Status200OK)
    {
        var result = (RazorSliceHttpResult<TModel>)Create(sliceFactory, model);
        result.StatusCode = statusCode;
        return result;
    }
}