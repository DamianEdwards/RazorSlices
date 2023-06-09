using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.AspNetCore.Mvc.Razor.Internal;

namespace RazorSlices.Samples.WebApp.Slices
{

    // Generated
    internal sealed class Todo : IRazorSliceProxy<RazorSlices.Samples.WebApp.Todo>
    {
        private static SliceFactory<RazorSlices.Samples.WebApp.Todo> _factory = (SliceFactory<RazorSlices.Samples.WebApp.Todo>)RazorSlice.GetSliceFactory(Type.GetType("AspNetCoreGeneratedDocument.Slices_Todo"));

        [DynamicDependency(DynamicallyAccessedMemberTypes.All, "AspNetCoreGeneratedDocument.Slices_Todo", "RazorSlices.Samples.WebApp")]
        public static RazorSlice<RazorSlices.Samples.WebApp.Todo> Create(RazorSlices.Samples.WebApp.Todo model) => RazorSlice.CreateHttpResult(_factory, model);
    }

    internal sealed class Todos : IRazorSliceProxy<RazorSlices.Samples.WebApp.Todo[]>
    {
        private static SliceFactory<RazorSlices.Samples.WebApp.Todo[]> _factory = (SliceFactory<RazorSlices.Samples.WebApp.Todo[]>)RazorSlice.GetSliceFactory(Type.GetType("AspNetCoreGeneratedDocument.Slices_Todos"));

        [DynamicDependency(DynamicallyAccessedMemberTypes.All, "AspNetCoreGeneratedDocument.Slices_Todos", "RazorSlices.Samples.WebApp")]
        public static RazorSlice<RazorSlices.Samples.WebApp.Todo[]> Create(RazorSlices.Samples.WebApp.Todo[] model) => RazorSlice.CreateHttpResult(_factory, model);
    }

    internal sealed class TodoRow : IRazorSliceProxy<RazorSlices.Samples.WebApp.Todo>
    {
        private static SliceFactory<RazorSlices.Samples.WebApp.Todo> _factory = (SliceFactory<RazorSlices.Samples.WebApp.Todo>)RazorSlice.GetSliceFactory(Type.GetType("AspNetCoreGeneratedDocument.Slices_TodoRow"));

        [DynamicDependency(DynamicallyAccessedMemberTypes.All, "AspNetCoreGeneratedDocument.Slices_TodoRow", "RazorSlices.Samples.WebApp")]
        public static RazorSlice<RazorSlices.Samples.WebApp.Todo> Create(RazorSlices.Samples.WebApp.Todo model) => RazorSlice.CreateHttpResult(_factory, model);
    }

    internal sealed class _Footer : IRazorSliceProxy
    {
        private static SliceFactory _factory = (SliceFactory)RazorSlice.GetSliceFactory(Type.GetType("AspNetCoreGeneratedDocument.Slices__Footer"));

        [DynamicDependency(DynamicallyAccessedMemberTypes.All, "AspNetCoreGeneratedDocument.Slices__Footer", "RazorSlices.Samples.WebApp")]
        public static RazorSlice Create() => RazorSlice.Create(_factory);
    }

    internal sealed class LoremDynamic : IRazorSliceProxy<LoremParams>
    {
        private static SliceFactory<LoremParams> _factory = (SliceFactory<LoremParams>)RazorSlice.GetSliceFactory(Type.GetType("AspNetCoreGeneratedDocument.Slices_LoremDynamic"));

        [DynamicDependency(DynamicallyAccessedMemberTypes.All, "AspNetCoreGeneratedDocument.Slices_LoremDynamic", "RazorSlices.Samples.WebApp")]
        public static RazorSlice<LoremParams> Create(LoremParams model) => RazorSlice.CreateHttpResult(_factory, model);
    }

    internal sealed class LoremFormattable : IRazorSliceProxy<LoremParams>
    {
        private static SliceFactory<LoremParams> _factory = (SliceFactory<LoremParams>)RazorSlice.GetSliceFactory(Type.GetType("AspNetCoreGeneratedDocument.Slices_LoremFormattable"));

        [DynamicDependency(DynamicallyAccessedMemberTypes.All, "AspNetCoreGeneratedDocument.Slices_LoremFormattable", "RazorSlices.Samples.WebApp")]
        public static RazorSlice<LoremParams> Create(LoremParams model) => RazorSlice.CreateHttpResult(_factory, model);
    }

    internal sealed class LoremHtmlContent : IRazorSliceProxy<HtmlContentParams>
    {
        private static SliceFactory<HtmlContentParams> _factory = (SliceFactory<HtmlContentParams>)RazorSlice.GetSliceFactory(Type.GetType("AspNetCoreGeneratedDocument.Slices_LoremHtmlContent"));

        [DynamicDependency(DynamicallyAccessedMemberTypes.All, "AspNetCoreGeneratedDocument.Slices_LoremHtmlContent", "RazorSlices.Samples.WebApp")]
        public static RazorSlice<HtmlContentParams> Create(HtmlContentParams model) => RazorSlice.CreateHttpResult(_factory, model);
    }

    internal sealed class LoremInjectableProperties : IRazorSliceProxy<LoremParams>
    {
        private static readonly Type _type = Type.GetType("AspNetCoreGeneratedDocument.Slices_LoremInjectableProperties");
        private static readonly SliceFactory<LoremParams> _factory = (SliceFactory<LoremParams>)RazorSlice.GetSliceFactory(
            _type,
            _type.GetProperties().Where(p => p.GetCustomAttribute<RazorInjectAttribute>() is not null));
        private static readonly Action<RazorSlice, IServiceProvider> _init = (slice, sp) => { };

        [DynamicDependency(DynamicallyAccessedMemberTypes.All, "AspNetCoreGeneratedDocument.Slices_LoremInjectableProperties", "RazorSlices.Samples.WebApp")]
        public static RazorSlice<LoremParams> Create(LoremParams model)
        {
            var slice = RazorSlice.CreateHttpResult(_factory, model);
            slice.Initialize = _init;
            return slice;
        }
    }

    internal sealed class LoremStatic : IRazorSliceProxy
    {
        private static SliceFactory _factory = (SliceFactory)RazorSlice.GetSliceFactory(Type.GetType("AspNetCoreGeneratedDocument.Slices_LoremStatic"));

        [DynamicDependency(DynamicallyAccessedMemberTypes.All, "AspNetCoreGeneratedDocument.Slices_LoremStatic", "RazorSlices.Samples.WebApp")]
        public static RazorSlice Create() => RazorSlice.CreateHttpResult(_factory);
    }

    internal sealed class Unicode : IRazorSliceProxy
    {
        private static SliceFactory _factory = (SliceFactory)RazorSlice.GetSliceFactory(Type.GetType("AspNetCoreGeneratedDocument.Slices_Unicode"));

        [DynamicDependency(DynamicallyAccessedMemberTypes.All, "AspNetCoreGeneratedDocument.Slices_Unicode", "RazorSlices.Samples.WebApp")]
        public static RazorSlice Create() => RazorSlice.CreateHttpResult(_factory);
    }
}

namespace RazorSlices
{
    public interface IRazorSliceProxy
    {
        abstract static RazorSlice Create();
    }

    public interface IRazorSliceProxy<TModel>
    {
        abstract static RazorSlice<TModel> Create(TModel model);
    }
}

//namespace Microsoft.AspNetCore.Http
//{
//    internal static class RazorSlicesExtensions
//    {
//        private static readonly SliceFactory<LoremParams> _factory = (SliceFactory<LoremParams>)RazorSlice.GetSliceFactory(Type.GetType("AspNetCoreGeneratedDocument.Slices_LoremInjectableProperties"));
//        private static readonly Action<RazorSlice, IServiceProvider> _init = (slice, sp) => { };

//        public static RazorSliceHttpResult<LoremParams> Slice<TSlice>(this IResultExtensions _, LoremParams model)
//        {

//        }

//        public static RazorSliceHttpResult<TModel> Slice<TSlice, TModel>(this IResultExtensions _, TModel model)
//        {
//            if (typeof(TSlice) == typeof(LoremInjectableProperties))
//            {
//                var slice = RazorSlice.CreateHttpResult(_factory, (LoremParams)(object)model);
//                slice.Initialize = _init;
//                return slice;
//            }

//            throw new InvalidOperationException("Unknown slice type");
//        }
//    }
//}