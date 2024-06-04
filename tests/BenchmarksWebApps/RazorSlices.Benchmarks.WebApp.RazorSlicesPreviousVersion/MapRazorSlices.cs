using Microsoft.AspNetCore.Http.HttpResults;
using RazorSlices;

namespace Microsoft.AspNetCore.Builder;

public static class SliceBuilderExtensions
{
    public static IEndpointRouteBuilder MapRazorSlicesEndpoints(this IEndpointRouteBuilder routes)
    {
        var helloSlice = RazorSlice.ResolveSliceFactory("/Slices/Hello.cshtml");
        routes.MapGet("/hello", () => (RazorSliceHttpResult)helloSlice());

        return routes;
    }
}
