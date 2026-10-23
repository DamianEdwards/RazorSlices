using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http;

namespace RazorSlices;

/// <summary>
/// The normal Razor template base, supporting dependency injection, helpers, layouts, partials, and HTTP results.
/// </summary>
public abstract partial class RazorSlice : RazorSliceBase, IHelperContext
{
    private Stack<(PipeWriter?, TextWriter?)>? _writerStack;
    private IServiceProvider? _serviceProvider;
    private bool _initialized;
    private RazorSliceHelperCache? _helpers;
    private RazorSlice? _helperParent;
    private RazorSliceBase? _activeHelperSlice;
    internal SliceActivation? Activation { get; set; }

    /// <summary>Gets the context for helpers shared throughout this render graph.</summary>
    protected IHelperContext Helpers => this;

    /// <summary>Gets or sets the provider used for property injection.</summary>
    public IServiceProvider? ServiceProvider
    {
        get => _serviceProvider ??= HttpContext?.RequestServices;
        set => _serviceProvider = value;
    }

    /// <summary>Gets or sets the current HTTP context.</summary>
    public HttpContext? HttpContext { get; set; }

    /// <summary>Gets or sets the infrastructure callback for deferred property injection.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public Action<RazorSlice, IServiceProvider?, HttpContext?>? Initialize
    {
        get => Activation?.Initialize;
        set => Activation = value is null ? null : Activation is { } activation ? activation with { Initialize = value } : new(value);
    }

    void IHelperContext.EnsureActive()
    {
        GetActiveHelperSlice();
    }

    TContract IHelperContext.GetHelper<TContract, TImplementation>(Func<TImplementation> create)
    {
        ((IHelperContext)this).EnsureActive();
        ArgumentNullException.ThrowIfNull(create);
        var root = GetHelperRoot();
        var helpers = root._helpers ??= new();
        if (helpers.TryGetValue(typeof(TImplementation), out var existing))
        {
            return (TContract)(object)existing;
        }
        var helper = RazorSliceHelperActivation<TImplementation>.Create(create)
            ?? throw new InvalidOperationException("The helper factory returned null.");
        if (helper is not TContract contract)
        {
            throw new InvalidOperationException($"Helper {helper.GetType()} does not implement {typeof(TContract)}.");
        }
        helper.Bind(root);
        helpers.Add(typeof(TImplementation), helper);
        return contract;
    }

    private RazorSlice GetHelperRoot()
    {
        var root = this;
        while (root._helperParent is not null) { root = root._helperParent; }
        return root;
    }

    internal RazorSliceBase GetActiveHelperSlice()
    {
        var root = GetHelperRoot();
        var active = root._activeHelperSlice ?? root;
        active.EnsureRendering();
        return active;
    }

    private RazorSliceBase? EnterHelperRender()
    {
        if (_helperParent is null) { return null; }
        var root = GetHelperRoot();
        var previous = root._activeHelperSlice;
        root._activeHelperSlice = this;
        return previous;
    }

    private void ExitHelperRender(RazorSliceBase? previous)
    {
        if (_helperParent is not null) { GetHelperRoot()._activeHelperSlice = previous; }
    }

    /// <summary>Required by the Razor SDK for templated Razor delegates.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    protected void PushWriter(TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        (_writerStack ??= new()).Push((_pipeWriter, _textWriter));
        _textWriter = writer;
        _pipeWriter = null;
    }

    /// <summary>Restores the writer after a templated Razor delegate.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    protected void PopWriter()
    {
        Debug.Assert(_writerStack is not null);
        (_pipeWriter, _textWriter) = _writerStack.Pop();
    }

    internal Task ExecuteAsyncImpl()
    {
        EnsureInitialized();
        return ExecuteAsync();
    }

    /// <inheritdoc />
    protected override ValueTask RenderCoreAsync(bool renderLayout, bool disposeSlice)
    {
        try
        {
            EnsureInitialized();
            if (renderLayout && this is IUsesLayout usesLayout)
            {
                var layout = CreateLayoutForRender(usesLayout);
                return _pipeWriter is not null
                    ? layout.RenderToPipeWriterAsync(_pipeWriter, _htmlEncoder, CancellationToken)
                    : layout.RenderToTextWriterAsync(_textWriter!, _htmlEncoder, CancellationToken);
            }
        }
        catch (Exception error)
        {
            DisposeAfterFailure(error);
            throw;
        }

        var previous = EnterHelperRender();
        try
        {
            var execute = ExecuteAsync();
            if (!execute.HandleSynchronousCompletion())
            {
                return AwaitExecute(this, execute, previous, disposeSlice);
            }
        }
        catch (Exception error)
        {
            ExitHelperRender(previous);
            DisposeAfterFailure(error);
            throw;
        }
        ExitHelperRender(previous);
        if (disposeSlice) { Dispose(); }
        return AutoFlush();
    }

    private static async ValueTask AwaitExecute(RazorSlice slice, Task execute, RazorSliceBase? previous, bool dispose)
    {
        try
        {
            await execute;
            await slice.AutoFlush();
        }
        catch (Exception error)
        {
            slice.ExitHelperRender(previous);
            if (dispose) { slice.DisposeAfterFailure(error); }
            throw;
        }
        slice.ExitHelperRender(previous);
        if (dispose) { slice.Dispose(); }
    }

    private RazorSlice CreateLayoutForRender(IUsesLayout usesLayout)
    {
        var layout = usesLayout.CreateLayoutImpl();
        if (ReferenceEquals(layout, this))
        {
            throw new InvalidOperationException("A layout must be a fresh slice outside the current render tree.");
        }
        if (layout is not IRazorLayoutSlice container)
        {
            throw new InvalidOperationException($"Layout slices must inherit from {nameof(RazorLayoutSlice)} or {nameof(RazorLayoutSlice)}<TModel>.");
        }
        container.ContentSlice = this;
        CopySliceState(this, layout);
        var root = GetHelperRoot();
        layout._helpers = root._helpers;
        root._helpers = null;
        root._helperParent = layout;
        return layout;
    }

    internal static void CopySliceState(RazorSlice source, RazorSlice destination)
    {
        destination.HttpContext = source.HttpContext;
        destination.ServiceProvider = source._serviceProvider;
    }

    internal ValueTask<HtmlString> RenderSectionAsyncImpl(string sectionName)
    {
        EnsureInitialized();
        var previous = EnterHelperRender();
        try
        {
            var section = ExecuteSectionAsync(sectionName);
            if (!section.HandleSynchronousCompletion()) { return AwaitSection(this, section, previous); }
        }
        catch { ExitHelperRender(previous); throw; }
        ExitHelperRender(previous);
        return ValueTask.FromResult(HtmlString.Empty);
    }

    private static async ValueTask<HtmlString> AwaitSection(RazorSlice slice, Task section, RazorSliceBase? previous)
    {
        try { await section; return HtmlString.Empty; }
        finally { slice.ExitHelperRender(previous); }
    }

    private void EnsureInitialized()
    {
        if (!_initialized && Activation?.Initialize is { } initialize)
        {
            initialize(this, _serviceProvider, HttpContext);
            _initialized = true;
        }
    }

    /// <inheritdoc />
    [SuppressMessage("Usage", "CA1816", Justification = "Base.Dispose calls GC.SuppressFinalize.")]
    public override void Dispose()
    {
        if (IsDisposed) { return; }
        base.Dispose();
        try
        {
            if (this is IRazorLayoutSlice { ContentSlice: { } content }) { content.Dispose(); }
        }
        finally
        {
            _helpers?.Clear();
            _helpers = null;
        }
    }
}
