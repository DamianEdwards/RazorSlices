using Microsoft.AspNetCore.Razor.TagHelpers;

namespace RazorSlices.Samples.WebApp.TagHelpers;

public sealed record CardTagModel(string Title, RazorSliceTagHelperContext Context);

[HtmlTargetElement("card")]
public sealed class CardTagHelper : RazorSliceTagHelper<Slices.TagHelpers.CardTemplate, CardTagModel>
{
    public string Title { get; set; } = "";

    protected override CardTagModel CreateModel(RazorSliceTagHelperContext context) => new(Title, context);
}
