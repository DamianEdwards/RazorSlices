using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using System.Threading.Tasks.Sources;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Razor.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace RazorSlices.Tests;

public class RazorSliceHelperTests : IDisposable
{
    private readonly ServiceProvider _services = new ServiceCollection().AddRazorSliceHelpers().BuildServiceProvider();

    public void Dispose()
    {
        _services.Dispose();
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReusesHelperAndPreservesWriterEncodingAttributesAndAsync(bool pipe)
    {
        SampleHelper? captured = null;
        var slice = CreateHost(async context =>
        {
            var first = context.GetHelper<ISample, SampleHelper>(static () => new());
            var second = context.GetHelper<ISample, SampleHelper>(static () => throw new Exception("Allocated twice"));
            Assert.Same(first, second);
            captured = Assert.IsType<SampleHelper>(first);
            Assert.Null(captured.Service);
            first.Render("<\u00e9>", true);
            await new HelperCompletion(first.RenderAsync("async"));
        });

        var html = await Render(slice, pipe, HtmlEncoder.Create(UnicodeRanges.All));

        Assert.Equal("<input checked=\"checked\" value=\"&lt;\u00e9&gt;\" />True|async", html);
        Assert.Throws<InvalidOperationException>(() => captured!.Render("after", false));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailureAlwaysUnbindsHelpers(bool asynchronous)
    {
        SampleHelper? helper = null;
        var slice = CreateHost(async context =>
        {
            helper = (SampleHelper)context.GetHelper<ISample, SampleHelper>(static () => new());
            if (asynchronous)
            {
                await Task.Yield();
            }

            throw new InvalidOperationException("failed");
        });

        var error = await Assert.ThrowsAsync<InvalidOperationException>(async () => await slice.RenderAsync(TextWriter.Null));
        Assert.Equal("failed", error.Message);
        Assert.Throws<InvalidOperationException>(() => helper!.Render("after", false));
        Assert.Throws<InvalidOperationException>(() => slice.Context.CurrentSlice);
    }

    [Fact]
    public async Task SynchronousExecuteFailureUnbindsHelpers()
    {
        SampleHelper? helper = null;
        var slice = CreateHost(context =>
        {
            helper = (SampleHelper)context.GetHelper<ISample, SampleHelper>(static () => new());
            throw new InvalidOperationException("sync");
        });
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await slice.RenderAsync(TextWriter.Null));
        Assert.Throws<InvalidOperationException>(() => helper!.Render("after", false));
    }

    [Fact]
    public async Task CompletionConsumesValueTaskAndPropagatesErrorsAndCancellation()
    {
        var source = new CompletedSource();
        Assert.Same(HtmlString.Empty, await new HelperCompletion(new ValueTask(source, 0)));
        Assert.Equal(1, source.Consumptions);
        var error = new InvalidOperationException("expected");
        Assert.Same(error, await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await new HelperCompletion(Task.FromException(error))));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await new HelperCompletion(Task.FromCanceled(new CancellationToken(true))));
    }

    [Fact]
    public async Task HelperCancellationUsesCallerToken()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var slice = CreateHost(async context =>
            await new HelperCompletion(context.GetHelper<ISample, SampleHelper>(static () => new()).RenderAsync("cancel")));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await slice.RenderAsync(TextWriter.Null, cancellationToken: cancellation.Token));
    }

    [Fact]
    public async Task HotReloadSubstitutesImplementationThroughStableContract()
    {
        var before = CreateHost(context =>
        {
            Assert.IsType<Original>(context.GetHelper<ISample, Original>(static () => new()));
            return Task.CompletedTask;
        });
        await before.RenderAsync(TextWriter.Null);
        RazorSliceHelperActivation<Original>.Replace([typeof(Updated)]);
        var after = CreateHost(context =>
        {
            Assert.IsType<Updated>(context.GetHelper<ISample, Original>(static () => throw new Exception("Old factory")));
            return Task.CompletedTask;
        });
        using var request = _services.CreateScope();
        after.ServiceProvider = request.ServiceProvider;
        await after.RenderAsync(TextWriter.Null);
    }

    [Fact]
    public async Task WarmHelperLookupAndCompletionDoNotAllocatePerCall()
    {
        var slice = CreateHost(async context =>
        {
            Func<SampleHelper> create = static () => new();
            for (var i = 0; i < 10_000; i++)
            {
                await new HelperCompletion(context.GetHelper<ISample, SampleHelper>(create).Complete());
            }

            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 100_000; i++)
            {
                await new HelperCompletion(context.GetHelper<ISample, SampleHelper>(create).Complete());
            }

            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Equal(0, allocated);
        });
        await slice.RenderAsync(TextWriter.Null);
    }

    private Host CreateHost(Func<IHelperManager, Task> render)
    {
        Host slice = new(render)
        {
            Activation = RazorSliceFactory.GetActivation(typeof(Host)),
            ServiceProvider = _services
        };
        return slice;
    }

    private static async Task<string> Render(RazorSlice slice, bool pipe, HtmlEncoder encoder)
    {
        if (!pipe)
        {
            return await slice.RenderAsync(encoder);
        }

        using var stream = new MemoryStream();
        var writer = PipeWriter.Create(stream, new StreamPipeWriterOptions(leaveOpen: true));
        await slice.RenderAsync(writer, encoder);
        await writer.CompleteAsync();
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private sealed class Host(Func<IHelperManager, Task> render) : RazorSlice
    {
        [RazorInject]
        public IHelperManager Helpers { get; set; } = null!;
        internal IHelperManager Context => Helpers;
        public override Task ExecuteAsync()
        {
            return render(Helpers);
        }
    }

    public interface ISample
    {
        void Render(string text, bool check);
        ValueTask RenderAsync(string text);
        ValueTask Complete();
    }

    public class SampleHelper : RazorSliceHelpers, ISample
    {
        [RazorInject] public object? Service { get; set; }
        public override Task ExecuteAsync()
        {
            return Task.CompletedTask;
        }

        public void Render(string text, bool check)
        {
            WriteLiteral("<input");
            BeginWriteAttribute("checked", " checked=\"", 0, "\"", 0, 1);
            WriteAttributeValue("", 0, check, 0, 0, false);
            EndWriteAttribute();
            BeginWriteAttribute("value", " value=\"", 0, "\"", 0, 1);
            WriteAttributeValue("", 0, text, 0, 0, false);
            EndWriteAttribute();
            WriteLiteral(" />");
            Write(check);
        }
        public async ValueTask RenderAsync(string text)
        {
            CancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            WriteLiteral("|");
            Write(text);
            await FlushAsync();
        }
        public ValueTask Complete()
        {
            return ValueTask.CompletedTask;
        }
    }

    public sealed class Original : SampleHelper { }
    [MetadataUpdateOriginalType(typeof(Original))]
    public sealed class Updated : SampleHelper { }

    private sealed class CompletedSource : IValueTaskSource
    {
        internal int Consumptions;
        public void GetResult(short token)
        {
            Consumptions++;
        }

        public ValueTaskSourceStatus GetStatus(short token)
        {
            return ValueTaskSourceStatus.Succeeded;
        }

        public void OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
        {
            throw new InvalidOperationException("Already complete");
        }
    }
}
