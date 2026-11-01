using Microsoft.AspNetCore.Http.HttpResults;
using RazorSlices;

namespace Microsoft.AspNetCore.Builder;

public static class SliceBuilderExtensions
{
    public static IEndpointRouteBuilder MapRazorSlicesEndpoints(this IEndpointRouteBuilder routes)
    {
        var helloSlice = RazorSlice.ResolveSliceFactory("/Slices/Hello.cshtml");
        routes.MapGet("/hello", () => (RazorSliceHttpResult)helloSlice());

        var lorem25Slice = RazorSlice.ResolveSliceFactory("/Slices/Lorem25.cshtml");
        routes.MapGet("/lorem25", () => (RazorSliceHttpResult)lorem25Slice());

        var lorem50Slice = RazorSlice.ResolveSliceFactory("/Slices/Lorem50.cshtml");
        routes.MapGet("/lorem50", () => (RazorSliceHttpResult)lorem50Slice());

        var lorem100Slice = RazorSlice.ResolveSliceFactory("/Slices/Lorem100.cshtml");
        routes.MapGet("/lorem100", () => (RazorSliceHttpResult)lorem100Slice());

        var lorem200Slice = RazorSlice.ResolveSliceFactory("/Slices/Lorem200.cshtml");
        routes.MapGet("/lorem200", () => (RazorSliceHttpResult)lorem200Slice());

        return routes;
    }
}
