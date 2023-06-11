namespace RazorSlices;

/// <summary>
/// 
/// </summary>
public interface IRazorSliceProxy
{
    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    abstract static RazorSlice Create();
}

/// <summary>
/// Represents a source-generated proxy type for a Razor slice.
/// </summary>
public interface IRazorSliceProxy<TModel> : IRazorSliceProxy
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="model"></param>
    /// <returns></returns>
    abstract static RazorSlice<TModel> Create(TModel model);
}
