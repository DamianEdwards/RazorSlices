namespace RazorSlices;

/// <summary>
/// Represents a type that can statically create <see cref="RazorSlice"/> instances.
/// </summary>
/// <remarks>
/// You do not need to implement this interface. Types that implement this interface will be automatically generated for
/// each <c>.cshtml</c> file in your project by the Razor Slices source generator.
/// </remarks>
public interface IRazorSliceProxy
{
    ///// <summary>
    ///// Gets the type of the <see cref="RazorSlice"/>.
    ///// </summary>
    //Type SliceType { get; }

    /// <summary>
    /// Creates a new <see cref="RazorSlice"/> instance.
    /// </summary>
    /// <returns>The slice instance.</returns>
    abstract static RazorSlice CreateSlice();

    /// <summary>
    /// Creates a new <see cref="RazorSlice{TModel}"/> instance.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <returns>The slice instance.</returns>
    abstract static RazorSlice<TModel> CreateSlice<TModel>(TModel model);
}
