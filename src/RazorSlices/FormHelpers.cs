using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http;

namespace RazorSlices;

/// <summary>A rendering facade for built-in form helpers. Instances do not retain request data.</summary>
public readonly record struct FormHelpers
{
    private readonly IHelperContext _context;

    internal FormHelpers(IHelperContext context) => _context = context;

    /// <summary>Renders an encoded hidden field from tokens prepared by WithAntiforgery before response start.</summary>
    public HtmlString AntiforgeryToken(HttpContext? httpContext)
    {
        _context.EnsureActive();
        _context.GetHelper<Implementation, Implementation>(static () => new()).AntiforgeryToken(httpContext);
        return HtmlString.Empty;
    }

    private sealed class Implementation : RazorSliceHelpers
    {
        public override Task ExecuteAsync()
            => throw new InvalidOperationException("Form helpers must be called through their rendering facade.");

        public void AntiforgeryToken(HttpContext? httpContext)
        {
            if (httpContext is null)
            {
                throw new InvalidOperationException("Rendering an antiforgery token requires an HttpContext.");
            }
            var tokens = httpContext.Features.Get<AntiforgeryTokenSet>()
                ?? throw new InvalidOperationException(
                    "Prepare antiforgery tokens with .WithAntiforgery() before rendering the form.");
#if RAZOR_SLICES_DISABLE_UTF8_LITERAL_OVERLOAD
            WriteLiteral("<input type=\"hidden\" name=\"");
#else
            WriteLiteral("<input type=\"hidden\" name=\""u8);
#endif
            Write(tokens.FormFieldName);
#if RAZOR_SLICES_DISABLE_UTF8_LITERAL_OVERLOAD
            WriteLiteral("\" value=\"");
#else
            WriteLiteral("\" value=\""u8);
#endif
            Write(tokens.RequestToken);
#if RAZOR_SLICES_DISABLE_UTF8_LITERAL_OVERLOAD
            WriteLiteral("\" />");
#else
            WriteLiteral("\" />"u8);
#endif
        }
    }
}
