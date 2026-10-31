using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Mvc.ViewFeatures.Infrastructure;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace RazorSlices.Samples.WebApp;

/// <summary>Sample-local spike for preparing TempData before returning an HTTP result.</summary>
public static class TempDataExtensions
{
    /// <summary>
    /// Registers cookie TempData without MVC. The spike's default serializer supports strings and null only.
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
        services.TryAddSingleton<TempDataSerializer>(new StringTempDataSerializer());
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

    private sealed class StringTempDataSerializer : TempDataSerializer
    {
        public override bool CanSerializeType(Type type) => type == typeof(string);

        public override byte[] Serialize(IDictionary<string, object?> values)
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                foreach (var (key, value) in values)
                {
                    if (value is not null and not string)
                    {
                        throw new InvalidOperationException(
                            $"The TempData spike supports strings and null only, not '{value.GetType()}'.");
                    }
                    writer.WriteString(key, (string?)value);
                }
                writer.WriteEndObject();
            }
            return stream.ToArray();
        }

        public override IDictionary<string, object?> Deserialize(byte[] unprotectedData)
        {
            using var document = JsonDocument.Parse(unprotectedData);
            var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in document.RootElement.EnumerateObject())
            {
                values.Add(entry.Name, entry.Value.GetString());
            }
            return values;
        }
    }
}
