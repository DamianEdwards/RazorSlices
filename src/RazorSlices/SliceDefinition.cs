using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace RazorSlices;

/// <summary>
/// 
/// </summary>
public class SliceDefinition
{
    /// <summary>
    /// Creates a new instance of <see cref="SliceDefinition"/>.
    /// </summary>
    /// <param name="sliceTypeName">The type name of the slice.</param>
    /// <exception cref="ArgumentException">Thrown if the specified slice type cannot be loaded.</exception>
    public SliceDefinition(string sliceTypeName)
    {
        SliceType = Type.GetType(sliceTypeName) ?? throw new ArgumentException($"Slice type {sliceTypeName} could not be loaded.", nameof(sliceTypeName));
        HasModel = RazorSliceFactory.IsModelSlice(SliceType);
        ModelProperty = SliceType.GetProperty("Model");
        ModelType = ModelProperty?.PropertyType;
        InjectableProperties = RazorSliceFactory.GetInjectableProperties(SliceType);
        Factory = RazorSliceFactory.GetSliceFactory(this);
    }

    /// <summary>
    /// 
    /// </summary>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    public Type SliceType { get; }

    /// <summary>
    /// 
    /// </summary>
    public bool HasModel { get; }

    /// <summary>
    /// 
    /// </summary>
    public PropertyInfo? ModelProperty { get; }

    /// <summary>
    /// 
    /// </summary>
    public Type? ModelType { get; }

    /// <summary>
    /// 
    /// </summary>
    public (bool Any, PropertyInfo[] Nullable, PropertyInfo[] NonNullable) InjectableProperties { get; }

    /// <summary>
    /// 
    /// </summary>
    public Delegate Factory { get; }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public RazorSlice CreateSlice() => HasModel
        ? throw new InvalidOperationException($"Slice {SliceType.Name} requires a model of type {ModelType?.Name}. Call Create<TModel>(TModel model) instead.")
        : ((Func<RazorSlice>)Factory)();

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="TModel"></typeparam>
    /// <param name="model"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public RazorSlice<TModel> CreateSlice<TModel>(TModel model) => !HasModel || !typeof(TModel).IsAssignableTo(ModelType)
        ? throw new InvalidOperationException($"""
            Cannot use model of type {typeof(TModel).Name} with slice {SliceType.Name}.
            {(HasModel ? $"Ensure the model is assignable to {ModelType!.Name}" : "It is not a strongly-typed slice.")}
            """)
        : (RazorSlice<TModel>)((Func<TModel, RazorSlice>)Factory)(model);
}
