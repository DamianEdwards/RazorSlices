namespace RazorSlices;

/// <summary>Provides caller-owned helper instances for generated rendering facades.</summary>
public interface IHelperContext
{
    /// <summary>Gets the built-in form rendering helpers for this render graph.</summary>
    FormHelpers FormHelpers => new(this);

    /// <summary>Throws when this context is no longer rendering.</summary>
    void EnsureActive();

    /// <summary>Gets one helper implementation per root render, shared by partials, layouts, and sections.</summary>
    TContract GetHelper<TContract, TImplementation>(Func<TImplementation> create)
        where TContract : class
        where TImplementation : RazorSliceHelpers;
}
