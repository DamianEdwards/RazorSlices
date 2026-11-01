using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Antiforgery;
using RazorSlices;
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
builder.Services.AddRazorSlicesTagHelpers();
builder.Services.AddSingleton<LoremService>();
builder.Services.AddRazorSliceTempData();
#if !NET11_0_OR_GREATER
builder.Services.AddAntiforgery();
#endif
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonContext.Default);
});

var app = builder.Build();

app.UseStatusCodePages();
app.UseStaticFiles();
app.UseRouting();
#if !NET11_0_OR_GREATER
app.UseAntiforgery();
#endif

// RC3-generated form binding reads the form before checking failed validation.
app.Use(async (context, next) =>
{
    if (context.GetEndpoint()?.Metadata.GetMetadata<IAntiforgeryMetadata>() is { RequiresValidation: true }
        && context.Features.Get<IAntiforgeryValidationFeature>() is { IsValid: false })
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }
    await next(context);
});

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    if (builder.Configuration["ENABLE_RESPONSE_BUFFERING"] == "true")
    {
        // Enable response buffering middleware to allow for response interception during local development
        app.UseResponseBuffering();
    }
}

app.MapSlices();

Console.WriteLine($"RuntimeFeature.IsDynamicCodeSupported = {RuntimeFeature.IsDynamicCodeSupported}");
Console.WriteLine($"RuntimeFeature.IsDynamicCodeCompiled = {RuntimeFeature.IsDynamicCodeCompiled}");

app.Run();
