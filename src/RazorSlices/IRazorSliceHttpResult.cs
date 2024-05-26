using Microsoft.AspNetCore.Http;

namespace RazorSlices;

/// <summary>
/// Represents an <see cref="IResult"/> that renders a <see cref="RazorSlice"/> template to the response.
/// </summary>
public interface IRazorSliceHttpResult : IResult, IStatusCodeHttpResult, IContentTypeHttpResult, IDisposable
{

}
