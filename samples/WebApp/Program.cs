using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Antiforgery;
using RazorSlices.Samples.WebApp;
using RazorSlices.Samples.WebApp.Services;

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
builder.Services.AddSingleton<HtmxTodoStore>();
builder.Services.AddAntiforgery(options => options.HeaderName = "X-CSRF-TOKEN");
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonContext.Default);
});

var app = builder.Build();

app.UseStatusCodePages();
app.UseStaticFiles();
app.UseAntiforgery();
app.Use(async (context, next) =>
{
    // Reject invalid tokens before form binding, including in AOT-generated handlers.
    if (context.Features.Get<IAntiforgeryValidationFeature>() is { IsValid: false })
    {
        await Results.Text("Invalid antiforgery token. Reload the page and try again.",
            statusCode: StatusCodes.Status400BadRequest).ExecuteAsync(context);
        return;
    }

    await next(context);
});

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    if (Environment.GetEnvironmentVariable("ENABLE_RESPONSE_BUFFERING") == "true")
    {
        // Enable response buffering middleware to allow for response interception during local development
        app.UseResponseBuffering();
    }
}

app.MapSlices();
app.MapHtmxTodoRoutes();

Console.WriteLine($"RuntimeFeature.IsDynamicCodeSupported = {RuntimeFeature.IsDynamicCodeSupported}");
Console.WriteLine($"RuntimeFeature.IsDynamicCodeCompiled = {RuntimeFeature.IsDynamicCodeCompiled}");

app.Run();
