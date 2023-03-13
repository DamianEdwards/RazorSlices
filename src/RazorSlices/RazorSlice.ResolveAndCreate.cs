using System.Collections.ObjectModel;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.AspNetCore.Razor.Hosting;

namespace RazorSlices;

public abstract partial class RazorSlice
{
    private static readonly ReadOnlyDictionary<string, (Type, Delegate)> _slicesByName;
    private static readonly ReadOnlyDictionary<Type, (string, Delegate)> _slicesByType;

    static RazorSlice()
    {
        // TODO: Support loading slices from referenced assemblies
        var sliceDefinitions = LoadSlices();
        _slicesByName = sliceDefinitions
            // Add entries without leading slash
            .Concat(sliceDefinitions.Select(slice => (slice.Identifier[1..], slice.Type, slice.Factory)))
            // Add entries without .cshtml suffix
            .Concat(sliceDefinitions.Select(slice => (slice.Identifier[..slice.Identifier.LastIndexOf('.')], slice.Type, slice.Factory)))
            // Add entries without leading slash and .cshtml suffix
            .Concat(sliceDefinitions.Select(slice => (slice.Identifier[1..slice.Identifier.LastIndexOf('.')], slice.Type, slice.Factory)))
            .ToDictionary(entry => entry.Item1, entry => (entry.Type, entry.Factory))
            .AsReadOnly();
        _slicesByType = sliceDefinitions.ToDictionary(item => item.Type, item => (item.Identifier, item.Factory)).AsReadOnly();
    }

    private static List<(string Identifier, Type Type, Delegate Factory)> LoadSlices() => LoadSlices(Assembly.GetEntryAssembly());

    private static List<(string Identifier, Type Type, Delegate Factory)> LoadSlices(Assembly? assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var allSlices= assembly.GetCustomAttributes<RazorCompiledItemAttribute>()
            .Where(rci => rci.Kind == "mvc.1.0.view" && rci.Type.IsAssignableTo(typeof(RazorSlice)))
            .Select(rci => (rci.Identifier, rci.Type, GetSliceFactory(rci.Type)));

        return allSlices.ToList();
    }

    private static Delegate GetSliceFactory(Type sliceType)
    {
        if (IsModelSlice(sliceType))
        {
            if (sliceType.GetConstructor(Array.Empty<Type>()) == null)
            {
                throw new InvalidOperationException("Slice must have a parameterless constructor.");
            }

            // Strongly-typed model slice
            var modelPropInfo = sliceType.GetProperty(nameof(RazorSlice<object>.Model))!;
            var modelType = modelPropInfo.PropertyType;
            var razorOfModelType = typeof(RazorSlice<>).MakeGenericType(modelType);

            var factoryDelegateType = typeof(SliceFactory<>).MakeGenericType(modelType);

            // Make a SliceFactory<TModel> like:
            //
            // RazorSlice<TModel> CreateSlice<TModel>(TModel model)
            // {
            //     var slice = new SliceType();
            //     slice.Model = model
            //     return slice;
            // }

            var sliceVariable = Expression.Variable(sliceType, "slice");
            var modelParam = Expression.Parameter(modelType, "model");
            var returnTarget = Expression.Label(razorOfModelType);

            return Expression.Lambda(
                delegateType: factoryDelegateType,
                body: Expression.Block(
                    variables: new[] { sliceVariable },
                    Expression.Assign(sliceVariable, Expression.New(sliceType)),
                    Expression.Assign(Expression.MakeMemberAccess(sliceVariable, modelPropInfo), modelParam),
                    Expression.Label(returnTarget, sliceVariable)
                ),
                name: "CreateSlice",
                parameters: new[] { modelParam })
            .Compile();
        }

        return Expression.Lambda<SliceFactory>(Expression.New(sliceType)).Compile();
    }

    private static bool IsModelSlice(Type sliceType)
    {
        var baseType = sliceType.BaseType;

        while (baseType is not null)
        {
            if (baseType.IsGenericType && baseType.GetGenericTypeDefinition() == typeof(RazorSlice<>))
            {
                return true;
            }

            baseType = baseType.BaseType;
        }

        return false;
    }

    /// <summary>
    /// Resolves the generated <see cref="Type" /> for the provided template name.
    /// </summary>
    /// <param name="sliceName">The project-relative path to the template .cshtml file, e.g. /Slices/MyTemplate.cshtml<c></c></param>
    /// <returns>The <see cref="Type" /> generated from the template .cshtml file.</returns>
    public static Type ResolveSliceType(string sliceName) => ResolveSliceType(sliceName, typeof(RazorSlice));

    /// <summary>
    /// Resolves the generated <see cref="Type" /> for the provided template name with a typed model.
    /// </summary>
    /// <param name="sliceName">The project-relative path to the template .cshtml file, e.g. /Slices/MyTemplate.cshtml<c></c></param>
    /// <typeparam name="TModel">The template model type.</typeparam>
    /// <returns>The <see cref="Type" /> generated from the template .cshtml file.</returns>
    public static Type ResolveSliceType<TModel>(string sliceName) => ResolveSliceType(sliceName);

    /// <summary>
    /// Resolves a <see cref="SliceFactory" /> delegate for the provided template name.
    /// </summary>
    /// <param name="sliceName">The project-relative path to the template .cshtml file, e.g. /Slices/MyTemplate.cshtml<c></c></param>
    /// <returns>The <see cref="SliceFactory" /> delegate that can be used to create instances of the template.</returns>
    public static SliceFactory ResolveSliceFactory(string sliceName) => ResolveSliceFactoryImpl(sliceName);

    /// <summary>
    /// Resolves a <see cref="SliceFactory{TModel}" /> delegate for the provided template name with a typed model.
    /// </summary>
    /// <param name="sliceName">The project-relative path to the template .cshtml file, e.g. /Slices/MyTemplate.cshtml<c></c></param>
    /// <typeparam name="TModel">The template model type.</typeparam>
    /// <returns>The <see cref="SliceFactory{TModel}" /> delegate that can be used to create instances of the template.</returns>
    public static SliceFactory<TModel> ResolveSliceFactory<TModel>(string sliceName) => ResolveSliceFactoryImpl<TModel>(sliceName);

    /// <summary>
    /// Resolves a <see cref="SliceFactory" /> delegate for the provided template <see cref="Type" />.
    /// </summary>
    /// <param name="sliceType"></param>
    /// <returns></returns>
    public static SliceFactory ResolveSliceFactory(Type sliceType)
    {
        if (!_slicesByType.ContainsKey(sliceType))
        {
            throw new ArgumentException($"No Razor slice of type {sliceType.Name} was found.", nameof(sliceType));
        }

        var (_, sliceActivator) = _slicesByType[sliceType];

        return (SliceFactory)sliceActivator;
    }

    /// <summary>
    /// Creates an instance of a <see cref="RazorSlice" /> template for the provided template name.
    /// </summary>
    /// <remarks>
    /// Note that this method incurs a lookup cost each time it's invoked. If avoiding that cost is desirable consider using <see cref="ResolveSliceFactory(string)" />
    /// to resolve the template creation delegate once and then use <see cref="Create(SliceFactory)" /> each time a template instance is required.
    /// </remarks>
    /// <param name="sliceName">The project-relative path to the template .cshtml file, e.g. /Slices/MyTemplate.cshtml<c></c></param>
    /// <returns>A <see cref="RazorSlice" /> instance for the template.</returns>
    public static RazorSlice Create(string sliceName) => Create(ResolveSliceFactory(sliceName));

    /// <summary>
    /// Creates an instance of a <see cref="RazorSlice" /> template using the provided <see cref="SliceFactory" /> delegate.
    /// </summary>
    /// <param name="sliceFactory">The <see cref="SliceFactory" /> delegate to create the template with.</param>
    /// <returns>A <see cref="RazorSlice" /> instance for the template.</returns>
    public static RazorSlice Create(SliceFactory sliceFactory) => sliceFactory();

    /// <summary>
    /// Creates an instance of a <see cref="RazorSlice" /> template for the provided template name with a typed model.
    /// </summary>
    /// <param name="sliceName">The project-relative path to the template .cshtml file, e.g. /Slices/MyTemplate.cshtml<c></c></param>
    /// <param name="model">The model to use for the template instance.</param>
    /// <typeparam name="TModel">The model type of the template.</typeparam>
    /// <returns>A <see cref="RazorSlice{TModel}" /> instance for the template.</returns>
    public static RazorSlice<TModel> Create<TModel>(string sliceName, TModel model) => Create(ResolveSliceFactory<TModel>(sliceName), model);

    /// <summary>
    /// Creates an instance of a <see cref="RazorSlice" /> template using the provided <see cref="SliceFactory" /> delegate with a typed model.
    /// </summary>
    /// <param name="sliceFactory">The <see cref="SliceFactory" /> delegate to create the template with.</param>
    /// <param name="model">The model to use for the template instance.</param>
    /// <typeparam name="TModel">The model type of the template.</typeparam>
    /// <returns>A <see cref="RazorSlice{TModel}" /> instance for the template.</returns>
    public static RazorSlice<TModel> Create<TModel>(SliceFactory<TModel> sliceFactory, TModel model) => sliceFactory(model);

    private static Type ResolveSliceType(string sliceName, Type mustBeAssignableTo)
    {
        if (!_slicesByName.ContainsKey(sliceName))
        {
            throw new ArgumentException($"No Razor slice with name '{sliceName}' was found.", nameof(sliceName));
        }

        var sliceType = _slicesByName[sliceName];

        if (!sliceType.Item1.IsAssignableTo(mustBeAssignableTo))
        {
            // TODO: Improve this exception message for cases where the template has a model and the passed model type is incorrect, e.g. RazorSlice<List<Todo>> vs. RazorSlice<Todo[]>
            throw new ArgumentException($"Razor slice with name '{sliceName}' of type {sliceType} was found but is not assignable to type {mustBeAssignableTo.Name}.", nameof(sliceName));
        }

        return sliceType.Item1;
    }

    private static SliceFactory ResolveSliceFactoryImpl(string sliceName)
    {
        if (!_slicesByName.ContainsKey(sliceName))
        {
            throw new ArgumentException($"No Razor slice with name '{sliceName}' was found.", nameof(sliceName));
        }

        var (_, sliceActivator) = _slicesByName[sliceName];

        return (SliceFactory)sliceActivator;
    }

    private static SliceFactory<TModel> ResolveSliceFactoryImpl<TModel>(string sliceName)
    {
        if (!_slicesByName.ContainsKey(sliceName))
        {
            throw new ArgumentException($"No Razor slice with name '{sliceName}' was found.", nameof(sliceName));
        }

        var (_, sliceActivator) = _slicesByName[sliceName];

        if (sliceActivator is SliceFactory<TModel> factory)
        {
            return factory;
        }

        if (sliceActivator.GetType().IsGenericType && sliceActivator.GetType().GetGenericTypeDefinition() == typeof(SliceFactory<>))
        {
            var modelType = sliceActivator.GetType().GenericTypeArguments[0];
            throw new InvalidOperationException($"Razor slice with name '{sliceName}' was found but it's model type is {modelType.Name}.");
        }

        throw new InvalidOperationException($"Razor slice with name '{sliceName}' was found but does not declare a model type. Ensure the slice uses `@inherits RazorSlice<{typeof(TModel).Name}>` or `@inherits RazorSliceHttpResult<{typeof(TModel).Name}>` to declare the model type.");
    }
}
