using Microsoft.AspNetCore.Http.HttpResults;
using RazorSlices.Benchmarks.WebApp.RazorComponents.Components;

namespace Microsoft.AspNetCore.Builder;

public static class ComponentBuilderExtensions
{
    public static IEndpointRouteBuilder MapRazorComponentsEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/hello", () => new RazorComponentResult<Hello>());

        routes.MapGet("/lorem25", () => new RazorComponentResult<Lorem25>());
        routes.MapGet("/lorem50", () => new RazorComponentResult<Lorem50>());
        routes.MapGet("/lorem100", () => new RazorComponentResult<Lorem100>());
        routes.MapGet("/lorem200", () => new RazorComponentResult<Lorem200>());

        return routes;
    }
}
