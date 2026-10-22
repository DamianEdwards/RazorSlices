namespace RazorSlices.Tests;

public class HelperPayForPlayTests
{
    [Fact]
    public void MinimalSliceHasNoApplicationOrHelperState()
    {
        Assert.False(typeof(Minimal).IsAssignableTo(typeof(RazorSlice)));
        Assert.False(typeof(Minimal).IsAssignableTo(typeof(Microsoft.AspNetCore.Http.IResult)));
        var fields = typeof(RazorSliceBase).GetFields(System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
        Assert.DoesNotContain(fields, static field =>
            field.FieldType == typeof(IServiceProvider) || field.FieldType == typeof(RazorSliceHelperCache) ||
            field.FieldType == typeof(Microsoft.AspNetCore.Http.HttpContext));
    }

#if DEBUG
    [Fact(Skip = "Debug tracing allocates; measure render allocations in Release.")]
#else
    [Fact]
#endif
    public async Task MinimalRenderingDoesNotAllocateBeyondSliceInstances()
    {
        for (var i = 0; i < 1_000; i++) { await new Minimal().RenderAsync(TextWriter.Null); }
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 10_000; i++) { GC.KeepAlive(new Minimal()); }
        var objects = GC.GetAllocatedBytesForCurrentThread() - before;
        before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 10_000; i++) { await new Minimal().RenderAsync(TextWriter.Null); }
        Assert.Equal(objects, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    private sealed class Minimal : RazorSliceBase
    {
        public override Task ExecuteAsync()
        {
            return Task.CompletedTask;
        }
    }
}
