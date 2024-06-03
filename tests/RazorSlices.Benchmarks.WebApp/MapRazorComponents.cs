using Microsoft.AspNetCore.Http.HttpResults;
using RazorSlices.Benchmarks.WebApp.Components;

namespace Microsoft.AspNetCore.Builder;

public static class ComponentBuilderExtensions
{
    public static IEndpointRouteBuilder MapRazorComponentsEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/components/hello", () => new RazorComponentResult<Hello>());

        return routes;
    }
}
