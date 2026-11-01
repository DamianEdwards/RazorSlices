using System.Text;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace RazorSlices.Tests;

public class RazorSliceIResultTests
{
    [Fact]
    public async Task ExecuteAsync_UsesDefaultHtmlContentType()
    {
        var slice = new TestSlice
        {
            HtmlEncoder = HtmlEncoder.Default
        };
        var httpContext = CreateHttpContext();

        await ((IResult)slice).ExecuteAsync(httpContext);

        Assert.Equal("text/html; charset=utf-8", httpContext.Response.ContentType);
    }

    [Fact]
    public async Task ExecuteAsync_UsesOverriddenContentType()
    {
        var slice = new TestSlice
        {
            ContentType = "application/xml; charset=utf-8",
            HtmlEncoder = HtmlEncoder.Default
        };
        var httpContext = CreateHttpContext();

        await ((IResult)slice).ExecuteAsync(httpContext);

        Assert.Equal("application/xml; charset=utf-8", httpContext.Response.ContentType);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExecuteAsync_ResolvesEncoderAndPreservesStatusAndCancellation(bool minimal)
    {
        using var services = new ServiceCollection().AddSingleton(HtmlEncoder.Create(UnicodeRanges.All)).BuildServiceProvider();
        using var cancellation = new CancellationTokenSource();
        var context = CreateHttpContext();
        context.RequestServices = services;
        context.RequestAborted = cancellation.Token;
        RazorSliceBase slice = minimal ? new MinimalTextSlice() : new FullTextSlice();
        slice.StatusCode = StatusCodes.Status202Accepted;

        await ((IResult)slice).ExecuteAsync(context);

        Assert.Equal(StatusCodes.Status202Accepted, context.Response.StatusCode);
        Assert.Equal("text/html; charset=utf-8", context.Response.ContentType);
        Assert.Equal("&lt;\u00e9&gt;", Encoding.UTF8.GetString(((MemoryStream)context.Response.Body).ToArray()));
        Assert.Equal(cancellation.Token, slice.CancellationToken);
        if (slice is RazorSlice full)
        {
            Assert.Same(context, full.HttpContext);
        }
    }

    [Fact]
    public async Task MinimalResultUsesDefaultEncoderWhenNoEncoderServiceIsRegistered()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var context = CreateHttpContext();
        context.RequestServices = services;
        var slice = new MinimalTextSlice();
        await ((IResult)slice).ExecuteAsync(context);
        Assert.Equal(HtmlEncoder.Default.Encode("<\u00e9>"), Encoding.UTF8.GetString(((MemoryStream)context.Response.Body).ToArray()));
    }

    [Fact]
    public async Task MinimalResultAwaitsResponseStartBeforeExecutingTemplate()
    {
        var context = CreateHttpContext();
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var stream = new PendingStartStream(start.Task);
        context.Response.Body = stream;
        var executed = false;
        var slice = new CallbackSlice(() => executed = true) { HtmlEncoder = HtmlEncoder.Default };
        var pending = ((IResult)slice).ExecuteAsync(context);
        Assert.False(pending.IsCompleted);
        Assert.False(executed);
        start.SetResult();
        await pending;
        Assert.True(executed);
    }

    [Fact]
    public async Task CanceledResponseStartDisposesMinimalSliceAndPropagatesToken()
    {
        using var cancellation = new CancellationTokenSource();
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var context = CreateHttpContext();
        context.RequestAborted = cancellation.Token;
        using var stream = new PendingStartStream(start.Task);
        context.Response.Body = stream;
        var slice = new CallbackSlice(() => throw new Exception("Must not execute")) { HtmlEncoder = HtmlEncoder.Default };
        var pending = ((IResult)slice).ExecuteAsync(context);
        cancellation.Cancel();
        start.SetCanceled(cancellation.Token);
        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.Equal(cancellation.Token, error.CancellationToken);
        Assert.True(slice.Disposed);
        Assert.Equal(cancellation.Token, stream.StartToken);
    }

    private static HttpContext CreateHttpContext()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        return httpContext;
    }

    private sealed class TestSlice : RazorSlice
    {
        public override Task ExecuteAsync() => Task.CompletedTask;
    }
    private sealed class MinimalTextSlice : RazorSliceBase
    {
        public override Task ExecuteAsync()
        {
            Write("<\u00e9>");
            return Task.CompletedTask;
        }
    }
    private sealed class FullTextSlice : RazorSlice
    {
        public override Task ExecuteAsync()
        {
            Write("<\u00e9>");
            return Task.CompletedTask;
        }
    }
    private sealed class CallbackSlice(Action execute) : RazorSliceBase
    {
        internal bool Disposed => IsDisposed;
        public override Task ExecuteAsync()
        {
            execute();
            return Task.CompletedTask;
        }
    }
    private sealed class PendingStartStream(Task pending) : MemoryStream
    {
        internal CancellationToken StartToken { get; private set; }
        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            StartToken = cancellationToken;
            return pending;
        }
    }
}
