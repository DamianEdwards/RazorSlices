using System.IO.Pipelines;

namespace RazorSlices.Benchmarks.RazorClassLibrary.Local;

public static class LocalVersion
{
    public static ValueTask RenderHello(PipeWriter pipeWriter)
    {
        var slice = Hello.Create();
        return slice.RenderAsync(pipeWriter);
    }
}
