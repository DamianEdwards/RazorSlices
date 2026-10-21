using Microsoft.AspNetCore.Http;

namespace RazorSlices;

internal sealed record SliceActivation(
    Action<RazorSlice, IServiceProvider?, HttpContext?>? Initialize,
    SliceServiceBindings? Services = null,
    Func<RazorSlice>? CreateReplacement = null)
{
    internal static readonly SliceActivation Empty = new(Initialize: null);
}
