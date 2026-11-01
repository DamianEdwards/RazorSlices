using Microsoft.AspNetCore.Razor.TagHelpers;

namespace RazorSlices;

/// <summary>Renders a full or minimal slice instance inline without its layout.</summary>
/// <remarks>The partial rendering owns disposal of the supplied instance. Supply a fresh instance for each invocation.</remarks>
[HtmlTargetElement("partial", TagStructure = TagStructure.WithoutEndTag)]
public sealed class RazorSlicePartialTagHelper : RazorSliceTagHelper
{
    /// <summary>Gets or sets the single-use slice instance to render.</summary>
    public RazorSliceBase? Slice { get; set; }

    /// <inheritdoc />
    protected override async ValueTask RenderAsync(RazorSliceTagHelperContext context)
    {
        var slice = Slice ?? throw new InvalidOperationException("The partial Tag Helper requires a non-null 'slice' attribute.");
        await context.RenderPartialAsync(slice);
    }
}
