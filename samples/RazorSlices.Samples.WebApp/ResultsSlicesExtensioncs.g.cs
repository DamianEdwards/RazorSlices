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
        if (typeof(TSliceProxy) == typeof(Slices.Todo))
        {
            var result = (RazorSliceHttpResult)Slices.Todo.Create();
            result.StatusCode = statusCode;
            return result;
        }

        throw new InvalidOperationException("Unknown slice");
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
        if (typeof(TSliceProxy) == typeof(Slices.Todo))
        {
            var result = (RazorSliceHttpResult<TModel>)Slices.Todo.Create(model);
            result.StatusCode = statusCode;
            return result;
        }

        throw new InvalidOperationException("Unknown slice");
    }
}
