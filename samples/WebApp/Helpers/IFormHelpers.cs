using Microsoft.AspNetCore.Http;

namespace RazorSlices.Samples.WebApp.Helpers;

public interface IFormHelpers
{
    void AntiforgeryToken(HttpContext? httpContext);
}
