using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace RazorSlices;

/// <summary>
/// Dictionary for storing items during the rendering of a Razor Slice.
/// </summary>
public class SliceItemsDictionary : IDictionary<string, object?>
{
    private Dictionary<string, object?>? _items;

    private string? _item1Key;
    private string? _item2Key;
    private string? _item3Key;

    private object? _item1Value;
    private object? _item2Value;
    private object? _item3Value;

    /// <inheritdoc/>
    public object? this[string key]
    {
        get
        {
            if (_items is { } items)
            {
                return items.TryGetValue(key, out var item)
                    ? item : null;
            }
            if (string.Equals(key, _item1Key, StringComparison.OrdinalIgnoreCase))
            {
                return _item1Value;
            }
            if (string.Equals(key, _item2Key, StringComparison.OrdinalIgnoreCase))
            {
                return _item2Value;
            }
            if (string.Equals(key, _item3Key, StringComparison.OrdinalIgnoreCase))
            {
                return _item3Value;
            }
            return null;
        }
        set => Add(key, value);
    }

    /// <inheritdoc/>
    public ICollection<string> Keys
    {
        get
        {
            if (_items is { } items)
            {
                return items.Keys;
            }
            else if (!string.IsNullOrEmpty(_item1Key))
            {
                var keys = new List<string>(3)
                {
                    _item1Key
                };
                if (!string.IsNullOrEmpty(_item2Key))
                {
                    keys.Add(_item2Key);
                }
                if (!string.IsNullOrEmpty(_item3Key))
                {
                    keys.Add(_item3Key);
                }

                return keys;
            }
            return [];
        }
    }

    /// <inheritdoc/>
    public ICollection<object?> Values
    {
        get
        {
            if (_items is { } items)
            {
                return items.Values;
            }
            else if (!string.IsNullOrEmpty(_item1Key))
            {
                var values = new List<object?>(3)
                {
                    _item1Value
                };
                if (!string.IsNullOrEmpty(_item2Key))
                {
                    values.Add(_item2Value);
                }
                if (!string.IsNullOrEmpty(_item3Key))
                {
                    values.Add(_item3Value);
                }

                return values;
            }
            return [];
        }
    }

    /// <inheritdoc/>
    public int Count
    {
        get
        {
            if (_items is { } items)
            {
                return items.Count;
            }
            if (!string.IsNullOrEmpty(_item3Key))
            {
                return 3;
            }
            if (!string.IsNullOrEmpty(_item2Key))
            {
                return 2;
            }
            if (!string.IsNullOrEmpty(_item1Key))
            {
                return 1;
            }
            return 0;
        }
    }

    /// <inheritdoc/>
    public bool IsReadOnly => false;

    /// <inheritdoc/>
    public void Add(string key, object? value)
    {
        if (_items is { } items)
        {
            items[key] = value;
            return;
        }
        if (!string.IsNullOrEmpty(_item3Key))
        {
            _items = new Dictionary<string, object?>(5, StringComparer.OrdinalIgnoreCase)
            {
                [_item1Key!] = _item1Value,
                [_item2Key!] = _item2Value,
                [_item3Key] = _item3Value,
                [key] = value
            };
            return;
        }
        if (!string.IsNullOrEmpty(_item2Key))
        {
            _item3Key = key;
            _item3Value = value;
            return;
        }
        if (!string.IsNullOrEmpty(_item1Key))
        {
            _item2Key = key;
            _item2Value = value;
            return;
        }
        _item1Key = key;
        _item1Value = value;
    }

    /// <inheritdoc/>
    public void Add(KeyValuePair<string, object?> item) => Add(item.Key, item.Value);

    /// <inheritdoc/>
    public void Clear()
    {
        _items?.Clear();
        _item1Key = null;
        _item1Value = null;
        _item2Key = null;
        _item2Value = null;
        _item3Key = null;
        _item3Value = null;
    }

    /// <inheritdoc/>
    public bool Contains(KeyValuePair<string, object?> item)
    {
        if (_items is { } items)
        {
            return items.Contains(item);
        }
        if (string.Equals(_item1Key, item.Key, StringComparison.OrdinalIgnoreCase))
        {
            return EqualityComparer<object?>.Default.Equals(_item1Value, item.Value);
        }
        if (string.Equals(_item2Key, item.Key, StringComparison.OrdinalIgnoreCase))
        {
            return EqualityComparer<object?>.Default.Equals(_item2Value, item.Value);
        }
        if (string.Equals(_item3Key, item.Key, StringComparison.OrdinalIgnoreCase))
        {
            return EqualityComparer<object?>.Default.Equals(_item3Value, item.Value);
        }
        return false;
    }

    /// <inheritdoc/>
    public bool ContainsKey(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return false;
        }
        if (_items is { } items)
        {
            return items.ContainsKey(key);
        }
        if (string.Equals(_item1Key, key, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        if (string.Equals(_item2Key, key, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        if (string.Equals(_item3Key, key, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        return false;
    }

    /// <inheritdoc/>
    public void CopyTo(KeyValuePair<string, object?>[] array, int arrayIndex)
    {
        if (_items is { } items)
        {
            ((IDictionary<string, object?>)items).CopyTo(array, arrayIndex);
            return;
        }
        if (!string.IsNullOrEmpty(_item1Key))
        {
            array[arrayIndex] = new(_item1Key, _item1Value);
            arrayIndex++;
        }
        if (!string.IsNullOrEmpty(_item2Key))
        {
            array[arrayIndex] = new(_item2Key, _item2Value);
            arrayIndex++;
        }
        if (!string.IsNullOrEmpty(_item3Key))
        {
            array[arrayIndex] = new(_item3Key, _item3Value);
        }
    }

    /// <inheritdoc/>
    public IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
    {
        if (_items is { } items)
        {
            return items.GetEnumerator();
        }
        else if (!string.IsNullOrEmpty(_item1Key))
        {
            var values = new List<KeyValuePair<string, object?>>(3)
                {
                    new(_item1Key, _item1Value)
                };
            if (!string.IsNullOrEmpty(_item2Key))
            {
                values.Add(new(_item2Key, _item2Value));
            }
            if (!string.IsNullOrEmpty(_item3Key))
            {
                values.Add(new(_item3Key, _item3Value));
            }

            return values.GetEnumerator();
        }
        return Enumerable.Empty<KeyValuePair<string, object?>>().GetEnumerator();
    }

    /// <inheritdoc/>
    public bool Remove(string key)
    {
        if (_items is { } items)
        {
            return items.Remove(key);
        }
        if (!string.IsNullOrEmpty(_item3Key))
        {
            _item3Key = null;
            _item3Value = null;
            _item2Key = null;
            _item2Value = null;
            _item1Key = null;
            _item1Value = null;
            return true;
        }
        if (!string.IsNullOrEmpty(_item2Key))
        {
            _item2Key = null;
            _item2Value = null;
            _item1Key = null;
            _item1Value = null;
            return true;
        }
        if (!string.IsNullOrEmpty(_item1Key))
        {
            _item1Key = null;
            _item1Value = null;
            return true;
        }
        return false;
    }

    /// <inheritdoc/>
    public bool Remove(KeyValuePair<string, object?> item)
    {
        if (_items is { } items)
        {
            return items.Remove(item.Key, out _);
        }
        if (!string.IsNullOrEmpty(_item3Key)
            && string.Equals(_item3Key, item.Key, StringComparison.OrdinalIgnoreCase)
            && EqualityComparer<object?>.Default.Equals(_item3Value, item.Value))
        {
            _item3Key = null;
            _item3Value = null;
            return true;
        }
        if (!string.IsNullOrEmpty(_item2Key)
            && string.Equals(_item2Key, item.Key, StringComparison.OrdinalIgnoreCase)
            && EqualityComparer<object?>.Default.Equals(_item2Value, item.Value))
        {
            _item2Key = null;
            _item2Value = null;
            return true;
        }
        if (!string.IsNullOrEmpty(_item1Key)
            && string.Equals(_item1Key, item.Key, StringComparison.OrdinalIgnoreCase)
            && EqualityComparer<object?>.Default.Equals(_item1Value, item.Value))
        {
            _item1Key = null;
            _item1Value = null;
            return true;
        }
        return false;
    }

    /// <inheritdoc/>
    public bool TryGetValue(string key, [MaybeNullWhen(false)] out object? value)
    {
        if (_items is { } items)
        {
            return items.TryGetValue(key, out value);
        }
        if (string.IsNullOrEmpty(key))
        {
            value = null;
            return false;
        }
        if (string.Equals(_item1Key, key, StringComparison.OrdinalIgnoreCase))
        {
            value = _item1Value;
            return true;
        }
        if (string.Equals(_item2Key, key, StringComparison.OrdinalIgnoreCase))
        {
            value = _item2Value;
            return true;
        }
        if (string.Equals(_item3Key, key, StringComparison.OrdinalIgnoreCase))
        {
            value = _item3Value;
            return true;
        }
        value = null;
        return false;
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
