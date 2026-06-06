using System.Diagnostics;
using System.IO.Pipelines;
using RazorSlices.Benchmarks.RazorClassLibrary.CompilerLiteralsUtf16;
using RazorSlices.Benchmarks.RazorClassLibrary.CompilerLiteralsUtf8;
using RazorSlices.Benchmarks.RazorClassLibrary.Local;

var scenario = args.Length > 0 ? args[0] : "utf16-lorem-pipe";
var paragraphGroups = args.Length > 1 && int.TryParse(args[1], out var parsedParagraphGroups)
    ? parsedParagraphGroups
    : 100;
var iterations = args.Length > 2 && int.TryParse(args[2], out var parsedIterations)
    ? parsedIterations
    : 1_000_000;
var warmupIterations = args.Length > 3 && int.TryParse(args[3], out var parsedWarmupIterations)
    ? parsedWarmupIterations
    : 10_000;

if (string.Equals(scenario, "--help", StringComparison.OrdinalIgnoreCase)
    || string.Equals(scenario, "-h", StringComparison.OrdinalIgnoreCase)
    || string.Equals(scenario, "/?", StringComparison.OrdinalIgnoreCase))
{
    PrintUsage();
    return;
}

if (!TryGetScenario(scenario, paragraphGroups, out var render))
{
    Console.Error.WriteLine($"Unknown scenario '{scenario}'.");
    PrintUsage();
    Environment.ExitCode = 1;
    return;
}

Console.WriteLine($"Scenario: {scenario}");
Console.WriteLine($"Paragraph groups: {paragraphGroups:N0}");
Console.WriteLine($"Warmup iterations: {warmupIterations:N0}");
Console.WriteLine($"Measured iterations: {iterations:N0}");

for (var i = 0; i < warmupIterations; i++)
{
    await render();
}

GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
GC.WaitForPendingFinalizers();
GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);

var allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);
var stopwatch = Stopwatch.StartNew();

for (var i = 0; i < iterations; i++)
{
    await render();
}

stopwatch.Stop();
var allocatedBytes = GC.GetTotalAllocatedBytes(precise: true) - allocatedBefore;

Console.WriteLine($"Elapsed: {stopwatch.Elapsed.TotalSeconds:N3}s");
Console.WriteLine($"Renders/second: {iterations / stopwatch.Elapsed.TotalSeconds:N0}");
Console.WriteLine($"Allocated bytes/render: {allocatedBytes / (double)iterations:N2}");

static bool TryGetScenario(string scenario, int paragraphGroups, out Func<ValueTask> render)
{
    var pipeWriter = new NullPipeWriter();
    var wrappedPipeWriter = new NonTrackingPipeWriter(pipeWriter);

    Func<ValueTask>? selectedRender = scenario.ToLowerInvariant() switch
    {
        "render-empty-pipe" => () => RenderPipe(() => RenderLifecycleScenarios.RenderEmpty(pipeWriter), pipeWriter),
        "render-empty-init-pipe" => () => RenderPipe(() => RenderLifecycleScenarios.RenderEmptyWithInitialize(pipeWriter), pipeWriter),
        "render-empty-wrapper-pipe" => () => RenderPipe(() => RenderLifecycleScenarios.RenderEmpty(wrappedPipeWriter), pipeWriter),
        "render-generated-empty-pipe" => () => RenderPipe(() => RenderLifecycleScenarios.RenderGeneratedStyleEmpty(pipeWriter), pipeWriter),
        "render-small-literal-pipe" => () => RenderPipe(() => RenderLifecycleScenarios.RenderSmallLiteral(pipeWriter), pipeWriter),
        "render-autoflush-pipe" => () => RenderPipe(() => RenderLifecycleScenarios.RenderAutoFlush(pipeWriter), pipeWriter),
        "render-async-yield-pipe" => () => RenderPipe(() => RenderLifecycleScenarios.RenderAsyncYield(pipeWriter), pipeWriter),
        "utf16-lorem-pipe" => () => RenderPipe(() => CompilerLiteralUtf16Version.RenderLorem(pipeWriter, paragraphGroups), pipeWriter),
        "utf8-lorem-pipe" => () => RenderPipe(() => CompilerLiteralUtf8Version.RenderLorem(pipeWriter, paragraphGroups), pipeWriter),
        "utf16-lorem-lifetime" => () =>
        {
            var slice = RazorSlices.Benchmarks.RazorClassLibrary.CompilerLiteralsUtf16.Lorem.Create(paragraphGroups);
            slice.Dispose();
            return ValueTask.CompletedTask;
        },
        "utf8-lorem-lifetime" => () =>
        {
            var slice = RazorSlices.Benchmarks.RazorClassLibrary.CompilerLiteralsUtf8.Lorem.Create(paragraphGroups);
            slice.Dispose();
            return ValueTask.CompletedTask;
        },
        "local-hello-pipe" => () => RenderPipe(() => LocalVersion.RenderHello(pipeWriter), pipeWriter),
        "local-hello-lifetime" => () =>
        {
            var slice = RazorSlices.Benchmarks.RazorClassLibrary.Local.Hello.Create();
            slice.Dispose();
            return ValueTask.CompletedTask;
        },
        "local-hello-string" => async () =>
        {
            _ = await LocalVersion.RenderHello();
        },
        _ => null
    };

    render = selectedRender!;
    return selectedRender is not null;
}

static async ValueTask RenderPipe(Func<ValueTask> render, NullPipeWriter pipeWriter)
{
    await render();
    pipeWriter.Reset();
}

static void PrintUsage()
{
    Console.WriteLine("""
        Usage:
          dotnet run --project tests\ProfilingHarness -c Release -- <scenario> [paragraphGroups] [iterations] [warmupIterations]

        Scenarios:
          render-empty-pipe      Empty hand-written slice rendered to PipeWriter
          render-empty-init-pipe Empty hand-written slice with an Initialize delegate
          render-empty-wrapper-pipe Empty slice rendered through FlushTrackingPipeWriter
          render-generated-empty-pipe Empty async-method-style slice rendered to PipeWriter
          render-small-literal-pipe Small literal hand-written slice rendered to PipeWriter
          render-autoflush-pipe Large literal hand-written slice that trips auto-flush
          render-async-yield-pipe Slice whose ExecuteAsync yields asynchronously
          utf16-lorem-pipe    Razor compiler string literals rendered to PipeWriter
          utf8-lorem-pipe     Razor compiler UTF-8 literals rendered to PipeWriter
          utf16-lorem-lifetime  Razor compiler string literal slice creation and disposal only
          utf8-lorem-lifetime   Razor compiler UTF-8 literal slice creation and disposal only
          local-hello-pipe    Local Hello slice rendered to PipeWriter
          local-hello-lifetime  Local Hello slice creation and disposal only
          local-hello-string  Local Hello slice rendered to string/TextWriter
        """);
}

internal sealed class NonTrackingPipeWriter(PipeWriter innerPipeWriter) : PipeWriter
{
    public override bool CanGetUnflushedBytes => false;

    public override void Advance(int bytes)
    {
        innerPipeWriter.Advance(bytes);
    }

    public override void CancelPendingFlush()
    {
        innerPipeWriter.CancelPendingFlush();
    }

    public override void Complete(Exception? exception = null)
    {
        innerPipeWriter.Complete(exception);
    }

    public override ValueTask<FlushResult> FlushAsync(CancellationToken cancellationToken = default)
    {
        return innerPipeWriter.FlushAsync(cancellationToken);
    }

    public override Memory<byte> GetMemory(int sizeHint = 0)
    {
        return innerPipeWriter.GetMemory(sizeHint);
    }

    public override Span<byte> GetSpan(int sizeHint = 0)
    {
        return innerPipeWriter.GetSpan(sizeHint);
    }
}

internal sealed class NullPipeWriter : PipeWriter
{
    private readonly byte[] _buffer = new byte[64 * 1024];
    private long _unflushedBytes;

    public override bool CanGetUnflushedBytes => true;

    public override long UnflushedBytes => _unflushedBytes;

    public override void Advance(int bytes)
    {
        _unflushedBytes += bytes;
    }

    public override void CancelPendingFlush()
    {
    }

    public override void Complete(Exception? exception = null)
    {
    }

    public override ValueTask<FlushResult> FlushAsync(CancellationToken cancellationToken = default)
    {
        _unflushedBytes = 0;
        return ValueTask.FromResult(new FlushResult(isCanceled: false, isCompleted: false));
    }

    public override Memory<byte> GetMemory(int sizeHint = 0)
    {
        return GetMemoryOrThrow(sizeHint);
    }

    public override Span<byte> GetSpan(int sizeHint = 0)
    {
        return GetMemoryOrThrow(sizeHint).Span;
    }

    public void Reset()
    {
        _unflushedBytes = 0;
    }

    private Memory<byte> GetMemoryOrThrow(int sizeHint)
    {
        if (sizeHint > _buffer.Length)
        {
            throw new InvalidOperationException($"The requested buffer size ({sizeHint}) is larger than the {nameof(NullPipeWriter)} buffer ({_buffer.Length}).");
        }

        return _buffer;
    }
}
