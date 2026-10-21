using Microsoft.AspNetCore.Mvc.Razor.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace RazorSlices.Tests;

public class HelperPayForPlayTests
{
    [Fact]
    public void ScopedResolutionReusesOneManagerWithoutPerInjectionAllocations()
    {
        using var provider = new ServiceCollection().AddRazorSliceHelpers().BuildServiceProvider();
        using var scope = provider.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IHelperManager>();
        for (var i = 0; i < 1_000; i++)
        {
            Assert.Same(manager, scope.ServiceProvider.GetRequiredService<IHelperManager>());
        }
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 10_000; i++)
        {
            Assert.Same(manager, scope.ServiceProvider.GetRequiredService<IHelperManager>());
        }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

#if DEBUG
    [Fact(Skip = "Debug tracing allocates; measure render allocations in Release.")]
#else
    [Fact]
#endif
    public async Task BindingDoesNotAddAnAsyncWrapperAllocation()
    {
        using var provider = new ServiceCollection().AddRazorSliceHelpers().BuildServiceProvider();
        using var scope = provider.CreateScope();
        long plain = 0;
        long bound = 0;
        for (var i = 0; i < 2_000; i++)
        {
            var gate = new TaskCompletionSource();
            var slice = new Plain { Pending = gate.Task };
            var before = GC.GetAllocatedBytesForCurrentThread();
            var plainRender = slice.RenderAsync(TextWriter.Null);
            var bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            gate.SetResult();
            await plainRender;
            if (i >= 1_000) { plain += bytes; }

            gate = new TaskCompletionSource();
            var participating = (Participating)RazorSliceFactory.Create(static () => new Participating());
            participating.Pending = gate.Task;
            participating.ServiceProvider = scope.ServiceProvider;
            before = GC.GetAllocatedBytesForCurrentThread();
            var boundRender = participating.RenderAsync(TextWriter.Null);
            bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            gate.SetResult();
            await boundRender;
            if (i >= 1_000) { bound += bytes; }
        }
        Assert.Equal(plain, bound);
    }

    public class Plain : RazorSlice
    {
        public Task Pending { get; set; } = Task.CompletedTask;
        public override Task ExecuteAsync()
        {
            return Pending;
        }
    }

    public sealed class Participating : Plain
    {
        [RazorInject]
        public IHelperManager Helpers { get; set; } = null!;
    }
}
