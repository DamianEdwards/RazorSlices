using System.ComponentModel;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Razor.Runtime.TagHelpers;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace RazorSlices;

public partial class RazorSlice
{
    /// <summary>Constructs an SDK-discovered streaming Tag Helper and binds it to this render.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    protected new TTagHelper CreateTagHelper<TTagHelper>() where TTagHelper : RazorSliceTagHelper, new()
    {
        EnsureRendering();
        var helper = new TTagHelper();
        helper.Bind(this);
        return helper;
    }

    /// <summary>Executes Tag Helper children without redirecting the slice's writer.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    protected new static void StartTagHelperWritingScope(HtmlEncoder? encoder)
    {
        if (encoder is not null)
        {
            throw new NotSupportedException("Streaming Tag Helpers do not support child-content encoder overrides.");
        }
    }

    /// <summary>Returns empty captured content because Tag Helper children stream directly.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    protected new static TagHelperContent EndTagHelperWritingScope() => TagHelperContentCache.Empty;

    /// <summary>Begins attribute-local capture required by the Razor SDK.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    protected void BeginWriteTagHelperAttribute() => PushWriter(new TagHelperAttributeWriter());

    /// <summary>Restores the output and returns a captured Tag Helper attribute.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    protected string EndWriteTagHelperAttribute()
    {
        var writer = GetTagHelperAttributeWriter();
        PopWriter();
        using (writer) { return writer.ToString(); }
    }

    /// <summary>Begins capture of a conditional, unbound Tag Helper attribute.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    protected void BeginAddHtmlAttributeValues(TagHelperExecutionContext context, string name, int count, HtmlAttributeValueStyle style)
        => PushWriter(new TagHelperAttributeWriter(context, name, count, style));

    /// <summary>Adds an SDK-generated piece of a conditional Tag Helper attribute.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    protected void AddHtmlAttributeValue(string prefix, int prefixOffset, object? value, int valueOffset, int valueLength, bool isLiteral)
    {
        var writer = GetTagHelperAttributeWriter();
        if (writer.Count == 1 && string.IsNullOrEmpty(prefix))
        {
            if (value is null or false)
            {
                writer.Context!.AddTagHelperAttribute(writer.Name!, value?.ToString() ?? "", writer.Style);
                writer.Suppressed = true;
                return;
            }
            if (value is true)
            {
                writer.Context!.AddHtmlAttribute(writer.Name!, writer.Name!, writer.Style);
                writer.Suppressed = true;
                return;
            }
        }
        if (value is not null)
        {
            WriteLiteral(prefix);
            if (isLiteral) { WriteLiteral(value); }
            else { Write(value); }
        }
    }

    /// <summary>Completes conditional Tag Helper attribute capture.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    protected void EndAddHtmlAttributeValues(TagHelperExecutionContext context)
    {
        var writer = GetTagHelperAttributeWriter();
        PopWriter();
        using (writer)
        {
            if (!writer.Suppressed)
            {
                context.AddHtmlAttribute(writer.Name!, new HtmlString(writer.ToString()), writer.Style);
            }
        }
    }

    /// <summary>Formats the SDK's error for an uninitialized dictionary-bound attribute.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    protected static string InvalidTagHelperIndexerAssignment(string attribute, string type, string property)
        => $"Cannot assign Tag Helper attribute '{attribute}' because '{type}.{property}' is null.";

    internal async ValueTask<HtmlString> RenderTagHelperBodyAsync(TagHelperOutput output)
    {
        var root = GetHelperRoot();
        var previous = root._activeHelperSlice;
        root._activeHelperSlice = this;
        try
        {
            await output.GetChildContentAsync();
            return HtmlString.Empty;
        }
        finally { root._activeHelperSlice = previous; }
    }

    private TagHelperAttributeWriter GetTagHelperAttributeWriter()
        => _textWriter as TagHelperAttributeWriter
            ?? throw new InvalidOperationException("There is no active Tag Helper attribute capture.");

    private void UnwindTagHelperAttributeScopes()
    {
        while (_writerStack is { Count: > 0 })
        {
            var capture = _textWriter as TagHelperAttributeWriter;
            PopWriter();
            capture?.Dispose();
        }
    }

    private static class TagHelperContentCache
    {
        internal static readonly TagHelperContent Empty = new DefaultTagHelperContent();
    }

    private sealed class TagHelperAttributeWriter(
        TagHelperExecutionContext? context = null, string? name = null, int count = 0,
        HtmlAttributeValueStyle style = HtmlAttributeValueStyle.DoubleQuotes) : StringWriter
    {
        internal TagHelperExecutionContext? Context { get; } = context;
        internal string? Name { get; } = name;
        internal int Count { get; } = count;
        internal HtmlAttributeValueStyle Style { get; } = style;
        internal bool Suppressed { get; set; }
    }
}
