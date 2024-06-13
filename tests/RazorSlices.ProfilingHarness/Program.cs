using System.Buffers;
using System.IO.Pipelines;
using RazorSlices.Benchmarks.RazorClassLibrary.Local;

var memoryPool = MemoryPool<byte>.Shared;

Console.WriteLine("Warming up");

var warmupCt = new CancellationTokenSource(TimeSpan.FromSeconds(2));
var warmupLoop = new RenderLoop(memoryPool, warmupCt.Token);
await warmupLoop.Run();

var ct = new CancellationTokenSource();
var loopCount = Environment.ProcessorCount;
var loops = new List<RenderLoop>(loopCount);

Console.WriteLine($"Press ENTER to start {loopCount} loops");
Console.ReadLine();

for (int i = 0; i < loopCount; i++)
{
    loops.Add(new(memoryPool, ct.Token));
}

var allLoopsTask = Task.WhenAll(loops.Select(l => Task.Run(l.Run, ct.Token)));

Console.WriteLine("Press ENTER to stop loops");
Console.ReadLine();

ct.Cancel();

Console.Write("Waiting for loops to exit...");
await allLoopsTask;
Console.WriteLine("Done!");


class RenderLoop(MemoryPool<byte> memoryPool, CancellationToken cancellationToken)
{
    private readonly Pipe _pipe = new(new(memoryPool, pauseWriterThreshold: 0) { });
    private readonly CancellationToken _cancellationToken = cancellationToken;

    public async Task Run()
    {
        while (!_cancellationToken.IsCancellationRequested)
        {
            await RenderSlice(_pipe, _cancellationToken);
        }
    }

    static ValueTask RenderSlice(Pipe pipe, CancellationToken cancellationToken = default)
    {
        return LocalVersion.RenderHello(pipe.Writer, cancellationToken);
        //await pipe.Writer.CompleteAsync();
        //await pipe.Reader.CompleteAsync();
        //pipe.Reset();
    }
}
