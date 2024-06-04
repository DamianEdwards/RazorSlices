using Slices = RazorSlices.Benchmarks.WebApp.RazorSlicesLocal.Slices;

namespace Microsoft.AspNetCore.Builder;

public static class SliceBuilderExtensions
{
    public static IEndpointRouteBuilder MapRazorSlicesEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/hello", () => Results.Extensions.RazorSlice<Slices.Hello>());
        routes.MapGet("/pride", () => Results.Extensions.RazorSlice<Slices.PrideAndPrejudice>());

        return routes;
    }
}
