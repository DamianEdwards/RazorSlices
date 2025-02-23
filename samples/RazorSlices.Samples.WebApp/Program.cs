using System.Runtime.CompilerServices;
using System.Text;
using RazorSlices;
using RazorSlices.Samples.WebApp;
using RazorSlices.Samples.WebApp.Services;
using Models = RazorSlices.Samples.WebApp.Models;
using Slices = RazorSlices.Samples.WebApp.Slices;
using LibrarySlices = RazorSlices.Samples.RazorClassLibrary.Slices;
using Microsoft.AspNetCore.Http.HttpResults;

#if DEBUG
// Use the default builder during inner-loop so Hot Reload works
var builder = WebApplication.CreateBuilder(args);
#else
// Use the slim builder for Release builds
var builder = WebApplication.CreateSlimBuilder(args);
builder.WebHost.UseKestrelHttpsConfiguration();
#endif

builder.Services.AddWebEncoders();
builder.Services.AddSingleton<LoremService>();
builder.Services.ConfigureHttpJsonOptions(options =>
{
  options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonContext.Default);
});
builder.Services.AddAntiforgery();

var app = builder.Build();

app.UseStatusCodePages();
app.UseStaticFiles();
app.UseAntiforgery();

if (app.Environment.IsDevelopment())
{
  app.UseDeveloperExceptionPage();
  if (Environment.GetEnvironmentVariable("ENABLE_RESPONSE_BUFFERING") == "true")
  {
    // Enable response buffering middleware to allow for response interception during local development
    app.UseResponseBuffering();
  }
}

app.MapGet("/lorem", () => Results.Redirect("/lorem-static"));
app.MapGet("/lorem-static", () => Results.Extensions.RazorSlice<Slices.Lorem.LoremStatic>());
app.MapGet("/lorem-dynamic", (int? paraCount, int? paraLength) =>
    Results.Extensions.RazorSlice<Slices.Lorem.LoremDynamic, LoremParams>(new LoremParams(paraCount, paraLength)));
app.MapGet("/lorem-formattable", (int? paraCount, int? paraLength) =>
    Results.Extensions.RazorSlice<Slices.Lorem.LoremFormattable, LoremParams>(new LoremParams(paraCount, paraLength)));
app.MapGet("/lorem-htmlcontent", (bool? encode) =>
    Results.Extensions.RazorSlice<Slices.Lorem.LoremHtmlContent, HtmlContentParams>(new HtmlContentParams(encode)));
app.MapGet("/lorem-injectableproperties", (int? paraCount, int? paraLength) =>
    Results.Extensions.RazorSlice<Slices.Lorem.LoremInjectableProperties, LoremParams>(new LoremParams(paraCount, paraLength)));

app.MapGet("/lorem-stream", async (HttpResponse httpResponse) =>
{
  var slice = Slices.Lorem.LoremStatic.Create();
  httpResponse.StatusCode = StatusCodes.Status200OK;
  httpResponse.ContentType = "text/html; charset=utf-8";
  await slice.RenderAsync(httpResponse.Body);
});
app.MapGet("/encoding", () => Results.Extensions.RazorSlice<Slices.Encoding>());
app.MapGet("/unicode", () => Results.Extensions.RazorSlice<Slices.Unicode>());
app.MapGet("/templated", (bool async = false) => Results.Extensions.RazorSlice<Slices.Templated, bool>(async));
app.MapGet("/library", () => Results.Extensions.RazorSlice<LibrarySlices.FromLibrary>());
app.MapGet("/render-to-string", async () =>
{
  var slice = Slices.Lorem.LoremFormattable.Create(new LoremParams(1, 4));
  var template = await slice.RenderAsync();
  return Results.Ok(new ResultDto(template));
});
app.MapGet("/render-to-stringbuilder", async (IServiceProvider serviceProvider) =>
{
  var stringBuilder = new StringBuilder();
  var slice = Slices.Lorem.LoremInjectableProperties.Create(new LoremParams(1, 4));
  slice.ServiceProvider = serviceProvider;
  await slice.RenderAsync(stringBuilder);
  return Results.Ok(new ResultDto(stringBuilder.ToString()));
});

app.MapGet("/", () =>
{
  var todos = Models.Todos.AllTodos.Values.ToList();
  return Results.Extensions.RazorSlice<Slices.Todos, List<Models.Todo>>(todos);
});
app.MapGet("/{id:int}", Results<RazorSliceHttpResult<Models.Todo>, NotFound> (int id) =>
{
  return Models.Todos.AllTodos.TryGetValue(id, out var todo)
        ? TypedResults.Extensions.RazorSlice<Slices.Todo, Models.Todo>(todo)
        : TypedResults.NotFound();
});


app.MapHtmxTodoRoutes();

Console.WriteLine($"RuntimeFeature.IsDynamicCodeSupported = {RuntimeFeature.IsDynamicCodeSupported}");
Console.WriteLine($"RuntimeFeature.IsDynamicCodeCompiled = {RuntimeFeature.IsDynamicCodeCompiled}");

app.Run();
