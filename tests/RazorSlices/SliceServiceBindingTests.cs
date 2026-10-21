using Microsoft.AspNetCore.Mvc.Razor.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace RazorSlices.Tests;

public class SliceServiceBindingTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task SharedServiceRestoresParentAndPopulatesPropertiesBeforeBinding(bool reflection, bool asynchronous)
    {
        var tracker = new Tracker();
        using var provider = new ServiceCollection().AddSingleton<ITracker>(tracker).AddSingleton(new Ordinary()).BuildServiceProvider();
        Host? parent = null;
        var child = Create(() => new Host(async slice =>
        {
            Assert.Same(slice, tracker.Current);
            Assert.NotNull(slice.Ordinary);
            if (asynchronous) { await Task.Yield(); }
        }), reflection);
        parent = Create(() => new Host(async slice =>
        {
            Assert.Same(slice, tracker.Current);
            await slice.Partial(child);
            Assert.Same(parent, tracker.Current);
        }), reflection);
        parent.ServiceProvider = provider;
        await parent.RenderAsync(TextWriter.Null);
        Assert.Empty(tracker.Frames);
        Assert.Equal(2, tracker.Binds);
        Assert.Equal(2, tracker.Unbinds);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task FailedBindUnwindsCompletedBindings(bool reflection, bool cleanupFailure)
    {
        var tracker = new Tracker { FailUnbind = cleanupFailure };
        using var provider = new ServiceCollection().AddSingleton<ITracker>(tracker)
            .AddSingleton<IFailure>(new Failure { FailBind = true }).AddSingleton(new Ordinary()).BuildServiceProvider();
        var slice = Create(() => new FailingHost(static () => throw new Exception("Must not execute")), reflection);
        slice.ServiceProvider = provider;
        var error = await Assert.ThrowsAnyAsync<Exception>(async () => await slice.RenderAsync(TextWriter.Null));
        Assert.Empty(tracker.Frames);
        Assert.Equal(1, tracker.Unbinds);
        Assert.Contains("bind failed", error.ToString());
        if (cleanupFailure) { Assert.Contains("unbind failed", error.ToString()); }
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(false, 1)]
    [InlineData(false, 2)]
    [InlineData(true, 0)]
    [InlineData(true, 1)]
    [InlineData(true, 2)]
    public async Task RenderErrorsUnwindAllServicesAndPreserveOriginalFailure(bool reflection, int mode)
    {
        var tracker = new Tracker();
        var failure = new Failure { FailUnbind = mode == 1 };
        using var provider = new ServiceCollection().AddSingleton<ITracker>(tracker)
            .AddSingleton<IFailure>(failure).AddSingleton(new Ordinary()).BuildServiceProvider();
        var slice = Create(() => new FailingHost(async () =>
        {
            await Task.Yield();
            if (mode == 2) { throw new OperationCanceledException(); }
            throw new InvalidOperationException("render failed");
        }), reflection);
        slice.ServiceProvider = provider;
        if (mode == 2)
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await slice.RenderAsync(TextWriter.Null));
        }
        else
        {
            var error = await Assert.ThrowsAnyAsync<Exception>(async () => await slice.RenderAsync(TextWriter.Null));
            Assert.Contains("render failed", error.ToString());
            if (mode == 1) { Assert.Contains("unbind failed", error.ToString()); }
        }
        Assert.Empty(tracker.Frames);
        Assert.Empty(failure.Frames);
    }

    [Fact]
    public async Task CustomInitializerRetainsCachedBindingMetadata()
    {
        var tracker = new Tracker();
        using var provider = new ServiceCollection().AddSingleton<ITracker>(tracker).AddSingleton(new Ordinary()).BuildServiceProvider();
        var slice = Create(() => new Host(static _ => Task.CompletedTask), false);
        var initialize = slice.Initialize!;
        var calls = 0;
        slice.Initialize = (instance, services, context) =>
        {
            initialize(instance, services, context);
            calls++;
        };
        slice.ServiceProvider = provider;
        await slice.RenderAsync(TextWriter.Null);
        Assert.Equal(1, calls);
        Assert.Equal(1, tracker.Binds);
        Assert.Equal(1, tracker.Unbinds);
    }

    [Fact]
    public void RegistrationIsScopedAndIdempotent()
    {
        using var provider = new ServiceCollection().AddRazorSliceHelpers().AddRazorSliceHelpers()
            .BuildServiceProvider(validateScopes: true);
        using var first = provider.CreateScope();
        using var second = provider.CreateScope();
        Assert.Same(first.ServiceProvider.GetRequiredService<IHelperManager>(), first.ServiceProvider.GetRequiredService<IHelperManager>());
        Assert.NotSame(first.ServiceProvider.GetRequiredService<IHelperManager>(), second.ServiceProvider.GetRequiredService<IHelperManager>());
        Assert.Single(first.ServiceProvider.GetServices<IHelperManager>());
    }

    private static T Create<T>(Func<T> create, bool reflection) where T : RazorSlice
    {
        var slice = (T)RazorSliceFactory.Create(create);
        if (reflection) { slice.Activation = RazorSliceFactory.GetActivation(typeof(T), forceReflection: true); }
        return slice;
    }

    public interface ITracker : IRazorSliceBindable;
    public interface IFailure : IRazorSliceBindable;
    public class Tracker : ITracker
    {
        public Stack<RazorSlice> Frames { get; } = new();
        public RazorSlice Current => Frames.Peek();
        public int Binds;
        public int Unbinds;
        public bool FailUnbind { get; init; }
        public virtual void Bind(RazorSlice slice) { Frames.Push(slice); Binds++; }
        public void Unbind(RazorSlice slice)
        {
            Assert.Same(slice, Frames.Pop());
            Unbinds++;
            if (FailUnbind) { throw new InvalidOperationException("unbind failed"); }
        }
    }
    public sealed class Failure : Tracker, IFailure
    {
        public bool FailBind { get; init; }
        public override void Bind(RazorSlice slice)
        {
            if (FailBind) { throw new InvalidOperationException("bind failed"); }
            base.Bind(slice);
        }
    }
    public sealed class Ordinary;
    public class Host(Func<Host, Task> execute) : RazorSlice
    {
        [RazorInject] public ITracker Tracker { get; set; } = null!;
        [RazorInject] public Ordinary Ordinary { get; set; } = null!;
        public override Task ExecuteAsync()
        {
            return execute(this);
        }

        public ValueTask<Microsoft.AspNetCore.Html.HtmlString> Partial(RazorSlice child)
        {
            return RenderPartialAsync(child);
        }
    }
    public sealed class FailingHost(Func<Task> execute) : RazorSlice
    {
        [RazorInject] public ITracker Tracker { get; set; } = null!;
        [RazorInject] public Ordinary Ordinary { get; set; } = null!;
        [RazorInject] public IFailure Failure { get; set; } = null!;
        public override Task ExecuteAsync()
        {
            return execute();
        }
    }
}
