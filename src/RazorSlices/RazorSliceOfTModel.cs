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

///// <summary>
///// Base class for a Razor Slice template with a model.
///// </summary>
///// <typeparam name="TModel">The model type.</typeparam>
///// <typeparam name="TLayout">The layout type.</typeparam>
//public abstract class RazorSlice<TModel, TLayout> : RazorSlice<TModel>, IUseLayout
//    where TLayout : IRazorSliceProxy
//{
//    RazorSlice IUseLayout.CreateLayout() => TLayout.Create();
//}

///// <summary>
///// Base class for a Razor Slice template with a model.
///// </summary>
///// <typeparam name="TModel">The model type.</typeparam>
///// <typeparam name="TLayout">The layout type.</typeparam>
///// <typeparam name="TLayoutModel">The layout model type.</typeparam>
//public abstract class RazorSlice<TModel, TLayout, TLayoutModel> : RazorSlice<TModel, TLayout>, IUseLayout
//    where TLayout : IRazorSliceProxy
//{
//    RazorSlice IUseLayout.CreateLayout() => TLayout.Create(LayoutModel);

//    /// <summary>
//    /// Gets the layout model.
//    /// </summary>
//    protected abstract TLayoutModel LayoutModel { get; }
//}
