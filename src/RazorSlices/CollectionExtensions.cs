using System.Collections.ObjectModel;

namespace RazorSlices;

#if NET6_0
internal static class CollectionExtensions
{
    public static ReadOnlyDictionary<TKey, TValue> AsReadOnly<TKey, TValue>(this IDictionary<TKey, TValue> dictionary) where TKey : notnull
    {
        return new ReadOnlyDictionary<TKey, TValue>(dictionary);
    }
}
#endif
