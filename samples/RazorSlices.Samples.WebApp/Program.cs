using Microsoft.AspNetCore.Http.HttpResults;
using RazorSlices;
using RazorSlices.Samples.WebApp;
using RazorSlices.Samples.WebApp.Services;
using System.Diagnostics.CodeAnalysis;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddWebEncoders();
builder.Services.AddSingleton<LoremService>();

var app = builder.Build();

app.UseStatusCodePages();
app.UseStaticFiles();

app.MapGet("/lorem", () => Results.Redirect("/lorem-static"));
app.MapGet("/lorem-static", () => Results.Extensions.RazorSlice("/Slices/LoremStatic.cshtml"));
app.MapGet("/lorem-dynamic", (int? paraCount, int? paraLength) =>
    Results.Extensions.RazorSlice("/Slices/LoremDynamic.cshtml", new LoremParams(paraCount, paraLength)));
app.MapGet("/lorem-formattable", (int? paraCount, int? paraLength) =>
    Results.Extensions.RazorSlice("/Slices/LoremFormattable.cshtml", new LoremParams(paraCount, paraLength)));
app.MapGet("/lorem-htmlcontent", (bool? encode) =>
    Results.Extensions.RazorSlice("/Slices/LoremHtmlContent.cshtml", new HtmlContentParams(encode)));
app.MapGet("/lorem-stream", (HttpContext httpContext) =>
{
    var slice = RazorSlice.Create("/Slices/LoremStatic.cshtml");
    httpContext.Response.StatusCode = StatusCodes.Status200OK;
    httpContext.Response.ContentType = "text/html; charset=utf-8";
    return slice.RenderAsync(httpContext.Response.Body);
});
app.MapGet("/lorem-injectableproperties", (int? paraCount, int? paraLength, IServiceProvider serviceProvider) =>
    Results.Extensions.RazorSlice("/Slices/LoremInjectableProperties.cshtml", new LoremParams(paraCount, paraLength), serviceProvider));
app.MapGet("/unicode", () => Results.Extensions.RazorSlice("/Slices/Unicode.cshtml"));
app.MapGet("/library", () => Results.Extensions.RazorSlice("/Slices/FromLibrary.cshtml"));
app.MapGet("/render-to-string", async () =>
{
    var slice = RazorSlice.Create("/Slices/LoremFormattable.cshtml", new LoremParams(1, 4));
    var template = await slice.RenderAsync();
    return Results.Ok(new { HtmlString = template });
});
app.MapGet("/render-to-stringbuilder", async (IServiceProvider serviceProvider) =>
{
    var stringBuilder = new StringBuilder();
    var slice = RazorSlice.Create("/Slices/LoremInjectableProperties.cshtml", new LoremParams(1, 4), serviceProvider);
    await slice.RenderAsync(stringBuilder);
    return Results.Ok(new { HtmlString = stringBuilder.ToString() });
});

var todosSliceFactory = RazorSlicesContext.GetTodosSliceFactory("");
//app.MapGet("/", () => Results.Extensions.RazorSlice("/slices/todos", Todos.AllTodos));
app.MapGet("/", () => (RazorSliceHttpResult<Todo[]>)todosSliceFactory(Todos.AllTodos));
app.MapGet("/{id:int}", (int id) =>
{
    var todo = Todos.AllTodos.FirstOrDefault(t => t.Id == id);
    return todo is not null
        ? Results.Extensions.RazorSlice("/Slices/Todo.cshtml", todo)
        : Results.NotFound();
});

app.Run();

struct LoremParams
{
    public int ParagraphCount;
    public int ParagraphSentenceCount;

    public LoremParams(int? paragraphCount, int? paragraphSentenceCount)
    {
        ParagraphCount = paragraphCount ?? 3;
        ParagraphSentenceCount = paragraphSentenceCount ?? 5;
    }
}

struct HtmlContentParams
{
    public bool Encode;

    public HtmlContentParams(bool? encode)
    {
        Encode = encode ?? false;
    }
}

public class RazorSlicesContext
{
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, "AspNetCoreGeneratedDocument.Slices__Footer", "RazorSlices.Samples.WebApp")]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, "AspNetCoreGeneratedDocument.Slices__ViewImports", "RazorSlices.Samples.WebApp")]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, "AspNetCoreGeneratedDocument.Slices_LoremDynamic", "RazorSlices.Samples.WebApp")]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, "AspNetCoreGeneratedDocument.Slices_LoremFormattable", "RazorSlices.Samples.WebApp")]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, "AspNetCoreGeneratedDocument.Slices_LoremHtmlContent", "RazorSlices.Samples.WebApp")]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, "AspNetCoreGeneratedDocument.Slices_LoremInjectableProperties", "RazorSlices.Samples.WebApp")]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, "AspNetCoreGeneratedDocument.Slices_LoremStatic", "RazorSlices.Samples.WebApp")]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, "AspNetCoreGeneratedDocument.Slices_Todo", "RazorSlices.Samples.WebApp")]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, "AspNetCoreGeneratedDocument.Slices_TodoRow", "RazorSlices.Samples.WebApp")]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, "AspNetCoreGeneratedDocument.Slices_Todos", "RazorSlices.Samples.WebApp")]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, "AspNetCoreGeneratedDocument.Slices_Unicode", "RazorSlices.Samples.WebApp")]
    public static SliceFactory<Todo[]> GetTodosSliceFactory(string path)
    {
        var sliceType = Type.GetType("AspNetCoreGeneratedDocument.Slices_Todos");
        var sliceFactory = RazorSlice.ResolveSliceFactory<Todo[]>(sliceType);
        return sliceFactory;
    }
}
