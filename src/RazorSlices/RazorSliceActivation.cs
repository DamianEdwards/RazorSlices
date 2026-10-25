using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Http;

namespace RazorSlices;

/// <summary>Holds compiler-generated initialization callbacks for a slice.</summary>
/// <remarks>This infrastructure type is used by generated proxies, not application code.</remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class RazorSliceActivation<TSlice> where TSlice : RazorSlice
{
    private SliceActivation _current;

    /// <summary>Creates an activation plan without discovering properties at runtime.</summary>
    public RazorSliceActivation(
        Action<RazorSlice, IServiceProvider?, HttpContext?>? initialize = null)
    {
        _current = initialize is null
            ? SliceActivation.Empty
            : new(initialize);
        if (HotReloadService.IsSupported)
        {
            HotReloadService.ClearCacheEvent += ReplaceSliceType;
        }
    }

    internal SliceActivation Current => Volatile.Read(ref _current);

    [UnconditionalSuppressMessage("Trimming", "IL2072",
        Justification = "Replacement types are used only during Hot Reload, not in trimmed applications.")]
    internal void ReplaceSliceType(Type[]? changedTypes)
    {
        if (HotReloadService.TryGetUpdatedType(changedTypes, typeof(TSlice), out var updatedType))
        {
            // Replacement SDK types are not assignable to TSlice; regenerate their complete activation together.
            var replacement = RazorSliceFactory.GetActivation(updatedType) with
            {
                CreateReplacement = () => (RazorSlice)Activator.CreateInstance(updatedType)!
            };
            Volatile.Write(ref _current, replacement);
        }
    }
}
