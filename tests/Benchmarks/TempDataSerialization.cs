using System.Text.Json;
using BenchmarkDotNet.Attributes;
using Microsoft.AspNetCore.Mvc.ViewFeatures.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using RazorSlices;

namespace Benchmarks;

[MemoryDiagnoser]
public class TempDataSerialization
{
    private readonly RazorSliceTempDataSerializer _serializer = new();
    private ServiceProvider _services = null!;
    private TempDataSerializer _mvc = null!;
    private Dictionary<string, object?> _values = null!;
    private byte[] _payload = null!;

    [Params(1, 8)]
    public int Entries { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMvcCore().AddViews();
        _services = services.BuildServiceProvider();
        _mvc = _services.GetRequiredService<TempDataSerializer>();
        _values = Enumerable.Range(0, Entries).ToDictionary(i => $"message{i}", _ => (object?)"Changes saved. <HTML>");
        _payload = _serializer.Serialize(_values);
    }

    [GlobalCleanup]
    public void Cleanup() => _services.Dispose();

    [Benchmark]
    public byte[] SerializeMvc() => _mvc.Serialize(_values);

    [Benchmark]
    public byte[] SerializeRazorSlices() => _serializer.Serialize(_values);

    [Benchmark]
    public IDictionary<string, object?> DeserializeMvc() => _mvc.Deserialize(_payload);

    [Benchmark]
    public IDictionary<string, object?> DeserializeRazorSlices() => _serializer.Deserialize(_payload);

    [Benchmark]
    public byte[] SerializeStringSpike()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            foreach (var entry in _values) { writer.WriteString(entry.Key, (string?)entry.Value); }
            writer.WriteEndObject();
        }
        return stream.ToArray();
    }
}
