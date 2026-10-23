using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Razor.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace RazorSlices.Tests;

public class RazorSliceBaseTests
{
    [Fact]
    public async Task ModelFactoryAndAllOutputAdaptersPreserveEncoding()
    {
        RazorSliceBase<string> Create()
        {
            return RazorSliceBaseFactory.Create("<model>", static model => new MinimalModel { Model = model });
        }
        Assert.Equal("<p>&lt;model&gt;</p>", await Create().RenderAsync());
        var builder = new StringBuilder();
        await Create().RenderAsync(builder);
        Assert.Equal("<p>&lt;model&gt;</p>", builder.ToString());
        using var stream = new MemoryStream();
        await Create().RenderAsync(stream);
        Assert.Equal("<p>&lt;model&gt;</p>", Encoding.UTF8.GetString(stream.ToArray()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailuresDisposeAndPropagate(bool asynchronous)
    {
        var failure = new InvalidOperationException("expected");
        var slice = new Minimal(async () =>
        {
            if (asynchronous) { await Task.Yield(); }
            throw failure;
        });
        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(async () => await slice.RenderAsync(TextWriter.Null)));
        Assert.Equal(1, slice.Disposals);
    }

    [Fact]
    public async Task TrulySynchronousFailureDisposesAndPreservesCleanupFailure()
    {
        var body = new InvalidOperationException("body");
        var cleanup = new InvalidOperationException("cleanup");
        var slice = new Minimal(() => throw body) { CleanupFailure = cleanup };
        var aggregate = await Assert.ThrowsAsync<AggregateException>(async () => await slice.RenderAsync(TextWriter.Null));
        Assert.Equal(new Exception[] { body, cleanup }, aggregate.InnerExceptions);
        Assert.Equal(1, slice.Disposals);
    }

    [Fact]
    public async Task CancellationDisposesAndPropagatesToken()
    {
        using var cancellation = new CancellationTokenSource();
        var gate = new TaskCompletionSource();
        var slice = new Minimal(() => gate.Task);
        var pending = slice.RenderAsync(TextWriter.Null, cancellationToken: cancellation.Token);
        Assert.False(pending.IsCompleted);
        Assert.Equal(cancellation.Token, slice.CancellationToken);
        gate.SetCanceled(cancellation.Token);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await pending);
        Assert.Equal(1, slice.Disposals);
    }

    [Fact]
    public async Task AutoFlushRemainsPendingUntilReaderConsumesOutput()
    {
        var pipe = new Pipe(new PipeOptions(pauseWriterThreshold: 1, resumeWriterThreshold: 1));
        var slice = new LargeLiteral();
        var pending = slice.RenderAsync(pipe.Writer);
        Assert.False(pending.IsCompleted);
        Assert.True(slice.Disposed);
        var read = await pipe.Reader.ReadAsync();
        Assert.Equal(20_000, read.Buffer.Length);
        pipe.Reader.AdvanceTo(read.Buffer.End);
        await pending;
        await pipe.Writer.CompleteAsync();
        await pipe.Reader.CompleteAsync();
    }

    [Fact]
    public async Task MinimalChildRestoresCapturedHelperContextAfterAsyncFailure()
    {
        var root = new Parent();
        Assert.Equal("parent", await root.RenderAsync());
        Assert.Equal("child", root.ChildOutput.ToString());
    }

    [Fact]
    public async Task DirectHttpResultWithExplicitEncoderDoesNotResolveRequestServices()
    {
        var context = new DefaultHttpContext { RequestServices = new ThrowingProvider() };
        using var output = new MemoryStream();
        context.Response.Body = output;
        var result = new MinimalModel
        {
            Model = "<http>",
            StatusCode = StatusCodes.Status201Created,
            ContentType = "text/html",
            HtmlEncoder = HtmlEncoder.Default
        };
        Assert.Equal(201, ((IStatusCodeHttpResult)result).StatusCode);
        Assert.Equal("text/html", ((IContentTypeHttpResult)result).ContentType);
        await ((IResult)result).ExecuteAsync(context);
        Assert.Equal(201, context.Response.StatusCode);
        Assert.Equal("<p>&lt;http&gt;</p>", Encoding.UTF8.GetString(output.ToArray()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HttpStartFailureDisposesWithoutExecutingTheTemplate(bool asynchronous)
    {
        var failure = new InvalidOperationException("start");
        var context = new DefaultHttpContext();
        context.Response.Body = new FailedStartStream(failure, asynchronous);
        var slice = new Minimal(() => throw new Exception("Must not execute")) { HtmlEncoder = HtmlEncoder.Default };
        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => ((IResult)slice).ExecuteAsync(context)));
        Assert.Equal(1, slice.Disposals);
    }

    [Fact]
    public async Task DirectHttpResultPreservesFullSliceInjectionAndCustomEncoder()
    {
        using var services = new ServiceCollection().AddSingleton("injected <\u00e9>").BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        using var output = new MemoryStream();
        context.Response.Body = output;
        var slice = RazorSliceFactory.Create(static () => new Injected());
        slice.HtmlEncoder = HtmlEncoder.Create(UnicodeRanges.All);
        await ((IResult)slice).ExecuteAsync(context);
        Assert.Equal("injected &lt;\u00e9&gt;", Encoding.UTF8.GetString(output.ToArray()));
    }

    [Fact]
    public async Task MinimalFactoriesUseHotReloadReplacementTypesAndAssignModels()
    {
        RazorSliceBaseFactory.SliceCache<Original>.ReplaceSliceType([typeof(Replacement)]);
        using var slice = RazorSliceBaseFactory.Create<Original, string>("replacement", static _ => throw new Exception("Old constructor"));
        Assert.IsType<Replacement>(slice);
        Assert.Equal("replacement", await slice.RenderAsync());
    }

    public sealed class Original : RazorSliceBase<string>
    {
        public override Task ExecuteAsync()
        {
            throw new Exception("Old implementation");
        }
    }
    [MetadataUpdateOriginalType(typeof(Original))]
    public sealed class Replacement : RazorSliceBase<string>
    {
        public override Task ExecuteAsync() { Write(Model); return Task.CompletedTask; }
    }

    private sealed class MinimalModel : RazorSliceBase<string>
    {
        public override Task ExecuteAsync()
        {
            WriteLiteral("<p>");
            Write(Model);
            WriteLiteral("</p>");
            return Task.CompletedTask;
        }
    }
    private sealed class Minimal(Func<Task> execute) : RazorSliceBase
    {
        internal int Disposals;
        internal Exception? CleanupFailure;
        public override Task ExecuteAsync()
        {
            return execute();
        }
        public override void Dispose()
        {
            Disposals++;
            base.Dispose();
            if (CleanupFailure is { } failure) { throw failure; }
        }
    }
    private sealed class Parent : RazorSlice
    {
        internal readonly StringWriter ChildOutput = new();
        public override async Task ExecuteAsync()
        {
            var helper = Helpers.GetHelper<IText, Helper>(static () => new());
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await RenderPartialAsync(new Child(helper, ChildOutput)));
            helper.Text("parent");
        }
    }
    private sealed class Child(IText helper, TextWriter output) : RazorSliceBase
    {
        public override async Task ExecuteAsync()
        {
            _textWriter = output;
            await Task.Yield();
            helper.Text("child");
            throw new InvalidOperationException("child");
        }
    }
    private interface IText { void Text(string text); }
    private sealed class Helper : RazorSliceHelpers, IText
    {
        public void Text(string text)
        {
            Write(text);
        }
        public override Task ExecuteAsync()
        {
            return Task.CompletedTask;
        }
    }
    private sealed class ThrowingProvider : IServiceProvider
    {
        public object? GetService(Type serviceType)
        {
            throw new Exception("Request scope must not be resolved");
        }
    }
    private sealed class FailedStartStream(Exception failure, bool asynchronous) : MemoryStream
    {
        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            if (!asynchronous)
            {
                throw failure;
            }
            return Task.FromException(failure);
        }
    }
    private sealed class LargeLiteral : RazorSliceBase
    {
        internal bool Disposed => IsDisposed;
        public override Task ExecuteAsync()
        {
            WriteLiteral(new string('x', 20_000));
            return Task.CompletedTask;
        }
    }
    private sealed class Injected : RazorSlice
    {
        [RazorInject] public string Value { get; set; } = null!;
        public override Task ExecuteAsync()
        {
            Write(Value);
            return Task.CompletedTask;
        }
    }
}
