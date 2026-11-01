using System.Globalization;
using System.Numerics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace RazorSlices.Tests;

public class TempDataValueTests
{
    [Fact]
    public void BuiltInTypesRoundTripExactly()
    {
        RoundTrip(byte.MaxValue);
        RoundTrip(sbyte.MinValue);
        RoundTrip(short.MinValue);
        RoundTrip(ushort.MaxValue);
        RoundTrip(int.MinValue);
        RoundTrip(uint.MaxValue);
        RoundTrip(long.MinValue);
        RoundTrip(ulong.MaxValue);
        RoundTrip(Int128.MinValue);
        RoundTrip(UInt128.MaxValue);
        RoundTrip(nint.MaxValue);
        RoundTrip(nuint.MaxValue);
        RoundTrip(Half.Epsilon);
        RoundTrip(float.Epsilon);
        RoundTrip(double.Epsilon);
        RoundTrip(float.NaN);
        RoundTrip(double.PositiveInfinity);
        RoundTrip(-0.0);
        RoundTrip(decimal.MaxValue);
        RoundTrip(BigInteger.Pow(10, 100));
        RoundTrip(new Complex(3.141592653589793, -12.345));
        RoundTrip(new DateTime(2026, 10, 31, 12, 0, 0, DateTimeKind.Utc).AddTicks(1));
        RoundTrip(new DateTime(2026, 10, 31, 12, 0, 0, DateTimeKind.Unspecified).AddTicks(1));
        RoundTrip(new DateTime(2026, 10, 31, 12, 0, 0, DateTimeKind.Local).AddTicks(1));
        RoundTrip(new DateTimeOffset(2026, 10, 31, 12, 0, 0, TimeSpan.FromHours(5)).AddTicks(1));
        RoundTrip(new DateOnly(2026, 10, 31));
        RoundTrip(new TimeOnly(12, 0, 0).Add(TimeSpan.FromTicks(1)));
        RoundTrip(TimeSpan.MaxValue);
        RoundTrip(Guid.Parse("b0b9ba01-96a6-4982-a4ae-2f22bc58b6e5"));
    }

    [Fact]
    public void CustomUtf8ValueRoundTripsWithoutReflection()
    {
        var provider = new MemoryProvider();
        using var services = Services(provider);
        var data = Open(services);
        data.SetUtf8("custom", new CustomValue(123));
        data.Close();
        data.Save();
        Assert.Equal(new CustomValue(123), Open(services).GetUtf8<CustomValue>("custom"));
    }

    [Fact]
    public void StringsAndBooleansKeepTheirTypes()
    {
        var provider = new MemoryProvider();
        using var services = Services(provider);
        var data = Open(services);
        data.Set("date", "2026-10-31T12:00:00Z");
        data.Set("guid", "b0b9ba01-96a6-4982-a4ae-2f22bc58b6e5");
        data.Set("null", (string?)null);
        data.Set("true", true);
        data.Set("false", false);
        data.Close();
        data.Save();
        var next = Open(services);
        Assert.Equal("2026-10-31T12:00:00Z", next.GetString("date"));
        Assert.Equal("b0b9ba01-96a6-4982-a4ae-2f22bc58b6e5", next.GetString("guid"));
        Assert.True(next.ContainsKey("null"));
        Assert.Null(next.GetString("null"));
        Assert.True(next.Get<bool>("true"));
        Assert.False(next.Get<bool>("false"));
    }

    [Fact]
    public void ReadsConsumeButPeekAndKeepRetain()
    {
        var provider = new MemoryProvider();
        using var services = Services(provider);
        var data = Open(services);
        data.Set("consume", 42);
        data.Set("peek", 43);
        data.Set("keep", 44);
        Assert.Equal(42, data.Get<int>("consume"));
        Assert.Equal(43, data.Peek<int>("peek"));
        Assert.True(data.TryGet<int>("keep", out var value));
        Assert.Equal(44, value);
        data.Keep("keep");
        Assert.False(data.TryGet<int>("missing", out _));
        data.Close();
        data.Save();
        var next = Open(services);
        Assert.False(next.ContainsKey("consume"));
        Assert.Equal(43, next.Peek<int>("peek"));
        Assert.Equal(44, next.Peek<int>("keep"));
    }

    [Fact]
    public void TypeMismatchThrowsWithoutConsuming()
    {
        using var services = Services(new MemoryProvider());
        var data = Open(services);
        data.Set("number", 42);
        Assert.Throws<InvalidOperationException>(() => data.Get<long>("number"));
        Assert.Throws<InvalidOperationException>(() => data.TryGet<long>("number", out _));
        Assert.Throws<InvalidOperationException>(() => data.GetString("number"));
        Assert.Equal(42, data.Peek<int>("number"));
        data.Keep();
    }

    [Theory]
    [InlineData("")]
    [InlineData("雪<&")]
    public void StringKeysAreCaseInsensitiveAndPreserved(string key)
    {
        var provider = new MemoryProvider();
        using var services = Services(provider);
        var data = Open(services);
        data.Set(key, "value");
        data.Set("Message", "hello");
        data.Close();
        data.Save();
        var next = Open(services);
        Assert.Equal("value", next.Peek(key));
        Assert.Equal("hello", next.Peek("message"));
    }

    [Fact]
    public void PayloadLimitsAndMalformedInputThrow()
    {
        var entries = new Dictionary<string, TempData.Entry> { ["x"] = TempData.Entry.FromString(new string('x', 300)) };
        Assert.Throws<InvalidOperationException>(() => TempDataPayload.Serialize(entries, 256));
        Assert.Throws<FormatException>(() => TempDataPayload.Deserialize([1, 0, 0, 0, 0], 256));
        Assert.Throws<FormatException>(() => TempDataPayload.Deserialize([1, 1, 0, 0, 0], 256));
        var payload = TempDataPayload.Serialize(new() { ["x"] = TempData.Entry.FromString("hello") }, 256);
        Assert.Throws<FormatException>(() => TempDataPayload.Deserialize(payload[..^1], 256));
        Assert.Throws<FormatException>(() => TempDataPayload.Deserialize([.. payload, 0], 256));
    }

    [Fact]
    public void ValueTypeSetAndGetDoNotAllocateBoxes()
    {
        using var services = Services(new MemoryProvider());
        var data = Open(services);
        for (var i = 0; i < 100; i++) { data.Set("x", 42); _ = data.Peek<int>("x"); }
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1_000; i++) { data.Set("x", 42); _ = data.Peek<int>("x"); }
        var perOperation = (GC.GetAllocatedBytesForCurrentThread() - before) / 1_000;
        // The scalar bytes are inline: no box, value buffer, or parsing allocation.
        Assert.Equal(0, perOperation);
        before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1_000; i++) { _ = data.Peek<int>("x"); }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void FloatingPointBitsAndDecimalScaleArePreserved()
    {
        using var services = Services(new MemoryProvider());
        var data = Open(services);
        var nan = BitConverter.Int64BitsToDouble(unchecked((long)0x7FF8000000001234));
        data.Set("negativeZero", -0.0);
        data.Set("decimal", 1.2300m);
        data.Set("nan", nan);
        data.Close();
        data.Save();
        data = Open(services);
        Assert.Equal(BitConverter.DoubleToInt64Bits(-0.0),
            BitConverter.DoubleToInt64Bits(data.Peek<double>("negativeZero")));
        Assert.Equal(BitConverter.DoubleToInt64Bits(nan),
            BitConverter.DoubleToInt64Bits(data.Peek<double>("nan")));
        Assert.Equal(decimal.GetBits(1.2300m), decimal.GetBits(data.Peek<decimal>("decimal")));
    }

    [Fact]
    public void StringsAndBigIntegersAreRetainedNativelyWithoutAllocatingOnAccess()
    {
        using var services = Services(new MemoryProvider());
        var data = Open(services);
        var text = new string('x', 1_000);
        var number = BigInteger.Pow(10, 500);
        for (var i = 0; i < 100; i++)
        {
            data.Set("text", text);
            data.Set("number", number);
            _ = data.Peek("text");
            _ = data.Peek<BigInteger>("number");
        }
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1_000; i++)
        {
            data.Set("text", text);
            data.Set("number", number);
            _ = data.Peek("text");
            _ = data.Peek<BigInteger>("number");
        }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Same(text, data.GetString("text"));
        Assert.Equal(number, data.Get<BigInteger>("number"));
    }

    [Fact]
    public void ConsumedStringsAreNeverEncodedAndOnlyRetainedValuesAreWritten()
    {
        var provider = new MemoryProvider();
        using var services = Services(provider);
        var data = Open(services);
        // An unpaired surrogate cannot be UTF-8 encoded. Consuming it must avoid encoding entirely.
        var text = "\uD800";
        data.Set("consumed", text);
        data.Set("retained", 0x01020304);
        Assert.Same(text, data.GetString("consumed"));
        data.Close();
        data.Save();
        Assert.DoesNotContain("consumed", System.Text.Encoding.UTF8.GetString(provider.Payload));
        var next = Open(services);
        Assert.False(next.ContainsKey("consumed"));
        Assert.Equal(0x01020304, next.Peek<int>("retained"));
        next.Set("invalid", text);
        next.Close();
        Assert.Throws<System.Text.EncoderFallbackException>(() => next.Save());
    }

    [Fact]
    public void NativeWireValuesUseExplicitWidthsAndLittleEndianEncoding()
    {
        var entries = new Dictionary<string, TempData.Entry>
        {
            ["x"] = TempData.Entry.FromNative(typeof(int).FullName!, TempDataValueKind.Int32, 0x01020304)
        };
        var payload = TempDataPayload.Serialize(entries, 256);
        Assert.Equal(new byte[] { 4, 3, 2, 1 }, payload[^4..]);
        var decoded = TempDataPayload.Deserialize(payload, 256);
        Assert.Equal(TempDataValueKind.Int32, decoded["x"].Kind);
        Assert.Equal(0x01020304, decoded["x"].ReadNative<int>());
        var invalidKind = (byte[])payload.Clone();
        invalidKind[^9] = 255;
        Assert.Throws<FormatException>(() => TempDataPayload.Deserialize(invalidKind, 256));
        var invalidLength = (byte[])payload.Clone();
        invalidLength[^8] = 3;
        Assert.Throws<FormatException>(() => TempDataPayload.Deserialize(invalidLength, 256));
    }

    [Fact]
    public void DateTimeKindsAndOffsetsRoundTrip()
    {
        var provider = new MemoryProvider();
        using var services = Services(provider);
        var data = Open(services);
        foreach (var kind in new[] { DateTimeKind.Utc, DateTimeKind.Local, DateTimeKind.Unspecified })
        {
            data.Set(kind.ToString(), new DateTime(2026, 10, 31, 12, 0, 0, kind).AddTicks(1));
        }
        var offset = new DateTimeOffset(2026, 10, 31, 12, 0, 0, TimeSpan.FromMinutes(345));
        data.Set("offset", offset);
        data.Close();
        data.Save();
        var next = Open(services);
        foreach (var kind in new[] { DateTimeKind.Utc, DateTimeKind.Local, DateTimeKind.Unspecified })
        {
            var date = next.Get<DateTime>(kind.ToString());
            Assert.Equal(kind, date.Kind);
            Assert.Equal(new DateTime(2026, 10, 31, 12, 0, 0, kind).AddTicks(1).Ticks, date.Ticks);
        }
        var actualOffset = next.Get<DateTimeOffset>("offset");
        Assert.Equal(offset.Offset, actualOffset.Offset);
        Assert.Equal(offset.Ticks, actualOffset.Ticks);
    }

    [Fact]
    public void CustomValuesAreFormattedEagerlyButNotReformattedWhenSaving()
    {
        TrackingValue.Formats = 0;
        using var services = Services(new MemoryProvider());
        var data = Open(services);
        data.SetUtf8("custom", new TrackingValue(42));
        Assert.Equal(1, TrackingValue.Formats);
        Assert.Equal(new TrackingValue(42), data.GetUtf8<TrackingValue>("custom", consume: false));
        data.Close();
        data.Save();
        Assert.Equal(1, TrackingValue.Formats);
        Assert.Equal(new TrackingValue(42), Open(services).GetUtf8<TrackingValue>("custom"));
        Assert.Equal(1, TrackingValue.Formats);
    }

    [Fact]
    public void LargeValuesUseOverflowStorageAndRespectTheLimit()
    {
        var provider = new MemoryProvider();
        using var services = Services(provider);
        var data = Open(services);
        var number = BigInteger.Pow(10, 500);
        data.Set("large", number);
        data.Set("text", new string('x', 1_000));
        data.Close();
        data.Save();
        var next = Open(services);
        Assert.Equal(number, next.Peek<BigInteger>("large"));
        Assert.Equal(new string('x', 1_000), next.Peek("text"));
        next.Set("tooLarge", new string('x', 65_536));
        next.Close();
        Assert.Throws<InvalidOperationException>(() => next.Save());
    }

    [Fact]
    public void UnsupportedCustomSpanOnlyTypesAndFormattingFailuresThrow()
    {
        using var services = Services(new MemoryProvider());
        var data = Open(services);
        Assert.Throws<InvalidOperationException>(() => data.SetUtf8("invalid", new Unformattable()));
        Assert.False(data.ContainsKey("invalid"));
        Assert.Throws<InvalidOperationException>(() => data.Set("char", 'x'));
    }

    private static void RoundTrip<T>(T value) where T : IUtf8SpanFormattable, ISpanParsable<T>
    {
        var provider = new MemoryProvider();
        using var services = Services(provider);
        var data = Open(services);
        data.Set("value", value);
        data.Close();
        data.Save();
        var next = Open(services);
        Assert.Equal(value, next.Get<T>("value"));
    }

    private static ServiceProvider Services(MemoryProvider provider)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IRazorSliceTempDataProvider>(provider);
        services.AddRazorSliceTempData();
        return services.BuildServiceProvider();
    }

    private static TempData Open(IServiceProvider services)
    {
        var data = TempData.GetOrCreate(new DefaultHttpContext { RequestServices = services });
        data.Activate();
        return data;
    }

    private sealed class MemoryProvider : IRazorSliceTempDataProvider
    {
        private byte[] _payload = [];
        internal byte[] Payload => _payload;
        public byte[] Load(HttpContext context) => _payload;
        public void Save(HttpContext context, byte[] payload) => _payload = payload;
    }

    private readonly record struct CustomValue(int Value) : IUtf8SpanFormattable, IUtf8SpanParsable<CustomValue>
    {
        public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
            => Value.TryFormat(utf8Destination, out bytesWritten, format, provider);
        public static CustomValue Parse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider) => new(int.Parse(utf8Text, provider));
        public static bool TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider, out CustomValue result)
        {
            var success = int.TryParse(utf8Text, provider, out var value);
            result = new(value);
            return success;
        }
    }

    private readonly struct Unformattable : IUtf8SpanFormattable, IUtf8SpanParsable<Unformattable>
    {
        public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
        {
            bytesWritten = 0;
            return false;
        }
        public static Unformattable Parse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider) => throw new FormatException();
        public static bool TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider, out Unformattable result)
        {
            result = default;
            return false;
        }
    }

    private readonly record struct TrackingValue(int Value) : IUtf8SpanFormattable, IUtf8SpanParsable<TrackingValue>
    {
        internal static int Formats;
        public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
        {
            Formats++;
            return Value.TryFormat(utf8Destination, out bytesWritten, format, provider);
        }
        public static TrackingValue Parse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider) => new(int.Parse(utf8Text, provider));
        public static bool TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider, out TrackingValue result)
        {
            var success = int.TryParse(utf8Text, provider, out var value);
            result = new(value);
            return success;
        }
    }
}
