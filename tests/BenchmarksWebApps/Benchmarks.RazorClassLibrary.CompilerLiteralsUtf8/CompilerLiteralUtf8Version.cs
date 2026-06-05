using System.IO.Pipelines;

namespace RazorSlices.Benchmarks.RazorClassLibrary.CompilerLiteralsUtf8;

public static class CompilerLiteralUtf8Version
{
    public static ValueTask RenderHello(PipeWriter pipeWriter)
    {
        var slice = Hello.Create();
        return slice.RenderAsync(pipeWriter);
    }

    public static ValueTask RenderLorem(PipeWriter pipeWriter, LoremModel model)
    {
        var slice = Lorem.Create(model);
        return slice.RenderAsync(pipeWriter);
    }
}

public sealed record LoremModel(int ParagraphGroups);
