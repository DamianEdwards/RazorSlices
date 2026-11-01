using System.IO.Pipelines;
using System.Text;
using System.Text.Encodings.Web;

namespace RazorSlices.Samples.WebApp.Tests;

public class RuntimeAsyncCompatibilityTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SynchronouslyCompletingSdkDelegateRenders(bool pipeOutput)
    {
        using var slice = Slices.Compatibility.Templated.Create(Task.CompletedTask);
        if (pipeOutput)
        {
            using var stream = new MemoryStream();
            var writer = PipeWriter.Create(stream);
            await slice.RenderAsync(writer, HtmlEncoder.Default);
            await writer.CompleteAsync();
            Assert.Contains("Completed template", Encoding.UTF8.GetString(stream.ToArray()));
        }
        else
        {
            Assert.Contains("Completed template", await slice.RenderAsync());
        }
    }

    [Fact]
    public async Task SuspendedSdkDelegateIsRejected()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var slice = Slices.Compatibility.Templated.Create(gate.Task);
        try
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => slice.RenderAsync().AsTask());
            Assert.Contains("incomplete Task", error.Message);
        }
        finally
        {
            gate.SetResult();
        }
    }

#if RAZOR_SLICES_TEST_RUNTIME_ASYNC
    [Fact]
    public void ActualSdkTemplatesInAppAndRclUseRuntimeAsyncButLibraryDoesNot()
    {
        using var appSlice = Slices.Compatibility.Templated.Create(Task.CompletedTask);
        using var librarySlice = RazorClassLibrary.Slices.FromLibrary.Create();
        foreach (var type in new[] { appSlice.GetType(), librarySlice.GetType() })
        {
            var method = type.GetMethod(nameof(RazorSlice.ExecuteAsync))!;
            Assert.True((method.GetMethodImplementationFlags() & System.Reflection.MethodImplAttributes.Async) != 0);
        }
        var libraryMethod = typeof(RazorSliceBase).GetMethod("AwaitExecute",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(libraryMethod);
        Assert.NotNull(libraryMethod.GetCustomAttributes(typeof(System.Runtime.CompilerServices.AsyncStateMachineAttribute), inherit: false).SingleOrDefault());
        Assert.False((libraryMethod.GetMethodImplementationFlags() & System.Reflection.MethodImplAttributes.Async) != 0);
    }
#endif
}
