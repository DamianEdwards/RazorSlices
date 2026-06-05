using System.IO.Pipelines;

namespace RazorSlices.Benchmarks.RazorClassLibrary.CompilerLiteralsUtf8;

public static class CompilerLiteralUtf8Version
{
    public static ValueTask RenderHello(PipeWriter pipeWriter)
    {
        var slice = Hello.Create();
        return slice.RenderAsync(pipeWriter);
    }

    public static ValueTask RenderLorem(PipeWriter pipeWriter, int paragraphGroups)
    {
        return Lorem.Create(paragraphGroups).RenderAsync(pipeWriter);
    }
}
