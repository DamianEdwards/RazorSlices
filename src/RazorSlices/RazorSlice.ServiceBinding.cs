using System.Runtime.ExceptionServices;
using Microsoft.AspNetCore.Html;

namespace RazorSlices;

public abstract partial class RazorSlice
{
    private void InitializeForRender()
    {
        try
        {
            EnsureInitialized();
        }
        catch (Exception error)
        {
            DisposeAfterFailure(error);
            throw;
        }
    }

    private ValueTask RenderBoundAsync(SliceServiceBindings services, bool pipe, bool renderLayout, bool disposeSlice)
    {
        try
        {
            services.Bind(this);
        }
        catch (Exception error)
        {
            DisposeAfterFailure(error);
            throw;
        }

        if (renderLayout && this is IUsesLayout usesLayout)
        {
            RazorSlice layout;
            try
            {
                layout = CreateLayoutForRender(usesLayout);
            }
            catch (Exception error)
            {
                FinishBoundRender(dispose: true, error);
                throw;
            }

            // Layout creation can use bound services; execution is a tail handoff to the layout.
            try
            {
                FinishBoundRender(dispose: false);
            }
            catch (Exception error)
            {
                DisposeAfterFailure(error);
                throw;
            }
            try
            {
                return pipe
                    ? layout.RenderToPipeWriterAsync(_pipeWriter!, _htmlEncoder, CancellationToken)
                    : layout.RenderToTextWriterAsync(_textWriter!, _htmlEncoder, CancellationToken);
            }
            catch (Exception error)
            {
                DisposeAfterFailure(error);
                throw;
            }
        }

        try
        {
            var execute = ExecuteAsync();
            if (!execute.HandleSynchronousCompletion())
            {
                return AwaitBoundExecute(this, execute, disposeSlice);
            }
        }
        catch (Exception error)
        {
            FinishBoundRender(dispose: true, error);
            throw;
        }

        try
        {
            if (disposeSlice)
            {
                Dispose();
            }
            var flush = pipe ? AutoFlush() : ValueTask.CompletedTask;
            if (!flush.IsCompletedSuccessfully)
            {
                return AwaitBoundFlush(flush, this);
            }
#pragma warning disable CA1849 // The operation has already completed successfully.
            flush.GetAwaiter().GetResult();
#pragma warning restore CA1849
        }
        catch (Exception error)
        {
            FinishBoundRender(dispose: false, error);
            throw;
        }
        FinishBoundRender(dispose: false);
        return ValueTask.CompletedTask;
    }

    private static async ValueTask AwaitBoundExecute(RazorSlice slice, Task execute, bool disposeSlice)
    {
        try
        {
            await execute;
            await slice.AutoFlush();
        }
        catch (Exception error)
        {
            slice.FinishBoundRender(disposeSlice, error);
            throw;
        }
        slice.FinishBoundRender(disposeSlice);
    }

    private static async ValueTask AwaitBoundFlush(ValueTask pending, RazorSlice slice)
    {
        try
        {
            await pending;
        }
        catch (Exception error)
        {
            slice.FinishBoundRender(dispose: false, error);
            throw;
        }
        slice.FinishBoundRender(dispose: false);
    }

    private ValueTask<HtmlString> RenderBoundSection(SliceServiceBindings services, string name)
    {
        services.Bind(this);
        try
        {
            var pending = ExecuteSectionAsync(name);
            if (!pending.HandleSynchronousCompletion())
            {
                return AwaitBoundSection(pending, this);
            }
        }
        catch (Exception error)
        {
            FinishBoundRender(dispose: false, error);
            throw;
        }
        FinishBoundRender(dispose: false);
        return ValueTask.FromResult(HtmlString.Empty);
    }

    private static async ValueTask<HtmlString> AwaitBoundSection(Task pending, RazorSlice slice)
    {
        try
        {
            await pending;
        }
        catch (Exception error)
        {
            slice.FinishBoundRender(dispose: false, error);
            throw;
        }
        slice.FinishBoundRender(dispose: false);
        return HtmlString.Empty;
    }

    private void FinishBoundRender(bool dispose, Exception? failure = null)
    {
        Exception? cleanup = null;
        if (dispose && !_disposed)
        {
            try
            {
                Dispose();
            }
            catch (Exception error)
            {
                cleanup = error;
            }
        }
        var unbind = Activation!.Services!.Unbind(this);
        if (unbind is not null)
        {
            cleanup = cleanup is null ? unbind : new AggregateException(cleanup, unbind);
        }
        if (cleanup is not null)
        {
            if (failure is not null)
            {
                throw new AggregateException(failure, cleanup);
            }
            ExceptionDispatchInfo.Capture(cleanup).Throw();
        }
    }

    private void DisposeAfterFailure(Exception failure)
    {
        if (!_disposed)
        {
            try
            {
                Dispose();
            }
            catch (Exception error)
            {
                throw new AggregateException(failure, error);
            }
        }
    }
}
