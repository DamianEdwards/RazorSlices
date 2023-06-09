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
//app.MapGet("/lorem-static", () => Results.Extensions.RazorSlice("/Slices/LoremStatic.cshtml"));
app.MapGet("/lorem-static", () => (IResult)Slices.LoremStatic.Create());
app.MapGet("/lorem-dynamic", (int? paraCount, int? paraLength) =>
    //Results.Extensions.RazorSlice("/Slices/LoremDynamic.cshtml", new LoremParams(paraCount, paraLength)));
    Slices.LoremDynamic.Create(new LoremParams(paraCount, paraLength)));
app.MapGet("/lorem-formattable", (int? paraCount, int? paraLength) =>
    //Results.Extensions.RazorSlice("/Slices/LoremFormattable.cshtml", new LoremParams(paraCount, paraLength)));
    Slices.LoremFormattable.Create(new LoremParams(paraCount, paraLength)));
app.MapGet("/lorem-htmlcontent", (bool? encode) =>
    //Results.Extensions.RazorSlice("/Slices/LoremHtmlContent.cshtml", new HtmlContentParams(encode)));
    Slices.LoremHtmlContent.Create(new HtmlContentParams(encode)));
app.MapGet("/lorem-stream", (HttpContext httpContext) =>
{
    //var slice = RazorSlice.Create("/Slices/LoremStatic.cshtml");
    var slice = Slices.LoremStatic.Create();
    httpContext.Response.StatusCode = StatusCodes.Status200OK;
    httpContext.Response.ContentType = "text/html; charset=utf-8";
    return slice.RenderAsync(httpContext.Response.Body);
});
app.MapGet("/lorem-injectableproperties", (int? paraCount, int? paraLength) =>
    //Results.Extensions.RazorSlice("/Slices/LoremInjectableProperties.cshtml", new LoremParams(paraCount, paraLength), serviceProvider));
    Slices.LoremInjectableProperties.Create(new LoremParams(paraCount, paraLength)));
app.MapGet("/unicode", () =>
    //Results.Extensions.RazorSlice("/Slices/Unicode.cshtml"));
    Slices.Unicode.Create());
app.MapGet("/library", () =>
    //Results.Extensions.RazorSlice("/Slices/FromLibrary.cshtml"));
    //LibrarySlices.FromLibrary.Create());
    Results.Extensions.Slice<LibrarySlices.FromLibrary>());
app.MapGet("/render-to-string", async () =>
{
    //var slice = RazorSlice.Create("/Slices/LoremFormattable.cshtml", new LoremParams(1, 4));
    var slice = Slices.LoremFormattable.Create(new LoremParams(1, 4));
    var template = await slice.RenderAsync();
    return Results.Ok(new { HtmlString = template });
});
app.MapGet("/render-to-stringbuilder", async () =>
{
    var stringBuilder = new StringBuilder();
    //var slice = RazorSlice.Create("/Slices/LoremInjectableProperties.cshtml", new LoremParams(1, 4), serviceProvider);
    var slice = Slices.LoremInjectableProperties.Create(new LoremParams(1, 4));
    await slice.RenderAsync(stringBuilder);
    return Results.Ok(new { HtmlString = stringBuilder.ToString() });
});

//var todosSliceFactory = RazorSlicesContext.GetTodosSliceFactory("");
//app.MapGet("/", () => Results.Extensions.RazorSlice("/slices/todos", Todos.AllTodos));
//app.MapGet("/", () => (RazorSliceHttpResult<Todo[]>)todosSliceFactory(Todos.AllTodos));
//app.MapGet("/", () => Slices.Todos.Create(Todos.AllTodos));
app.MapGet("/", () => Results.Extensions.Slice<Slices.Todos, Todo[]>(Todos.AllTodos));
app.MapGet("/{id:int}", (int id) =>
{
    var todo = Todos.AllTodos.FirstOrDefault(t => t.Id == id);
    return todo is not null
        //? Results.Extensions.RazorSlice("/Slices/Todo.cshtml", todo)
        ? Slices.Todo.Create(todo)
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


//public static class InterceptingStuff
//{
//    private static readonly Type slice_0 = Type.GetType("Slices__Footer");
//    private static readonly PropertyInfo[] slice_0_props = slice_0.GetProperties().Where(pi => pi.GetCustomAttribute<RazorInjectAttribute>() is not null).ToArray();
//    private static readonly Action<RazorSlice, IServiceProvider> slice_0_init = (slice, sp) =>
//    {
//        foreach (var prop in slice_0_props)
//        {
//            prop.SetValue(slice, sp.GetService(prop.PropertyType));
//        }
//    };

//    // CoreCLR version
//    private static readonly Delegate slice_1 = 

//    [Intercepts()]
//    public static IResult RazorSlice_slice0<TModel>(this IResultExtensions extensions, string _, TModel model)
//    {
//        var slice = Activator.CreateInstance(slice_0) as RazorSliceHttpResult<TModel>;
//        slice.Model = model;
//        slice.Initialize = slice_0_init;

//        return slice;
//    }
//}
