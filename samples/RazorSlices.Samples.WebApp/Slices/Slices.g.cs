using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Http.HttpResults;
using RazorSlices.Samples.WebApp.Services;

// Generated

namespace RazorSlices.Samples.WebApp.Slices;

public sealed class Todo : IRazorSliceProxy<RazorSliceHttpResult<RazorSlices.Samples.WebApp.Todo>, RazorSlices.Samples.WebApp.Todo>
{
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties)]
    private static readonly Type _type = Type.GetType("AspNetCoreGeneratedDocument.Slices_Todo");
    private static readonly ConstructorInfo _ctor = _type.GetConstructor(Type.EmptyTypes)!;
    private static readonly SliceFactory<RazorSlices.Samples.WebApp.Todo> _factory = RuntimeFeature.IsDynamicCodeCompiled
        ? RazorSliceFactory.GetSliceFactory<RazorSlices.Samples.WebApp.Todo>(_type)
        : static (model) =>
        {
            var slice = (RazorSlice<RazorSlices.Samples.WebApp.Todo>)_ctor.Invoke(null);
            slice.Model = model;
            return slice;
        };

    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor
        | DynamicallyAccessedMemberTypes.PublicProperties
        | DynamicallyAccessedMemberTypes.PublicMethods, "AspNetCoreGeneratedDocument.Slices_Todo", "RazorSlices.Samples.WebApp")]
    public static RazorSliceHttpResult<RazorSlices.Samples.WebApp.Todo> Create(RazorSlices.Samples.WebApp.Todo model) => RazorSlice.CreateHttpResult(_factory, model);
}

public sealed class Todos : IRazorSliceProxy<RazorSliceHttpResult<RazorSlices.Samples.WebApp.Todo[]>, RazorSlices.Samples.WebApp.Todo[]>
{
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties)]
    private static readonly Type _type = Type.GetType("AspNetCoreGeneratedDocument.Slices_Todos");
    private static readonly ConstructorInfo _ctor = _type.GetConstructor(Type.EmptyTypes)!;
    private static readonly SliceFactory<RazorSlices.Samples.WebApp.Todo[]> _factory = RuntimeFeature.IsDynamicCodeCompiled
        ? RazorSliceFactory.GetSliceFactory<RazorSlices.Samples.WebApp.Todo[]>(_type)
        : static (model) =>
        {
            var slice = (RazorSlice<RazorSlices.Samples.WebApp.Todo[]>)_ctor.Invoke(null);
            slice.Model = model;
            return slice;
        };

    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor
        | DynamicallyAccessedMemberTypes.PublicProperties
        | DynamicallyAccessedMemberTypes.PublicMethods, "AspNetCoreGeneratedDocument.Slices_Todos", "RazorSlices.Samples.WebApp")]
    public static RazorSliceHttpResult<RazorSlices.Samples.WebApp.Todo[]> Create(RazorSlices.Samples.WebApp.Todo[] model) => RazorSlice.CreateHttpResult(_factory, model);
}

public sealed class TodoRow : IRazorSliceProxy<RazorSlice<RazorSlices.Samples.WebApp.Todo>, RazorSlices.Samples.WebApp.Todo>
{
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties)]
    private static readonly Type _type = Type.GetType("AspNetCoreGeneratedDocument.Slices_TodoRow");
    private static readonly ConstructorInfo _ctor = _type.GetConstructor(Type.EmptyTypes)!;
    private static readonly SliceFactory<RazorSlices.Samples.WebApp.Todo> _factory = RuntimeFeature.IsDynamicCodeCompiled
        ? RazorSliceFactory.GetSliceFactory<RazorSlices.Samples.WebApp.Todo>(_type)
        : static (model) =>
        {
            var slice = (RazorSlice<RazorSlices.Samples.WebApp.Todo>)_ctor.Invoke(null);
            slice.Model = model;
            return slice;
        };

    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor
        | DynamicallyAccessedMemberTypes.PublicProperties
        | DynamicallyAccessedMemberTypes.PublicMethods, "AspNetCoreGeneratedDocument.Slices_TodoRow", "RazorSlices.Samples.WebApp")]
    public static RazorSlice<RazorSlices.Samples.WebApp.Todo> Create(RazorSlices.Samples.WebApp.Todo model) => RazorSliceFactory.Create(_factory, model);
}

public sealed class _Footer : IRazorSliceProxy<RazorSlice>
{
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties)]
    private static readonly Type _type = Type.GetType("AspNetCoreGeneratedDocument.Slices__Footer");
    private static readonly ConstructorInfo _ctor = _type.GetConstructor(Type.EmptyTypes)!;
    private static readonly SliceFactory _factory = RuntimeFeature.IsDynamicCodeCompiled
        ? RazorSliceFactory.GetSliceFactory(Type.GetType("AspNetCoreGeneratedDocument.Slices__Footer"))
        : static () => (RazorSlice)_ctor.Invoke(null);

    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor
        | DynamicallyAccessedMemberTypes.PublicProperties
        | DynamicallyAccessedMemberTypes.PublicMethods, "AspNetCoreGeneratedDocument.Slices__Footer", "RazorSlices.Samples.WebApp")]
    public static RazorSlice Create() => RazorSliceFactory.Create(_factory);
}

public sealed class LoremDynamic : IRazorSliceProxy<RazorSliceHttpResult<LoremParams>, LoremParams>
{
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties)]
    private static readonly Type _type = Type.GetType("AspNetCoreGeneratedDocument.Slices_LoremDynamic");
    private static readonly ConstructorInfo _ctor = _type.GetConstructor(Type.EmptyTypes)!;
    private static readonly SliceFactory<LoremParams> _factory = RuntimeFeature.IsDynamicCodeCompiled
        ? RazorSliceFactory.GetSliceFactory<LoremParams>(_type)
        : static (model) =>
        {
            var slice = (RazorSlice<LoremParams>)_ctor.Invoke(null);
            slice.Model = model;
            return slice;
        };

    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor
        | DynamicallyAccessedMemberTypes.PublicProperties
        | DynamicallyAccessedMemberTypes.PublicMethods, "AspNetCoreGeneratedDocument.Slices_LoremDynamic", "RazorSlices.Samples.WebApp")]
    public static RazorSliceHttpResult<LoremParams> Create(LoremParams model) => RazorSlice.CreateHttpResult(_factory, model);
}

public sealed class LoremFormattable : IRazorSliceProxy<RazorSliceHttpResult<LoremParams>, LoremParams>
{
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties)]
    private static readonly Type _type = Type.GetType("AspNetCoreGeneratedDocument.Slices_LoremForattable");
    private static readonly ConstructorInfo _ctor = _type.GetConstructor(Type.EmptyTypes)!;
    private static readonly SliceFactory<LoremParams> _factory = RuntimeFeature.IsDynamicCodeCompiled
        ? RazorSliceFactory.GetSliceFactory<LoremParams>(_type)
        : static (model) =>
        {
            var slice = (RazorSlice<LoremParams>)_ctor.Invoke(null);
            slice.Model = model;
            return slice;
        };

    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor
        | DynamicallyAccessedMemberTypes.PublicProperties
        | DynamicallyAccessedMemberTypes.PublicMethods, "AspNetCoreGeneratedDocument.Slices_LoremFormattable", "RazorSlices.Samples.WebApp")]
    public static RazorSliceHttpResult<LoremParams> Create(LoremParams model) => RazorSlice.CreateHttpResult(_factory, model);
}

public sealed class LoremHtmlContent : IRazorSliceProxy<RazorSliceHttpResult<HtmlContentParams>, HtmlContentParams>
{
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties)]
    private static readonly Type _type = Type.GetType("AspNetCoreGeneratedDocument.Slices_LoremHtmlContent");
    private static readonly ConstructorInfo _ctor = _type.GetConstructor(Type.EmptyTypes)!;
    private static readonly SliceFactory<HtmlContentParams> _factory = RuntimeFeature.IsDynamicCodeCompiled
        ? RazorSliceFactory.GetSliceFactory<HtmlContentParams>(_type)
        : static (model) =>
        {
            var slice = (RazorSlice<HtmlContentParams>)_ctor.Invoke(null);
            slice.Model = model;
            return slice;
        };

    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor
        | DynamicallyAccessedMemberTypes.PublicProperties
        | DynamicallyAccessedMemberTypes.PublicMethods, "AspNetCoreGeneratedDocument.Slices_LoremHtmlContent", "RazorSlices.Samples.WebApp")]
    public static RazorSliceHttpResult<HtmlContentParams> Create(HtmlContentParams model) => RazorSlice.CreateHttpResult(_factory, model);
}

public sealed class LoremInjectableProperties : IRazorSliceProxy<RazorSliceHttpResult<LoremParams>, LoremParams>
{
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties)]
    private static readonly Type _type = Type.GetType("AspNetCoreGeneratedDocument.Slices_LoremInjectableProperties");
    private static readonly ConstructorInfo _ctor = _type.GetConstructor(Type.EmptyTypes)!;
    private static readonly SliceFactory<LoremParams> _factory = RuntimeFeature.IsDynamicCodeCompiled
        ? RazorSliceFactory.GetSliceFactory<LoremParams>(_type, new[] { _type.GetProperty("LoremService")! })
        : static (model) =>
        {
            var slice = (RazorSlice<LoremParams>)_ctor.Invoke(null);
            slice.Initialize = static (s, sp) =>
            {
                _type.GetProperty("LoremService").SetValue(s, sp.GetRequiredService<LoremService>());
            };
            slice.Model = model;
            return slice;
        };

    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor
        | DynamicallyAccessedMemberTypes.PublicProperties
        | DynamicallyAccessedMemberTypes.PublicMethods, "AspNetCoreGeneratedDocument.Slices_LoremInjectableProperties", "RazorSlices.Samples.WebApp")]
    public static RazorSliceHttpResult<LoremParams> Create(LoremParams model) => RazorSlice.CreateHttpResult(_factory, model);
}

public sealed class LoremStatic : IRazorSliceProxy<RazorSliceHttpResult>
{
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties)]
    private static readonly Type _type = Type.GetType("AspNetCoreGeneratedDocument.Slices_LoremStatic");
    private static readonly ConstructorInfo _ctor = _type.GetConstructor(Type.EmptyTypes)!;
    private static readonly SliceFactory _factory = RuntimeFeature.IsDynamicCodeCompiled
        ? RazorSliceFactory.GetSliceFactory(_type)
        : static () => (RazorSlice)_ctor.Invoke(null);

    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor
        | DynamicallyAccessedMemberTypes.PublicProperties
        | DynamicallyAccessedMemberTypes.PublicMethods, "AspNetCoreGeneratedDocument.Slices_LoremStatic", "RazorSlices.Samples.WebApp")]
    public static RazorSliceHttpResult Create() => RazorSlice.CreateHttpResult(_factory);
}

public sealed class Unicode : IRazorSliceProxy<RazorSliceHttpResult>
{
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties)]
    private static readonly Type _type = Type.GetType("AspNetCoreGeneratedDocument.Slices_Unicode");
    private static readonly ConstructorInfo _ctor = _type.GetConstructor(Type.EmptyTypes)!;
    private static readonly SliceFactory _factory = RuntimeFeature.IsDynamicCodeCompiled
        ? RazorSliceFactory.GetSliceFactory(_type)
        : static () => (RazorSlice)_ctor.Invoke(null);

    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor
        | DynamicallyAccessedMemberTypes.PublicProperties
        | DynamicallyAccessedMemberTypes.PublicMethods, "AspNetCoreGeneratedDocument.Slices_Unicode", "RazorSlices.Samples.WebApp")]
    public static RazorSliceHttpResult Create() => RazorSlice.CreateHttpResult(_factory);
}
