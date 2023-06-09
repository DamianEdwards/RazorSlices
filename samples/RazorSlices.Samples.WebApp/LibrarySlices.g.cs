using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Http.HttpResults;

namespace RazorSlices.Samples.RazorClassLibrary.Slices;

internal sealed class FromLibrary
{
    private static SliceFactory _factory = (SliceFactory)RazorSlice.GetSliceFactory(Type.GetType("AspNetCoreGeneratedDocument.Slices_Todo"));

    [DynamicDependency(DynamicallyAccessedMemberTypes.All, "AspNetCoreGeneratedDocument.Slices_FromLibrary", "RazorSlices.Samples.RazorClassLibrary")]
    public static RazorSliceHttpResult Create() => RazorSlice.CreateHttpResult(_factory);
}

internal sealed class Todos
{
    private static SliceFactory _factory = (SliceFactory)RazorSlice.GetSliceFactory(Type.GetType("AspNetCoreGeneratedDocument.Slices_Todos"));

    [DynamicDependency(DynamicallyAccessedMemberTypes.All, "AspNetCoreGeneratedDocument.Slices_Todos", "RazorSlices.Samples.RazorClassLibrary")]
    public static RazorSliceHttpResult Create() => RazorSlice.CreateHttpResult(_factory);
}
