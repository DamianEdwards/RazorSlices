using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace RazorSlices;

/// <summary>Access to the active slice output and a single-use, streaming Tag Helper body.</summary>
public sealed class RazorSliceTagHelperContext
{
    private RazorSlice? _owner;
    private TagHelperContext? _context;
    private TagHelperOutput? _output;
    private bool _bodyRendered;

    internal RazorSliceTagHelperContext(RazorSlice owner, TagHelperContext context, TagHelperOutput output)
    {
        _owner = owner;
        _context = context;
        _output = output;
    }

    private RazorSlice Owner
    {
        get
        {
            var owner = _owner ?? throw new InvalidOperationException("The Tag Helper render has completed.");
            owner.EnsureRendering();
            return owner;
        }
    }

    /// <summary>Gets the input tag's name.</summary>
    public string TagName { get { _ = Owner; return _context!.TagName; } }

    /// <summary>Gets the input tag's syntax.</summary>
    public TagMode TagMode { get { _ = Owner; return _output!.TagMode; } }

    /// <summary>Gets unbound attributes that can be forwarded to rendered markup.</summary>
    public ReadOnlyTagHelperAttributeList Attributes { get { _ = Owner; return _output!.Attributes; } }

    /// <summary>Gets all input attributes, including those bound to the Tag Helper.</summary>
    public ReadOnlyTagHelperAttributeList AllAttributes { get { _ = Owner; return _context!.AllAttributes; } }

    /// <summary>Gets helpers shared throughout the current render graph.</summary>
    public IHelperContext Helpers => Owner;

    /// <summary>Gets the calling slice's service provider, if one is available.</summary>
    public IServiceProvider? ServiceProvider => Owner.ServiceProvider;

    /// <summary>Gets the calling slice's HTTP context, if one is available.</summary>
    public HttpContext? HttpContext => Owner.HttpContext;

    /// <summary>Gets the calling render's cancellation token.</summary>
    public CancellationToken CancellationToken => Owner.CancellationToken;

    /// <summary>Writes trusted markup without HTML encoding.</summary>
    public void WriteLiteral(string? value) => Owner.WriteLiteral(value);

#if !RAZOR_SLICES_DISABLE_UTF8_LITERAL_OVERLOAD
    /// <summary>Writes trusted UTF-8 markup without decoding it for pipe output.</summary>
    public void WriteLiteral(ReadOnlySpan<byte> value) => Owner.WriteLiteral(value);
#endif

    /// <summary>Writes a value with the slice's normal formatting and HTML-content semantics.</summary>
    public void Write<T>(T? value) => Owner.Write(value);

    /// <summary>Writes a complete HTML attribute using its original quoting and encoding semantics.</summary>
    public void WriteAttribute(TagHelperAttribute attribute)
    {
        ArgumentNullException.ThrowIfNull(attribute);
        Owner.WriteHtml(attribute);
    }

    /// <summary>Flushes the active output without capturing the Tag Helper body.</summary>
    public ValueTask<HtmlString> FlushAsync() => Owner.FlushAsync();

    /// <summary>Streams the input body's SDK-generated rendering once, at the current position.</summary>
    public ValueTask<HtmlString> RenderBodyAsync()
    {
        var owner = Owner;
        if (_bodyRendered)
        {
            throw new InvalidOperationException("A streaming Tag Helper body can only be rendered once.");
        }
        owner.CancellationToken.ThrowIfCancellationRequested();
        _bodyRendered = true;
        return owner.RenderTagHelperBodyAsync(_output!);
    }

    /// <summary>Renders a full slice inline, sharing the calling slice's output and helper graph.</summary>
    public ValueTask<HtmlString> RenderPartialAsync(RazorSlice partial) => Owner.RenderPartialAsync(partial);

    /// <summary>Renders a full or minimal slice inline using the calling slice's output, encoder, and cancellation token.</summary>
    public ValueTask<HtmlString> RenderPartialAsync(RazorSliceBase partial) => Owner.RenderPartialAsync(partial);

    /// <summary>Renders a model-free SDK-compiled slice inline.</summary>
    public ValueTask<HtmlString> RenderPartialAsync<TSlice>() where TSlice : IRazorSliceProxy
        => Owner.RenderPartialAsync<TSlice>();

    /// <summary>Renders a model-based SDK-compiled slice inline.</summary>
    public ValueTask<HtmlString> RenderPartialAsync<TSlice, TModel>(TModel model) where TSlice : IRazorSliceProxy<TModel>
        => Owner.RenderPartialAsync<TSlice, TModel>(model);

    internal void Complete()
    {
        _owner = null;
        _context = null;
        _output = null;
    }
}
