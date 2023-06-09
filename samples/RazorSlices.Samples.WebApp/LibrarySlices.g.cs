using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Http.HttpResults;

// Generated

namespace RazorSlices.Samples.RazorClassLibrary.Slices;

internal sealed class FromLibrary : IRazorSliceProxy<RazorSliceHttpResult>
{
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties)]
    private static readonly Type _type = Assembly.Load("RazorSlices.Samples.RazorClassLibrary").GetType("AspNetCoreGeneratedDocument.Slices_FromLibrary");
    private static readonly ConstructorInfo _ctor = _type.GetConstructor(Type.EmptyTypes)!;
    private static readonly SliceFactory _factory = RuntimeFeature.IsDynamicCodeCompiled
        ? RazorSlice.GetSliceFactory(_type)
        : static () => (RazorSlice)_ctor.Invoke(null);

    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor
        | DynamicallyAccessedMemberTypes.PublicProperties
        | DynamicallyAccessedMemberTypes.PublicMethods, "AspNetCoreGeneratedDocument.Slices_FromLibrary", "RazorSlices.Samples.RazorClassLibrary")]
    public static RazorSliceHttpResult Create() => RazorSlice.CreateHttpResult(_factory);
}

internal sealed class Todos : IRazorSliceProxy<RazorSliceHttpResult>
{
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties)]
    private static readonly Type _type = Assembly.Load("RazorSlices.Samples.RazorClassLibrary").GetType("AspNetCoreGeneratedDocument.Slices_Todos");
    private static readonly ConstructorInfo _ctor = _type.GetConstructor(Type.EmptyTypes)!;
    private static SliceFactory _factory = RuntimeFeature.IsDynamicCodeCompiled
        ? RazorSlice.GetSliceFactory(_type)
        : static () => (RazorSlice) _ctor.Invoke(null);

    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor
        | DynamicallyAccessedMemberTypes.PublicProperties
        | DynamicallyAccessedMemberTypes.PublicMethods, "AspNetCoreGeneratedDocument.Slices_Todos", "RazorSlices.Samples.RazorClassLibrary")]
    public static RazorSliceHttpResult Create() => RazorSlice.CreateHttpResult(_factory);
}
