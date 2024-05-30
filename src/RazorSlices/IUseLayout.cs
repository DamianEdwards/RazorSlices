using System.ComponentModel;

namespace RazorSlices;

/// <summary>
/// Do not use. Required by the Razor Slices infrastrucutre.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)] // Hide from IntelliSense.
public interface IUseLayout
{
    /// <summary>
    /// Do not use. Required by the Razor Slices infrastrucutre.
    /// </summary>
    internal RazorSlice CreateLayoutImpl();
}

/// <summary>
/// Represents a Razor Slice that uses another Razor Slice for layout.
/// </summary>
public interface IUseLayout<TLayout> : IUseLayout
    where TLayout : IRazorSliceProxy
{
    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public RazorSlice CreateLayout() => TLayout.Create();

    RazorSlice IUseLayout.CreateLayoutImpl() => CreateLayout();
}

/// <summary>
/// Represents a Razor Slice that uses another Razor Slice for layout.
/// </summary>
public interface IUseLayout<TLayout, TLayoutModel> : IUseLayout<TLayout>
    where TLayout : IRazorSliceProxy
{
    /// <summary>
    /// Gets the layout model.
    /// </summary>
    TLayoutModel LayoutModel { get; }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public new RazorSlice<TLayoutModel> CreateLayout() => TLayout.Create(LayoutModel);

    RazorSlice IUseLayout.CreateLayoutImpl() => CreateLayout();
}
