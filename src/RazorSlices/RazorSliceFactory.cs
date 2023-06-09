using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Mvc.Razor.Internal;
using Microsoft.AspNetCore.Razor.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace RazorSlices;

/// <summary>
/// 
/// </summary>
public static class RazorSliceFactory
{
    //private static readonly HashSet<string> ExcludedSliceNames =
    //    new(StringComparer.OrdinalIgnoreCase) { "_ViewImports.cshtml", "_ViewStart.cshtml", "_PageImports.cshtml", "_PageStart.cshtml" };
    private static readonly HashSet<string> ExcludedServiceNames =
        new(StringComparer.OrdinalIgnoreCase) { "IModelExpressionProvider", "IUrlHelper", "IViewComponentHelper", "IJsonHelper", "IHtmlHelper`1" };
    //private static readonly ReadOnlyDictionary<string, SliceDefinition> _slicesByName;
    //private static readonly ReadOnlyDictionary<Type, SliceDefinition> _slicesByType;

    //#pragma warning disable CA1810 // Initialize reference type static fields inline
    //    static RazorSlice()
    //#pragma warning restore CA1810 // Initialize reference type static fields inline
    //    {
    //        var entryAssembly = Assembly.GetEntryAssembly() ?? throw new NotSupportedException("Application entry assembly could not be determined.");

    //        var sliceDefinitions = new List<SliceDefinition>();

    //        // Load slices from app/entry assembly
    //        // TODO: This is likely problematic for testing, etc. Should almost certainly be doing this via IHostingEnvironment & DI.
    //        AddSlicesFromAssembly(sliceDefinitions, entryAssembly);

    //        if (RuntimeFeature.IsDynamicCodeSupported)
    //        {
    //            // Load slices from referenced assemblies
    //            var referencedAssemblies = entryAssembly.GetReferencedAssemblies();
    //            foreach (var assemblyName in referencedAssemblies)
    //            {
    //                if (!IgnoreAssembly(assemblyName.Name))
    //                {
    //                    try
    //                    {
    //                        var assembly = Assembly.Load(assemblyName);
    //                        AddSlicesFromAssembly(sliceDefinitions, assembly);
    //                    }
    //                    catch (Exception) { } // Ignore exceptions when loading assemblies
    //                }
    //            }
    //        }

    //        _slicesByName = sliceDefinitions
    //            // Add entries without leading slash
    //            .Concat(sliceDefinitions.Select(slice => new SliceDefinition(slice.Identifier[1..], slice.SliceType, slice.ModelType, slice.Factory, slice.InjectableProperties)))
    //            // Add entries without .cshtml suffix
    //            .Concat(sliceDefinitions.Select(slice => new SliceDefinition(slice.Identifier[..slice.Identifier.LastIndexOf('.')], slice.SliceType, slice.ModelType, slice.Factory, slice.InjectableProperties)))
    //            // Add entries without leading slash and .cshtml suffix
    //            .Concat(sliceDefinitions.Select(slice => new SliceDefinition(slice.Identifier[1..slice.Identifier.LastIndexOf('.')], slice.SliceType, slice.ModelType, slice.Factory, slice.InjectableProperties)))
    //            // Case-insensitive dictionary so lookup is case-insensitive
    //            .ToDictionary(entry => entry.Identifier, entry => entry, StringComparer.OrdinalIgnoreCase)
    //            .AsReadOnly();
    //        _slicesByType = sliceDefinitions.ToDictionary(item => item.SliceType, item => item).AsReadOnly();

    //        static bool IgnoreAssembly(string? assemblyName)
    //        {
    //            return assemblyName is null
    //                || assemblyName.StartsWith("System.", StringComparison.OrdinalIgnoreCase)
    //                || assemblyName.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase);
    //        }

    //        static void AddSlicesFromAssembly(List<SliceDefinition> definitions, Assembly assembly)
    //        {
    //            foreach (var slice in LoadSlices(assembly))
    //            {
    //                if (!ExcludedSliceNames.Contains(slice.Identifier)
    //                    && !definitions.Exists(sd => string.Equals(sd.Identifier, slice.Identifier, StringComparison.Ordinal)))
    //                {
    //                    // Slice with same identifier isn't already in the list so add it
    //                    definitions.Add(slice);
    //                }
    //            }
    //        }
    //    }

    //    private static List<SliceDefinition> LoadSlices(Assembly assembly)
    //    {
    //        ArgumentNullException.ThrowIfNull(assembly);

    //        IEnumerable<RazorCompiledItemAttribute> rcis = assembly.GetCustomAttributes<RazorCompiledItemAttribute>()
    //            .Where(rci => rci.Kind == "mvc.1.0.view" && rci.Type.IsAssignableTo(typeof(RazorSlice)));

    //        List<SliceDefinition> allSlices = new();

    //        foreach (var rci in rcis)
    //        {
    //            IEnumerable<PropertyInfo> injectableProperties = rci.Type.GetProperties()
    //                .Where(property => property.IsDefined(typeof(RazorInjectAttribute)) && !ExcludedServiceNames.Contains(property.PropertyType.Name));
    //            Delegate sliceFactory;
    //            if (injectableProperties.Any())
    //            {
    //                // if rci has injectable properties then create a SliceWithServicesFactory
    //                sliceFactory = GetSliceFactory(rci.Type, injectableProperties);
    //            }
    //            else
    //            {
    //                sliceFactory = GetSliceFactory(rci.Type);
    //            }
    //            var sliceDefinition = new SliceDefinition(rci.Identifier, rci.Type, GetSliceModel(rci.Type), sliceFactory, injectableProperties);
    //            allSlices.Add(sliceDefinition);
    //        }

    //        return allSlices;
    //    }

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)]
    private static readonly Type _serviceProviderType = typeof(IServiceProvider);
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)]
    private static readonly Type _serviceProviderExtensionsType = typeof(ServiceProviderServiceExtensions);
    private static readonly MethodInfo _getServiceMethodInfo = _serviceProviderType
        .GetMethod(nameof(IServiceProvider.GetService), new[] { typeof(Type) })!;
    private static readonly MethodInfo _getRequiredServiceMethodInfo = _serviceProviderExtensionsType
        .GetMethod(nameof(ServiceProviderServiceExtensions.GetRequiredService), new[] { typeof(IServiceProvider), typeof(Type) })!;
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicProperties)]
    private static readonly Type _razorSliceType = typeof(RazorSlice);
    private static readonly PropertyInfo _razorSliceInitializeProperty = _razorSliceType.GetProperty(nameof(RazorSlice.Initialize))!;
    private static readonly Type _initializeDelegateType = _razorSliceInitializeProperty.PropertyType;
    private static readonly NullabilityInfoContext _nullabilityContext = new();

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="TModel"></typeparam>
    /// <param name="sliceType"></param>
    /// <returns></returns>
#if NET7_0_OR_GREATER
    [RequiresDynamicCode("")]
#endif
    public static SliceFactory<TModel> GetSliceFactory<TModel>(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
        Type sliceType)
    {
        // TODO: Just use straight-up reflection if we're running in native AOT

        if (sliceType.GetConstructor(Type.EmptyTypes) == null)
        {
            throw new InvalidOperationException($"Slice type {sliceType.Name} must have a parameterless constructor.");
        }

        // Strongly-typed model slice
        var modelType = typeof(TModel);
        var modelPropInfo = sliceType.GetProperty(nameof(RazorSlice<object>.Model))!;
        //var modelType = modelPropInfo.PropertyType;
        //var razorOfModelType = MakeGenericType(_razorSliceOfTModelType, modelType);
        //var factoryDelegateType = MakeGenericType(_sliceFactoryOfTModelType, modelType);

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
        var returnTarget = Expression.Label(typeof(RazorSlice<TModel>));

        return Expression.Lambda<SliceFactory<TModel>>(
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

    /// <summary>
    /// 
    /// </summary>
    /// <param name="sliceType"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
#if NET7_0_OR_GREATER
    [RequiresDynamicCode("")]
#endif
    public static SliceFactory GetSliceFactory(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
        Type sliceType)
    {
        return Expression.Lambda<SliceFactory>(Expression.New(sliceType)).Compile();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="TModel"></typeparam>
    /// <param name="sliceType"></param>
    /// <param name="injectableProperties"></param>
    /// <returns></returns>
#if NET7_0_OR_GREATER
    [RequiresDynamicCode("")]
#endif
    public static SliceFactory<TModel> GetSliceFactory<TModel>(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
        Type sliceType,
        IEnumerable<PropertyInfo> injectableProperties)
    {
        //Type factoryDelegateType;
        List<ParameterExpression> parameters = new();
        List<Expression> factoryBody = new();

        // Create expression in a lambda that's set on the RazorSlice.Initialize property:
        //     slice.Initialize = (s, sp) => {
        //         ((SliceType)s).SomeNotNullProperty = (MyService)sp.GetRequiredService(typeof(MyService))
        //         ((SliceType)s).SomeOtherProperty = (MyOtherService)sp.GetService(typeof(MyOtherService))
        //     };
        var initializeBody = new List<Expression>();
        var sliceParameter = Expression.Parameter(_razorSliceType, "s");
        var sliceParameterCast = Expression.Convert(sliceParameter, sliceType);
        var serviceProviderParam = Expression.Parameter(_serviceProviderType, "sp");

        foreach (PropertyInfo injectablePropertyInfo in injectableProperties)
        {
            if (ExcludedServiceNames.Contains(injectablePropertyInfo.PropertyType.Name))
            {
                continue;
            }

            var propertyAccess = Expression.MakeMemberAccess(sliceParameterCast, injectablePropertyInfo);
            // Check if property type is nullable and call GetService instead of GetRequiredService appropriaately
            var getServiceCall = !IsNullable(injectablePropertyInfo)
                ? Expression.Call(serviceProviderParam, _getServiceMethodInfo, Expression.Constant(injectablePropertyInfo.PropertyType))
                : Expression.Call(_getRequiredServiceMethodInfo, serviceProviderParam, Expression.Constant(injectablePropertyInfo.PropertyType));
            initializeBody.Add(Expression.Assign(propertyAccess, Expression.Convert(getServiceCall, injectablePropertyInfo.PropertyType)));
        }
        var initializeLambda = Expression.Lambda(
            delegateType: _initializeDelegateType,
            body: Expression.Block(initializeBody),
            name: "InitializeSlice",
            parameters: new[] { sliceParameter, serviceProviderParam });

        var sliceVariable = Expression.Variable(sliceType, "slice");

        if (sliceType.GetConstructor(Type.EmptyTypes) == null)
        {
            throw new InvalidOperationException("Slice must have a parameterless constructor.");
        }

        // Strongly-typed model slice
        var modelType = typeof(TModel);
        var modelPropInfo = sliceType.GetProperty(nameof(RazorSlice<object>.Model))!;
        //var modelType = modelPropInfo.PropertyType;
        //var razorOfModelType = MakeGenericType(_razorSliceOfTModelType, modelType);


        // Make a SliceFactory<TModel> like:
        //
        // RazorSlice<TModel> CreateSlice<TModel>(TModel model)
        // {
        //     var slice = new SliceType<TModel>();
        //     slice.Initialize = (s, sp) => {
        //         ((SliceType)s).SomeNotNullProperty = sp.GetRequiredService<MyService>()
        //         ((SliceType)s).SomeOtherProperty = sp.GetService<MyService>()
        //     };
        //     slice.Model = model
        //     return slice;
        // }

        factoryBody.Add(Expression.Assign(Expression.MakeMemberAccess(sliceVariable, _razorSliceInitializeProperty), initializeLambda));

        var modelParam = Expression.Parameter(modelType, "model");
        factoryBody.Add(Expression.Assign(Expression.MakeMemberAccess(sliceVariable, modelPropInfo), modelParam));
        var returnTarget = Expression.Label(typeof(RazorSlice<TModel>));

        factoryBody.Add(Expression.Label(returnTarget, sliceVariable));

        parameters.Add(modelParam);

        return Expression.Lambda<SliceFactory<TModel>>(
            body: Expression.Block(
                variables: new[] { sliceVariable },
                Expression.Assign(sliceVariable, Expression.New(sliceType)),
                Expression.Block(factoryBody)
            ),
            name: "CreateSlice",
            parameters: parameters
        ).Compile();
    }

    /// <summary>
    /// Creates a <see cref="Delegate"/> that can be used to create a <see cref="RazorSlice"/> of the specified <see cref="Type"/>.
    /// </summary>
    /// <param name="sliceType">The slice type.</param>
    /// <param name="injectableProperties"></param>
    /// <returns>A <see cref="Delegate"/> that can be used to create an instance of the slice.</returns>
    /// <exception cref="InvalidOperationException"></exception>
#if NET7_0_OR_GREATER
    [RequiresDynamicCode("")]
#endif
    public static SliceFactory GetSliceFactory(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] 
        Type sliceType,
        IEnumerable<PropertyInfo> injectableProperties)
    {
        // TODO: Just use straight-up reflection if we're running in native AOT

        //Type factoryDelegateType;
        List<ParameterExpression> parameters = new();
        List<Expression> factoryBody = new();

        // Create expression in a lambda that's set on the RazorSlice.Initialize property:
        //     slice.Initialize = (s, sp) => {
        //         ((SliceType)s).SomeNotNullProperty = sp.GetRequiredService<MyService>()
        //         ((SliceType)s).SomeOtherProperty = sp.GetService<MyService>()
        //     };
        var initializeBody = new List<Expression>();
        var sliceParameter = Expression.Parameter(_razorSliceType, "s");
        var sliceParameterCast = Expression.Convert(sliceParameter, sliceType);
        var serviceProviderParam = Expression.Parameter(_serviceProviderType, "sp");

        foreach (PropertyInfo injectablePropertyInfo in injectableProperties)
        {
            if (ExcludedServiceNames.Contains(injectablePropertyInfo.PropertyType.Name))
            {
                continue;
            }

            var propertyAccess = Expression.MakeMemberAccess(sliceParameterCast, injectablePropertyInfo);
            // Check if property type is nullable and call GetService<T> instead of GetRequiredService<T> appropriaately
            var getServiceCall = !IsNullable(injectablePropertyInfo)
                ? Expression.Call(serviceProviderParam, _getServiceMethodInfo, Expression.Constant(injectablePropertyInfo.PropertyType))
                : Expression.Call(_getRequiredServiceMethodInfo, serviceProviderParam, Expression.Constant(injectablePropertyInfo.PropertyType));
            initializeBody.Add(Expression.Assign(propertyAccess, Expression.Convert(getServiceCall, injectablePropertyInfo.PropertyType)));
        }
        var initializeLambda = Expression.Lambda(
            delegateType: _initializeDelegateType,
            body: Expression.Block(initializeBody),
            name: "InitializeSlice",
            parameters: new[] { sliceParameter, serviceProviderParam });

        var sliceVariable = Expression.Variable(sliceType, "slice");

        // Make a SliceFactory like:
        //
        // RazorSlice CreateSlice()
        // {
        //     var slice = new SliceType();
        //     slice.Initialize = (s, sp) => {
        //         ((SliceType)s).SomeNotNullProperty = sp.GetRequiredService<MyService>()
        //         ((SliceType)s).SomeOtherProperty = sp.GetService<MyService>()
        //     };
        //     return slice;
        // }
        factoryBody.Add(Expression.Assign(Expression.MakeMemberAccess(sliceVariable, _razorSliceInitializeProperty), initializeLambda));
        factoryBody.Add(Expression.Label(Expression.Label(typeof(RazorSlice)), sliceVariable));

        return Expression.Lambda<SliceFactory>(
            body: Expression.Block(
                variables: new[] { sliceVariable },
                Expression.Assign(sliceVariable, Expression.New(sliceType)),
                Expression.Block(factoryBody)
            ),
            name: "CreateSlice",
            parameters: parameters
        ).Compile();
    }

    private static bool IsNullable(PropertyInfo info) =>
        Nullable.GetUnderlyingType(info.PropertyType) is not null
            || _nullabilityContext.Create(info).WriteState is not NullabilityState.NotNull;

    //private static bool IsModelSlice(Type sliceType)
    //{
    //    var baseType = sliceType.BaseType;

    //    while (baseType is not null)
    //    {
    //        if (baseType.IsGenericType && baseType.GetGenericTypeDefinition() == typeof(RazorSlice<>))
    //        {
    //            return true;
    //        }

    //        baseType = baseType.BaseType;
    //    }

    //    return false;
    //}

    //private static Type? GetSliceModel(Type sliceType)
    //{
    //    var modelPropInfo = sliceType.GetProperty(nameof(RazorSlice<object>.Model));
    //    var modelType = modelPropInfo?.PropertyType;
    //    return modelType;
    //}

    ///// <summary>
    ///// Resolves the generated <see cref="Type" /> for the provided template name.
    ///// </summary>
    ///// <param name="sliceName">The project-relative path to the template .cshtml file, e.g. /Slices/MyTemplate.cshtml<c></c></param>
    ///// <returns>The <see cref="Type" /> generated from the template .cshtml file.</returns>
    //public static Type ResolveSliceType(string sliceName) => ResolveSliceType(sliceName, typeof(RazorSlice));

    ///// <summary>
    ///// Resolves the generated <see cref="Type" /> for the provided template name with a typed model.
    ///// </summary>
    ///// <param name="sliceName">The project-relative path to the template .cshtml file, e.g. /Slices/MyTemplate.cshtml<c></c></param>
    ///// <typeparam name="TModel">The template model type.</typeparam>
    ///// <returns>The <see cref="Type" /> generated from the template .cshtml file.</returns>
    //public static Type ResolveSliceType<TModel>(string sliceName) => ResolveSliceType(sliceName);

    ///// <summary>
    ///// Resolves a <see cref="SliceFactory" /> delegate for the provided template name.
    ///// </summary>
    ///// <param name="sliceName">The project-relative path to the template .cshtml file, e.g. /Slices/MyTemplate.cshtml<c></c></param>
    ///// <returns>The <see cref="SliceFactory" /> delegate that can be used to create instances of the template.</returns>
    //public static SliceFactory ResolveSliceFactory(string sliceName) => ResolveSliceFactoryImpl(sliceName);

    ///// <summary>
    ///// Resolves a <see cref="SliceWithServicesFactory" /> delegate for the provided template name.
    ///// </summary>
    ///// <param name="sliceName">The project-relative path to the template .cshtml file, e.g. /Slices/MyTemplate.cshtml<c></c></param>
    ///// <returns>The <see cref="SliceWithServicesFactory" /> delegate that can be used to create instances of the template.</returns>
    //public static SliceWithServicesFactory ResolveSliceWithServiceFactory(string sliceName) => ResolveSliceWithServiceFactoryImpl(sliceName);

    ///// <summary>
    ///// Resolves a <see cref="SliceFactory{TModel}" /> delegate for the provided template name with a typed model.
    ///// </summary>
    ///// <param name="sliceName">The project-relative path to the template .cshtml file, e.g. /Slices/MyTemplate.cshtml<c></c></param>
    ///// <typeparam name="TModel">The template model type.</typeparam>
    ///// <returns>The <see cref="SliceFactory{TModel}" /> delegate that can be used to create instances of the template.</returns>
    //public static SliceFactory<TModel> ResolveSliceFactory<TModel>(string sliceName) => ResolveSliceFactoryImpl<TModel>(sliceName);

    ///// <summary>
    ///// Resolves a <see cref="SliceWithServicesFactory{TModel}" /> delegate for the provided template name with a typed model.
    ///// </summary>
    ///// <param name="sliceName">The project-relative path to the template .cshtml file, e.g. /Slices/MyTemplate.cshtml<c></c></param>
    ///// <typeparam name="TModel">The template model type.</typeparam>
    ///// <returns>The <see cref="SliceWithServicesFactory{TModel}" /> delegate that can be used to create instances of the template.</returns>
    //public static SliceWithServicesFactory<TModel> ResolveSliceWithServiceFactory<TModel>(string sliceName) 
    //    => ResolveSliceWithServiceFactoryImpl<TModel>(sliceName);

    ///// <summary>
    ///// Resolves a <see cref="SliceFactory" /> delegate for the provided template <see cref="Type" />.
    ///// </summary>
    ///// <param name="sliceType"></param>
    ///// <returns></returns>
    //public static SliceFactory ResolveSliceFactory(Type sliceType)
    //{
    //    if (!_slicesByType.TryGetValue(sliceType, out var sliceDefinition))
    //    {
    //        throw new ArgumentException($"No Razor slice of type {sliceType.Name} was found.", nameof(sliceType));
    //    }

    //    return (SliceFactory)sliceDefinition.Factory;
    //}

    ///// <summary>
    ///// Resolves a <see cref="SliceFactory" /> delegate for the provided template <see cref="Type" />.
    ///// </summary>
    ///// <param name="sliceType"></param>
    ///// <returns></returns>
    //public static SliceFactory<TModel> ResolveSliceFactory<TModel>(Type sliceType)
    //{
    //    if (!_slicesByType.TryGetValue(sliceType, out var sliceDefinition))
    //    {
    //        throw new ArgumentException($"No Razor slice of type {sliceType.Name} was found.", nameof(sliceType));
    //    }

    //    return (SliceFactory<TModel>)sliceDefinition.Factory;
    //}

    ///// <summary>
    ///// Creates an instance of a <see cref="RazorSlice" /> template for the provided template name.
    ///// </summary>
    ///// <remarks>
    ///// Note that this method incurs a lookup cost each time it's invoked. If avoiding that cost is desirable consider using <see cref="ResolveSliceFactory(string)" />
    ///// to resolve the template creation delegate once and then use <see cref="Create(SliceFactory)" /> each time a template instance is required.
    ///// </remarks>
    ///// <param name="sliceName">The project-relative path to the template .cshtml file, e.g. /Slices/MyTemplate.cshtml<c></c></param>
    ///// <returns>A <see cref="RazorSlice" /> instance for the template.</returns>
    //public static RazorSlice Create(string sliceName) => Create(ResolveSliceFactory(sliceName));

    ///// <summary>
    ///// Creates an instance of a <see cref="RazorSlice" /> template for the provided template name.
    ///// </summary>
    ///// <remarks>
    ///// Note that this method incurs a lookup cost each time it's invoked. If avoiding that cost is desirable consider using <see cref="ResolveSliceWithServiceFactory(string)" />
    ///// to resolve the template creation delegate once and then use <see cref="Create(SliceWithServicesFactory, IServiceProvider)" /> each time a template instance is required.
    ///// </remarks>
    ///// <param name="sliceName">The project-relative path to the template .cshtml file, e.g. /Slices/MyTemplate.cshtml<c></c></param>
    ///// <param name="serviceProvider">The <see cref="IServiceProvider" /> to use when setting the template's <c>@inject</c> properties.</param>
    ///// <returns>A <see cref="RazorSlice" /> instance for the template.</returns>
    //public static RazorSlice Create(string sliceName, IServiceProvider serviceProvider) => Create(ResolveSliceWithServiceFactory(sliceName), serviceProvider);

    /// <summary>
    /// Creates an instance of a <see cref="RazorSlice" /> template using the provided <see cref="SliceFactory" /> delegate.
    /// </summary>
    /// <param name="sliceFactory">The <see cref="SliceFactory" /> delegate to create the template with.</param>
    /// <returns>A <see cref="RazorSlice" /> instance for the template.</returns>
    public static RazorSlice Create(SliceFactory sliceFactory) => sliceFactory();

    ///// <summary>
    ///// Creates an instance of a <see cref="RazorSlice" /> template using the provided <see cref="SliceWithServicesFactory" /> delegate.
    ///// </summary>
    ///// <param name="sliceFactory">The <see cref="SliceWithServicesFactory" /> delegate to create the template with.</param>
    ///// <param name="serviceProvider">The <see cref="IServiceProvider" /> to use when setting the template's <c>@inject</c> properties.</param>
    ///// <returns>A <see cref="RazorSlice" /> instance for the template.</returns>
    //public static RazorSlice Create(SliceWithServicesFactory sliceFactory, IServiceProvider serviceProvider) => sliceFactory(serviceProvider);

    ///// <summary>
    ///// Creates an instance of a <see cref="RazorSlice" /> template for the provided template name with a typed model.
    ///// </summary>
    ///// <param name="sliceName">The project-relative path to the template .cshtml file, e.g. /Slices/MyTemplate.cshtml<c></c></param>
    ///// <param name="model">The model to use for the template instance.</param>
    ///// <typeparam name="TModel">The model type of the template.</typeparam>
    ///// <returns>A <see cref="RazorSlice{TModel}" /> instance for the template.</returns>
    //public static RazorSlice<TModel> Create<TModel>(string sliceName, TModel model) => Create(ResolveSliceFactory<TModel>(sliceName), model);

    ///// <summary>
    ///// Creates an instance of a <see cref="RazorSlice" /> template for the provided template name with a typed model.
    ///// </summary>
    ///// <remarks>
    ///// Note that this method incurs a lookup cost each time it's invoked. If avoiding that cost is desirable consider using <see cref="ResolveSliceWithServiceFactory{TModel}(string)" />
    ///// to resolve the template creation delegate once and then use <see cref="Create{TModel}(SliceWithServicesFactory{TModel}, TModel, IServiceProvider)" /> each time a template instance is required.
    ///// </remarks>
    ///// <param name="sliceName">The project-relative path to the template .cshtml file, e.g. /Slices/MyTemplate.cshtml<c></c></param>
    ///// <param name="model">The model to use for the template instance.</param>
    ///// <param name="serviceProvider">The <see cref="IServiceProvider" /> to use when setting the template's <c>@inject</c> properties.</param>
    ///// <typeparam name="TModel">The model type of the template.</typeparam>
    ///// <returns>A <see cref="RazorSlice{TModel}" /> instance for the template.</returns>
    //public static RazorSlice<TModel> Create<TModel>(string sliceName, TModel model, IServiceProvider serviceProvider) => Create(ResolveSliceWithServiceFactory<TModel>(sliceName), model, serviceProvider);

    /// <summary>
    /// Creates an instance of a <see cref="RazorSlice" /> template using the provided <see cref="SliceFactory" /> delegate with a typed model.
    /// </summary>
    /// <param name="sliceFactory">The <see cref="SliceFactory" /> delegate to create the template with.</param>
    /// <param name="model">The model to use for the template instance.</param>
    /// <typeparam name="TModel">The model type of the template.</typeparam>
    /// <returns>A <see cref="RazorSlice{TModel}" /> instance for the template.</returns>
    public static RazorSlice<TModel> Create<TModel>(SliceFactory<TModel> sliceFactory, TModel model) => sliceFactory(model);

    ///// <summary>
    ///// Creates an instance of a <see cref="RazorSlice" /> template using the provided <see cref="SliceWithServicesFactory{TModel}" /> delegate with a typed model.
    ///// </summary>
    ///// <param name="sliceFactory">The <see cref="SliceWithServicesFactory{TModel}" /> delegate to create the template with.</param>
    ///// <param name="model">The model to use for the template instance.</param>
    ///// <param name="serviceProvider">The <see cref="IServiceProvider" /> to use when setting the template's <c>@inject</c> properties.</param>
    ///// <typeparam name="TModel">The model type of the template.</typeparam>
    ///// <returns>A <see cref="RazorSlice{TModel}" /> instance for the template.</returns>
    //public static RazorSlice<TModel> Create<TModel>(SliceWithServicesFactory<TModel> sliceFactory, TModel model, IServiceProvider serviceProvider) 
    //    => sliceFactory(model, serviceProvider);

    //private static Type ResolveSliceType(string sliceName, Type mustBeAssignableTo)
    //{
    //    if (!_slicesByName.TryGetValue(sliceName, out var sliceDefinition))
    //    {
    //        throw new ArgumentException($"No Razor slice with name '{sliceName}' was found.", nameof(sliceName));
    //    }

    //    var sliceType = sliceDefinition.SliceType;

    //    if (!sliceType.IsAssignableTo(mustBeAssignableTo))
    //    {
    //        // TODO: Improve this exception message for cases where the template has a model and the passed model type is incorrect, e.g. RazorSlice<List<Todo>> vs. RazorSlice<Todo[]>
    //        throw new ArgumentException($"Razor slice with name '{sliceName}' of type {sliceType} was found but is not assignable to type {mustBeAssignableTo.Name}.", nameof(sliceName));
    //    }

    //    return sliceType;
    //}

    //internal static SliceDefinition ResolveSliceDefinition(string sliceName)
    //{
    //    if (!_slicesByName.TryGetValue(sliceName, out var sliceDefinition))
    //    {
    //        throw new ArgumentException($"No Razor slice with name '{sliceName}' was found.", nameof(sliceName));
    //    }

    //    return sliceDefinition;
    //}

    //private static SliceFactory ResolveSliceFactoryImpl(string sliceName)
    //{
    //    if (!_slicesByName.TryGetValue(sliceName, out var sliceDefinition))
    //    {
    //        throw new ArgumentException($"No Razor slice with name '{sliceName}' was found.", nameof(sliceName));
    //    }

    //    if (sliceDefinition.HasInjectableProperties)
    //    {
    //        throw new InvalidOperationException($"{sliceName} has injectable properties but IServiceProvider is not provided");
    //    }

    //    return (SliceFactory)sliceDefinition.Factory;
    //}

    //private static SliceWithServicesFactory ResolveSliceWithServiceFactoryImpl(string sliceName)
    //{
    //    if (!_slicesByName.TryGetValue(sliceName, out var sliceDefinition))
    //    {
    //        throw new ArgumentException($"No Razor slice with name '{sliceName}' was found.", nameof(sliceName));
    //    }

    //    return (SliceWithServicesFactory)sliceDefinition.Factory;
    //}

    //private static SliceFactory<TModel> ResolveSliceFactoryImpl<TModel>(string sliceName)
    //{
    //    if (!_slicesByName.TryGetValue(sliceName, out var sliceDefinition))
    //    {
    //        throw new ArgumentException($"No Razor slice with name '{sliceName}' was found.", nameof(sliceName));
    //    }

    //    if (sliceDefinition.HasInjectableProperties)
    //    {
    //        throw new InvalidOperationException($"{sliceName} has injectable properties but IServiceProvider is not provided");
    //    }

    //    if (sliceDefinition.Factory is SliceFactory<TModel> factory)
    //    {
    //        return factory;
    //    }

    //    if (sliceDefinition.Factory.GetType().IsGenericType && sliceDefinition.Factory.GetType().GetGenericTypeDefinition() == typeof(SliceFactory<>))
    //    {
    //        var modelType = sliceDefinition.Factory.GetType().GenericTypeArguments[0];
    //        throw new InvalidOperationException($"Razor slice with name '{sliceName}' was found but it's model type is {modelType.Name}.");
    //    }

    //    throw new InvalidOperationException($"Razor slice with name '{sliceName}' was found but does not declare a model type. Ensure the slice uses `@inherits RazorSlice<{typeof(TModel).Name}>` or `@inherits RazorSliceHttpResult<{typeof(TModel).Name}>` to declare the model type.");
    //}

    //private static SliceWithServicesFactory<TModel> ResolveSliceWithServiceFactoryImpl<TModel>(string sliceName)
    //{
    //    if (!_slicesByName.TryGetValue(sliceName, out var sliceDefinition))
    //    {
    //        throw new ArgumentException($"No Razor slice with name '{sliceName}' was found.", nameof(sliceName));
    //    }

    //    if (sliceDefinition.Factory is SliceWithServicesFactory<TModel> factory)
    //    {
    //        return factory;
    //    }

    //    if (sliceDefinition.Factory.GetType().IsGenericType && sliceDefinition.Factory.GetType().GetGenericTypeDefinition() == typeof(SliceWithServicesFactory<>))
    //    {
    //        var modelType = sliceDefinition.Factory.GetType().GenericTypeArguments[0];
    //        throw new InvalidOperationException($"Razor slice with name '{sliceName}' was found but it's model type is {modelType.Name}.");
    //    }

    //    throw new InvalidOperationException($"Razor slice with name '{sliceName}' was found but does not declare a model type. Ensure the slice uses `@inherits RazorSlice<{typeof(TModel).Name}>` or `@inherits RazorSliceHttpResult<{typeof(TModel).Name}>` to declare the model type.");
    //}
}
