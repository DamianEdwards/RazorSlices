using System.Diagnostics.CodeAnalysis;

#nullable enable

// TODO: Source generate this assuming only knowing the .cshtml file names in the project

namespace RazorSlices.Samples.WebApp.Slices;

public sealed class Todo : IRazorSliceProxy
{
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, TypeName, "RazorSlices.Samples.WebApp")]
    private const string TypeName = "AspNetCoreGeneratedDocument.Slices_Todo, RazorSlices.Samples.WebApp";
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    private static readonly Type _sliceType = Type.GetType(TypeName)!;
    private static readonly SliceDefinition _sliceDefinition = new(_sliceType);

    public static RazorSlice Create() => _sliceDefinition.CreateSlice();
    public static RazorSlice<TModel> Create<TModel>(TModel model) => _sliceDefinition.CreateSlice(model);
}

public sealed class Todos : IRazorSliceProxy
{
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, TypeName, "RazorSlices.Samples.WebApp")]
    private const string TypeName = "AspNetCoreGeneratedDocument.Slices_Todos, RazorSlices.Samples.WebApp";
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    private static readonly Type _sliceType = Type.GetType(TypeName)!;
    private static readonly SliceDefinition _sliceDefinition = new(_sliceType);

    public static RazorSlice Create() => _sliceDefinition.CreateSlice();
    public static RazorSlice<TModel> Create<TModel>(TModel model) => _sliceDefinition.CreateSlice(model);
}

public sealed class TodoRow : IRazorSliceProxy
{
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, TypeName, "RazorSlices.Samples.WebApp")]
    private const string TypeName = "AspNetCoreGeneratedDocument.Slices_TodoRow, RazorSlices.Samples.WebApp";
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    private static readonly Type _sliceType = Type.GetType(TypeName)!;
    private static readonly SliceDefinition _sliceDefinition = new(_sliceType);

    public static RazorSlice Create() => _sliceDefinition.CreateSlice();
    public static RazorSlice<TModel> Create<TModel>(TModel model) => _sliceDefinition.CreateSlice(model);
}

public sealed class _Footer : IRazorSliceProxy
{
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, TypeName, "RazorSlices.Samples.WebApp")]
    private const string TypeName = "AspNetCoreGeneratedDocument.Slices__Footer, RazorSlices.Samples.WebApp";
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    private static readonly Type _sliceType = Type.GetType(TypeName)!;
    private static readonly SliceDefinition _sliceDefinition = new(_sliceType);

    public static RazorSlice Create() => _sliceDefinition.CreateSlice();
    public static RazorSlice<TModel> Create<TModel>(TModel model) => _sliceDefinition.CreateSlice(model);
}

public sealed class LoremDynamic : IRazorSliceProxy
{
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, TypeName, "RazorSlices.Samples.WebApp")]
    private const string TypeName = "AspNetCoreGeneratedDocument.Slices_LoremDynamic, RazorSlices.Samples.WebApp";
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    private static readonly Type _sliceType = Type.GetType(TypeName)!;
    private static readonly SliceDefinition _sliceDefinition = new(_sliceType);

    public static RazorSlice Create() => _sliceDefinition.CreateSlice();
    public static RazorSlice<TModel> Create<TModel>(TModel model) => _sliceDefinition.CreateSlice(model);
}

public sealed class LoremFormattable : IRazorSliceProxy
{
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, TypeName, "RazorSlices.Samples.WebApp")]
    private const string TypeName = "AspNetCoreGeneratedDocument.Slices_LoremFormattable, RazorSlices.Samples.WebApp";
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    private static readonly Type _sliceType = Type.GetType(TypeName)!;
    private static readonly SliceDefinition _sliceDefinition = new(_sliceType);

    public static RazorSlice Create() => _sliceDefinition.CreateSlice();
    public static RazorSlice<TModel> Create<TModel>(TModel model) => _sliceDefinition.CreateSlice(model);
}

public sealed class LoremHtmlContent : IRazorSliceProxy
{
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, TypeName, "RazorSlices.Samples.WebApp")]
    private const string TypeName = "AspNetCoreGeneratedDocument.Slices_LoremHtmlContent, RazorSlices.Samples.WebApp";
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    private static readonly Type _sliceType = Type.GetType(TypeName)!;
    private static readonly SliceDefinition _sliceDefinition = new(_sliceType);

    public static RazorSlice Create() => _sliceDefinition.CreateSlice();
    public static RazorSlice<TModel> Create<TModel>(TModel model) => _sliceDefinition.CreateSlice(model);
}

public sealed class LoremInjectableProperties : IRazorSliceProxy
{
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, TypeName, "RazorSlices.Samples.WebApp")]
    private const string TypeName = "AspNetCoreGeneratedDocument.Slices_LoremInjectableProperties, RazorSlices.Samples.WebApp";
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    private static readonly Type _sliceType = Type.GetType(TypeName)!;
    private static readonly SliceDefinition _sliceDefinition = new(_sliceType);

    public static RazorSlice Create() => _sliceDefinition.CreateSlice();
    public static RazorSlice<TModel> Create<TModel>(TModel model) => _sliceDefinition.CreateSlice(model);
}

public sealed class LoremStatic : IRazorSliceProxy
{
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, TypeName, "RazorSlices.Samples.WebApp")]
    private const string TypeName = "AspNetCoreGeneratedDocument.Slices_LoremStatic, RazorSlices.Samples.WebApp";
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    private static readonly Type _sliceType = Type.GetType(TypeName)!;
    private static readonly SliceDefinition _sliceDefinition = new(_sliceType);

    public static RazorSlice Create() => _sliceDefinition.CreateSlice();
    public static RazorSlice<TModel> Create<TModel>(TModel model) => _sliceDefinition.CreateSlice(model);
}

public sealed class Unicode : IRazorSliceProxy
{
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, TypeName, "RazorSlices.Samples.WebApp")]
    private const string TypeName = "AspNetCoreGeneratedDocument.Slices_Unicode, RazorSlices.Samples.WebApp";
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    private static readonly Type _sliceType = Type.GetType(TypeName)!;
    private static readonly SliceDefinition _sliceDefinition = new(_sliceType);

    public static RazorSlice Create() => _sliceDefinition.CreateSlice();
    public static RazorSlice<TModel> Create<TModel>(TModel model) => _sliceDefinition.CreateSlice(model);
}
