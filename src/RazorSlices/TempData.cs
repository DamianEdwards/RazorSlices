using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace RazorSlices;

/// <summary>
/// Experimental typed, request-local TempData. Access is limited to the WithTempData handler invocation.
/// Pass retrieved values, not this object, to templates.
/// </summary>
[Experimental("RSLICE0200")]
public sealed class TempData : IBindableFromHttpContext<TempData>
{
    private static readonly object RequestKey = new();
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private readonly HttpContext _context;
    private Dictionary<string, Entry>? _entries;
    private IRazorSliceTempDataProvider? _provider;
    private AccessState _state;

    private TempData(HttpContext context) => _context = context;

    /// <summary>Binds the same guarded instance to every TempData parameter in the request.</summary>
    public static ValueTask<TempData?> BindAsync(HttpContext context, ParameterInfo parameter)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(parameter);
        if (context.GetEndpoint()?.Metadata.GetMetadata<TempDataExtensions.TempDataMetadata>() is null)
        {
            throw new InvalidOperationException("TempData parameters require .WithTempData() on the endpoint or route group.");
        }
        return ValueTask.FromResult<TempData?>(GetOrCreate(context));
    }

    /// <summary>Gets and consumes a string value, or sets a string value. Missing keys return null.</summary>
    public string? this[string key]
    {
        get => GetString(key);
        set => Set(key, value);
    }

    /// <summary>Gets the number of current values, including values consumed in this request.</summary>
    public int Count => Entries.Count;

    /// <summary>Stores a string without inferring numbers, dates, or GUIDs from its contents.</summary>
    public void Set(string key, string? value)
    {
        EnsureAccess();
        Store(key, Entry.FromString(value));
    }

    /// <summary>Stores a Boolean without boxing.</summary>
    public void Set(string key, bool value)
    {
        EnsureAccess();
        Store(key, Entry.FromNative(TypeName<bool>(), NativeType<bool>.Kind, value));
    }

    /// <summary>
    /// Stores a built-in numeric, date/time, or GUID value natively without boxing or formatting.
    /// Custom types should use SetUtf8 instead.
    /// </summary>
    public void Set<T>(string key, T value)
        where T : IUtf8SpanFormattable, ISpanParsable<T>
    {
        if (!TypeInfo<T>.IsBuiltIn)
        {
            EnsureAccess();
            throw new InvalidOperationException("Custom TempData types must support UTF-8 parsing and use SetUtf8.");
        }
        EnsureAccess();
        Store(key, Entry.FromNative(TypeName<T>(), NativeType<T>.Kind, value));
    }

    /// <summary>Stores a custom value supporting both UTF-8 formatting and UTF-8 parsing without boxing.</summary>
    public void SetUtf8<T>(string key, T value)
        where T : IUtf8SpanFormattable, IUtf8SpanParsable<T>
    {
        if (TypeInfo<T>.IsBuiltIn)
        {
            EnsureAccess();
            Store(key, Entry.FromNative(TypeName<T>(), NativeType<T>.Kind, value));
            return;
        }
        SetCore(key, value);
    }

    private void SetCore<T>(string key, T value) where T : IUtf8SpanFormattable
    {
        EnsureAccess();
        ArgumentNullException.ThrowIfNull(key);
        if (!typeof(T).IsValueType && value is null) { throw new ArgumentNullException(nameof(value)); }
        Span<byte> scratch = stackalloc byte[128];
        if (value.TryFormat(scratch, out var written, default, CultureInfo.InvariantCulture))
        {
            Store(key, new(TypeName<T>(), scratch[..written]));
            return;
        }
        var limit = Options.MaxPayloadBytes;
        for (var size = 256; ; size = Math.Min(checked(size * 2), limit))
        {
            var length = Math.Min(size, limit);
            var buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(length);
            try
            {
                if (value.TryFormat(buffer.AsSpan(0, length), out written, default, CultureInfo.InvariantCulture))
                {
                    Store(key, new(TypeName<T>(), buffer.AsSpan(0, written)));
                    return;
                }
            }
            finally
            {
                System.Buffers.ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
            }
            if (length == limit)
            {
                throw new InvalidOperationException("The formatted TempData value exceeds MaxPayloadBytes.");
            }
        }
    }

    /// <summary>Gets and consumes a string, returning null for a missing key. Type mismatches throw.</summary>
    public string? GetString(string key) => ReadString(key, consume: true);

    /// <summary>Gets a string without consuming it.</summary>
    public string? Peek(string key) => ReadString(key, consume: false);

    /// <summary>Gets and consumes a typed value. Missing keys return default; type mismatches throw.</summary>
    public T? Get<T>(string key) where T : ISpanParsable<T>
        => TryRead<T>(key, consume: true, out var value) ? value : default;

    /// <summary>Gets a typed value without consuming it. Missing keys return default.</summary>
    public T? Peek<T>(string key) where T : ISpanParsable<T>
        => TryRead<T>(key, consume: false, out var value) ? value : default;

    /// <summary>Gets and consumes a typed value. Returns false only when the key is missing.</summary>
    public bool TryGet<T>(string key, [MaybeNullWhen(false)] out T value) where T : ISpanParsable<T>
        => TryRead(key, consume: true, out value);

    /// <summary>Gets a custom UTF-8-parsable value, consuming it by default. Type mismatches throw.</summary>
    public T? GetUtf8<T>(string key, bool consume = true) where T : IUtf8SpanFormattable, IUtf8SpanParsable<T>
    {
        if (!Entries.TryGetValue(key, out var entry)) { return default; }
        CheckType(key, entry, TypeName<T>());
        T value;
        if (entry.Kind != TempDataValueKind.Utf8)
        {
            value = entry.ReadNative<T>();
        }
        else
        {
            value = T.Parse(entry.Bytes, CultureInfo.InvariantCulture);
        }
        MarkConsumed(key, entry, consume);
        return value;
    }

    /// <summary>Returns whether the key exists without consuming it.</summary>
    public bool ContainsKey(string key) => Entries.ContainsKey(key);

    /// <summary>Retains all values for the next request.</summary>
    public void Keep()
    {
        foreach (var key in Entries.Keys)
        {
            var entry = Entries[key];
            entry.Consumed = false;
            Entries[key] = entry;
        }
    }

    /// <summary>Retains one value for the next request, if present.</summary>
    public void Keep(string key)
    {
        if (Entries.TryGetValue(key, out var entry))
        {
            entry.Consumed = false;
            Entries[key] = entry;
        }
    }

    /// <summary>Removes a value.</summary>
    public bool Remove(string key) => Entries.Remove(key);

    /// <summary>Removes all values.</summary>
    public void Clear() => Entries.Clear();

    private string? ReadString(string key, bool consume)
    {
        if (!Entries.TryGetValue(key, out var entry)) { return null; }
        CheckType(key, entry, "string");
        var value = entry.Text;
        MarkConsumed(key, entry, consume);
        return value;
    }

    private bool TryRead<T>(string key, bool consume, [MaybeNullWhen(false)] out T value)
        where T : ISpanParsable<T>
    {
        if (!Entries.TryGetValue(key, out var entry))
        {
            value = default;
            return false;
        }
        CheckType(key, entry, TypeName<T>());
        if (entry.Kind != TempDataValueKind.Utf8)
        {
            value = entry.ReadNative<T>();
            MarkConsumed(key, entry, consume);
            return true;
        }
        var length = Utf8.GetCharCount(entry.Bytes);
        char[]? rented = null;
        Span<char> text = length <= 256 ? stackalloc char[length]
            : (rented = System.Buffers.ArrayPool<char>.Shared.Rent(length)).AsSpan(0, length);
        try
        {
            Utf8.GetChars(entry.Bytes, text);
            value = T.Parse(text, CultureInfo.InvariantCulture);
        }
        finally
        {
            if (rented is not null) { System.Buffers.ArrayPool<char>.Shared.Return(rented, clearArray: true); }
        }
        MarkConsumed(key, entry, consume);
        return true;
    }

    private static string TypeName<T>() => TypeInfo<T>.Name;

    private static class TypeInfo<T>
    {
        private static readonly Type Type = typeof(T);
        internal static readonly bool IsBuiltIn = NativeType<T>.Kind != TempDataValueKind.Utf8;
        internal static readonly string Name = (Type.FullName
            ?? throw new InvalidOperationException("TempData values must have a stable type name."))
            + (IsBuiltIn ? "" : "," + Type.Assembly.GetName().Name);
    }

    private static void CheckType(string key, Entry entry, string expected)
    {
        if (entry.Type != expected)
        {
            throw new InvalidOperationException($"TempData key '{key}' contains '{entry.Type}', not '{expected}'.");
        }
    }

    private void MarkConsumed(string key, Entry entry, bool consume)
    {
        if (!consume) { return; }
        entry.Consumed = true;
        Entries[key] = entry;
    }

    private void Store(string key, Entry entry)
    {
        ArgumentNullException.ThrowIfNull(key);
        Entries[key] = entry;
    }

    private RazorSliceTempDataOptions Options => _context.RequestServices
        .GetRequiredService<Microsoft.Extensions.Options.IOptions<RazorSliceTempDataOptions>>().Value;

    private Dictionary<string, Entry> Entries
    {
        get
        {
            EnsureAccess();
            if (_entries is not null) { return _entries; }
            _provider = _context.RequestServices.GetService<IRazorSliceTempDataProvider>()
                ?? throw new InvalidOperationException("TempData requires AddRazorSliceTempData().");
            var payload = _provider.Load(_context);
            _entries = TempDataPayload.Deserialize(payload, Options.MaxPayloadBytes);
            return _entries;
        }
    }

    internal static TempData GetOrCreate(HttpContext context)
    {
        if (context.Items.TryGetValue(RequestKey, out var existing)) { return (TempData)existing!; }
        var data = new TempData(context);
        context.Items.Add(RequestKey, data);
        return data;
    }

    internal void Activate()
    {
        if (_state != AccessState.Bound) { throw new InvalidOperationException("TempData access cannot be reopened."); }
        EnsureResponseNotStarted();
        _state = AccessState.Active;
    }

    internal void Close() => _state = AccessState.Closed;

    internal void Save()
    {
        if (_entries is null) { return; }
        EnsureResponseNotStarted();
        var payload = TempDataPayload.Serialize(_entries, Options.MaxPayloadBytes);
        _provider!.Save(_context, payload);
    }

    private void EnsureAccess()
    {
        if (_state != AccessState.Active)
        {
            throw new InvalidOperationException(
                "TempData is only accessible inside a .WithTempData() handler, before it returns. Pass retrieved values to templates instead.");
        }
        EnsureResponseNotStarted();
    }

    private void EnsureResponseNotStarted()
    {
        if (_context.Response.HasStarted)
        {
            throw new InvalidOperationException("TempData must be accessed and saved before the response starts.");
        }
    }

    internal struct Entry
    {
        private InlineBytes _inline;
        private readonly byte[]? _overflow;
        private readonly BigInteger _bigInteger;
        internal readonly string? Text;
        internal readonly TempDataValueKind Kind;
        internal readonly string Type;
        internal readonly int Length;
        internal bool Consumed;

        internal Entry(string type, ReadOnlySpan<byte> value)
        {
            this = default;
            Type = type;
            Length = value.Length;
            if (value.Length <= 16) { value.CopyTo(_inline); }
            else { _overflow = value.ToArray(); }
        }

        private Entry(string? text)
        {
            this = default;
            Type = "string";
            Kind = TempDataValueKind.String;
            Text = text;
        }

        private Entry(string type, BigInteger value)
        {
            this = default;
            Type = type;
            Kind = TempDataValueKind.BigInteger;
            _bigInteger = value;
        }

        private Entry(string type, TempDataValueKind kind)
        {
            this = default;
            Type = type;
            Kind = kind;
        }

        internal static Entry FromString(string? value) => new(value);

        internal static Entry FromNative<T>(string type, TempDataValueKind kind, T value)
        {
            if (kind == TempDataValueKind.BigInteger && typeof(T) == typeof(BigInteger))
            {
                return new(type, Unsafe.As<T, BigInteger>(ref value));
            }
            if (kind is TempDataValueKind.Utf8 or TempDataValueKind.String ||
                kind != NativeType<T>.Kind || RuntimeHelpers.IsReferenceOrContainsReferences<T>() ||
                Unsafe.SizeOf<T>() > 16)
            {
                throw new InvalidOperationException("Unsupported native TempData type.");
            }
            var entry = new Entry(type, kind);
            Unsafe.WriteUnaligned(ref entry._inline[0], value);
            return entry;
        }

        internal T ReadNative<T>()
        {
            if (Kind != NativeType<T>.Kind) { throw new InvalidOperationException("TempData storage type mismatch."); }
            if (Kind == TempDataValueKind.BigInteger && typeof(T) == typeof(BigInteger))
            {
                var number = _bigInteger;
                return Unsafe.As<BigInteger, T>(ref number);
            }
            if (Kind is TempDataValueKind.Utf8 or TempDataValueKind.String ||
                RuntimeHelpers.IsReferenceOrContainsReferences<T>() || Unsafe.SizeOf<T>() > 16)
            {
                throw new InvalidOperationException("Unsupported native TempData type.");
            }
            return Unsafe.ReadUnaligned<T>(ref _inline[0]);
        }

        internal readonly bool IsNull => Kind == TempDataValueKind.String && Text is null;

        internal readonly BigInteger BigInteger => _bigInteger;

        [UnscopedRef]
        internal readonly ReadOnlySpan<byte> Bytes => _overflow is not null
            ? _overflow : _inline[..Math.Max(Length, 0)];

        [InlineArray(16)]
        private struct InlineBytes
        {
            private byte _first;
        }
    }

    private enum AccessState { Bound, Active, Closed }
}
