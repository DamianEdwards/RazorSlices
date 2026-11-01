using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace RazorSlices;

/// <summary>Service collection extensions for Razor Slices.</summary>
public static class RazorSlicesServiceCollectionExtensions
{
    /// <summary>Registers services required by streaming Razor Slice Tag Helpers.</summary>
    /// <remarks>
    /// Register this when a slice uses dynamic unbound <c>data-*</c> attributes with Tag Helpers.
    /// </remarks>
    public static IServiceCollection AddRazorSlicesTagHelpers(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<RazorSliceTagHelperHtml>();
        return services;
    }
}
