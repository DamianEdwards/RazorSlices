using System.Diagnostics.CodeAnalysis;

namespace RazorSlices;

// Keep the first helper inline and the next two in an array before switching to hashing.
internal sealed class RazorSliceHelperCache
{
    private Type? _firstKey;
    private RazorSliceHelpers? _firstValue;
    private object? _additional;
    private const int ArrayCapacity = 2;

    internal bool TryGetValue(Type key, [NotNullWhen(true)] out RazorSliceHelpers? value)
    {
        if (_firstKey == key) { value = _firstValue!; return true; }
        if (_additional is Dictionary<Type, RazorSliceHelpers> dictionary)
        {
            return dictionary.TryGetValue(key, out value);
        }
        if (_additional is KeyValuePair<Type, RazorSliceHelpers>[] entries)
        {
            foreach (var entry in entries)
            {
                if (entry.Key == key) { value = entry.Value; return true; }
                if (entry.Key is null) { break; }
            }
        }
        value = null;
        return false;
    }

    internal void Add(Type key, RazorSliceHelpers value)
    {
        if (_firstKey is null) { _firstKey = key; _firstValue = value; return; }
        if (_firstKey == key) { throw new ArgumentException("A helper with this key already exists.", nameof(key)); }
        if (_additional is Dictionary<Type, RazorSliceHelpers> dictionary)
        {
            dictionary.Add(key, value);
            return;
        }
        var entries = _additional as KeyValuePair<Type, RazorSliceHelpers>[] ?? [];
        for (var i = 0; i < entries.Length; i++)
        {
            if (entries[i].Key == key) { throw new ArgumentException("A helper with this key already exists.", nameof(key)); }
            if (entries[i].Key is null) { entries[i] = new(key, value); return; }
        }
        if (entries.Length == ArrayCapacity)
        {
            // Skip the three-entry hash capacity already covered by the inline/array storage.
            dictionary = new(7);
            foreach (var entry in entries) { dictionary.Add(entry.Key, entry.Value); }
            dictionary.Add(key, value);
            _additional = dictionary;
            return;
        }
        entries = new KeyValuePair<Type, RazorSliceHelpers>[ArrayCapacity];
        entries[0] = new(key, value);
        _additional = entries;
    }

    internal void Clear()
    {
        _firstValue?.Unbind();
        if (_additional is Dictionary<Type, RazorSliceHelpers> dictionary)
        {
            foreach (var entry in dictionary) { entry.Value.Unbind(); }
        }
        else if (_additional is KeyValuePair<Type, RazorSliceHelpers>[] entries)
        {
            foreach (var entry in entries)
            {
                if (entry.Key is null) { break; }
                entry.Value.Unbind();
            }
        }
        _firstKey = null;
        _firstValue = null;
        _additional = null;
    }
}
