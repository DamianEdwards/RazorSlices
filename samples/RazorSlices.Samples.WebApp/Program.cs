using System.Runtime.CompilerServices;
using System.Text;
using RazorSlices;
using RazorSlices.Samples.WebApp;
using RazorSlices.Samples.WebApp.Services;
using Slices = RazorSlices.Samples.WebApp.Slices;
using LibrarySlices = RazorSlices.Samples.RazorClassLibrary.Slices;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddWebEncoders();
builder.Services.AddSingleton<LoremService>();

var app = builder.Build();

app.UseStatusCodePages();
app.UseStaticFiles();

app.MapGet("/lorem", () => Results.Redirect("/lorem-static"));
app.MapGet("/lorem-static", () => Results.Extensions.RazorSlice<Slices.LoremStatic>());
app.MapGet("/lorem-dynamic", (int? paraCount, int? paraLength) =>
    Results.Extensions.RazorSlice<Slices.LoremDynamic, LoremParams>(new LoremParams(paraCount, paraLength)));
app.MapGet("/lorem-formattable", (int? paraCount, int? paraLength) =>
    Results.Extensions.RazorSlice<Slices.LoremFormattable, LoremParams>(new LoremParams(paraCount, paraLength)));
app.MapGet("/lorem-htmlcontent", (bool? encode) =>
    Results.Extensions.RazorSlice<Slices.LoremHtmlContent, HtmlContentParams>(new HtmlContentParams(encode)));
app.MapGet("/lorem-injectableproperties", (int? paraCount, int? paraLength) =>
    Results.Extensions.RazorSlice<Slices.LoremInjectableProperties, LoremParams>(new LoremParams(paraCount, paraLength)));
app.MapGet("/lorem-stream", async (HttpContext httpContext) =>
{
    var slice = Slices.LoremStatic.Create();
    httpContext.Response.StatusCode = StatusCodes.Status200OK;
    httpContext.Response.ContentType = "text/html; charset=utf-8";
    await slice.RenderAsync(httpContext.Response.Body);
});
app.MapGet("/unicode", () => Results.Extensions.RazorSlice<Slices.Unicode>());
app.MapGet("/library", () => Results.Extensions.RazorSlice<LibrarySlices.FromLibrary>());
app.MapGet("/render-to-string", async () =>
{
    var slice = Slices.LoremFormattable.Create(new LoremParams(1, 4));
    var template = await slice.RenderAsync();
    return Results.Ok(new { HtmlString = template });
});
app.MapGet("/render-to-stringbuilder", async () =>
{
    var stringBuilder = new StringBuilder();
    var slice = Slices.LoremInjectableProperties.Create(new LoremParams(1, 4));
    await slice.RenderAsync(stringBuilder);
    return Results.Ok(new { HtmlString = stringBuilder.ToString() });
});

app.MapGet("/", () => Results.Extensions.RazorSlice<Slices.Todos, Todo[]>(Todos.AllTodos));
app.MapGet("/{id:int}", (int id) =>
{
    var todo = Todos.AllTodos.FirstOrDefault(t => t.Id == id);
    return todo is not null
        //? Results.Extensions.RazorSlice("/Slices/Todo.cshtml", todo)
        ? Results.Extensions.RazorSlice<Slices.Todo, Todo>(todo)
        : Results.NotFound();
});

Console.WriteLine($"RuntimeFeature.IsDynamicCodeSupported = {RuntimeFeature.IsDynamicCodeSupported}");
Console.WriteLine($"RuntimeFeature.IsDynamicCodeCompiled = {RuntimeFeature.IsDynamicCodeCompiled}");

app.Run();

public struct LoremParams
{
    public int ParagraphCount;
    public int ParagraphSentenceCount;

    public LoremParams(int? paragraphCount, int? paragraphSentenceCount)
    {
        ParagraphCount = paragraphCount ?? 3;
        ParagraphSentenceCount = paragraphSentenceCount ?? 5;
    }
}

public struct HtmlContentParams
{
    public bool Encode;

    public HtmlContentParams(bool? encode)
    {
        Encode = encode ?? false;
    }
}
