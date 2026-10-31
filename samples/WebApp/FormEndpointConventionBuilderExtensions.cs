using Microsoft.AspNetCore.Antiforgery;

namespace RazorSlices.Samples.WebApp;

/// <summary>Sample-local antiforgery preparation and validation for result-returning form endpoints.</summary>
public static class FormEndpointConventionBuilderExtensions
{
    /// <summary>
    /// Prepares tokens before rendering and validates unsafe requests before invoking the handler.
    /// Apply before WithTempData so invalid requests do not consume or save TempData.
    /// </summary>
    public static TBuilder WithAntiforgery<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddEndpointFilter(async (context, next) =>
        {
            var httpContext = context.HttpContext;
            if (httpContext.Response.HasStarted)
            {
                throw new InvalidOperationException("Antiforgery preparation must run before the response starts.");
            }
            var antiforgery = httpContext.RequestServices.GetRequiredService<IAntiforgery>();
            var method = httpContext.Request.Method;
            var safeMethod = HttpMethods.IsGet(method) || HttpMethods.IsHead(method)
                || HttpMethods.IsOptions(method) || HttpMethods.IsTrace(method);
            if (!safeMethod && !await antiforgery.IsRequestValidAsync(httpContext))
            {
                return TypedResults.BadRequest("Invalid antiforgery token.");
            }
            httpContext.Features.Set(antiforgery.GetAndStoreTokens(httpContext));
            return await next(context);
        });
    }
}
