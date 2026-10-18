namespace RazorSlices;

/// <summary>Provides caller-owned helper instances for generated rendering facades.</summary>
public interface IHelperContext
{
    /// <summary>Throws if this context is no longer rendering.</summary>
    void EnsureActive();
    /// <summary>
    /// Gets one implementation instance per calling slice render. The instance must not escape the render.
    /// </summary>
    TContract GetHelper<TContract, TImplementation>(Func<TImplementation> create)
        where TContract : class
        where TImplementation : RazorSliceHelpers;
}
