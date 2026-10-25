namespace RazorSlices;

/// <summary>A generated factory for a minimal slice without a model.</summary>
public interface IRazorSliceBaseProxy
{
    /// <summary>Creates a minimal slice.</summary>
    static abstract RazorSliceBase CreateSlice();
}

/// <summary>A generated factory for a minimal slice with a model.</summary>
/// <typeparam name="TModel">The model type.</typeparam>
public interface IRazorSliceBaseProxy<TModel>
{
    /// <summary>Creates a minimal slice with the specified model.</summary>
    static abstract RazorSliceBase<TModel> CreateSlice(TModel model);
}
