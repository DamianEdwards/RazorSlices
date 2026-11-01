using Slices = RazorSlices.Benchmarks.WebApp.RazorSlicesLocal.Slices;

namespace Microsoft.AspNetCore.Builder;

public static class SliceBuilderExtensions
{
    public static IEndpointRouteBuilder MapRazorSlicesEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/hello", () => Results.RazorSlice<Slices.Hello>());
        routes.MapGet("/lorem25", () => Results.RazorSlice<Slices.Lorem25>());
        routes.MapGet("/lorem50", () => Results.RazorSlice<Slices.Lorem50>());
        routes.MapGet("/lorem100", () => Results.RazorSlice<Slices.Lorem100>());
        routes.MapGet("/lorem200", () => Results.RazorSlice<Slices.Lorem200>());

        return routes;
    }
}
