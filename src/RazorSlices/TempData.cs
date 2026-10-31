using System.Collections;
using System.Reflection;
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace RazorSlices;

/// <summary>
/// A bindable, request-local TempData dictionary accessible only while a WithTempData endpoint filter is active.
/// Pass retrieved values, not this dictionary, to templates. The filter owns loading and saving.
/// </summary>
[SuppressMessage("Naming", "CA1710", Justification = "TempData is the established API name for this route-handler parameter.")]
public sealed class TempData : IDictionary<string, object?>, IBindableFromHttpContext<TempData>
{
    private static readonly object RequestKey = new();
    private readonly HttpContext _context;
    private ITempDataDictionary? _dictionary;
    private AccessState _state;

    private TempData(HttpContext context) => _context = context;

    /// <summary>Binds the same guarded TempData instance to all TempData parameters in a request.</summary>
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

    /// <summary>Gets and consumes a value, or sets a value for the next request.</summary>
    public object? this[string key]
    {
        get => Dictionary[key];
        set => Dictionary[key] = value;
    }

    /// <summary>Gets guarded keys. Captured collection views cannot be used after the handler returns.</summary>
    public ICollection<string> Keys
    {
        get
        {
            EnsureAccess();
            return new GuardedCollection<string>(this, static data => data.Keys);
        }
    }

    /// <summary>Gets guarded values. Captured collection views cannot be used after the handler returns.</summary>
    public ICollection<object?> Values
    {
        get
        {
            EnsureAccess();
            return new GuardedCollection<object?>(this, static data => data.Values);
        }
    }

    /// <inheritdoc />
    public int Count => Dictionary.Count;
    /// <inheritdoc />
    public bool IsReadOnly => Dictionary.IsReadOnly;

    /// <summary>Gets a value without marking it as consumed.</summary>
    public object? Peek(string key) => Dictionary.Peek(key);

    /// <summary>Retains all values for the next request.</summary>
    public void Keep() => Dictionary.Keep();

    /// <summary>Retains a value for the next request.</summary>
    public void Keep(string key) => Dictionary.Keep(key);

    /// <inheritdoc />
    public void Add(string key, object? value) => Dictionary.Add(key, value);
    /// <inheritdoc />
    public bool ContainsKey(string key) => Dictionary.ContainsKey(key);
    /// <inheritdoc />
    public bool Remove(string key) => Dictionary.Remove(key);
    /// <summary>Gets a value and marks it consumed, matching MVC TempData.</summary>
    public bool TryGetValue(string key, out object? value) => Dictionary.TryGetValue(key, out value);
    /// <inheritdoc />
    public void Add(KeyValuePair<string, object?> item) => Dictionary.Add(item);
    /// <inheritdoc />
    public void Clear() => Dictionary.Clear();
    /// <inheritdoc />
    public bool Contains(KeyValuePair<string, object?> item) => Dictionary.Contains(item);
    /// <inheritdoc />
    public void CopyTo(KeyValuePair<string, object?>[] array, int arrayIndex) => Dictionary.CopyTo(array, arrayIndex);
    /// <inheritdoc />
    public bool Remove(KeyValuePair<string, object?> item) => Dictionary.Remove(item);
    /// <summary>Enumerates entries, marking each consumed when its Current value is accessed.</summary>
    public IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
        => new GuardedEnumerator<KeyValuePair<string, object?>>(this, Dictionary.GetEnumerator());
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    internal static TempData GetOrCreate(HttpContext context)
    {
        if (context.Items.TryGetValue(RequestKey, out var existing)) { return (TempData)existing!; }
        var data = new TempData(context);
        context.Items.Add(RequestKey, data);
        return data;
    }

    internal void Activate()
    {
        if (_state != AccessState.Bound)
        {
            throw new InvalidOperationException("The request's TempData access window cannot be reopened.");
        }
        EnsureResponseNotStarted();
        _state = AccessState.Active;
    }

    internal void Close() => _state = AccessState.Closed;

    internal void Save()
    {
        if (_dictionary is null) { return; }
        EnsureResponseNotStarted();
        _dictionary.Save();
    }

    private ITempDataDictionary Dictionary
    {
        get
        {
            EnsureAccess();
            return _dictionary ??= (_context.RequestServices.GetService<ITempDataDictionaryFactory>()
                ?? throw new InvalidOperationException(
                    "TempData requires AddRazorSliceTempData() or existing MVC TempData service registrations."))
                .GetTempData(_context);
        }
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
            throw new InvalidOperationException(
                "TempData must be accessed and saved before the response starts. Return a result instead of writing to the response inside the handler.");
        }
    }

    private enum AccessState { Bound, Active, Closed }

    private sealed class GuardedCollection<T>(
        TempData owner, Func<ITempDataDictionary, ICollection<T>> select) : ICollection<T>
    {
        private ICollection<T> Collection => select(owner.Dictionary);
        public int Count => Collection.Count;
        public bool IsReadOnly => Collection.IsReadOnly;
        public void Add(T item) => Collection.Add(item);
        public void Clear() => Collection.Clear();
        public bool Contains(T item) => Collection.Contains(item);
        public void CopyTo(T[] array, int arrayIndex) => Collection.CopyTo(array, arrayIndex);
        public bool Remove(T item) => Collection.Remove(item);
        public IEnumerator<T> GetEnumerator() => new GuardedEnumerator<T>(owner, Collection.GetEnumerator());
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class GuardedEnumerator<T>(TempData owner, IEnumerator<T> inner) : IEnumerator<T>
    {
        public T Current
        {
            get
            {
                owner.EnsureAccess();
                return inner.Current;
            }
        }
        object? IEnumerator.Current => Current;
        public bool MoveNext()
        {
            owner.EnsureAccess();
            return inner.MoveNext();
        }
        public void Reset()
        {
            owner.EnsureAccess();
            inner.Reset();
        }
        public void Dispose() => inner.Dispose();
    }
}
