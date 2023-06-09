using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Http.HttpResults;

// TODO: Source generate this

namespace RazorSlices.Samples.RazorClassLibrary.Slices;

public sealed class FromLibrary : IRazorSliceProxy<RazorSliceHttpResult>
{
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    private static readonly Type _type = Type.GetType("AspNetCoreGeneratedDocument.Slices_FromLibrary, RazorSlices.Samples.RazorClassLibrary");
    private static readonly SliceFactory _factory = RuntimeFeature.IsDynamicCodeCompiled
        ? RazorSliceFactory.GetSliceFactory(_type)
        : static () => (RazorSlice)Activator.CreateInstance(_type);

    [DynamicDependency(DynamicallyAccessedMemberTypes.All, "AspNetCoreGeneratedDocument.Slices_FromLibrary", "RazorSlices.Samples.RazorClassLibrary")]
    public static RazorSliceHttpResult Create() => RazorSliceFactory.CreateHttpResult(_factory);
}

public sealed class Todos : IRazorSliceProxy<RazorSliceHttpResult>
{
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    private static readonly Type _type = Type.GetType("AspNetCoreGeneratedDocument.Slices_Todos, RazorSlices.Samples.RazorClassLibrary");
    private static SliceFactory _factory = RuntimeFeature.IsDynamicCodeCompiled
        ? RazorSliceFactory.GetSliceFactory(_type)
        : static () => (RazorSlice)Activator.CreateInstance(_type);

    [DynamicDependency(DynamicallyAccessedMemberTypes.All, "AspNetCoreGeneratedDocument.Slices_Todos", "RazorSlices.Samples.RazorClassLibrary")]
    public static RazorSliceHttpResult Create() => RazorSliceFactory.CreateHttpResult(_factory);
}
