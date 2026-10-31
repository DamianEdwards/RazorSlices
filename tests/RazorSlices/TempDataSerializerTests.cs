using System.Collections.Concurrent;
using System.Text;
using Microsoft.AspNetCore.Mvc.ViewFeatures.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace RazorSlices.Tests;

public class TempDataSerializerTests
{
    private readonly RazorSliceTempDataSerializer _serializer = new();

    public static TheoryData<object?> SupportedValues => new()
    {
        null, "<hello>", 42, true, false, Guid.Parse("b0b9ba01-96a6-4982-a4ae-2f22bc58b6e5"),
        new DateTime(2026, 10, 31, 12, 0, 0, DateTimeKind.Utc), DayOfWeek.Friday,
        new[] { 1, -2, int.MaxValue }, new[] { "hello", "<world>", null! },
        new List<int> { 1, 2 }, new List<string> { "one", "two" },
        Array.Empty<int>(), Array.Empty<string>(),
        new Dictionary<string, string> { ["quote\""] = "hello", ["null"] = null! },
        new Dictionary<string, string>(),
        "b0b9ba01-96a6-4982-a4ae-2f22bc58b6e5", "2026-10-31T12:00:00Z"
    };

    [Theory]
    [MemberData(nameof(SupportedValues))]
    public void PayloadsAreInterchangeableWithMvcDefaultSerializer(object? value)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMvcCore().AddViews();
        using var container = services.BuildServiceProvider();
        var mvc = container.GetRequiredService<TempDataSerializer>();
        var values = new Dictionary<string, object?> { ["value"] = value };
        var ours = _serializer.Serialize(values);
        var theirs = mvc.Serialize(values);
        Assert.Equal(theirs, ours);
        var normalized = mvc.Serialize(mvc.Deserialize(theirs));
        Assert.Equal(normalized, mvc.Serialize(_serializer.Deserialize(theirs)));
        Assert.Equal(normalized, _serializer.Serialize(mvc.Deserialize(ours)));
    }

    [Fact]
    public void EmptyDataMatchesMvcAndTypeChecksAreExplicit()
    {
        Assert.Empty(_serializer.Serialize(new Dictionary<string, object?>()));
        Assert.Empty(_serializer.Deserialize([]));
        Assert.True(_serializer.CanSerializeType(typeof(int?)));
        Assert.True(_serializer.CanSerializeType(typeof(DayOfWeek)));
        Assert.False(_serializer.CanSerializeType(typeof(long)));
        Assert.False(_serializer.CanSerializeType(typeof(object)));
        Assert.Throws<InvalidOperationException>(() => _serializer.Serialize(new Dictionary<string, object?> { ["value"] = 42L }));
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("{\"value\":[true]}")]
    [InlineData("{\"value\":[1,\"two\"]}")]
    [InlineData("{\"value\":")]
    [InlineData("{}{}")]
    [InlineData("{\"value\":{\"nested\":{}}}")]
    public void InvalidPayloadsThrow(string json)
        => Assert.ThrowsAny<Exception>(() => _serializer.Deserialize(Encoding.UTF8.GetBytes(json)));

    [Fact]
    public void FailedAndLargeWritesDoNotCorruptTheNextUse()
    {
        var invalid = new Dictionary<string, object?> { ["first"] = "secret", ["unsupported"] = new object() };
        Assert.Throws<InvalidOperationException>(() => _serializer.Serialize(invalid));
        var large = new string('x', 100_000);
        Assert.Equal(large, _serializer.Deserialize(_serializer.Serialize(new Dictionary<string, object?> { ["value"] = large }))["value"]);
        var small = new Dictionary<string, object?> { ["value"] = "small" };
        Assert.Equal("{\"value\":\"small\"}", Encoding.UTF8.GetString(_serializer.Serialize(small)));
    }

    [Fact]
    public void ConcurrentRequestsHaveIndependentWriterState()
    {
        var errors = new ConcurrentQueue<int>();
        Parallel.For(0, 1_000, i =>
        {
            var result = _serializer.Deserialize(_serializer.Serialize(new Dictionary<string, object?> { ["value"] = i }));
            if (!Equals(i, result["value"])) { errors.Enqueue(i); }
        });
        Assert.Empty(errors);
    }

    [Fact]
    public void EnumValuesMustFitInInt32()
    {
        var supported = new Dictionary<string, object?> { ["value"] = LongEnum.Small };
        Assert.Equal(1, _serializer.Deserialize(_serializer.Serialize(supported))["value"]);
        Assert.Throws<OverflowException>(() => _serializer.Serialize(
            new Dictionary<string, object?> { ["value"] = LongEnum.Large }));
    }

    private enum LongEnum : long { Small = 1, Large = long.MaxValue }
}
