using System.Diagnostics;
using System.IO.Pipelines;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Order;
using BenchmarkDotNet.Running;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;
using Perfolizer.Horology;
using RazorSlices.Benchmarks.RazorClassLibrary.CompilerLiteralsUtf16;
using RazorSlices.Benchmarks.RazorClassLibrary.CompilerLiteralsUtf8;
using RazorSlices.Benchmarks.RazorClassLibrary.Local;
using RazorSlices.Benchmarks.RazorClassLibrary.PreviousVersion;
using RazorSlices.Benchmarks.WebApp;

if (args.Contains("--compiler-literal-smoke", StringComparer.Ordinal))
{
    await RunCompilerLiteralSmoke();
    return;
}

if (args.Length > 0 && string.Equals(args[0], "--compiler-literal-trace", StringComparison.Ordinal))
{
    await RunCompilerLiteralTrace(args);
    return;
}

BenchmarkSwitcher.FromTypes([typeof(RazorSlicesCompilerLiteralRendering)]).Run(args);

static async Task RunCompilerLiteralSmoke()
{
    var pipeWriter = new NullPipeWriter();

    foreach (var paragraphGroups in new[] { 1, 5, 20, 100 })
    {
        await CompilerLiteralUtf16Version.RenderLorem(pipeWriter, paragraphGroups);
        pipeWriter.Reset();

        await CompilerLiteralUtf8Version.RenderLorem(pipeWriter, paragraphGroups);
        pipeWriter.Reset();
    }

    Console.WriteLine("Compiler literal smoke test completed.");
}

static async Task RunCompilerLiteralTrace(string[] args)
{
    var implementation = args.Length > 1 ? args[1] : "";
    var paragraphGroups = args.Length > 2 && int.TryParse(args[2], out var parsedParagraphGroups)
        ? parsedParagraphGroups
        : 100;
    var renders = args.Length > 3 && int.TryParse(args[3], out var parsedRenders)
        ? parsedRenders
        : 1_000_000;

    if (!string.Equals(implementation, "string", StringComparison.OrdinalIgnoreCase)
        && !string.Equals(implementation, "utf8", StringComparison.OrdinalIgnoreCase))
    {
        Console.Error.WriteLine("Usage: --compiler-literal-trace <string|utf8> [paragraphGroups] [renders]");
        Environment.ExitCode = 1;
        return;
    }

    var pipeWriter = new NullPipeWriter();

    for (var i = 0; i < 1_000; i++)
    {
        await RenderOnce(implementation, pipeWriter, paragraphGroups);
        pipeWriter.Reset();
    }

    var stopwatch = Stopwatch.StartNew();

    for (var i = 0; i < renders; i++)
    {
        await RenderOnce(implementation, pipeWriter, paragraphGroups);
        pipeWriter.Reset();
    }

    stopwatch.Stop();
    Console.WriteLine($"Rendered {renders:N0} {implementation} literal slices with {paragraphGroups} paragraph groups in {stopwatch.Elapsed.TotalSeconds:N3}s.");
}

static ValueTask RenderOnce(string implementation, NullPipeWriter pipeWriter, int paragraphGroups)
{
    return string.Equals(implementation, "string", StringComparison.OrdinalIgnoreCase)
        ? CompilerLiteralUtf16Version.RenderLorem(pipeWriter, paragraphGroups)
        : CompilerLiteralUtf8Version.RenderLorem(pipeWriter, paragraphGroups);
}

[MemoryDiagnoser, Config(typeof(Config))]
public class RazorSlicesCompilerLiteralRendering
{
    private readonly NullPipeWriter _pipeWriter = new();

    [Params(1, 5, 20, 100)]
    public int ParagraphGroups { get; set; }

    [IterationSetup]
    public void Setup()
    {
        _pipeWriter.Reset();
    }

    [Benchmark(Baseline = true)]
    public void StringLiterals()
    {
        EnsureCompleted(CompilerLiteralUtf16Version.RenderLorem(_pipeWriter, ParagraphGroups));
        _pipeWriter.Reset();
    }

    [Benchmark]
    public void Utf8Literals()
    {
        EnsureCompleted(CompilerLiteralUtf8Version.RenderLorem(_pipeWriter, ParagraphGroups));
        _pipeWriter.Reset();
    }

    private static void EnsureCompleted(ValueTask valueTask)
    {
        if (!valueTask.IsCompletedSuccessfully)
        {
            valueTask.AsTask().GetAwaiter().GetResult();
        }
    }

    private class Config : ManualConfig
    {
        public Config()
        {
            AddJob(Job.ShortRun
                .WithMinIterationTime(TimeInterval.FromMilliseconds(100))
                .WithIterationCount(8)
                .WithWarmupCount(3)
                .WithCustomBuildConfiguration("Benchmarks")
                .WithId(".NET 11 Preview"));
        }
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

[MemoryDiagnoser, Config(typeof(Config))]
public class RazorSlicesStringRendering
{
    private readonly int _iterations = 1000;

    [Benchmark(Baseline = true)]
    public async ValueTask<long> RazorSlicesHello()
    {
        long totalLength = 0;
        for (int i = 0; i < _iterations; i++)
        {
            totalLength += (await LocalVersion.RenderHello()).Length;
        }
        return totalLength;
    }

    private class Config : ManualConfig
    {
        public Config()
        {
            AddJob(Job.Default.WithCustomBuildConfiguration("Benchmarks").WithId("Benchmarks"));
        }
    }
}

[MemoryDiagnoser, Config(typeof(Config))]
public class RazorSlicesBenchmarks
{
    private Pipe _pipe = new();
    private readonly int _iterations = 1000;

    [IterationSetup]
    public void Setup()
    {
        ResetPipe();
    }

    [Benchmark(Baseline = true)]
    public async ValueTask RazorSlicesHello()
    {
        for (int i = 0; i < _iterations; i++)
        {
            await PreviousVersion.RenderHello(_pipe.Writer);
            ResetPipe();
        }
    }

    [Benchmark]
    public async ValueTask RazorSlicesHello_Local()
    {
        for (int i = 0; i < _iterations; i++)
        {
            await LocalVersion.RenderHello(_pipe.Writer);
            ResetPipe();
        }
    }

    private void ResetPipe()
    {
        _pipe.Reader.Complete();
        _pipe.Writer.Complete();
        _pipe.Reset();
    }

    private class Config : ManualConfig
    {
        public Config()
        {
            AddJob(Job.Default.WithCustomBuildConfiguration("Benchmarks").WithId("Benchmarks"));
        }
    }
}

[MemoryDiagnoser]
[AnyCategoriesFilter("Lorem"), Orderer(SummaryOrderPolicy.FastestToSlowest) /*GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)*/]
[Config(typeof(Config))]
public class RazorSlicesAppBenchmarks
{
    private readonly HttpClient _slicesNuGetClient = new();
    private readonly HttpClient _slicesLocalClient = new();
    private readonly HttpClient _pagesClient = new();
    private readonly HttpClient _componentsClient = new();
    private readonly HttpClient _blazorClient = new();
    private readonly byte[] _buffer = new byte[1024 * 256]; // 256 KB buffer
    private readonly int _iterations = 100;

    public RazorSlicesAppBenchmarks()
    {
        _slicesNuGetClient = CreateHttpClient<BenchmarksWebAppRazorSlicesPreviousVersion>();
        _slicesLocalClient = CreateHttpClient<BenchmarksWebApp>();
        _pagesClient = CreateHttpClient<BenchmarksRazorPagesWebApp>();
        _componentsClient = CreateHttpClient<BenchmarksRazorComponentsWebApp>();
        _blazorClient = CreateHttpClient<BenchmarksBlazorWebApp>();
    }

    [Benchmark(Baseline = true), BenchmarkCategory("Hello", "RazorSlices")]
    public Task<int> RazorSlicesHello_NuGet() => GetPath(_slicesNuGetClient, "/hello");

    [Benchmark, BenchmarkCategory("Hello", "RazorSlices", "Local")]
    public Task<int> RazorSlicesHello_Local() => GetPath(_slicesLocalClient, "/hello");

    [Benchmark, BenchmarkCategory("Hello", "RazorPages")]
    public Task<int> RazorPagesHello() => GetPath(_pagesClient, "/hello");

    [Benchmark, BenchmarkCategory("Hello", "RazorComponents")]
    public Task<int> RazorComponentsHello() => GetPath(_componentsClient, "/hello");

    [Benchmark, BenchmarkCategory("Hello", "BlazorSSR")]
    public Task<int> BlazorSSRHello() => GetPath(_blazorClient, "/hello");

    [Benchmark, BenchmarkCategory("Lorem", "Lorem25", "RazorSlices", "Local")]
    public Task<int> RazorSlicesLorem25_Local() => GetPath(_slicesLocalClient, "/lorem25");

    [Benchmark, BenchmarkCategory("Lorem", "Lorem50", "RazorSlices", "Local")]
    public Task<int> RazorSlicesLorem50_Local() => GetPath(_slicesLocalClient, "/lorem50");

    [Benchmark, BenchmarkCategory("Lorem", "Lorem100", "RazorSlices", "Local")]
    public Task<int> RazorSlicesLorem100_Local() => GetPath(_slicesLocalClient, "/lorem100");

    [Benchmark, BenchmarkCategory("Lorem", "Lorem200", "RazorSlices", "Local")]
    public Task<int> RazorSlicesLorem200_Local() => GetPath(_slicesLocalClient, "/lorem200");

    [Benchmark(Baseline = true), BenchmarkCategory("Lorem", "Lorem25", "RazorSlices")]
    public Task<int> RazorSlicesLorem25_NuGet() => GetPath(_slicesNuGetClient, "/lorem25");

    [Benchmark, BenchmarkCategory("Lorem", "Lorem50", "RazorSlices")]
    public Task<int> RazorSlicesLorem50_NuGet() => GetPath(_slicesNuGetClient, "/lorem50");

    [Benchmark, BenchmarkCategory("Lorem", "Lorem100", "RazorSlices")]
    public Task<int> RazorSlicesLorem100_NuGet() => GetPath(_slicesNuGetClient, "/lorem100");

    [Benchmark, BenchmarkCategory("Lorem", "Lorem200", "RazorSlices")]
    public Task<int> RazorSlicesLorem200_NuGet() => GetPath(_slicesNuGetClient, "/lorem200");

    [Benchmark, BenchmarkCategory("Lorem", "Lorem25", "RazorPages")]
    public Task<int> RazorPagesLorem25() => GetPath(_pagesClient, "/lorem25");

    [Benchmark, BenchmarkCategory("Lorem", "Lorem50", "RazorPages")]
    public Task<int> RazorPagesLorem50() => GetPath(_pagesClient, "/lorem50");

    [Benchmark, BenchmarkCategory("Lorem", "Lorem100", "RazorPages")]
    public Task<int> RazorPagesLorem100() => GetPath(_pagesClient, "/lorem100");

    [Benchmark, BenchmarkCategory("Lorem", "Lorem200", "RazorPages")]
    public Task<int> RazorPagesLorem200() => GetPath(_pagesClient, "/lorem200");

    [Benchmark, BenchmarkCategory("Lorem", "Lorem25", "RazorComponents")]
    public Task<int> RazorComponentsLorem25() => GetPath(_componentsClient, "/lorem25");

    [Benchmark, BenchmarkCategory("Lorem", "Lorem50", "RazorComponents")]
    public Task<int> RazorComponentsLorem50() => GetPath(_componentsClient, "/lorem50");

    [Benchmark, BenchmarkCategory("Lorem", "Lorem100", "RazorComponents")]
    public Task<int> RazorComponentsLorem100() => GetPath(_componentsClient, "/lorem100");

    [Benchmark, BenchmarkCategory("Lorem", "Lorem200", "RazorComponents")]
    public Task<int> RazorComponentsLorem200() => GetPath(_componentsClient, "/lorem200");

    [Benchmark, BenchmarkCategory("Lorem", "Lorem25", "BlazorSSR")]
    public Task<int> BlazorSSRLorem25() => GetPath(_blazorClient, "/lorem25");

    [Benchmark, BenchmarkCategory("Lorem", "Lorem50", "BlazorSSR")]
    public Task<int> BlazorSSRLorem50() => GetPath(_blazorClient, "/lorem50");

    [Benchmark, BenchmarkCategory("Lorem", "Lorem100", "BlazorSSR")]
    public Task<int> BlazorSSRLorem100() => GetPath(_blazorClient, "/lorem100");

    [Benchmark, BenchmarkCategory("Lorem", "Lorem200", "BlazorSSR")]
    public Task<int> BlazorSSRLorem200() => GetPath(_blazorClient, "/lorem200");

    private async Task<int> GetPath(HttpClient httpClient, string path)
    {
        var bytesRead = 0;

        for (int i = 0; i < _iterations; i++)
        {
            var body = await httpClient.GetStreamAsync(path);
            bytesRead += await body.ReadAsync(_buffer);
        }

        return bytesRead;
    }

    private static HttpClient CreateHttpClient<TApp>() where TApp : class
    {
        var waf = new WebApplicationFactory<TApp>();
        waf.WithWebHostBuilder(webHost => webHost.ConfigureLogging(logging => logging.ClearProviders()));
        return waf.CreateClient();
    }

    private class Config : ManualConfig
    {
        public Config()
        {
            AddJob(Job.ShortRun.WithCustomBuildConfiguration("Benchmarks").WithId("Benchmarks"));
        }
    }
}
