using LibrarySlices = RazorSlices.Samples.RazorClassLibrary.Slices;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace RazorSlices.Samples.WebApp;

internal static class MapSlicesExtensions
{
    public static IEndpointRouteBuilder MapSlices(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/lorem", () => Results.Redirect("/lorem-static"));
        endpoints.MapGet("/minimal", RazorSliceBase () => Minimal.Greeting.Create("<minimal>"));
        endpoints.MapGet("/minimal-partial", RazorSlice () => Slices.MinimalHost.Create());
        endpoints.MapGet("/tag-helpers", RazorSlice () => Slices.TagHelpers.Demo.Create())
            .DisableResponseBuffering();
        var tempDataEndpoints = endpoints.MapGroup("/tempdata");
#if !NET11_0_OR_GREATER
        tempDataEndpoints.WithAntiforgery();
#endif
        tempDataEndpoints.WithTempData();
        tempDataEndpoints.MapGet("", RazorSlice (TempData tempData, bool keep = false) =>
        {
            var message = tempData.Get<string>("message");
            if (keep) { tempData.Keep("message"); }
            return Slices.TempDataDemo.Create(message);
        });
        tempDataEndpoints.MapPost("", (TempData tempData, [FromForm] string message) =>
        {
            tempData.Set("message", message);
            return TypedResults.Redirect("/tempdata");
        });

        endpoints.MapGet("/lorem-static", () =>
            Results.RazorSlice<Slices.Lorem.LoremStatic>());
        endpoints.MapGet("/lorem-dynamic", (int? paraCount, int? paraLength) =>
            Results.RazorSlice<Slices.Lorem.LoremDynamic, LoremParams>(new LoremParams(paraCount, paraLength)));
        endpoints.MapGet("/lorem-formattable", (int? paraCount, int? paraLength) =>
            Results.RazorSlice<Slices.Lorem.LoremFormattable, LoremParams>(new LoremParams(paraCount, paraLength)));
        endpoints.MapGet("/lorem-htmlcontent", (bool? encode) =>
            Results.RazorSlice<Slices.Lorem.LoremHtmlContent, HtmlContentParams>(new HtmlContentParams(encode)));
        endpoints.MapGet("/lorem-injectableproperties", (int? paraCount, int? paraLength) =>
            Results.RazorSlice<Slices.Lorem.LoremInjectableProperties, LoremParams>(new LoremParams(paraCount, paraLength)));

        endpoints.MapGet("/lorem-stream", async (HttpResponse httpResponse) =>
        {
            var slice = Slices.Lorem.LoremStatic.Create();
            slice.HttpContext = httpResponse.HttpContext;
            httpResponse.StatusCode = StatusCodes.Status200OK;
            httpResponse.ContentType = "text/html; charset=utf-8";
            await slice.RenderAsync(httpResponse.Body);
        });
        endpoints.MapGet("/encoding", () => Results.RazorSlice<Slices.Encoding>());
        endpoints.MapGet("/attribute-rendering", () => Results.RazorSlice<Slices.AttributeRendering>());
        endpoints.MapGet("/streaming", () => Results.RazorSlice<Slices.Streaming>())
            .DisableResponseBuffering();
        endpoints.MapGet("/unicode", () => Results.RazorSlice<Slices.Unicode>());
        endpoints.MapGet("/templated", (bool async = false) => Results.RazorSlice<Slices.Templated, bool>(async));
        endpoints.MapGet("/helpers", () => Results.RazorSlice<Slices.HelpersDemo>());
        endpoints.MapGet("/library", () => Results.RazorSlice<LibrarySlices.FromLibrary>());
        endpoints.MapGet("/render-to-string", async (IServiceProvider serviceProvider) =>
        {
            var slice = Slices.Lorem.LoremFormattable.Create(new LoremParams(1, 4));
            slice.ServiceProvider = serviceProvider;
            var template = await slice.RenderAsync();
            return Results.Ok(new ResultDto(template));
        });
        endpoints.MapGet("/render-to-stringbuilder", async (IServiceProvider serviceProvider) =>
        {
            var stringBuilder = new StringBuilder();
            var slice = Slices.Lorem.LoremInjectableProperties.Create(new LoremParams(1, 4));
            slice.ServiceProvider = serviceProvider;
            await slice.RenderAsync(stringBuilder);
            return Results.Ok(new ResultDto(stringBuilder.ToString()));
        });

        endpoints.MapGet("/nested", () => Results.RazorSlice<Slices.Nested, Models.Todo.Nested>(new Models.Todo.Nested { Extra = "Test" }));
        endpoints.MapGet("/", () => Results.RazorSlice<Slices.Todos, Models.Todo[]>(Models.Todos.AllTodos));
        endpoints.MapGet("/{id:int}", Results<RazorSlice, NotFound> (int id) =>
        {
            var todo = Models.Todos.AllTodos.FirstOrDefault(t => t.Id == id);
            return todo is not null
                ? TypedResults.RazorSlice<Slices.Todo, Models.Todo>(todo)
                : TypedResults.NotFound();
        });

        return endpoints;
    }
}