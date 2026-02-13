namespace RazorSlices;

/// <summary>
/// Represents a type that can statically create <see cref="RazorSlice"/> instances for slices without a model.
/// </summary>
/// <remarks>
/// You do not need to implement this interface. Types that implement this interface will be automatically generated for
/// each <c>.cshtml</c> file in your project by the Razor Slices source generator.
/// </remarks>
public interface IRazorSliceProxy
{
    /// <summary>
    /// Creates a new <see cref="RazorSlice"/> instance.
    /// </summary>
    /// <returns>The slice instance.</returns>
    abstract static RazorSlice CreateSlice();
}

/// <summary>
/// Represents a type that can statically create <see cref="RazorSlice{TModel}"/> instances for slices with a strongly-typed model.
/// </summary>
/// <typeparam name="TModel">The model type.</typeparam>
/// <remarks>
/// You do not need to implement this interface. Types that implement this interface will be automatically generated for
/// each <c>.cshtml</c> file in your project that declares a model type via <c>@inherits</c> by the Razor Slices source generator.
/// </remarks>
public interface IRazorSliceProxy<TModel>
{
    /// <summary>
    /// Creates a new <see cref="RazorSlice{TModel}"/> instance with the specified model.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <returns>The slice instance.</returns>
    abstract static RazorSlice<TModel> CreateSlice(TModel model);
}
