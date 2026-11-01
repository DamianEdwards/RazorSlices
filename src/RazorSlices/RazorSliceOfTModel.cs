namespace RazorSlices;

/// <summary>
/// Base class for a Razor Slice template with a model. Inherit from this class or <see cref="RazorSlice"/> in your <c>.cshtml</c> files using the <c>@inherit</c> directive.
/// </summary>
/// <seealso cref="RazorSlice"/>
/// <typeparam name="TModel">The model type.</typeparam>
public abstract class RazorSlice<TModel> : RazorSlice
{
    /// <summary>
    /// Gets or sets the model.
    /// </summary>
    public required TModel Model { get; set; }
}
