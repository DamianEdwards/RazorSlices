using System.IO.Pipelines;
using Microsoft.AspNetCore.Mvc.Razor.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace RazorSlices.Tests;

public class SliceServiceBindingFailureTests
{
    [Fact]
    public async Task LayoutCreationFailureUnbindsServices()
    {
        using var provider = new ServiceCollection().AddRazorSliceHelpers().BuildServiceProvider();
        using var scope = provider.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IHelperManager>();
        var root = RazorSliceFactory.Create(static () => new BadLayout());
        root.ServiceProvider = scope.ServiceProvider;
        var error = await Assert.ThrowsAsync<InvalidOperationException>(async () => await root.RenderAsync(TextWriter.Null));
        Assert.Equal("layout failed", error.Message);
        Assert.Throws<InvalidOperationException>(() => manager.CurrentSlice);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FlushFailureUnbindsServices(bool asynchronous)
    {
        using var provider = new ServiceCollection().AddRazorSliceHelpers().BuildServiceProvider();
        using var scope = provider.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IHelperManager>();
        var root = RazorSliceFactory.Create(static () => new Host());
        root.ServiceProvider = scope.ServiceProvider;
        var writer = new FailingWriter(asynchronous);
        var pending = root.RenderAsync(writer);
        if (asynchronous) { writer.Fail(); }
        var error = await Assert.ThrowsAsync<InvalidOperationException>(async () => await pending);
        Assert.Equal("flush failed", error.Message);
        Assert.Throws<InvalidOperationException>(() => manager.CurrentSlice);
    }

    [Fact]
    public async Task MissingOptionalBindingIsSkipped()
    {
        using var provider = new ServiceCollection().AddRazorSliceHelpers().BuildServiceProvider();
        using var scope = provider.CreateScope();
        var root = (Host)RazorSliceFactory.Create(static () => new Host());
        root.ServiceProvider = scope.ServiceProvider;
        await root.RenderAsync(TextWriter.Null);
        Assert.Null(root.Optional);
    }

    public class Host : RazorSlice
    {
        [RazorInject] public IHelperManager Helpers { get; set; } = null!;
        [RazorInject] public IOptional? Optional { get; set; }
        public override Task ExecuteAsync()
        {
            return Task.CompletedTask;
        }
    }
    public interface IOptional : IRazorSliceBindable;
    public sealed class BadLayout : Host, IUsesLayout<UnusedProxy>
    {
        public RazorSlice CreateLayout()
        {
            throw new InvalidOperationException("layout failed");
        }
    }
    public sealed class UnusedProxy : IRazorSliceProxy
    {
        public static RazorSlice CreateSlice()
        {
            throw new NotSupportedException();
        }
    }
    private sealed class FailingWriter(bool asynchronous) : PipeWriter
    {
        private readonly TaskCompletionSource<FlushResult> _completion = new();
        public override bool CanGetUnflushedBytes => true;
        public override long UnflushedBytes => 16_384;
        public void Fail()
        {
            _completion.SetException(new InvalidOperationException("flush failed"));
        }

        public override ValueTask<FlushResult> FlushAsync(CancellationToken cancellationToken = default)
        {
            return asynchronous ? new(_completion.Task) : ValueTask.FromException<FlushResult>(new InvalidOperationException("flush failed"));
        }

        public override void Advance(int bytes)
        {
            throw new NotSupportedException();
        }

        public override Memory<byte> GetMemory(int sizeHint = 0)
        {
            throw new NotSupportedException();
        }

        public override Span<byte> GetSpan(int sizeHint = 0)
        {
            throw new NotSupportedException();
        }

        public override void CancelPendingFlush()
        {
            throw new NotSupportedException();
        }

        public override void Complete(Exception? exception = null) { }
    }
}
