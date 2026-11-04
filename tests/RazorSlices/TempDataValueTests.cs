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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GenericStringReadsPreserveNullsAndConsumption(bool reload)
    {
        var provider = new MemoryProvider();
        using var services = Services(provider);
        var data = Open(services);
        data.Set("get", "hello");
        data.Set("try", "");
        data.Set("null", (string?)null);
        data.Set("peek", "retained");
        data.Set("peekNull", (string?)null);
        data.Set("keep", "kept");
        if (reload)
        {
            data.Close();
            data.Save();
            data = Open(services);
        }

        Assert.Null(data.Get<string>("missing"));
        Assert.Null(data.Peek<string>("missing"));
        Assert.False(data.TryGet<string>("missing", out var missing));
        Assert.Null(missing);
        Assert.Equal("hello", data.Get<string>("get"));
        Assert.True(data.TryGet<string>("try", out var empty));
        Assert.Equal("", empty);
        Assert.True(data.TryGet<string>("null", out var nullValue));
        Assert.Null(nullValue);
        Assert.True(data.ContainsKey("null"));
        Assert.Null(data.Get<string>("null"));
        Assert.Equal("retained", data.Peek<string>("peek"));
        Assert.Null(data.Peek<string>("peekNull"));
        Assert.Equal("kept", data.Get<string>("keep"));
        data.Keep("keep");
        data.Close();
        data.Save();

        var next = Open(services);
        Assert.Equal(3, next.Count);
        Assert.False(next.ContainsKey("get"));
        Assert.False(next.ContainsKey("try"));
        Assert.False(next.ContainsKey("null"));
        Assert.Equal("retained", next.Get<string>("peek"));
        Assert.True(next.TryGet<string>("peekNull", out var retainedNull));
        Assert.Null(retainedNull);
        Assert.Equal("kept", next.Get<string>("keep"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Utf8ReadsDistinguishMissingAndDefaultAndRespectConsumption(bool reload)
    {
        var provider = new MemoryProvider();
        using var services = Services(provider);
        var data = Open(services);
        data.SetUtf8("get", new CustomValue(123));
        data.SetUtf8("try", default(CustomValue));
        data.SetUtf8("peek", new CustomValue(456));
        data.SetUtf8("keep", new CustomValue(789));
        if (reload)
        {
            data.Close();
            data.Save();
            data = Open(services);
        }

        Assert.Equal(default, data.GetUtf8<CustomValue>("missing"));
        Assert.Equal(default, data.PeekUtf8<CustomValue>("missing"));
        Assert.False(data.TryGetUtf8<CustomValue>("missing", out var missing));
        Assert.Equal(default, missing);
        Assert.Equal(new CustomValue(123), data.GetUtf8<CustomValue>("get"));
        Assert.True(data.TryGetUtf8<CustomValue>("try", out var zero));
        Assert.Equal(default, zero);
        Assert.Equal(new CustomValue(456), data.PeekUtf8<CustomValue>("peek"));
        Assert.True(data.TryGetUtf8<CustomValue>("keep", out var kept));
        Assert.Equal(new CustomValue(789), kept);
        data.Keep("keep");
        data.Close();
        data.Save();

        var next = Open(services);
        Assert.Equal(2, next.Count);
        Assert.False(next.ContainsKey("get"));
        Assert.False(next.ContainsKey("try"));
        Assert.Equal(new CustomValue(456), next.GetUtf8<CustomValue>("peek"));
        Assert.Equal(new CustomValue(789), next.GetUtf8<CustomValue>("keep"));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void BuiltInAndDualParsableValuesInteroperateAcrossReadFamilies(bool reload, bool utf8Writer)
    {
        var provider = new MemoryProvider();
        using var services = Services(provider);
        var data = Open(services);
        data.Set("native", 42);
        data.SetUtf8("utf8", 43);
        if (utf8Writer) { data.SetUtf8("custom", new DualValue(44)); }
        else { data.Set("custom", new DualValue(44)); }
        if (reload)
        {
            data.Close();
            data.Save();
            data = Open(services);
        }

        Assert.Equal(42, data.PeekUtf8<int>("native"));
        Assert.True(data.TryGetUtf8<int>("native", out var native));
        Assert.Equal(42, native);
        Assert.Equal(42, data.Get<int>("native"));
        Assert.Equal(43, data.Peek<int>("utf8"));
        Assert.True(data.TryGet<int>("utf8", out var utf8));
        Assert.Equal(43, utf8);
        Assert.Equal(43, data.GetUtf8<int>("utf8"));
        Assert.Equal(new DualValue(44), data.Peek<DualValue>("custom"));
        Assert.Equal(new DualValue(44), data.PeekUtf8<DualValue>("custom"));
        Assert.True(data.TryGet<DualValue>("custom", out var custom));
        Assert.Equal(new DualValue(44), custom);
        Assert.Equal(custom, data.Get<DualValue>("custom"));
        Assert.True(data.TryGetUtf8<DualValue>("custom", out var customUtf8));
        Assert.Equal(custom, customUtf8);
        data.Close();
        data.Save();
        Assert.Equal(0, Open(services).Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WrongTypesThrowWithoutConsumingAcrossReadFamilies(bool reload)
    {
        var provider = new MemoryProvider();
        using var services = Services(provider);
        var data = Open(services);
        data.Set("string", "42");
        data.Set("number", 42);
        data.SetUtf8("custom", new CustomValue(42));
        if (reload)
        {
            data.Close();
            data.Save();
            data = Open(services);
        }

        foreach (var key in new[] { "string", "number", "custom" })
        {
            Assert.Throws<InvalidOperationException>(() => data.Get<long>(key));
            Assert.Throws<InvalidOperationException>(() => data.Peek<long>(key));
            Assert.Throws<InvalidOperationException>(() => data.TryGet<long>(key, out _));
            Assert.Throws<InvalidOperationException>(() => data.GetUtf8<long>(key));
            Assert.Throws<InvalidOperationException>(() => data.PeekUtf8<long>(key));
            Assert.Throws<InvalidOperationException>(() => data.TryGetUtf8<long>(key, out _));
        }
        foreach (var key in new[] { "number", "custom" })
        {
            Assert.Throws<InvalidOperationException>(() => data.Get<string>(key));
            Assert.Throws<InvalidOperationException>(() => data.Peek<string>(key));
            Assert.Throws<InvalidOperationException>(() => data.TryGet<string>(key, out _));
        }
        data.Close();
        data.Save();
        var next = Open(services);
        Assert.Equal(3, next.Count);
        Assert.Equal("42", next.Get<string>("string"));
        Assert.Equal(42, next.Get<int>("number"));
        Assert.Equal(new CustomValue(42), next.GetUtf8<CustomValue>("custom"));
    }

    [Fact]
    public void ParsingFailuresThrowWithoutConsumingAcrossReadFamilies()
    {
        var provider = new MemoryProvider();
        using var services = Services(provider);
        var typeName = typeof(DualValue).FullName + "," + typeof(DualValue).Assembly.GetName().Name;
        var payload = TempDataPayload.Serialize(new()
        {
            ["invalid"] = new TempData.Entry(typeName, "not a number"u8)
        }, 256);
        provider.Save(new DefaultHttpContext(), payload);
        var data = Open(services);
        Assert.Throws<FormatException>(() => data.Get<DualValue>("invalid"));
        Assert.Throws<FormatException>(() => data.Peek<DualValue>("invalid"));
        Assert.Throws<FormatException>(() => data.TryGet<DualValue>("invalid", out _));
        Assert.Throws<FormatException>(() => data.GetUtf8<DualValue>("invalid"));
        Assert.Throws<FormatException>(() => data.PeekUtf8<DualValue>("invalid"));
        Assert.Throws<FormatException>(() => data.TryGetUtf8<DualValue>("invalid", out _));
        data.Close();
        data.Save();
        Assert.Equal(payload, provider.Payload);
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
        Assert.Equal("2026-10-31T12:00:00Z", next.Get<string>("date"));
        Assert.Equal("b0b9ba01-96a6-4982-a4ae-2f22bc58b6e5", next.Get<string>("guid"));
        Assert.True(next.ContainsKey("null"));
        Assert.Null(next.Get<string>("null"));
        Assert.True(next.Get<bool>("true"));
        Assert.False(next.Get<bool>("false"));
    }

    [Fact]
    public void MissingBuiltInReadsReturnDefaultButTryGetDistinguishesStoredDefaults()
    {
        using var services = Services(new MemoryProvider());
        var data = Open(services);
        Assert.Equal(0, data.Get<int>("missing"));
        Assert.Equal(0, data.Peek<int>("missing"));
        Assert.False(data.Get<bool>("missing"));
        Assert.False(data.TryGet<int>("missing", out var missing));
        Assert.Equal(0, missing);
        data.Set("zero", 0);
        data.Set("false", false);
        Assert.True(data.TryGet<int>("zero", out var zero));
        Assert.Equal(0, zero);
        Assert.True(data.TryGet<bool>("false", out var boolean));
        Assert.False(boolean);
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
        Assert.Throws<InvalidOperationException>(() => data.Get<string>("number"));
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
        Assert.Equal("value", next.Peek<string>(key));
        Assert.Equal("hello", next.Peek<string>("message"));
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
            _ = data.Peek<string>("text");
            _ = data.Get<string>("text");
            _ = data.TryGet<string>("text", out _);
            _ = data.Peek<BigInteger>("number");
        }
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1_000; i++)
        {
            data.Set("text", text);
            data.Set("number", number);
            _ = data.Peek<string>("text");
            _ = data.Get<string>("text");
            _ = data.TryGet<string>("text", out _);
            _ = data.Peek<BigInteger>("number");
        }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Same(text, data.Get<string>("text"));
        Assert.True(data.TryGet<string>("text", out var storedString));
        Assert.Same(text, storedString);
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
        Assert.Same(text, data.Get<string>("consumed"));
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
        Assert.Equal(new TrackingValue(42), data.PeekUtf8<TrackingValue>("custom"));
        data.Close();
        data.Save();
        Assert.Equal(1, TrackingValue.Formats);
        Assert.Equal(new TrackingValue(42), Open(services).GetUtf8<TrackingValue>("custom"));
        Assert.Equal(1, TrackingValue.Formats);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(1_000)]
    public void CustomCharacterSpanValuesAreFormattedImmediatelyAndRoundTrip(int length)
    {
        var provider = new MemoryProvider();
        using var services = Services(provider);
        var data = Open(services);
        var text = new string('\u96EA', length);
        var original = new SpanValue(text);
        data.Set("custom", original);
        Assert.True(original.Formats > 0);
        if (length <= 3) { Assert.Equal(1, original.Formats); }
        var formats = original.Formats;
        original.Value = "changed after Set";
        Assert.Equal(text, data.Peek<SpanValue>("custom")?.Value);
        Assert.Equal(text, data.Get<SpanValue>("custom")?.Value);
        Assert.True(data.TryGet<SpanValue>("custom", out var value));
        Assert.NotNull(value);
        Assert.Equal(text, value.Value);
        Assert.Equal(formats, original.Formats);
        data.Keep("custom");
        data.Close();
        data.Save();
        Assert.Equal(formats, original.Formats);

        var next = Open(services);
        Assert.Equal(text, next.Peek<SpanValue>("custom")?.Value);
        Assert.Equal(text, next.Get<SpanValue>("custom")?.Value);
        Assert.True(next.TryGet<SpanValue>("custom", out var restored));
        Assert.NotNull(restored);
        Assert.Equal(text, restored.Value);
        next.Close();
        next.Save();
        Assert.False(Open(services).ContainsKey("custom"));
        Assert.Equal(formats, original.Formats);
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
        Assert.Equal(new string('x', 1_000), next.Peek<string>("text"));
        next.Set("tooLarge", new string('x', 65_536));
        next.Close();
        Assert.Throws<InvalidOperationException>(() => next.Save());
    }

    [Fact]
    public void CustomFormattingFailuresAndNullValuesThrowWithoutReplacingEntries()
    {
        using var services = Services(new MemoryProvider());
        var data = Open(services);
        Assert.Throws<InvalidOperationException>(() => data.SetUtf8("invalid", new Unformattable()));
        Assert.False(data.ContainsKey("invalid"));
        Assert.Throws<InvalidOperationException>(() => data.Set("invalid", new Unformattable()));
        Assert.False(data.ContainsKey("invalid"));
        data.Set("existing", 42);
        Assert.Throws<InvalidOperationException>(() => data.Set("existing", new Unformattable()));
        Assert.Equal(42, data.Peek<int>("existing"));
        Assert.Throws<ArgumentNullException>(() => data.Set<SpanValue>("null", null!));
        Assert.False(data.ContainsKey("null"));
    }

    [Fact]
    public void CharacterValuesUseTheFormattingFallback()
    {
        RoundTrip('x');
        RoundTrip('\u96EA');
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

    private readonly record struct DualValue(int Value) : IUtf8SpanFormattable, IUtf8SpanParsable<DualValue>, ISpanParsable<DualValue>
    {
        public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
            => Value.TryFormat(utf8Destination, out bytesWritten, format, provider);
        public static DualValue Parse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider) => new(int.Parse(utf8Text, provider));
        public static DualValue Parse(ReadOnlySpan<char> text, IFormatProvider? provider) => new(int.Parse(text, provider));
        public static DualValue Parse(string text, IFormatProvider? provider) => new(int.Parse(text, provider));
        public static bool TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider, out DualValue result)
        {
            var success = int.TryParse(utf8Text, provider, out var value);
            result = new(value);
            return success;
        }
        public static bool TryParse(ReadOnlySpan<char> text, IFormatProvider? provider, out DualValue result)
        {
            var success = int.TryParse(text, provider, out var value);
            result = new(value);
            return success;
        }
        public static bool TryParse(string? text, IFormatProvider? provider, out DualValue result)
            => TryParse(text.AsSpan(), provider, out result);
    }

    private sealed class SpanValue(string value) : IUtf8SpanFormattable, ISpanParsable<SpanValue>
    {
        internal string Value { get; set; } = value;
        internal int Formats { get; private set; }

        public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
        {
            Formats++;
            return System.Text.Encoding.UTF8.TryGetBytes(Value, utf8Destination, out bytesWritten);
        }
        public static SpanValue Parse(ReadOnlySpan<char> text, IFormatProvider? provider) => new(text.ToString());
        public static SpanValue Parse(string text, IFormatProvider? provider) => new(text);
        public static bool TryParse(ReadOnlySpan<char> text, IFormatProvider? provider, out SpanValue result)
        {
            result = Parse(text, provider);
            return true;
        }
        public static bool TryParse(string? text, IFormatProvider? provider, out SpanValue result)
            => TryParse(text.AsSpan(), provider, out result);
    }

    private readonly struct Unformattable : IUtf8SpanFormattable, IUtf8SpanParsable<Unformattable>, ISpanParsable<Unformattable>
    {
        public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
        {
            bytesWritten = 0;
            return false;
        }
        public static Unformattable Parse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider) => throw new FormatException();
        public static Unformattable Parse(ReadOnlySpan<char> text, IFormatProvider? provider) => throw new FormatException();
        public static Unformattable Parse(string text, IFormatProvider? provider) => throw new FormatException();
        public static bool TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider, out Unformattable result)
        {
            result = default;
            return false;
        }
        public static bool TryParse(ReadOnlySpan<char> text, IFormatProvider? provider, out Unformattable result)
        {
            result = default;
            return false;
        }
        public static bool TryParse(string? text, IFormatProvider? provider, out Unformattable result)
            => TryParse(text.AsSpan(), provider, out result);
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
