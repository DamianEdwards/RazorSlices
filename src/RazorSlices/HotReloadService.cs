using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;

[assembly: MetadataUpdateHandler(typeof(RazorSlices.HotReloadService))]

namespace RazorSlices;

/// <summary>
/// A service that provides hot reload functionality for Razor Slices.
/// See https://learn.microsoft.com/dotnet/api/system.reflection.metadata.metadataupdatehandlerattribute for more information.
/// </summary>
internal sealed class HotReloadService
{
    public static readonly bool IsSupported = bool.TryParse(AppContext.GetData("System.StartupHookProvider.IsSupported")?.ToString() ?? "false", out var value) && value;

    public static event Action<Type[]?>? ClearCacheEvent;
    public static event Action<Type[]?>? UpdateApplicationEvent;

    public static void ClearCache(Type[]? changedTypes)
    {
        Debug.WriteLine($"{nameof(HotReloadService)}.{nameof(ClearCache)} invoked with {changedTypes?.Length ?? 0} changed types.");
        ClearCacheEvent?.Invoke(changedTypes);
    }

    public static void UpdateApplication(Type[]? changedTypes)
    {
        Debug.WriteLine($"{nameof(HotReloadService)}.{nameof(UpdateApplication)} invoked with {changedTypes?.Length ?? 0} changed types.");
        UpdateApplicationEvent?.Invoke(changedTypes);
    }

    public static bool TryGetUpdatedType(Type[]? changedTypes, Type originalType, [NotNullWhen(true)] out Type? updatedType)
    {
        if (changedTypes is not null)
        {
            foreach (var type in changedTypes)
            {
                var originalTypeAttribute = type.GetCustomAttribute<MetadataUpdateOriginalTypeAttribute>();
                if (originalTypeAttribute?.OriginalType == originalType)
                {
                    updatedType = type;
                    Debug.WriteLine($"Type '{originalType.Name}' was replaced with type '{updatedType.Name}' by Hot Reload");
                    return true;
                }
            }
        }

        updatedType = null;
        return false;
    }
}
