using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace RazorSlices;

/// <summary>
/// Contains methods for creating instances of slices.
/// </summary>
/// <remarks>
/// You generally shouldn't have to call methods on this class directly.
/// </remarks>
public static class RazorSliceFactory
{
    //private static readonly HashSet<string> ExcludedSliceNames =
    //    new(StringComparer.OrdinalIgnoreCase) { "_ViewImports.cshtml", "_ViewStart.cshtml", "_PageImports.cshtml", "_PageStart.cshtml" };
    private static readonly HashSet<string> ExcludedServiceNames =
        new(StringComparer.OrdinalIgnoreCase) { "IModelExpressionProvider", "IUrlHelper", "IViewComponentHelper", "IJsonHelper", "IHtmlHelper`1" };

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
    [RequiresDynamicCode("Uses System.Linq.Expressions to dynamically generate delegates for creating slices")]
#endif
    public static SliceFactory<TModel> GetSliceFactory<TModel>(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
        Type sliceType)
    {
        if (sliceType.GetConstructor(Type.EmptyTypes) == null)
        {
            throw new InvalidOperationException($"Slice type {sliceType.Name} must have a parameterless constructor.");
        }

        // Strongly-typed model slice
        var modelType = typeof(TModel);
        var modelPropInfo = sliceType.GetProperty(nameof(RazorSlice<object>.Model))!;

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
    [RequiresDynamicCode("Uses System.Linq.Expressions to dynamically generate delegates for creating slices")]
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
    [RequiresDynamicCode("Uses System.Linq.Expressions to dynamically generate delegates for creating slices")]
#endif
    public static SliceFactory<TModel> GetSliceFactory<TModel>(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
        Type sliceType,
        IEnumerable<PropertyInfo> injectableProperties)
    {
        if (sliceType.GetConstructor(Type.EmptyTypes) == null)
        {
            throw new InvalidOperationException("Slice must have a parameterless constructor.");
        }

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

        // Strongly-typed model slice
        var modelType = typeof(TModel);
        var modelPropInfo = sliceType.GetProperty(nameof(RazorSlice<object>.Model))!;


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
    [RequiresDynamicCode("Uses System.Linq.Expressions to dynamically generate delegates for creating slices")]
#endif
    public static SliceFactory GetSliceFactory(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] 
        Type sliceType,
        IEnumerable<PropertyInfo> injectableProperties)
    {
        if (sliceType.GetConstructor(Type.EmptyTypes) == null)
        {
            throw new InvalidOperationException("Slice must have a parameterless constructor.");
        }

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

    /// <summary>
    /// Creates an instance of a <see cref="RazorSlice" /> template using the provided <see cref="SliceFactory" /> delegate.
    /// </summary>
    /// <param name="sliceFactory">The <see cref="SliceFactory" /> delegate to create the template with.</param>
    /// <returns>A <see cref="RazorSlice" /> instance for the template.</returns>
    public static RazorSlice Create(SliceFactory sliceFactory) => sliceFactory();

    /// <summary>
    /// Creates an instance of a <see cref="RazorSlice" /> template using the provided <see cref="SliceFactory" /> delegate with a typed model.
    /// </summary>
    /// <param name="sliceFactory">The <see cref="SliceFactory" /> delegate to create the template with.</param>
    /// <param name="model">The model to use for the template instance.</param>
    /// <typeparam name="TModel">The model type of the template.</typeparam>
    /// <returns>A <see cref="RazorSlice{TModel}" /> instance for the template.</returns>
    public static RazorSlice<TModel> Create<TModel>(SliceFactory<TModel> sliceFactory, TModel model) => sliceFactory(model);
}
