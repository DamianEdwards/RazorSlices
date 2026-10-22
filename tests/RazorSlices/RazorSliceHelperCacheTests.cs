namespace RazorSlices.Tests;

public class RazorSliceHelperCacheTests
{
    private static readonly Type[] Keys =
    [
        typeof(bool), typeof(byte), typeof(sbyte), typeof(short), typeof(ushort), typeof(int),
        typeof(uint), typeof(long), typeof(ulong), typeof(float), typeof(double), typeof(decimal),
        typeof(char), typeof(string), typeof(object), typeof(Guid)
    ];

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(16)]
    public void GrowthPreservesEntriesAndClearReleasesThem(int count)
    {
        var cache = new RazorSliceHelperCache();
        var helpers = Enumerable.Range(0, count).Select(static _ => new Helper()).ToArray();
        for (var i = 0; i < count; i++)
        {
            cache.Add(Keys[i], helpers[i]);
            for (var j = 0; j <= i; j++)
            {
                Assert.True(cache.TryGetValue(Keys[j], out var value));
                Assert.Same(helpers[j], value);
            }
            Assert.Throws<ArgumentException>(() => cache.Add(Keys[i], new Helper()));
        }
        Assert.False(cache.TryGetValue(typeof(DateTime), out _));
        cache.Clear();
        foreach (var key in Keys) { Assert.False(cache.TryGetValue(key, out _)); }
        cache.Add(Keys[0], new Helper());
        Assert.True(cache.TryGetValue(Keys[0], out _));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(8)]
    [InlineData(16)]
    public void WarmLookupsDoNotAllocate(int count)
    {
        var cache = new RazorSliceHelperCache();
        for (var i = 0; i < count; i++) { cache.Add(Keys[i], new Helper()); }
        for (var i = 0; i < 10_000; i++) { cache.TryGetValue(Keys[i % count], out _); }
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 100_000; i++) { cache.TryGetValue(Keys[i % count], out _); }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    private sealed class Helper : RazorSliceHelpers
    {
        public override Task ExecuteAsync()
        {
            return Task.CompletedTask;
        }
    }
}
