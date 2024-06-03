using Slices = RazorSlices.Benchmarks.WebApp.Slices;

namespace Microsoft.AspNetCore.Builder;

public static class SliceBuilderExtensions
{
    public static IEndpointRouteBuilder MapRazorSlicesEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/hello", () => Results.Extensions.RazorSlice<Slices.Hello>());

        return routes;
    }
}
