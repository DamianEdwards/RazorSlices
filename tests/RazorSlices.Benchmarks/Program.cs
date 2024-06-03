using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Microsoft.AspNetCore.Mvc.Testing;
using RazorSlices.Benchmarks.WebApp;

BenchmarkRunner.Run<RazorSlicesBenchmarks>();

[MemoryDiagnoser, ShortRunJob]
public class RazorSlicesBenchmarks
{
    private readonly WebApplicationFactory<BenchmarksWebApp> _slicesWAF = new();
    private readonly WebApplicationFactory<BenchmarksRazorPagesWebApp> _pagesWAF = new();
    private readonly WebApplicationFactory<BenchmarksRazorComponentsWebApp> _componentsWAF = new();
    private readonly WebApplicationFactory<BenchmarksBlazorWebApp> _blazorWAF = new();
    private readonly byte[] _buffer = new byte[1024 * 128]; // 128 KB buffer
    private readonly int _iterations = 100;

    [Benchmark(Baseline = true)]
    public  Task<int> RazorSlices() => GetPath(_slicesWAF, "/hello");

    [Benchmark]
    public Task<int> RazorPages() => GetPath(_pagesWAF, "/hello");

    [Benchmark]
    public Task<int> RazorComponentsManual() => GetPath(_componentsWAF, "/hello");

    [Benchmark]
    public Task<int> BlazorSSR() => GetPath(_blazorWAF, "/hello");

    private async Task<int> GetPath<TApp>(WebApplicationFactory<TApp> waf, string path) where TApp : class
    {
        var bytesRead = 0;
        using var client = waf.CreateClient();

        for (int i = 0; i < _iterations; i++)
        {
            var body = await client.GetStreamAsync(path);
            bytesRead += await body.ReadAsync(_buffer);
        }

        return bytesRead;
    }
}