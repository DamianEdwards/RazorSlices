using System.Diagnostics;
using Microsoft.AspNetCore.Html;

namespace RazorSlices;

public partial class RazorSlice
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="slice"></param>
    /// <param name="name"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    protected ValueTask<HtmlString> RenderPartialAsync(string name)
    {
        var partialDefinition = RazorSlice.ResolveSliceDefinition(name);

        if (partialDefinition.ModelType is not null)
        {
            throw new InvalidOperationException($"""
                The slice '{name}' requires a model of type '{partialDefinition.ModelType.Name}'.
                Call RenderPartialAsync<TModel>(string name, TModel model) to provide the model.
                """);
        }

        if (partialDefinition.HasInjectableProperties && ServiceProvider is null)
        {
            throw new InvalidOperationException($"""
                The slice '{name}' has @inject properties but the current slice was not provided an IServiceProvider.
                You can only render partials with @inject properties from a slice that itself has @inject properties.
                """);
        }

        var partialSlice = ((SliceFactory)partialDefinition.Factory)();
        return RenderPartialAsync(partialSlice);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="partial"></param>
    /// <returns></returns>
    protected internal ValueTask<HtmlString> RenderPartialAsync(RazorSlice partial)
    {
        ArgumentNullException.ThrowIfNull(partial);

        ValueTask renderPartialTask = default;

#pragma warning disable CA2012 // Use ValueTasks correctly: Completion handled by HandleSynchronousCompletion
        if (_bufferWriter is not null)
        {
            renderPartialTask = partial.RenderToBufferWriterAsync(_bufferWriter, _outputFlush, _htmlEncoder, CancellationToken);
        }
        else if (_textWriter is not null)
        {
            renderPartialTask = partial.RenderToTextWriterAsync(_textWriter, _htmlEncoder, CancellationToken);
        }
#pragma warning restore CA2012
        else
        {
#if NET7_0_OR_GREATER
            throw new UnreachableException();
#else
            Debug.Fail("Unreachable");
            throw new InvalidOperationException();
#endif
        }

        if (renderPartialTask.HandleSynchronousCompletion())
        {
            return ValueTask.FromResult(HtmlString.Empty);
        }

        return AwaitPartialTask(renderPartialTask);
    }

    private static async ValueTask<HtmlString> AwaitPartialTask(ValueTask partialTask)
    {
        await partialTask;
        return HtmlString.Empty;
    }
}
