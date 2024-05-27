namespace RazorSlices;

/// <summary>
/// Base class for a Razor Slice template with a model.
/// </summary>
/// <typeparam name="TModel">The model type.</typeparam>
public abstract class RazorSlice<TModel> : RazorSlice
{
    /// <summary>
    /// Gets or sets the model.
    /// </summary>
    public required TModel Model { get; set; }
}
