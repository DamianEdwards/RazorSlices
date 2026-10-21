namespace RazorSlices;

/// <summary>Participates in the rendering lifecycle of slices that inject this service contract.</summary>
/// <remarks>
/// The declared injected type must implement this interface and be a reference type.
/// Bindings are nested and non-concurrent; concurrent use is unsupported and has undefined behavior.
/// Injected properties must not be reassigned while rendering.
/// </remarks>
public interface IRazorSliceBindable
{
    /// <summary>Enters a slice's rendering context. If this throws, restore any state changed by this call.</summary>
    void Bind(RazorSlice slice);

    /// <summary>Leaves the slice's rendering context and restores the previous caller.</summary>
    void Unbind(RazorSlice slice);
}
