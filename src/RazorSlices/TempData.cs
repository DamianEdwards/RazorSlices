using System.Diagnostics.CodeAnalysis;
using System.Globalization;
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
[Experimental("RSLICE001")]
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
        Store(key, value is null ? new("string", null) : new("string", Utf8.GetBytes(value)));
    }

    /// <summary>Stores a Boolean without boxing.</summary>
    public void Set(string key, bool value)
    {
        EnsureAccess();
        Store(key, new(TypeName<bool>(), value ? "true"u8 : "false"u8));
    }

    /// <summary>
    /// Stores a built-in numeric, date/time, or GUID value without boxing. Uses invariant culture and round-trip
    /// formats. Custom types should use SetUtf8 instead.
    /// </summary>
    public void Set<T>(string key, T value)
        where T : IUtf8SpanFormattable, ISpanParsable<T>
    {
        if (!TypeInfo<T>.IsBuiltIn)
        {
            EnsureAccess();
            throw new InvalidOperationException("Custom TempData types must support UTF-8 parsing and use SetUtf8.");
        }
        SetCore(key, value);
    }

    /// <summary>Stores a custom value supporting both UTF-8 formatting and UTF-8 parsing without boxing.</summary>
    public void SetUtf8<T>(string key, T value)
        where T : IUtf8SpanFormattable, IUtf8SpanParsable<T>
        => SetCore(key, value);

    private void SetCore<T>(string key, T value) where T : IUtf8SpanFormattable
    {
        EnsureAccess();
        ArgumentNullException.ThrowIfNull(key);
        if (!typeof(T).IsValueType && value is null) { throw new ArgumentNullException(nameof(value)); }
        Span<byte> scratch = stackalloc byte[128];
        var format = typeof(T) == typeof(DateTime) || typeof(T) == typeof(DateTimeOffset) ||
            typeof(T) == typeof(DateOnly) || typeof(T) == typeof(TimeOnly) ? "O"
            : typeof(T) == typeof(TimeSpan) ? "c"
            : typeof(T) == typeof(float) || typeof(T) == typeof(double) || typeof(T) == typeof(Half) ? "R" : null;
        if (value.TryFormat(scratch, out var written, format, CultureInfo.InvariantCulture))
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
                if (value.TryFormat(buffer.AsSpan(0, length), out written, format, CultureInfo.InvariantCulture))
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
        if (typeof(T) == typeof(DateTime))
        {
            if (!System.Buffers.Text.Utf8Parser.TryParse(entry.Bytes, out DateTime date, out var consumed, 'O')
                || consumed != entry.Length)
            {
                throw new FormatException($"TempData key '{key}' contains an invalid DateTime.");
            }
            value = Unsafe.As<DateTime, T>(ref date);
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
        var value = entry.IsNull ? null : Utf8.GetString(entry.Bytes);
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
        var length = Utf8.GetCharCount(entry.Bytes);
        char[]? rented = null;
        Span<char> text = length <= 256 ? stackalloc char[length]
            : (rented = System.Buffers.ArrayPool<char>.Shared.Rent(length)).AsSpan(0, length);
        try
        {
            Utf8.GetChars(entry.Bytes, text);
            if (typeof(T) == typeof(DateTime))
            {
                var date = DateTime.ParseExact(text, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                value = System.Runtime.CompilerServices.Unsafe.As<DateTime, T>(ref date);
            }
            else
            {
                value = T.Parse(text, CultureInfo.InvariantCulture);
            }
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
        internal static readonly bool IsBuiltIn =
            (Type.IsPrimitive && Type != typeof(char)) || Type == typeof(decimal) ||
            Type == typeof(Int128) || Type == typeof(UInt128) || Type == typeof(Half) ||
            Type == typeof(System.Numerics.BigInteger) || Type == typeof(System.Numerics.Complex) || Type == typeof(DateTime) ||
            Type == typeof(DateTimeOffset) || Type == typeof(DateOnly) || Type == typeof(TimeOnly) ||
            Type == typeof(TimeSpan) || Type == typeof(Guid);
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
        internal readonly string Type;
        internal readonly int Length;
        internal bool Consumed;

        internal Entry(string type, byte[]? value) : this(type, value.AsSpan())
        {
            if (value is null) { Length = -1; }
        }

        internal Entry(string type, ReadOnlySpan<byte> value)
        {
            this = default;
            Type = type;
            Length = value.Length;
            if (value.Length <= 64) { value.CopyTo(_inline); }
            else { _overflow = value.ToArray(); }
        }

        internal readonly bool IsNull => Length < 0;

        [UnscopedRef]
        internal readonly ReadOnlySpan<byte> Bytes => _overflow is not null
            ? _overflow : _inline[..Math.Max(Length, 0)];

        [InlineArray(64)]
        private struct InlineBytes
        {
            private byte _first;
        }
    }

    private enum AccessState { Bound, Active, Closed }
}
