using System.Diagnostics.CodeAnalysis;

namespace RazorSlices;

/// <summary>Infrastructure for generated minimal-slice factories, including Hot Reload replacement.</summary>
public static class RazorSliceBaseFactory
{
    /// <summary>Creates a minimal slice without runtime injection discovery.</summary>
    public static RazorSliceBase Create<TSlice>(Func<TSlice> createSlice) where TSlice : RazorSliceBase
    {
        ArgumentNullException.ThrowIfNull(createSlice);
        return SliceCache<TSlice>.CreateReplacement is { } replacement ? replacement() : createSlice();
    }

    /// <summary>Creates a minimal model slice without runtime injection discovery.</summary>
    public static RazorSliceBase<TModel> Create<TSlice, TModel>(TModel model, Func<TModel, TSlice> createSlice)
        where TSlice : RazorSliceBase<TModel>
    {
        ArgumentNullException.ThrowIfNull(createSlice);
        if (SliceCache<TSlice>.CreateReplacement is not { } replacement) { return createSlice(model); }
        var slice = (RazorSliceBase<TModel>)replacement();
        slice.Model = model;
        return slice;
    }

    internal static class SliceCache<TSlice> where TSlice : RazorSliceBase
    {
        internal static volatile Func<RazorSliceBase>? CreateReplacement;

        static SliceCache()
        {
            if (HotReloadService.IsSupported) { HotReloadService.ClearCacheEvent += ReplaceSliceType; }
        }

        [UnconditionalSuppressMessage("Trimming", "IL2067",
            Justification = "Replacement construction is only used for Hot Reload, never in trimmed applications.")]
        internal static void ReplaceSliceType(Type[]? changedTypes)
        {
            if (HotReloadService.TryGetUpdatedType(changedTypes, typeof(TSlice), out var updatedType))
            {
                CreateReplacement = () => (RazorSliceBase)Activator.CreateInstance(updatedType)!;
            }
        }
    }
}
