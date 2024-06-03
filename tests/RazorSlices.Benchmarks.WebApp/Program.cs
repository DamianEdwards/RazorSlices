using RazorSlices.Benchmarks.WebApp.ComponentPages;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents();
builder.Services.AddRazorPages();

var app = builder.Build();

app.MapWhen(c => c.Request.Path.StartsWithSegments("/componentpages"), appBuilder =>
{
    appBuilder.UseRouting();
    appBuilder.UseAntiforgery();
    appBuilder.UseEndpoints(endpoints =>
    {
        endpoints.MapRazorComponents<App>();
    });
});

app.MapRazorPages();
app.MapRazorComponentsEndpoints();
app.MapRazorSlicesEndpoints();

app.Run();
