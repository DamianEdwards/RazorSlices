using RazorSlices;
using RazorSlices.Samples.WebApp;

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.UseStatusCodePages();
app.UseStaticFiles();

app.MapGet("/", () => RazorSlice.CreateHttpResult("/Slices/Todos.cshtml", Todos.AllTodos));

app.Run();