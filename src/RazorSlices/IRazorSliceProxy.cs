namespace RazorSlices;

/// <summary>
/// Represents a source-generated proxy type for a Razor slice.
/// </summary>
public interface IRazorSliceProxy<TSlice> where TSlice : RazorSlice
{
#if NET7_0_OR_GREATER
    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
#pragma warning disable CA1000 // Do not declare static members on generic types: static abstract
    abstract static TSlice Create();
#pragma warning restore CA1000
#endif
}

/// <summary>
/// Represents a source-generated proxy type for a strongly-typed Razor slice.
/// </summary>
public interface IRazorSliceProxy<TSlice, TModel> where TSlice : RazorSlice<TModel>
{
#if NET7_0_OR_GREATER
    /// <summary>
    /// 
    /// </summary>
    /// <param name="model"></param>
    /// <returns></returns>
#pragma warning disable CA1000 // Do not declare static members on generic types: static abstract
    abstract static TSlice Create(TModel model);
#pragma warning restore CA1000
#endif
}
