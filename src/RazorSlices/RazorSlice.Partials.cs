using System.Diagnostics;
using Microsoft.AspNetCore.Html;

namespace RazorSlices;

public partial class RazorSlice
{
    /// <summary>
    /// Renders a template inline. Call this method from within a template to render another template, e.g.
    /// <c>@await RenderPartialAsync("/_Footer.cshtml")</c>
    /// </summary>
    /// <param name="name">The name of the partial template to render.</param>
    /// <returns>A <see cref="ValueTask"/> representing the rendering of the template.</returns>
    /// <exception cref="InvalidOperationException"></exception>
    protected ValueTask<HtmlString> RenderPartialAsync(string name)
    {
        var partialDefinition = ResolveSliceDefinition(name);

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
                The template '{name}' has @inject properties but the current slice was not provided an IServiceProvider.
                You can only render partials with @inject properties from a slice that itself has @inject properties.
                """);
        }

        var partialSlice = partialDefinition.HasInjectableProperties
            ? ((SliceWithServicesFactory)partialDefinition.Factory)(ServiceProvider!)
            : ((SliceFactory)partialDefinition.Factory)();

        return RenderPartialAsyncImpl(partialSlice);
    }

    /// <summary>
    /// Renders a template inline. Call this method from within a template to render another template, e.g.
    /// <c>@await RenderPartialAsync("/_Footer.cshtml")</c>
    /// </summary>
    /// <param name="name">The name of the partial template to render.</param>
    /// <param name="model">The model to use when rendering the partial template.</param>
    /// <returns>A <see cref="ValueTask"/> representing the rendering of the template.</returns>
    /// <exception cref="InvalidOperationException"></exception>
    protected ValueTask<HtmlString> RenderPartialAsync<TModel>(string name, TModel model)
    {
        var partialDefinition = ResolveSliceDefinition(name);

        if (partialDefinition.ModelType is null || !partialDefinition.ModelType.IsAssignableFrom(typeof(TModel)))
        {
            throw new InvalidOperationException($"""
                The template '{name}' requires does not require a model of type '{typeof(TModel).Name}'.
                Call RenderPartialAsync(string name) to render the template without a model.
                """);
        }

        if (partialDefinition.HasInjectableProperties && ServiceProvider is null)
        {
            throw new InvalidOperationException($"""
                The template '{name}' has @inject properties but the current template was not provided an IServiceProvider.
                You can only render partials with @inject properties from a template that itself has @inject properties.
                """);
        }

        var partialSlice = partialDefinition.HasInjectableProperties
            ? ((SliceWithServicesFactory<TModel>)partialDefinition.Factory)(model, ServiceProvider!)
            : ((SliceFactory<TModel>)partialDefinition.Factory)(model);

        return RenderPartialAsyncImpl(partialSlice);
    }

    /// <summary>
    /// Renders a template inline.
    /// </summary>
    /// <param name="partial">The template instance to render.</param>
    /// <returns>A <see cref="ValueTask"/> representing the rendering of the template.</returns>
    protected internal ValueTask<HtmlString> RenderPartialAsync(RazorSlice partial)
    {
        ArgumentNullException.ThrowIfNull(partial);

        return RenderPartialAsyncImpl(partial);
    }

    private ValueTask<HtmlString> RenderPartialAsyncImpl(RazorSlice partial)
    {
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
