using System.IO.Pipelines;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Internal;
using Microsoft.AspNetCore.Mvc.Razor;

namespace RazorSlices.Tests;

public class HelperResultTests
{
    [Fact]
    public async Task CompletedDelegateWritesToTextWriter()
    {
        using var writer = new StringWriter();
        using var slice = new HelperSlice(new HelperResult(async output =>
        {
            await Task.CompletedTask;
            output.Write("completed");
        }));

        await slice.RenderAsync(writer, HtmlEncoder.Default);

        Assert.Equal("completed", writer.ToString());
    }

    [Fact]
    public async Task CompletedSuspendedDelegateDoesNotDependOnTaskImplementation()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var task = FinishAsync();
        completion.SetResult();
        await task;

        using var writer = new StringWriter();
        using var slice = new HelperSlice(new HelperResult(output =>
        {
            output.Write("completed");
            return task;
        }));
        await slice.RenderAsync(writer, HtmlEncoder.Default);
        Assert.Equal("completed", writer.ToString());

        async Task FinishAsync() => await completion.Task;
    }

    [Fact]
    public async Task FaultedDelegatePropagatesOriginalException()
    {
        var error = new InvalidOperationException("delegate failed");
        using var slice = new HelperSlice(new HelperResult(_ => Task.FromException(error)));

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            slice.RenderAsync(TextWriter.Null, HtmlEncoder.Default).AsTask());

        Assert.Same(error, actual);
    }

    [Fact]
    public async Task CanceledDelegatePropagatesCancellation()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        using var slice = new HelperSlice(new HelperResult(_ => Task.FromCanceled(source.Token)));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            slice.RenderAsync(TextWriter.Null, HtmlEncoder.Default).AsTask());
    }

    [Fact]
    public void IncompleteDelegateDoesNotReturnItsWriterToPool()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        TextWriter? retainedWriter = null;
        using var stream = new MemoryStream();
        var pipe = PipeWriter.Create(stream);
        using var slice = new HelperSlice(new HelperResult(output =>
        {
            retainedWriter = output;
            return completion.Task;
        }));

        try
        {
            var error = Assert.Throws<InvalidOperationException>(() =>
                slice.RenderAsync(pipe, HtmlEncoder.Default).AsTask().GetAwaiter().GetResult());
            Assert.Contains("templated Razor delegate", error.Message);
            var nextWriter = Utf8PipeTextWriter.Get(pipe);
            try
            {
                Assert.NotSame(retainedWriter, nextWriter);
            }
            finally
            {
                Utf8PipeTextWriter.Return(nextWriter);
            }
        }
        finally
        {
            completion.SetResult();
            pipe.Complete();
        }
    }

    [Fact]
    public async Task RejectedDelegateCannotWriteThroughARecycledWriter()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = Task.CompletedTask;
        using var rejectedOutput = new MemoryStream();
        using var nextOutput = new MemoryStream();
        var rejectedPipe = PipeWriter.Create(rejectedOutput);
        var nextPipe = PipeWriter.Create(nextOutput);
        using var slice = new HelperSlice(new HelperResult(output => pending = WriteLateAsync(output)));

        Assert.Throws<InvalidOperationException>(() =>
            slice.RenderAsync(rejectedPipe, HtmlEncoder.Default).AsTask().GetAwaiter().GetResult());
        var nextWriter = Utf8PipeTextWriter.Get(nextPipe);
        try
        {
            nextWriter.Write("next");
            nextWriter.Flush();
        }
        finally
        {
            Utf8PipeTextWriter.Return(nextWriter);
            gate.SetResult();
        }
        await pending;
        await rejectedPipe.CompleteAsync();
        await nextPipe.CompleteAsync();
        Assert.Equal("late", System.Text.Encoding.UTF8.GetString(rejectedOutput.ToArray()));
        Assert.Equal("next", System.Text.Encoding.UTF8.GetString(nextOutput.ToArray()));

        async Task WriteLateAsync(TextWriter output)
        {
            await gate.Task.ConfigureAwait(false);
            output.Write("late");
            output.Flush();
        }
    }

    private sealed class HelperSlice(HelperResult helper) : RazorSlice
    {
        public override Task ExecuteAsync()
        {
            WriteHtml(helper);
            return Task.CompletedTask;
        }
    }
}
