using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;
using RazorSlices.Benchmarks.WebApp;

BenchmarkRunner.Run<RazorSlicesBenchmarks>();

[MemoryDiagnoser]
public class RazorSlicesBenchmarks
{
    private readonly HttpClient _slicesNuGetClient = new();
    private readonly HttpClient _slicesLocalClient = new();
    private readonly HttpClient _pagesClient = new();
    private readonly HttpClient _componentsClient = new();
    private readonly HttpClient _blazorClient = new();
    private readonly byte[] _buffer = new byte[1024 * 128]; // 128 KB buffer
    private readonly int _iterations = 100;

    public RazorSlicesBenchmarks()
    {
        _slicesNuGetClient = CreateHttpClient<BenchmarksWebAppRazorSlicesPreviousVersion>();
        _slicesLocalClient = CreateHttpClient<BenchmarksWebApp>();
        _pagesClient = CreateHttpClient<BenchmarksRazorPagesWebApp>();
        _componentsClient = CreateHttpClient<BenchmarksRazorComponentsWebApp>();
        _blazorClient = CreateHttpClient<BenchmarksBlazorWebApp>();
    }

    [Benchmark(Baseline = true), BenchmarkCategory("Hello")]
    public Task<int> RazorSlicesNuGet() => GetPath(_slicesNuGetClient, "/hello");

    [Benchmark(), BenchmarkCategory("Hello")]
    public Task<int> RazorSlicesLocal() => GetPath(_slicesLocalClient, "/hello");

    [Benchmark, BenchmarkCategory("Hello")]
    public Task<int> RazorPages() => GetPath(_pagesClient, "/hello");

    [Benchmark, BenchmarkCategory("Hello")]
    public Task<int> RazorComponents() => GetPath(_componentsClient, "/hello");

    [Benchmark, BenchmarkCategory("Hello")]
    public Task<int> BlazorSSR() => GetPath(_blazorClient, "/hello");

    [Benchmark(), BenchmarkCategory("Pride")]
    public Task<int> RazorSlicesPrideLocal() => GetPath(_slicesLocalClient, "/pride");

    private async Task<int> GetPath(HttpClient httpClient, string path)
    {
        var bytesRead = 0;

        for (int i = 0; i < _iterations; i++)
        {
            var body = await httpClient.GetStreamAsync(path);
            bytesRead += await body.ReadAsync(_buffer);
        }

        return bytesRead;
    }

    private static HttpClient CreateHttpClient<TApp>() where TApp : class
    {
        var waf = new WebApplicationFactory<TApp>();
        waf.WithWebHostBuilder(webHost => webHost.ConfigureLogging(logging => logging.ClearProviders()));
        return waf.CreateClient();
    }
}