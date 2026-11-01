using RazorSlices.Samples.PagesAndSlices.Slices;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

var app = builder.Build();

app.UseStaticFiles();

// Return a Razor slice instance directly from the endpoint handler
app.MapGet("/", () => Hello.Create());

app.MapGet("/goodbye", () => Results.RazorSlice<Goodbye>());

app.MapRazorPages();

app.Run();
