using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Mvc.ViewFeatures.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace RazorSlices;

/// <summary>Registers and enables route-handler TempData without changing slice rendering.</summary>
public static class TempDataExtensions
{
    /// <summary>
    /// Registers cookie TempData without MVC, using a reflection-free MVC-compatible JSON serializer.
    /// Existing provider, factory, and serializer registrations are preserved.
    /// </summary>
    public static IServiceCollection AddRazorSliceTempData(
        this IServiceCollection services, Action<CookieTempDataProviderOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddDataProtection();
        services.AddLogging();
        services.AddOptions();
        if (configure is not null) { services.Configure(configure); }
        services.TryAddSingleton<TempDataSerializer, RazorSliceTempDataSerializer>();
        services.TryAddSingleton<ITempDataProvider, CookieTempDataProvider>();
        services.TryAddSingleton<ITempDataDictionaryFactory, TempDataDictionaryFactory>();
        return services;
    }

    /// <summary>
    /// Enables TempData parameters and saves accessed TempData after the handler returns, before result execution.
    /// Can be applied to an endpoint or route group. Handlers must not start the response when using TempData.
    /// </summary>
    public static TBuilder WithTempData<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Add(endpoint =>
        {
            if (endpoint.Metadata.OfType<TempDataMetadata>().Any()) { return; }
            endpoint.Metadata.Add(TempDataMetadata.Instance);
            endpoint.FilterFactories.Add(static (_, next) => context => InvokeAsync(context, next));
        });
        return builder;
    }

    private static async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var data = TempData.GetOrCreate(context.HttpContext);
        data.Activate();
        object? result;
        try
        {
            result = await next(context);
        }
        finally
        {
            data.Close();
        }
        data.Save();
        return result;
    }

    internal sealed class TempDataMetadata
    {
        public static readonly TempDataMetadata Instance = new();
    }
}
