using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace RazorSlices;

/// <summary>Stores the opaque RazorSlices TempData payload. This contract is independent of MVC TempData.</summary>
[Experimental("RSLICE0200")]
public interface IRazorSliceTempDataProvider
{
    /// <summary>Loads the request payload, returning an empty array when none exists.</summary>
    byte[] Load(HttpContext context);
    /// <summary>Persists the payload, or removes it when empty. Must run before response start.</summary>
    void Save(HttpContext context, byte[] payload);
}

/// <summary>Configures experimental RazorSlices TempData. Payloads are not MVC-compatible.</summary>
[Experimental("RSLICE0200")]
public sealed class RazorSliceTempDataOptions
{
    /// <summary>Cookie settings. Consent is required when cookie policy requires it.</summary>
    public CookieBuilder Cookie { get; set; } = new()
    {
        Name = ".RazorSlices.TempData",
        HttpOnly = true,
        SameSite = SameSiteMode.Lax,
        SecurePolicy = CookieSecurePolicy.SameAsRequest
    };

    /// <summary>Maximum unprotected payload size, including keys and type tags. Defaults to 64 KiB.</summary>
    public int MaxPayloadBytes { get; set; } = 64 * 1024;
}

internal sealed class RazorSliceCookieTempDataProvider(
    IDataProtectionProvider protection, IOptions<RazorSliceTempDataOptions> options,
    ILogger<RazorSliceCookieTempDataProvider> logger) : IRazorSliceTempDataProvider
{
    private static readonly Action<ILogger, Exception?> LogLoadFailure = LoggerMessage.Define(
        LogLevel.Warning, new EventId(1, "TempDataCookieLoadFailure"),
        "The RazorSlices TempData cookie could not be decoded or unprotected.");
    private readonly IDataProtector _protector = protection.CreateProtector("RazorSlices.TempData.v2");
    private readonly ChunkingCookieManager _cookies = new();

    public byte[] Load(HttpContext context)
    {
        var cookie = _cookies.GetRequestCookie(context, options.Value.Cookie.Name!);
        if (string.IsNullOrEmpty(cookie)) { return []; }
        try
        {
            return _protector.Unprotect(WebEncoders.Base64UrlDecode(cookie));
        }
        catch (Exception error) when (error is CryptographicException or FormatException)
        {
            LogLoadFailure(logger, error);
            if (!context.Response.HasStarted) { Delete(context); }
            return [];
        }
    }

    public void Save(HttpContext context, byte[] payload)
    {
        if (payload.Length == 0) { Delete(context); return; }
        var cookie = options.Value.Cookie;
        _cookies.AppendResponseCookie(context, cookie.Name!,
            WebEncoders.Base64UrlEncode(_protector.Protect(payload)), cookie.Build(context));
    }

    private void Delete(HttpContext context)
    {
        var cookie = options.Value.Cookie;
        _cookies.DeleteCookie(context, cookie.Name!, cookie.Build(context));
    }
}

/// <summary>Stores experimental RazorSlices TempData in ASP.NET Core session. Requires session middleware.</summary>
[Experimental("RSLICE0200")]
public sealed class RazorSliceSessionTempDataProvider : IRazorSliceTempDataProvider
{
    private const string Key = "__RazorSlicesTempData.v2";

    /// <inheritdoc />
    public byte[] Load(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        // Defer removal until Save, so a failed handler does not consume the stored payload.
        return context.Session.TryGetValue(Key, out var value) ? value : [];
    }

    /// <inheritdoc />
    public void Save(HttpContext context, byte[] payload)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(payload);
        if (payload.Length == 0) { context.Session.Remove(Key); }
        else { context.Session.Set(Key, payload); }
    }
}
