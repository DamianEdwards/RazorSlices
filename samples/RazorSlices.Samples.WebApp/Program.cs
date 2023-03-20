using RazorSlices.Samples.WebApp;

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.UseStatusCodePages();
app.UseStaticFiles();

app.MapGet("/lorem", () => Results.Redirect("/lorem-static"));
app.MapGet("/lorem-static", () => Results.Extensions.RazorSlice("/Slices/LoremStatic.cshtml"));
app.MapGet("/lorem-dynamic", (int? paraCount, int? paraLength) =>
    Results.Extensions.RazorSlice("/Slices/LoremDynamic.cshtml", new LoremParams(paraCount, paraLength)));
app.MapGet("/lorem-formattable", (int? paraCount, int? paraLength) =>
    Results.Extensions.RazorSlice("/Slices/LoremFormattable.cshtml", new LoremParams(paraCount, paraLength)));

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