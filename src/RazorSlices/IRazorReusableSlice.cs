namespace RazorSlices;

/// <summary>
/// Represents a reusable Razor Slice.
/// </summary>
public interface IRazorReusableSlice
{
    /// <summary>
    /// Tries to reset the state of the slice for reuse. This method is called when the slice has been rendered and is no longer needed.
    /// </summary>
    /// <remarks>
    /// Return <c>true</c> from this method to indicate the slice has been successfully reset and can be reused. Return <c>false</c> to indicate the slice cannot be reused.
    /// </remarks>
    public bool TryReset() => true;
}
