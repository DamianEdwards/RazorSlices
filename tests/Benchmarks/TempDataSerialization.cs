using BenchmarkDotNet.Attributes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using RazorSlices;

namespace Benchmarks;

[MemoryDiagnoser]
public class TempDataSerialization
{
    private ServiceProvider _services = null!;
    private TempData _data = null!;
    private TempDataDictionary _mvc = null!;

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IRazorSliceTempDataProvider, MemoryProvider>();
        services.AddRazorSliceTempData();
        _services = services.BuildServiceProvider();
        _data = TempData.GetOrCreate(new DefaultHttpContext { RequestServices = _services });
        _data.Activate();
        _data.Set("count", 42);
        _mvc = new TempDataDictionary(new DefaultHttpContext(), new MvcProvider());
        _mvc["count"] = 42;
    }

    [GlobalCleanup]
    public void Cleanup() => _services.Dispose();

    [Benchmark(Baseline = true)]
    public int MvcSetAndRead()
    {
        _mvc["count"] = 42;
        return (int)_mvc.Peek("count")!;
    }

    [Benchmark]
    public int RazorSlicesSetAndRead()
    {
        _data.Set("count", 42);
        return _data.Peek<int>("count");
    }

    [Benchmark]
    public int RazorSlicesRead() => _data.Peek<int>("count");

    private sealed class MemoryProvider : IRazorSliceTempDataProvider
    {
        public byte[] Load(HttpContext context) => [];
        public void Save(HttpContext context, byte[] payload) { }
    }

    private sealed class MvcProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
