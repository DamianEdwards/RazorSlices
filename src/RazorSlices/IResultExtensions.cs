using Microsoft.AspNetCore.Http.HttpResults;
using RazorSlices;

#if NET7_0_OR_GREATER
namespace Microsoft.AspNetCore.Http;

///// <summary>
///// 
///// </summary>
//public static class RazorSlicesExtensions
//{
//    /// <summary>
//    /// 
//    /// </summary>
//    /// <param name="_"></param>
//    /// <param name="sliceProxy"></param>
//    /// <param name="statusCode"></param>
//    /// <returns></returns>
//    public static RazorSliceHttpResult RazorSlice(this IResultExtensions _, IRazorSliceProxy sliceProxy, int statusCode = StatusCodes.Status200OK)
//    {
//        var result = (RazorSliceHttpResult)sliceProxy.Create();
//        result.StatusCode = statusCode;
//        return result;
//    }

//    /// <summary>
//    /// 
//    /// </summary>
//    /// <typeparam name="TModel"></typeparam>
//    /// <param name="_"></param>
//    /// <param name="sliceProxy"></param>
//    /// <param name="model"></param>
//    /// <param name="statusCode"></param>
//    /// <returns></returns>
//    public static RazorSliceHttpResult<TModel> RazorSlice<TModel>(this IResultExtensions _, IRazorSliceProxy<TModel> sliceProxy, TModel model, int statusCode = StatusCodes.Status200OK)
//    {
//        var result = (RazorSliceHttpResult<TModel>)sliceProxy.Create(model);
//        result.StatusCode = statusCode;
//        return result;
//    }
//}
#endif
