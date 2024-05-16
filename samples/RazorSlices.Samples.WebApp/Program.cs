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
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonContext.Default);
});

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

app.MapGet("/lorem-stream", async (HttpResponse httpResponse) =>
{
    var slice = Slices.LoremStatic.Create();
    httpResponse.StatusCode = StatusCodes.Status200OK;
    httpResponse.ContentType = "text/html; charset=utf-8";
    await slice.RenderAsync(httpResponse.Body);
});
app.MapGet("/unicode", () => Results.Extensions.RazorSlice<Slices.Unicode>());
app.MapGet("/library", () => Results.Extensions.RazorSlice<LibrarySlices.FromLibrary>());
app.MapGet("/render-to-string", async () =>
{
    var slice = Slices.LoremFormattable.Create(new LoremParams(1, 4));
    var template = await slice.RenderAsync();
    return Results.Ok(new ResultDto(template));
});
app.MapGet("/render-to-stringbuilder", async (IServiceProvider serviceProvider) =>
{
    var stringBuilder = new StringBuilder();
    var slice = Slices.LoremInjectableProperties.Create(new LoremParams(1, 4));
    slice.ServiceProvider = serviceProvider;
    await slice.RenderAsync(stringBuilder);
    return Results.Ok(new ResultDto(stringBuilder.ToString()));
});

app.MapGet("/", () => Results.Extensions.RazorSlice<Slices.Todos, Todo[]>(Todos.AllTodos));
app.MapGet("/{id:int}", (int id) =>
{
    var todo = Todos.AllTodos.FirstOrDefault(t => t.Id == id);
    return todo is not null
        ? Results.Extensions.RazorSlice<Slices.Todo, Todo>(todo)
        : Results.NotFound();
});

Console.WriteLine($"RuntimeFeature.IsDynamicCodeSupported = {RuntimeFeature.IsDynamicCodeSupported}");
Console.WriteLine($"RuntimeFeature.IsDynamicCodeCompiled = {RuntimeFeature.IsDynamicCodeCompiled}");

app.Run();
