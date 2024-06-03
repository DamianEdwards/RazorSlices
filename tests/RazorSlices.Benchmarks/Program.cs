using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Microsoft.AspNetCore.Mvc.Testing;
using RazorSlices.Benchmarks.WebApp;

BenchmarkRunner.Run<RazorSlicesBenchmarks>();

[MemoryDiagnoser, ShortRunJob]
public class RazorSlicesBenchmarks
{
    private readonly WebApplicationFactory<BenchmarksWebApp> _waf = new();
    private readonly byte[] _buffer = new byte[1024 * 128]; // 128 KB buffer
    private readonly int _iterations = 100;

    [Benchmark(Baseline = true)]
    public  Task<int> RazorSlices() => GetPath("/slices/hello");

    [Benchmark]
    public Task<int> RazorPages() => GetPath("/pages/hello");

    [Benchmark]
    public Task<int> RazorComponentsManual() => GetPath("/components/hello");

    [Benchmark]
    public Task<int> RazorComponentPages() => GetPath("/componentpages/hello");

    private async Task<int> GetPath(string path)
    {
        var bytesRead = 0;
        using var client = _waf.CreateClient();

        for (int i = 0; i < _iterations; i++)
        {
            var body = await client.GetStreamAsync(path);
            bytesRead += await body.ReadAsync(_buffer);
        }

        return bytesRead;
    }
}