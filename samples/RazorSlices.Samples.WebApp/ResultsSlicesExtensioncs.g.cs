using System.Reflection;
using Microsoft.AspNetCore.Http.HttpResults;

namespace RazorSlices.Samples.WebApp;

#nullable enable

/// <summary>
/// 
/// </summary>
public static class RazorSlicesExtensions
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="_"></param>
    /// <param name="statusCode"></param>
    /// <returns></returns>
    public static RazorSliceHttpResult RazorSlice<TSliceProxy>(this IResultExtensions _, int statusCode = StatusCodes.Status200OK)
        where TSliceProxy : IRazorSliceProxy
    {
        var result = (RazorSliceHttpResult)TSliceProxy.Create();
        result.StatusCode = statusCode;
        return result;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="TModel"></typeparam>
    /// <param name="_"></param>
    /// <param name="sliceProxy"></param>
    /// <param name="model"></param>
    /// <param name="statusCode"></param>
    /// <returns></returns>
    public static RazorSliceHttpResult<TModel> RazorSlice<TSliceProxy, TModel>(this IResultExtensions _, TModel model, int statusCode = StatusCodes.Status200OK)
        where TSliceProxy : IRazorSliceProxy
    {
        var result = (RazorSliceHttpResult<TModel>)TSliceProxy.Create(model);
        result.StatusCode = statusCode;
        return result;
    }
}
