namespace RazorSlices;

/// <summary>
/// 
/// </summary>
public interface IRazorSliceProxy
{
#if NET8_0_OR_GREATER
    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    abstract static RazorSlice Create();

    /// <summary>
    /// 
    /// </summary>
    /// <param name="model"></param>
    /// <returns></returns>
    abstract static RazorSlice<TModel> Create<TModel>(TModel model);
#endif
}
