using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace RazorSlices;

public abstract partial class RazorSlice
{
    private const string TagHelpersNotSupportedMessage = "Tag Helpers are not supported in Razor Slices";
    private const string RazorPagesNotSupportedMessage = "The @page directive is not supported on Razor Slices";

    /// <summary>
    /// Not supported on Razor Slices. Do not use.
    /// </summary>
    [Obsolete(RazorPagesNotSupportedMessage, error: true)]
    protected ViewContext PageContext
    {
        get => throw new NotSupportedException(RazorPagesNotSupportedMessage);
        set => throw new NotSupportedException(RazorPagesNotSupportedMessage);
    }

    /// <summary>
    /// Not supported on Razor Slices. Do not use.
    /// </summary>
    [Obsolete(TagHelpersNotSupportedMessage, error: true)]
    protected TTagHelper CreateTagHelper<TTagHelper>() where TTagHelper : ITagHelper
    {
        throw new NotSupportedException(TagHelpersNotSupportedMessage);
    }

    /// <summary>
    /// Not supported on Razor Slices. Do not use.
    /// </summary>
    [Obsolete(TagHelpersNotSupportedMessage, error: true)]
    protected void StartTagHelperWritingScope(HtmlEncoder encoder)
    {
        throw new NotSupportedException(TagHelpersNotSupportedMessage);
    }

    /// <summary>
    /// Not supported on Razor Slices. Do not use.
    /// </summary>
    [Obsolete(TagHelpersNotSupportedMessage, error: true)]
    protected TagHelperContent EndTagHelperWritingScope()
    {
        throw new NotSupportedException(TagHelpersNotSupportedMessage);
    }

    /// <summary>
    /// Not supported on Razor Slices. Do not use.
    /// </summary>
    [Obsolete(TagHelpersNotSupportedMessage, error: true)]
    protected void DefineSection(string name, Func<Task> section)
    {
        throw new NotSupportedException("@section is not supported on Razor Slices. Override the RenderSectionAsync(string name) method instead.");
    }
}
