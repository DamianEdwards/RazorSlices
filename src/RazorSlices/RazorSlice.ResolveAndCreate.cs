using System.Collections.ObjectModel;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.AspNetCore.Mvc.Razor.Internal;
using Microsoft.AspNetCore.Razor.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace RazorSlices;

public abstract partial class RazorSlice
{
    private static readonly HashSet<string> ExcludedSliceNames =
        new(StringComparer.OrdinalIgnoreCase) { "_ViewImports.cshtml", "_ViewStart.cshtml", "_PageImports.cshtml", "_PageStart.cshtml" };
    private static readonly HashSet<string> ExcludedServiceNames =
        new(StringComparer.OrdinalIgnoreCase) { "IModelExpressionProvider", "IUrlHelper", "IViewComponentHelper", "IJsonHelper", "IHtmlHelper`1" };
    private static readonly ReadOnlyDictionary<string, SliceDefinition> _slicesByName;
    private static readonly ReadOnlyDictionary<Type, SliceDefinition> _slicesByType;

#pragma warning disable CA1810 // Initialize reference type static fields inline
    static RazorSlice()
#pragma warning restore CA1810 // Initialize reference type static fields inline
    {
        var entryAssembly = Assembly.GetEntryAssembly() ?? throw new NotSupportedException("Application entry assembly could not be determined.");

        var sliceDefinitions = new List<SliceDefinition>();

        // Load slices from app/entry assembly
        // TODO: This is likely problematic for testing, etc. Should almost certainly be doing this via IHostingEnvironment & DI.
        AddSlicesFromAssembly(sliceDefinitions, entryAssembly);

        // Load slices from referenced assemblies
        var referencedAssemblies = entryAssembly.GetReferencedAssemblies();
        foreach (var assemblyName in referencedAssemblies)
        {
            AddSlicesFromAssembly(sliceDefinitions, Assembly.Load(assemblyName));
        }

        // Load slices from bin-deployed assemblies
        if (Path.GetDirectoryName(entryAssembly.Location) is { } binDir && Directory.Exists(binDir))
        {
            var assembliesInBin = Directory.GetFiles(binDir, "*.dll");
            foreach (var assemblyPath in assembliesInBin)
            {
                if (assemblyPath != entryAssembly.Location && File.Exists(assemblyPath))
                {
                    var peerAssembly = Assembly.LoadFrom(assemblyPath);
                    if (referencedAssemblies.FirstOrDefault(ra => ra.FullName == peerAssembly.GetName().FullName) is null)
                    {
                        AddSlicesFromAssembly(sliceDefinitions, peerAssembly);
                    }
                }
            }
        }

        _slicesByName = sliceDefinitions
            // Add entries without leading slash
            .Concat(sliceDefinitions.Select(slice => new SliceDefinition(slice.Identifier[1..], slice.SliceType, slice.Factory, slice.InjectableProperties)))
            // Add entries without .cshtml suffix
            .Concat(sliceDefinitions.Select(slice => new SliceDefinition(slice.Identifier[..slice.Identifier.LastIndexOf('.')], slice.SliceType, slice.Factory, slice.InjectableProperties)))
            // Add entries without leading slash and .cshtml suffix
            .Concat(sliceDefinitions.Select(slice => new SliceDefinition(slice.Identifier[1..slice.Identifier.LastIndexOf('.')], slice.SliceType, slice.Factory, slice.InjectableProperties)))
            .ToDictionary(entry => entry.Identifier, entry => entry)
            .AsReadOnly();
        _slicesByType = sliceDefinitions.ToDictionary(item => item.SliceType, item => item).AsReadOnly();

        static void AddSlicesFromAssembly(List<SliceDefinition> definitions, Assembly assembly)
        {
            foreach (var slice in LoadSlices(assembly))
            {
                if (!ExcludedSliceNames.Contains(slice.Identifier)
                    && !definitions.Exists(sd => string.Equals(sd.Identifier, slice.Identifier, StringComparison.Ordinal)))
                {
                    // Slice with same identifier isn't already in the list so add it
                    definitions.Add(slice);
                }
            }
        }
    }

    private static List<SliceDefinition> LoadSlices(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        IEnumerable<RazorCompiledItemAttribute> rcis = assembly.GetCustomAttributes<RazorCompiledItemAttribute>()
            .Where(rci => rci.Kind == "mvc.1.0.view" && rci.Type.IsAssignableTo(typeof(RazorSlice)));

        List<SliceDefinition> allSlices = new();

        foreach (var rci in rcis)
        {
            IEnumerable<PropertyInfo> injectableProperties = rci.Type.GetProperties()
                .Where(property => property.IsDefined(typeof(RazorInjectAttribute)) && !ExcludedServiceNames.Contains(property.PropertyType.Name));
            Delegate sliceFactory;
            if (injectableProperties.Any())
            {
                // if rci has injectable properties then create a SliceWithServicesFactory
                sliceFactory = GetSliceFactory(rci.Type, injectableProperties);
            }
            else
            {
                sliceFactory = GetSliceFactory(rci.Type);
            }
            var sliceDefinition = new SliceDefinition(rci.Identifier, rci.Type, sliceFactory, injectableProperties);
            allSlices.Add(sliceDefinition);
        }

        return allSlices;
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

    private static Delegate GetSliceFactory(Type sliceType, IEnumerable<PropertyInfo> injectableProperties)
    {
        Type serviceProviderType = typeof(IServiceProvider);
        Type serviceProviderExtensionsType = typeof(ServiceProviderServiceExtensions);
        MethodInfo getRequiredServiceMethod = serviceProviderExtensionsType.GetMethods()
            .Where(method => method.Name == "GetRequiredService" && method.IsGenericMethod)
            .First();

        Type factoryDelegateType;
        List<ParameterExpression> parameters = new();
        List<Expression> expressions = new();

        var sliceVariable = Expression.Variable(sliceType, "slice");
        var serviceProviderParam = Expression.Parameter(serviceProviderType, "serviceProvider");

        // Create expressions to set all the injectable properties
        foreach (PropertyInfo injectablePropertyInfo in injectableProperties)
        {
            var memberAccess = Expression.MakeMemberAccess(sliceVariable, injectablePropertyInfo);
            var getRequiredServiceCall = Expression.Call(getRequiredServiceMethod.MakeGenericMethod(injectablePropertyInfo.PropertyType), serviceProviderParam);
            expressions.Add(Expression.Assign(memberAccess, getRequiredServiceCall));
        }

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

            factoryDelegateType = typeof(SliceWithServicesFactory<>).MakeGenericType(modelType);

            // Make a SliceWithServicesFactory<TModel> like:
            //
            // RazorSlice<TModel> CreateSlice<TModel>(TModel model, IServiceProvider serviceProvider)
            // {
            //     var slice = new SliceType();
            //     slice.MyService = serviceProvider.GetRequiredServices<MyService>()
            //     // ...
            //     slice.Model = model
            //     return slice;
            // }

            var modelParam = Expression.Parameter(modelType, "model");
            var returnTarget = Expression.Label(razorOfModelType);

            expressions.Add(Expression.Assign(Expression.MakeMemberAccess(sliceVariable, modelPropInfo), modelParam));
            expressions.Add(Expression.Label(returnTarget, sliceVariable));

            parameters.Add(modelParam);
        }
        else
        {
            // Make a SliceWithServicesFactory like:
            //
            // RazorSlice CreateSlice(IServiceProvider serviceProvider)
            // {
            //     var slice = new SliceType();
            //     slice.MyService = serviceProvider.GetRequiredServices<MyService>()
            //     // ...
            //     return slice;
            // }
            factoryDelegateType = typeof(SliceWithServicesFactory);
            expressions.Add(Expression.Label(Expression.Label(typeof(RazorSlice)), sliceVariable));
        }
        
        parameters.Add(serviceProviderParam);

        return Expression.Lambda(
            delegateType: factoryDelegateType,
            body: Expression.Block(
                variables: new[] { sliceVariable },
                Expression.Assign(sliceVariable, Expression.New(sliceType)),
                Expression.Block(expressions)
            ),
            name: "CreateSlice",
            parameters: parameters
        ).Compile();
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
    /// Resolves a <see cref="SliceFactory" /> delegate for the provided template name.
    /// </summary>
    /// <param name="sliceName">The project-relative path to the template .cshtml file, e.g. /Slices/MyTemplate.cshtml<c></c></param>
    /// <returns>The <see cref="SliceWithServicesFactory" /> delegate that can be used to create instances of the template.</returns>
    public static SliceWithServicesFactory ResolveSliceWithServiceFactory(string sliceName) => ResolveSliceWithServiceFactoryImpl(sliceName);

    /// <summary>
    /// Resolves a <see cref="SliceFactory{TModel}" /> delegate for the provided template name with a typed model.
    /// </summary>
    /// <param name="sliceName">The project-relative path to the template .cshtml file, e.g. /Slices/MyTemplate.cshtml<c></c></param>
    /// <typeparam name="TModel">The template model type.</typeparam>
    /// <returns>The <see cref="SliceFactory{TModel}" /> delegate that can be used to create instances of the template.</returns>
    public static SliceFactory<TModel> ResolveSliceFactory<TModel>(string sliceName) => ResolveSliceFactoryImpl<TModel>(sliceName);

    /// <summary>
    /// Resolves a <see cref="SliceFactory{TModel}" /> delegate for the provided template name with a typed model.
    /// </summary>
    /// <param name="sliceName">The project-relative path to the template .cshtml file, e.g. /Slices/MyTemplate.cshtml<c></c></param>
    /// <typeparam name="TModel">The template model type.</typeparam>
    /// <returns>The <see cref="SliceWithServicesFactory{TModel}" /> delegate that can be used to create instances of the template.</returns>
    public static SliceWithServicesFactory<TModel> ResolveSliceWithServiceFactory<TModel>(string sliceName) 
        => ResolveSliceWithServiceFactoryImpl<TModel>(sliceName);

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

        var sliceDefinition = _slicesByType[sliceType];

        return (SliceFactory)sliceDefinition.Factory;
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
    /// Creates an instance of a <see cref="RazorSlice" /> template using the provided <see cref="SliceFactory" /> delegate.
    /// </summary>
    /// <param name="sliceFactory">The <see cref="SliceFactory" /> delegate to create the template with.</param>
    /// <param name="serviceProvider"></param>
    /// <returns>A <see cref="RazorSlice" /> instance for the template.</returns>
    public static RazorSlice Create(SliceWithServicesFactory sliceFactory, IServiceProvider serviceProvider) => sliceFactory(serviceProvider);

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

    /// <summary>
    /// Creates an instance of a <see cref="RazorSlice" /> template using the provided <see cref="SliceFactory" /> delegate with a typed model.
    /// </summary>
    /// <param name="sliceFactory">The <see cref="SliceFactory" /> delegate to create the template with.</param>
    /// <param name="model">The model to use for the template instance.</param>
    /// <param name="serviceProvider"></param>
    /// <typeparam name="TModel">The model type of the template.</typeparam>
    /// <returns>A <see cref="RazorSlice{TModel}" /> instance for the template.</returns>
    public static RazorSlice<TModel> Create<TModel>(SliceWithServicesFactory<TModel> sliceFactory, TModel model, IServiceProvider serviceProvider) 
        => sliceFactory(model, serviceProvider);

    private static Type ResolveSliceType(string sliceName, Type mustBeAssignableTo)
    {
        if (!_slicesByName.ContainsKey(sliceName))
        {
            throw new ArgumentException($"No Razor slice with name '{sliceName}' was found.", nameof(sliceName));
        }

        var sliceDefinition = _slicesByName[sliceName];
        var sliceType = sliceDefinition.SliceType;

        if (!sliceType.IsAssignableTo(mustBeAssignableTo))
        {
            // TODO: Improve this exception message for cases where the template has a model and the passed model type is incorrect, e.g. RazorSlice<List<Todo>> vs. RazorSlice<Todo[]>
            throw new ArgumentException($"Razor slice with name '{sliceName}' of type {sliceType} was found but is not assignable to type {mustBeAssignableTo.Name}.", nameof(sliceName));
        }

        return sliceType;
    }

    private static SliceFactory ResolveSliceFactoryImpl(string sliceName)
    {
        if (!_slicesByName.ContainsKey(sliceName))
        {
            throw new ArgumentException($"No Razor slice with name '{sliceName}' was found.", nameof(sliceName));
        }

        var sliceDefinition = _slicesByName[sliceName];

        if (sliceDefinition.HasInjectableProperties)
        {
            throw new InvalidOperationException($"{sliceName} has injectable properties but IServiceProvider is not provided");
        }

        return (SliceFactory)sliceDefinition.Factory;
    }

    private static SliceWithServicesFactory ResolveSliceWithServiceFactoryImpl(string sliceName)
    {
        if (!_slicesByName.ContainsKey(sliceName))
        {
            throw new ArgumentException($"No Razor slice with name '{sliceName}' was found.", nameof(sliceName));
        }

        var sliceDefinition = _slicesByName[sliceName];

        return (SliceWithServicesFactory)sliceDefinition.Factory;
    }

    private static SliceFactory<TModel> ResolveSliceFactoryImpl<TModel>(string sliceName)
    {
        if (!_slicesByName.ContainsKey(sliceName))
        {
            throw new ArgumentException($"No Razor slice with name '{sliceName}' was found.", nameof(sliceName));
        }

        var sliceDefinition = _slicesByName[sliceName];

        if (sliceDefinition.HasInjectableProperties)
        {
            throw new InvalidOperationException($"{sliceName} has injectable properties but IServiceProvider is not provided");
        }

        if (sliceDefinition.Factory is SliceFactory<TModel> factory)
        {
            return factory;
        }

        if (sliceDefinition.Factory.GetType().IsGenericType && sliceDefinition.Factory.GetType().GetGenericTypeDefinition() == typeof(SliceFactory<>))
        {
            var modelType = sliceDefinition.Factory.GetType().GenericTypeArguments[0];
            throw new InvalidOperationException($"Razor slice with name '{sliceName}' was found but it's model type is {modelType.Name}.");
        }

        throw new InvalidOperationException($"Razor slice with name '{sliceName}' was found but does not declare a model type. Ensure the slice uses `@inherits RazorSlice<{typeof(TModel).Name}>` or `@inherits RazorSliceHttpResult<{typeof(TModel).Name}>` to declare the model type.");
    }

    private static SliceWithServicesFactory<TModel> ResolveSliceWithServiceFactoryImpl<TModel>(string sliceName)
    {
        if (!_slicesByName.ContainsKey(sliceName))
        {
            throw new ArgumentException($"No Razor slice with name '{sliceName}' was found.", nameof(sliceName));
        }

        var sliceDefinition = _slicesByName[sliceName];

        if (sliceDefinition.Factory is SliceWithServicesFactory<TModel> factory)
        {
            return factory;
        }

        if (sliceDefinition.Factory.GetType().IsGenericType && sliceDefinition.Factory.GetType().GetGenericTypeDefinition() == typeof(SliceWithServicesFactory<>))
        {
            var modelType = sliceDefinition.Factory.GetType().GenericTypeArguments[0];
            throw new InvalidOperationException($"Razor slice with name '{sliceName}' was found but it's model type is {modelType.Name}.");
        }

        throw new InvalidOperationException($"Razor slice with name '{sliceName}' was found but does not declare a model type. Ensure the slice uses `@inherits RazorSlice<{typeof(TModel).Name}>` or `@inherits RazorSliceHttpResult<{typeof(TModel).Name}>` to declare the model type.");
    }
}
