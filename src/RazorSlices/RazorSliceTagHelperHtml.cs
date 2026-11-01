using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Html;

namespace RazorSlices;

/// <summary>A minimal replacement for the SDK's injected Html helper when Tag Helper attribute lowering calls Html.Raw.</summary>
/// <remarks>Register this type as a service and use <c>@inject RazorSliceTagHelperHtml Html</c> in templates that need it. No MVC services are required.</remarks>
public sealed class RazorSliceTagHelperHtml
{
    /// <summary>Marks an SDK-captured, already encoded attribute value as HTML.</summary>
    [SuppressMessage("Performance", "CA1822", Justification = "The Razor SDK emits an instance call on its injected Html property.")]
    public HtmlString Raw(string? value) => new(value);
}
