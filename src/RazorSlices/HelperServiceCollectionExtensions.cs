using Microsoft.Extensions.DependencyInjection.Extensions;
using RazorSlices;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Registers opt-in Razor Slice helper services.</summary>
public static class HelperServiceCollectionExtensions
{
    /// <summary>Registers one helper manager per service scope, normally the HTTP request.</summary>
    public static IServiceCollection AddRazorSliceHelpers(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddScoped<IHelperManager, HelperManager>();
        return services;
    }
}
