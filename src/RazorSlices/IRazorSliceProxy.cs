namespace RazorSlices;

/// <summary>
/// Represents a type that can create <see cref="RazorSlice"/> instances.
/// </summary>
public interface IRazorSliceProxy
{
#if NET8_0_OR_GREATER
    /// <summary>
    /// Creates a new <see cref="RazorSlice"/> instance.
    /// </summary>
    /// <returns>The slice instance.</returns>
    abstract static RazorSlice Create();

    /// <summary>
    /// Creates a new <see cref="RazorSlice{TModel}"/> instance.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <returns>The slice instance.</returns>
    abstract static RazorSlice<TModel> Create<TModel>(TModel model);
#endif
}
