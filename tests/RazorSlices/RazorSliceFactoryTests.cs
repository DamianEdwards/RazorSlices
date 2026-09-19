using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Razor.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace RazorSlices.Tests;

public class RazorSliceFactoryTests
{
    [Fact]
    public void Create_UsesDirectConstructorAndReturnsFreshInstances()
    {
        using var first = RazorSliceFactory.Create(static () => new ConstructorSlice("first"));
        using var second = RazorSliceFactory.Create(static () => new ConstructorSlice("second"));

        Assert.Equal("first", Assert.IsType<ConstructorSlice>(first).Value);
        Assert.Equal("second", Assert.IsType<ConstructorSlice>(second).Value);
        Assert.NotSame(first, second);
        Assert.Null(first.Initialize);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("model")]
    public void Create_AssignsModelInConstructorCallback(string? model)
    {
        using var slice = RazorSliceFactory.Create(model, static value => new ModelSlice { Model = value });

        Assert.IsType<ModelSlice>(slice);
        Assert.Equal(model, slice.Model);
        Assert.Null(slice.Initialize);
    }

    [Fact]
    public void Create_PreservesConstructorExceptions()
    {
        var exception = new InvalidOperationException("Constructor failed.");
        var actual = Assert.Throws<InvalidOperationException>(
            () => RazorSliceFactory.Create<ConstructorSlice>(() => throw exception));

        Assert.Same(exception, actual);
    }

    [Fact]
    public void Create_RejectsNullCallbacks()
    {
        Assert.Throws<ArgumentNullException>(() => RazorSliceFactory.Create<ConstructorSlice>(null!));
        Assert.Throws<ArgumentNullException>(() => RazorSliceFactory.Create<ModelSlice, string?>(null, null!));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Injection_IsDeferredAndUsesExplicitServicesBeforeHttpContext(bool reflection, bool explicitServices)
    {
        var requestService = new TestService();
        var explicitService = new TestService();
        using var requestServices = new ServiceCollection().AddSingleton(requestService).BuildServiceProvider();
        using var services = new ServiceCollection().AddSingleton(explicitService).BuildServiceProvider();
        using var slice = CreateInjectableSlice(reflection);
        slice.HttpContext = new DefaultHttpContext { RequestServices = requestServices };
        if (explicitServices)
        {
            slice.ServiceProvider = services;
        }

        Assert.Null(slice.Service);
        await slice.ExecuteAsyncImpl();

        Assert.Same(explicitServices ? explicitService : requestService, slice.Service);
        Assert.Null(slice.Optional);
        Assert.Null(slice.OptionalValue);
        Assert.Null(slice.MvcService);

        using var emptyServices = new ServiceCollection().BuildServiceProvider();
        slice.ServiceProvider = emptyServices;
        await slice.ExecuteAsyncImpl();
        Assert.Same(explicitServices ? explicitService : requestService, slice.Service);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Injection_ResolvesOptionalServices(bool reflection)
    {
        var optional = new OptionalService();
        using var services = new ServiceCollection()
            .AddSingleton(new TestService())
            .AddSingleton(optional)
            .BuildServiceProvider();
        using var slice = CreateInjectableSlice(reflection);
        slice.ServiceProvider = services;

        await slice.ExecuteAsyncImpl();

        Assert.Same(optional, slice.Optional);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Injection_ThrowsWithoutServiceProvider(bool reflection)
    {
        using var slice = CreateInjectableSlice(reflection);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => slice.ExecuteAsyncImpl());

        Assert.Contains("ServiceProvider property is null", exception.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Injection_ThrowsForMissingRequiredService(bool reflection)
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        using var slice = CreateInjectableSlice(reflection);
        slice.ServiceProvider = services;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => slice.ExecuteAsyncImpl());

        Assert.Contains(nameof(TestService), exception.Message);
    }

    [Fact]
    public async Task HotReload_ReplacesConstructionAndInjectionTogether()
    {
        var original = RazorSliceFactory.SliceCache<OriginalSlice>.Current;
        try
        {
            using var before = RazorSliceFactory.Create(static () => new OriginalSlice());
            RazorSliceFactory.SliceCache<OriginalSlice>.ReplaceSliceType([typeof(ReplacementSlice)]);
            using var after = RazorSliceFactory.Create<OriginalSlice>(static () => throw new InvalidOperationException("Old constructor called."));
            using var services = new ServiceCollection().AddSingleton(new TestService()).BuildServiceProvider();
            after.ServiceProvider = services;
            await after.ExecuteAsyncImpl();

            Assert.IsType<OriginalSlice>(before);
            Assert.Null(before.Initialize);
            Assert.NotNull(Assert.IsType<ReplacementSlice>(after).Service);

            RazorSliceFactory.SliceCache<OriginalSlice>.ReplaceSliceType([typeof(SecondReplacementSlice)]);
            using var next = RazorSliceFactory.Create(static () => new OriginalSlice());
            Assert.IsType<SecondReplacementSlice>(next);
            Assert.Null(next.Initialize);
        }
        finally
        {
            RazorSliceFactory.SliceCache<OriginalSlice>.Current = original;
        }
    }

    [Fact]
    public void HotReload_PreservesModelOnReplacement()
    {
        var original = RazorSliceFactory.SliceCache<ModelSlice>.Current;
        try
        {
            RazorSliceFactory.SliceCache<ModelSlice>.ReplaceSliceType([typeof(ReplacementModelSlice)]);
            using var slice = RazorSliceFactory.Create<ModelSlice, string?>("updated", static value => new ModelSlice { Model = value });

            Assert.IsType<ReplacementModelSlice>(slice);
            Assert.Equal("updated", slice.Model);
        }
        finally
        {
            RazorSliceFactory.SliceCache<ModelSlice>.Current = original;
        }
    }

    [Fact]
    public void HotReload_IgnoresUnrelatedUpdates()
    {
        var original = RazorSliceFactory.SliceCache<ConstructorSlice>.Current;

        RazorSliceFactory.SliceCache<ConstructorSlice>.ReplaceSliceType(null);
        RazorSliceFactory.SliceCache<ConstructorSlice>.ReplaceSliceType([typeof(ReplacementSlice)]);

        Assert.Same(original, RazorSliceFactory.SliceCache<ConstructorSlice>.Current);
    }

    private static InjectableSlice CreateInjectableSlice(bool reflection)
    {
        var slice = (InjectableSlice)RazorSliceFactory.Create(static () => new InjectableSlice());
        if (reflection)
        {
            slice.Initialize = RazorSliceFactory.GetReflectionInitAction(
                typeof(InjectableSlice), RazorSliceFactory.GetInjectableProperties(typeof(InjectableSlice)));
        }
        return slice;
    }

    public sealed class ConstructorSlice(string value) : RazorSlice
    {
        public string Value { get; } = value;
        public override Task ExecuteAsync() => Task.CompletedTask;
    }

    public sealed class ModelSlice : RazorSlice<string?>
    {
        public override Task ExecuteAsync() => Task.CompletedTask;
    }

    public abstract class InjectableBaseSlice : RazorSlice
    {
        [RazorInject]
        public TestService Service { get; set; } = null!;
    }

    public sealed class InjectableSlice : InjectableBaseSlice
    {
        [RazorInject]
        public OptionalService? Optional { get; set; }

        [RazorInject]
        public int? OptionalValue { get; set; }

        [RazorInject]
        public Microsoft.AspNetCore.Mvc.IUrlHelper MvcService { get; set; } = null!;

        public override Task ExecuteAsync() => Task.CompletedTask;
    }

    public sealed class TestService;
    public sealed class OptionalService;

    public sealed class OriginalSlice : RazorSlice
    {
        public override Task ExecuteAsync() => Task.CompletedTask;
    }

    [MetadataUpdateOriginalType(typeof(OriginalSlice))]
    public sealed class ReplacementSlice : InjectableBaseSlice
    {
        public override Task ExecuteAsync() => Task.CompletedTask;
    }

    [MetadataUpdateOriginalType(typeof(OriginalSlice))]
    public sealed class SecondReplacementSlice : RazorSlice
    {
        public override Task ExecuteAsync() => Task.CompletedTask;
    }

    [MetadataUpdateOriginalType(typeof(ModelSlice))]
    public sealed class ReplacementModelSlice : RazorSlice<string?>
    {
        public override Task ExecuteAsync() => Task.CompletedTask;
    }
}
