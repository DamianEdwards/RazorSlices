using RazorSlices.Samples.PagesAndSlices.Slices;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

var app = builder.Build();

app.MapGet("/", () => Results.Extensions.RazorSlice<Hello>());
app.MapGet("/goodbye", () => Results.Extensions.RazorSlice<Goodbye>());

app.MapRazorPages();

app.Run();
