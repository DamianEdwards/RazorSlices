using System.IO.Pipelines;

namespace RazorSlices.Benchmarks.RazorClassLibrary.Local;

public static class LocalVersion
{
    public static ValueTask RenderHello(PipeWriter pipeWriter, CancellationToken cancellationToken = default)
    {
        var slice = Hello.Create();
        return slice.RenderAsync(pipeWriter, cancellationToken: cancellationToken);
    }
}
