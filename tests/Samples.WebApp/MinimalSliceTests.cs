using Microsoft.AspNetCore.Http;

namespace RazorSlices.Samples.WebApp.Tests;

public class MinimalSliceTests
{
    [Fact]
    public async Task MinimalProxyHasTypedModelAndEncodesConditionalAttributes()
    {
        var slice = Create<Minimal.Greeting, string>("<model>");
        Assert.Equal("<model>", slice.Model);
        Assert.Equal("""<p class="minimal &lt;model&gt;">&lt;model&gt;</p>""", (await slice.RenderAsync()).Trim());
    }

#if NET10_0_OR_GREATER
    [Fact]
    public void ResultExtensionsAcceptMinimalInstancesWithoutWrappingThem()
    {
        using var slice = Minimal.Greeting.Create("result");
        var encoder = System.Text.Encodings.Web.HtmlEncoder.Create(System.Text.Unicode.UnicodeRanges.All);
        slice.HtmlEncoder = encoder;
        Assert.Same(slice, Microsoft.AspNetCore.Http.TypedResults.RazorSlice(slice, 201));
        Assert.Equal(201, slice.StatusCode);
        Assert.Same(encoder, slice.HtmlEncoder);
        Assert.Same(slice, Microsoft.AspNetCore.Http.Results.RazorSlice(slice, 202));
        Assert.Equal(202, slice.StatusCode);
        slice.HtmlEncoder = null;
        Assert.Same(slice, Microsoft.AspNetCore.Http.TypedResults.RazorSlice(slice));
        Assert.Same(System.Text.Encodings.Web.HtmlEncoder.Default, slice.HtmlEncoder);
    }
#endif

#if DEBUG
    [Fact(Skip = "Debug tracing allocates; measure SDK slices in Release.")]
#else
    [Fact]
#endif
    public void ActualSdkSlicesHaveExpectedOptimizedAllocationFootprint()
    {
        if (IntPtr.Size != 8) { return; }
        var minimal = Allocation(static () => Minimal.Empty.Create());
        var full = Allocation(static () => Minimal.FullEmpty.Create());
        Assert.Equal(104, minimal);
        Assert.Equal(168, full);
    }

    private static RazorSliceBase<TModel> Create<TProxy, TModel>(TModel model)
        where TProxy : IRazorSliceBaseProxy<TModel>
    {
        return TProxy.CreateSlice(model);
    }

    private static long Allocation(Func<RazorSliceBase> create)
    {
        for (var i = 0; i < 1_000; i++) { Consume(create().RenderAsync(TextWriter.Null)); }
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 10_000; i++) { Consume(create().RenderAsync(TextWriter.Null)); }
        return (GC.GetAllocatedBytesForCurrentThread() - before) / 10_000;
    }

    private static void Consume(ValueTask task)
    {
        Assert.True(task.IsCompletedSuccessfully);
#pragma warning disable CA1849 // The render has already completed successfully.
        task.GetAwaiter().GetResult();
#pragma warning restore CA1849
    }
}
