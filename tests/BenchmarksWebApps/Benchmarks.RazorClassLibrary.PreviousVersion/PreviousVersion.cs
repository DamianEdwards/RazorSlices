using System.IO.Pipelines;

namespace RazorSlices.Benchmarks.RazorClassLibrary.PreviousVersion;

public static class PreviousVersion
{
    private static readonly SliceFactory _helloSliceFactory = RazorSlice.ResolveSliceFactory("/Hello.cshtml");

    public static ValueTask RenderHello(PipeWriter pipeWriter)
    {
        var slice = RazorSlice.Create(_helloSliceFactory);
        return slice.RenderAsync(pipeWriter);
    }
}
