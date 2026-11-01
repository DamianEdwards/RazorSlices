using BenchmarkDotNet.Attributes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using System.Numerics;
using Microsoft.Extensions.DependencyInjection;
using RazorSlices;

namespace Benchmarks;

[MemoryDiagnoser]
public class TempDataSerialization
{
    private ServiceProvider _services = null!;
    private TempData _data = null!;
    private TempData _referenceData = null!;
    private TempDataDictionary _mvc = null!;
    private readonly string _text = new('x', 1_000);
    private readonly BigInteger _number = BigInteger.Pow(10, 500);

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
        _referenceData = TempData.GetOrCreate(new DefaultHttpContext { RequestServices = _services });
        _referenceData.Activate();
        _referenceData.Set("text", _text);
        _referenceData.Set("number", _number);
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

    [Benchmark]
    public string? RazorSlicesStringSetAndRead()
    {
        _referenceData.Set("text", _text);
        return _referenceData.Peek("text");
    }

    [Benchmark]
    public BigInteger RazorSlicesBigIntegerSetAndRead()
    {
        _referenceData.Set("number", _number);
        return _referenceData.Peek<BigInteger>("number");
    }

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
