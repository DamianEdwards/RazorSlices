using RazorSlices;
using RazorSlices.Samples.WebApp;
using RazorSlices.Samples.WebApp.Services;

var builder = WebApplication.CreateBuilder(args);

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

app.MapGet("/", () => Results.Extensions.RazorSlice("/Slices/Todos.cshtml", Todos.AllTodos));
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

    public LoremParams(int? paragraphCount, int? paragraphSenceCount)
    {
        ParagraphCount = paragraphCount ?? 3;
        ParagraphSentenceCount = paragraphSenceCount ?? 5;
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