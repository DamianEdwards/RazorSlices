using RazorSlices;
using RazorSlices.Samples.WebApp;

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.UseStatusCodePages();
app.UseStaticFiles();

app.MapGet("/", () => RazorSlice.CreateHttpResult("/Slices/Todos.cshtml", Todos.AllTodos));
app.MapGet("/{id:int}", (int id) =>
{
    var todo = Todos.AllTodos.FirstOrDefault(t => t.Id == id);
    return todo is not null
        ? RazorSlice.CreateHttpResult("/Slices/Todo.cshtml", todo)
        : Results.NotFound();
});

app.Run();