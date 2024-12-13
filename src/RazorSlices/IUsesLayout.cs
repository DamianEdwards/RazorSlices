using System.ComponentModel;

namespace RazorSlices;

/// <summary>
/// Do not use. Required by the Razor Slices infrastrucutre.
/// </summary>
/// <remarks>
/// To use a layout use the <see cref="IUsesLayout{TLayout}"/> or <see cref="IUsesLayout{TLayout, TLayoutModel}"/> interfaces.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)] // Hide from IntelliSense.
public interface IUsesLayout
{
    /// <summary>
    /// Do not use. Required by the Razor Slices infrastrucutre.
    /// </summary>
    internal RazorSlice CreateLayoutImpl();
}

/// <summary>
/// Represents a Razor Slice that uses another Razor Slice for layout. The layout Razor Slice must inherit from <see cref="RazorLayoutSlice"/>.
/// </summary>
/// <remarks>
/// Use this interface to specify the layout type for a Razor Slice, e.g:
/// <example>
/// <code>
/// @implements IUseLayout&lt;_Layout[]&gt;
/// </code>
/// </example>
/// </remarks>
public interface IUsesLayout<TLayout> : IUsesLayout
    where TLayout : IRazorSliceProxy
{
    /// <summary>
    /// Creates a new instance of the layout Razor Slice by calling <see cref="IRazorSliceProxy.CreateSlice()"/> on <typeparamref name="TLayout"/>.
    /// </summary>
    /// <returns>The layout Razor Slice.</returns>
    public RazorSlice CreateLayout() => TLayout.CreateSlice();

    RazorSlice IUsesLayout.CreateLayoutImpl() => CreateLayout();
}

/// <summary>
/// Represents a Razor Slice that uses another Razor Slice for layout. The layout Razor Slice must inherit from <see cref="RazorLayoutSlice"/>.
/// </summary>
/// <remarks>
/// Use this interface to specify the layout type for a Razor Slice, e.g:
/// <example>
/// <code>
/// @implements IUseLayout&lt;_Layout[], LayoutModel&gt;
/// </code>
/// </example>
/// </remarks>
public interface IUsesLayout<TLayout, TLayoutModel> : IUsesLayout<TLayout>
    where TLayout : IRazorSliceProxy
{
    /// <summary>
    /// Gets the layout model.
    /// </summary>
    TLayoutModel LayoutModel { get; }

    /// <summary>
    /// Creates a new instance of the layout Razor Slice by calling <see cref="IRazorSliceProxy.CreateSlice{TModel}(TModel)"/> on <typeparamref name="TLayout"/>.
    /// </summary>
    /// <returns>The layout Razor Slice.</returns>
    public new RazorSlice<TLayoutModel> CreateLayout() => TLayout.CreateSlice(LayoutModel);

    RazorSlice IUsesLayout.CreateLayoutImpl() => CreateLayout();
}
