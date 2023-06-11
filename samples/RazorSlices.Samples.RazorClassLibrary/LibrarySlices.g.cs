using System.Diagnostics.CodeAnalysis;

// TODO: Source generate this

namespace RazorSlices.Samples.RazorClassLibrary.Slices;

public sealed class FromLibrary : IRazorSliceProxy
{
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, TypeName, "RazorSlices.Samples.WebApp")]
    private const string TypeName = "AspNetCoreGeneratedDocument.Slices_FromLibrary, RazorSlices.Samples.RazorClassLibrary";
    private static readonly SliceDefinition _sliceDefinition = new(TypeName);

    public static RazorSlice Create() => _sliceDefinition.CreateSlice();
    public static RazorSlice<TModel> Create<TModel>(TModel model) => _sliceDefinition.CreateSlice(model);
}

public sealed class Todos : IRazorSliceProxy
{
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, TypeName, "RazorSlices.Samples.WebApp")]
    private const string TypeName = "AspNetCoreGeneratedDocument.Slices_Todos, RazorSlices.Samples.RazorClassLibrary";
    private static readonly SliceDefinition _sliceDefinition = new(TypeName);

    public static RazorSlice Create() => _sliceDefinition.CreateSlice();
    public static RazorSlice<TModel> Create<TModel>(TModel model) => _sliceDefinition.CreateSlice(model);
}
