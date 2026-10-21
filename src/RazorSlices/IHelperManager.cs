namespace RazorSlices;

/// <summary>Provides request-scoped helpers that write through the currently bound slice.</summary>
/// <remarks>
/// Register with AddRazorSliceHelpers and inject this contract into participating slices.
/// Only sequential, nested rendering is supported. Concurrent use and retaining helpers beyond
/// their service scope are unsupported and have undefined behavior.
/// </remarks>
public interface IHelperManager : IRazorSliceBindable
{
    /// <summary>Gets the currently rendering, participating slice.</summary>
    RazorSlice CurrentSlice { get; }

    /// <summary>Gets one helper implementation per service scope.</summary>
    TContract GetHelper<TContract, TImplementation>(Func<TImplementation> create)
        where TContract : class
        where TImplementation : RazorSliceHelpers;
}
