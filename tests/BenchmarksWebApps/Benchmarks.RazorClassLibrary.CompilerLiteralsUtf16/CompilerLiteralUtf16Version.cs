using System.IO.Pipelines;

namespace RazorSlices.Benchmarks.RazorClassLibrary.CompilerLiteralsUtf16;

public static class CompilerLiteralUtf16Version
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

    public static Func<PipeWriter, ValueTask> CreateLoremRenderer(int paragraphGroups)
    {
        var slice = Lorem.Create(paragraphGroups);
        return pipeWriter => slice.RenderAsync(pipeWriter);
    }
}
