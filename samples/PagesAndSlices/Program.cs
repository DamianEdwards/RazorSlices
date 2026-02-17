using RazorSlices.Samples.PagesAndSlices.Slices;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

var app = builder.Build();

#if NET10_0_OR_GREATER
app.MapGet("/", () => Results.RazorSlice<Hello>());
app.MapGet("/goodbye", () => Results.RazorSlice<Goodbye>());
#else
app.MapGet("/", () => Results.Extensions.RazorSlice<Hello>());
app.MapGet("/goodbye", () => Results.Extensions.RazorSlice<Goodbye>());
#endif

app.MapRazorPages();

app.Run();
