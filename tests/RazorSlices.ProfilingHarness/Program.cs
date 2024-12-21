using System.IO.Pipelines;
using RazorSlices.Benchmarks.RazorClassLibrary.Local;

var iterations = 1000;
//var memoryPool = MemoryPool<byte>.Shared;
//var pipe = new Pipe(new(memoryPool, pauseWriterThreshold: 0) { });

Console.WriteLine("Warming up");

for (int i = 0; i < iterations; i++)
{
    //await RenderSlice(pipe);
    await RenderSliceToString();
}

Console.WriteLine("Press ENTER to run test");
Console.ReadLine();

for (int i = 0; i < iterations; i++)
{
    //await RenderSlice(pipe);
    await RenderSliceToString();
}

static ValueTask<string> RenderSliceToString()
{
    return LocalVersion.RenderReusableHello();
}

static async ValueTask RenderSlice(Pipe pipe)
{
    await LocalVersion.RenderHello(pipe.Writer);
    await pipe.Writer.CompleteAsync();
    await pipe.Reader.CompleteAsync();
    pipe.Reset();
}
