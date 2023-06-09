using Microsoft.AspNetCore.Http.HttpResults;
using RazorSlices;

#if NET7_0_OR_GREATER
namespace Microsoft.AspNetCore.Http;

/// <summary>
/// 
/// </summary>
public static class RazorSlicesExtensions
{
    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="TSlice"></typeparam>
    /// <param name="_"></param>
    /// <returns></returns>
    public static RazorSliceHttpResult RazorSlice<TSlice>(this IResultExtensions _)
        where TSlice : IRazorSliceProxy<RazorSliceHttpResult>
    {
        return TSlice.Create();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="TSlice"></typeparam>
    /// <typeparam name="TModel"></typeparam>
    /// <param name="_"></param>
    /// <param name="model"></param>
    /// <returns></returns>
    public static RazorSliceHttpResult<TModel> RazorSlice<TSlice, TModel>(this IResultExtensions _, TModel model)
        where TSlice : IRazorSliceProxy<RazorSliceHttpResult<TModel>, TModel>
    {
        return TSlice.Create(model);
    }
}
#endif
