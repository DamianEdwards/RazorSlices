using Microsoft.AspNetCore.Razor.TagHelpers;

namespace RazorSlices;

/// <summary>A forward-only streaming Tag Helper. Instances are constructed for each tag invocation.</summary>
/// <remarks>Only one rendering Tag Helper may match an element. Children can be rendered once, but not captured or replayed.</remarks>
public abstract class RazorSliceTagHelper : TagHelper
{
    private static readonly object RendererKey = new();
    private RazorSlice? _owner;

    /// <inheritdoc />
    public sealed override int Order => 0;

    internal void Bind(RazorSlice owner) => _owner = owner;

    /// <inheritdoc />
    public sealed override void Init(TagHelperContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Items.TryGetValue(RendererKey, out var renderer) && ReferenceEquals(renderer, context))
        {
            throw new InvalidOperationException("Only one Razor Slice Tag Helper may render an element.");
        }
        context.Items[RendererKey] = context;
    }

    /// <inheritdoc />
    public sealed override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(output);
        var owner = _owner ?? throw new InvalidOperationException("The Tag Helper is not bound to a rendering slice.");
        var render = new RazorSliceTagHelperContext(owner, context, output);
        try
        {
            owner.CancellationToken.ThrowIfCancellationRequested();
            output.SuppressOutput();
            await RenderAsync(render);
        }
        finally
        {
            render.Complete();
            _owner = null;
        }
    }

    /// <summary>Writes this tag's output, optionally rendering its forward-only body.</summary>
    protected abstract ValueTask RenderAsync(RazorSliceTagHelperContext context);
}

/// <summary>A discoverable C# adapter that renders a model-based SDK-compiled Razor Slice fragment as a Tag Helper.</summary>
/// <typeparam name="TSlice">The generated slice proxy.</typeparam>
/// <typeparam name="TModel">The template's model, which may carry its Tag Helper context.</typeparam>
/// <remarks>The template must not use a layout; attempting to render one throws <see cref="NotSupportedException"/>.</remarks>
public abstract class RazorSliceTagHelper<TSlice, TModel> : RazorSliceTagHelper
    where TSlice : IRazorSliceProxy<TModel>
{
    /// <summary>Creates the strongly typed model from the adapter's bound attributes.</summary>
    protected abstract TModel CreateModel(RazorSliceTagHelperContext context);

    /// <inheritdoc />
    protected sealed override async ValueTask RenderAsync(RazorSliceTagHelperContext context)
    {
#pragma warning disable CA2000 // Disposed on layout rejection or by RenderPartialAsync.
        var template = TSlice.CreateSlice(CreateModel(context));
        if (template is IUsesLayout)
        {
            template.Dispose();
            throw new NotSupportedException("A .cshtml Tag Helper template must be a fragment and cannot use a layout.");
        }
        await context.RenderPartialAsync(template);
#pragma warning restore CA2000
    }
}
