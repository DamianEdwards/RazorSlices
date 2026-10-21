using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Http;

namespace RazorSlices;

/// <summary>Holds compiler-generated initialization and service-binding callbacks for a slice.</summary>
/// <remarks>This infrastructure type is used by generated proxies, not application code.</remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class RazorSliceActivation<TSlice> where TSlice : RazorSlice
{
    private SliceActivation _current;

    /// <summary>Creates an activation plan without discovering properties at runtime.</summary>
    public RazorSliceActivation(
        Action<RazorSlice, IServiceProvider?, HttpContext?>? initialize = null,
        Action<RazorSlice>? bind = null,
        Func<RazorSlice, Exception?>? unbind = null)
    {
        if ((bind is null) != (unbind is null))
        {
            throw new ArgumentException("Binding and unbinding callbacks must be supplied together.");
        }
        _current = initialize is null && bind is null
            ? SliceActivation.Empty
            : new(initialize, bind is null ? null : new(bind, unbind!));
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
