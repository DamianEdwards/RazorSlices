using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;
using RazorSlices.Benchmarks.WebApp;

BenchmarkRunner.Run<RazorSlicesBenchmarks>();

[MemoryDiagnoser, AnyCategoriesFilter("Hello"), Config(typeof(Config))]
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
    public Task<int> RazorSlicesHelloNuGet() => GetPath(_slicesNuGetClient, "/hello");

    [Benchmark(), BenchmarkCategory("Hello")]
    public Task<int> RazorSlicesHelloLocal() => GetPath(_slicesLocalClient, "/hello");

    [Benchmark, BenchmarkCategory("Hello")]
    public Task<int> RazorPagesHello() => GetPath(_pagesClient, "/hello");

    [Benchmark, BenchmarkCategory("Hello")]
    public Task<int> RazorComponentsHello() => GetPath(_componentsClient, "/hello");

    [Benchmark, BenchmarkCategory("Hello")]
    public Task<int> BlazorSSRHello() => GetPath(_blazorClient, "/hello");

    [Benchmark(), BenchmarkCategory("Lorem")]
    public Task<int> RazorSlicesLorem25Local() => GetPath(_slicesLocalClient, "/lorem25");

    [Benchmark(), BenchmarkCategory("Lorem")]
    public Task<int> RazorSlicesLorem50Local() => GetPath(_slicesLocalClient, "/lorem50");

    [Benchmark(), BenchmarkCategory("Lorem")]
    public Task<int> RazorSlicesLorem100Local() => GetPath(_slicesLocalClient, "/lorem100");

    [Benchmark(), BenchmarkCategory("Lorem")]
    public Task<int> RazorSlicesLorem200Local() => GetPath(_slicesLocalClient, "/lorem200");

    [Benchmark(), BenchmarkCategory("Lorem")]
    public Task<int> RazorSlicesLorem25() => GetPath(_slicesNuGetClient, "/lorem25");

    [Benchmark(), BenchmarkCategory("Lorem")]
    public Task<int> RazorSlicesLorem50() => GetPath(_slicesNuGetClient, "/lorem50");

    [Benchmark(), BenchmarkCategory("Lorem")]
    public Task<int> RazorSlicesLorem100() => GetPath(_slicesNuGetClient, "/lorem100");

    [Benchmark(), BenchmarkCategory("Lorem")]
    public Task<int> RazorSlicesLorem200() => GetPath(_slicesNuGetClient, "/lorem200");

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

    private class Config : ManualConfig
    {
        public Config()
        {
            AddJob(Job.ShortRun.WithCustomBuildConfiguration("Benchmarks").WithId("Benchmarks"));
        }
    }
}
