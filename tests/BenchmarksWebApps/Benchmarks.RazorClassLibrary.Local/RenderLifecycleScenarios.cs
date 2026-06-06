using System.IO.Pipelines;

namespace RazorSlices.Benchmarks.RazorClassLibrary.Local;

public static class RenderLifecycleScenarios
{
    public static ValueTask RenderEmpty(PipeWriter pipeWriter)
    {
        var slice = new EmptySlice();
        return slice.RenderAsync(pipeWriter);
    }

    public static ValueTask RenderEmptyWithInitialize(PipeWriter pipeWriter)
    {
        var slice = new EmptySlice { Initialize = static (_, _, _) => { } };
        return slice.RenderAsync(pipeWriter);
    }

    public static ValueTask RenderGeneratedStyleEmpty(PipeWriter pipeWriter)
    {
        var slice = new GeneratedStyleEmptySlice();
        return slice.RenderAsync(pipeWriter);
    }

    public static ValueTask RenderSmallLiteral(PipeWriter pipeWriter)
    {
        var slice = new SmallLiteralSlice();
        return slice.RenderAsync(pipeWriter);
    }

    public static ValueTask RenderAutoFlush(PipeWriter pipeWriter)
    {
        var slice = new AutoFlushSlice();
        return slice.RenderAsync(pipeWriter);
    }

    public static ValueTask RenderAsyncYield(PipeWriter pipeWriter)
    {
        var slice = new AsyncYieldSlice();
        return slice.RenderAsync(pipeWriter);
    }

    private sealed class EmptySlice : RazorSlice
    {
        public override Task ExecuteAsync()
        {
            return Task.CompletedTask;
        }
    }

    private sealed class GeneratedStyleEmptySlice : RazorSlice
    {
#pragma warning disable CS1998
        // Matches Razor-generated sync-completing async methods that have no awaits.
        public override async Task ExecuteAsync()
        {
        }
#pragma warning restore CS1998
    }

    private sealed class SmallLiteralSlice : RazorSlice
    {
        public override Task ExecuteAsync()
        {
            WriteLiteral("Hello, World!");
            return Task.CompletedTask;
        }
    }

    private sealed class AutoFlushSlice : RazorSlice
    {
        private static readonly string _html = new('a', 20 * 1024);

        public override Task ExecuteAsync()
        {
            WriteLiteral(_html);
            return Task.CompletedTask;
        }
    }

    private sealed class AsyncYieldSlice : RazorSlice
    {
        public override async Task ExecuteAsync()
        {
            await Task.Yield();
        }
    }
}
