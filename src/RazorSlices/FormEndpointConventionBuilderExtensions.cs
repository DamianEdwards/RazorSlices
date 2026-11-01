using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace RazorSlices;

/// <summary>Antiforgery preparation and validation for result-returning form endpoints.</summary>
public static class FormEndpointConventionBuilderExtensions
{
    /// <summary>
    /// Prepares tokens before rendering and validates unsafe requests before invoking the handler.
    /// Apply before WithTempData so invalid requests do not consume or save TempData.
    /// </summary>
    /// <remarks>
    /// Endpoint filters run after parameter binding. Endpoints with FromForm or file-upload parameters
    /// additionally require ASP.NET Core antiforgery middleware on .NET 10. On .NET 11, WebApplication
    /// provides default browser-header CSRF protection. This filter still validates tokens and does not
    /// replace framework validation during form binding.
    /// </remarks>
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
