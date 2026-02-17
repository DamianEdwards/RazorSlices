using RazorSlices.Samples.PagesAndSlices.Slices;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

var app = builder.Build();

// Return a Razor slice instance directly from the endpoint handler
app.MapGet("/", () => Hello.Create());

#if NET10_0_OR_GREATER
app.MapGet("/goodbye", () => Results.RazorSlice<Goodbye>());
#else
app.MapGet("/goodbye", () => Results.Extensions.RazorSlice<Goodbye>());
#endif

app.MapRazorPages();

app.Run();
