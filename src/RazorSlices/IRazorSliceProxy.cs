using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Html;

namespace RazorSlices;

/// <summary>
/// Represents a type that can create <see cref="RazorSlice"/> instances.
/// </summary>
/// <remarks>
/// You do not need to implement this interface. Types that implement this interface will be automatically generated for
/// each <c>.cshtml</c> file in your project by the Razor Slices source generator.
/// </remarks>
public interface IRazorSliceProxy
{
    /// <summary>
    /// Creates a new <see cref="RazorSlice"/> instance.
    /// </summary>
    /// <returns>The slice instance.</returns>
    abstract static RazorSlice Create();

    /// <summary>
    /// Creates a new <see cref="RazorSlice{TModel}"/> instance.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <returns>The slice instance.</returns>
    abstract static RazorSlice<TModel> Create<TModel>(TModel model);
}


//internal sealed class Sample : IRazorSliceProxy
//{
//    [DynamicDependency(DynamicallyAccessedMemberTypes.All, TypeName, "RazorSlices.Samples.WebApp")]
//    private const string TypeName = "AspNetCoreGeneratedDocument.Slices_Todo, RazorSlices.Samples.WebApp";
//    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
//    private static readonly Type _sliceType = Type.GetType(TypeName)
//        ?? throw new InvalidOperationException($"Razor view type '{TypeName}' was not found. This is likely a bug in the RazorSlices source generator.");
//    private static readonly SliceDefinition _sliceDefinition = new(_sliceType);

//    public static RazorSlice Create() => _sliceDefinition.CreateSlice();
//    public static RazorSlice<TModel> Create<TModel>(TModel model) => _sliceDefinition.CreateSlice(model);

//    public static ValueTask<PartialRenderer> RenderAsPartialAsync(RazorSlice parent)
//    {
//        var slice = Create();
//        var renderTask = parent.RenderPartialAsync(slice);
//        return ValueTask.FromResult(HtmlString.Empty);
//    }
//}

//public class PartialRenderer : IHtmlContent
//{

//}
