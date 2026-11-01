using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;

namespace RazorSlices.Samples.WebApp.Tests;

public class ResponseBufferingTests
{
    [Theory]
    [InlineData("/buffered", "buffered")]
    [InlineData("/streaming", "buffered")]
    [InlineData("/opted-out", "unbuffered")]
    [InlineData("/opted-out/", "unbuffered")]
    [InlineData("/group/first", "unbuffered")]
    [InlineData("/group/second", "unbuffered")]
    [InlineData("/attribute", "unbuffered")]
    [InlineData("/custom", "unbuffered")]
    public async Task Middleware_UsesEndpointMetadataToDisableBuffering(string path, string expected)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        await using var app = builder.Build();
        app.UseRouting();
        app.Use(async (context, next) =>
        {
            context.Items[typeof(Stream)] = context.Response.Body;
            await next(context);
        });
        app.UseResponseBuffering();
        app.MapGet("/buffered", BufferingStatus);
        app.MapGet("/streaming", BufferingStatus);

        var endpoint = app.MapGet("/opted-out", BufferingStatus);
        Assert.Same(endpoint, endpoint.DisableResponseBuffering());

        var group = app.MapGroup("/group");
        Assert.Same(group, group.DisableResponseBuffering());
        group.MapGet("/first", BufferingStatus);
        group.MapGet("/second", BufferingStatus);

        app.MapGet("/attribute", UnbufferedStatus);
        app.MapGet("/custom", BufferingStatus)
            .WithMetadata(new CustomDisableResponseBufferingMetadata());

        await app.StartAsync();
        using var client = app.GetTestClient();

        Assert.Equal(expected, await client.GetStringAsync(path));
    }

    private static string BufferingStatus(HttpContext context) =>
        ReferenceEquals(context.Items[typeof(Stream)], context.Response.Body) ? "unbuffered" : "buffered";

    [DisableResponseBuffering]
    private static string UnbufferedStatus(HttpContext context) => BufferingStatus(context);

    private sealed class CustomDisableResponseBufferingMetadata : IDisableResponseBufferingMetadata
    {
    }
}
