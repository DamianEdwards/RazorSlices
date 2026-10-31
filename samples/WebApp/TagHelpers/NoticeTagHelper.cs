using Microsoft.AspNetCore.Razor.TagHelpers;

namespace RazorSlices.Samples.WebApp.TagHelpers;

[HtmlTargetElement("notice")]
public sealed class NoticeTagHelper : RazorSliceTagHelper
{
    public string Label { get; set; } = "";
    public bool Show { get; set; } = true;

    protected override async ValueTask RenderAsync(RazorSliceTagHelperContext context)
    {
        if (!Show) { return; }
        context.WriteLiteral("<aside");
        foreach (var attribute in context.Attributes)
        {
            context.WriteLiteral(" ");
            context.WriteAttribute(attribute);
        }
        context.WriteLiteral("><strong>");
        context.Write(Label);
        context.WriteLiteral("</strong>");
        await context.RenderBodyAsync();
        context.WriteLiteral("</aside>");
    }
}

[HtmlTargetElement("count-badge", TagStructure = TagStructure.WithoutEndTag)]
public sealed class CountBadgeTagHelper : RazorSliceTagHelper
{
    public int Count { get; set; }
    public bool Success { get; set; }

    protected override ValueTask RenderAsync(RazorSliceTagHelperContext context)
    {
        context.WriteLiteral(Success ? "<span class=\"badge text-bg-success\">" : "<span class=\"badge text-bg-secondary\">");
        context.Write(Count);
        context.WriteLiteral("</span>");
        return ValueTask.CompletedTask;
    }
}
