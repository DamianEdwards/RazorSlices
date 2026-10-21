using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Mvc.Razor.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace RazorSlices.Tests;

public class GeneratedActivationTests
{
    [Fact]
    public async Task PlainPlanDoesNotInspectAttributedProperties()
    {
        var plan = new RazorSliceActivation<Plain>();
        using var slice = RazorSliceFactory.Create(static () => new Plain(), plan);
        Assert.Null(slice.Initialize);
        Assert.Null(slice.Activation);
        await slice.RenderAsync(TextWriter.Null);
    }

    [Fact]
    public async Task TypedInitializerAndBindingsAreUsedWithoutReflection()
    {
        var tracker = new Tracker();
        using var services = new ServiceCollection().AddSingleton(tracker).BuildServiceProvider();
        var plan = new RazorSliceActivation<Original>(
            static (slice, provider, _) => ((Original)slice).Service = provider!.GetRequiredService<Tracker>(),
            static slice => ((Original)slice).Service.Bind(slice),
            static slice => { ((Original)slice).Service.Unbind(slice); return null; });
        using var root = (Original)RazorSliceFactory.Create(static () => new Original(), plan);
        root.ServiceProvider = services;
        await root.RenderAsync(TextWriter.Null);
        Assert.Equal(1, tracker.Binds);
        Assert.Equal(1, tracker.Unbinds);
    }

    [Fact]
    public async Task HotReloadReplacementUsesItsOwnCompleteRuntimeActivation()
    {
        var plan = new RazorSliceActivation<Original>(static (_, _, _) => throw new Exception("Old initializer"));
        plan.ReplaceSliceType([typeof(Replacement)]);
        using var services = new ServiceCollection().AddSingleton(new Tracker()).BuildServiceProvider();
        using var root = RazorSliceFactory.Create<Original>(static () => throw new Exception("Old constructor"), plan);
        root.ServiceProvider = services;
        await root.RenderAsync(TextWriter.Null);
        Assert.IsType<Replacement>(root);
        Assert.NotNull(((Replacement)root).Service);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RuntimeFallbackCanSetInheritedPrivateGenericProperty(bool reflection)
    {
        using var services = new ServiceCollection().AddSingleton("inherited").BuildServiceProvider();
        using var root = new GenericDerived();
        root.Activation = RazorSliceFactory.GetActivation(typeof(GenericDerived), reflection);
        root.ServiceProvider = services;
        await root.RenderAsync(TextWriter.Null);
        Assert.Equal("inherited", root.Value);
    }

    public sealed class Plain : RazorSlice
    {
        [RazorInject] public object UnsupportedReadOnly => throw new Exception("Must not inspect");
        public override Task ExecuteAsync()
        {
            return Task.CompletedTask;
        }
    }
    public class Tracker : IRazorSliceBindable
    {
        public int Binds;
        public int Unbinds;
        public void Bind(RazorSlice slice)
        {
            Binds++;
        }

        public void Unbind(RazorSlice slice)
        {
            Unbinds++;
        }
    }
    public sealed class Original : RazorSlice
    {
        public Tracker Service { get; set; } = null!;
        public override Task ExecuteAsync()
        {
            return Task.CompletedTask;
        }
    }
    [MetadataUpdateOriginalType(typeof(Original))]
    public sealed class Replacement : RazorSlice
    {
        [RazorInject] public Tracker Service { get; set; } = null!;
        public override Task ExecuteAsync()
        {
            return Task.CompletedTask;
        }
    }
    public abstract class GenericBase<T> : RazorSlice
    {
        [RazorInject] public T Value { get; private set; } = default!;
    }
    public sealed class GenericDerived : GenericBase<string>
    {
        public override Task ExecuteAsync()
        {
            return Task.CompletedTask;
        }
    }
}
