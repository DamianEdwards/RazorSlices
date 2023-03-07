using Microsoft.AspNetCore.Http.HttpResults;

namespace RazorSlices;

public abstract partial class RazorSlice
{
    /// <summary>
    /// Creates and returns a new instance of a <see cref="RazorSliceHttpResult" /> based on the provided name.
    /// </summary>
    /// <param name="sliceName">The project-relative path to the template .cshtml file, e.g. /Slices/MyTemplate.cshtml<c></c></param>
    /// <returns>The <see cref="RazorSliceHttpResult" /> instance.</returns>
    public static RazorSliceHttpResult CreateHttpResult(string sliceName) => (RazorSliceHttpResult)Create(ResolveSliceFactory(sliceName, typeof(RazorSliceHttpResult)));

    /// <summary>
    /// Creates and returns a new instance of a <see cref="RazorSliceHttpResult" /> using the provided <see cref="SliceFactory" /> delegate.
    /// </summary>
    /// <param name="sliceFactory">The <see cref="SliceFactory" /> delegate.</param>
    /// <returns>The <see cref="RazorSliceHttpResult" /> instance.</returns>
    public static RazorSliceHttpResult CreateHttpResult(SliceFactory sliceFactory) => (RazorSliceHttpResult)sliceFactory();

    /// <summary>
    /// Creates and returns a new instance of a <see cref="RazorSliceHttpResult{TModel}" /> based on the provided name.
    /// </summary>
    /// <param name="sliceName">The project-relative path to the template .cshtml file, e.g. /Slices/MyTemplate.cshtml<c></c></param>
    /// <param name="model">The model to use for the template instance.</param>
    /// <typeparam name="TModel">The model type of the template.</typeparam>
    /// <returns>The <see cref="RazorSliceHttpResult{TModel}" /> instance.</returns>
    public static RazorSliceHttpResult<TModel> CreateHttpResult<TModel>(string sliceName, TModel model) => (RazorSliceHttpResult<TModel>)Create(sliceName, model);

    /// <summary>
    /// Creates and returns a new instance of a <see cref="RazorSliceHttpResult{TModel}" /> based on the provided name.
    /// </summary>
    /// <param name="sliceFactory">The <see cref="SliceFactory" /> delegate.</param>
    /// <param name="model">The model to use for the template instance.</param>
    /// <typeparam name="TModel">The model type of the template.</typeparam>
    /// <returns>The <see cref="RazorSliceHttpResult{TModel}" /> instance.</returns>
    public static RazorSliceHttpResult<TModel> CreateHttpResult<TModel>(SliceFactory sliceFactory, TModel model) => (RazorSliceHttpResult<TModel>)Create(sliceFactory, model);
}