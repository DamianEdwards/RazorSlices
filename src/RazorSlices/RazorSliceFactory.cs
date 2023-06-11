using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc.Razor.Internal;
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
    private static readonly ConstructorInfo _ioeCtor = typeof(InvalidOperationException).GetConstructor(new[] { typeof(string) })!;
    private static readonly NullabilityInfoContext _nullabilityContext = new();
    private static readonly Action<RazorSlice, IServiceProvider?> _emptyInit = (_, __) => { };

    /// <summary>
    /// 
    /// </summary>
    /// <param name="sliceType"></param>
    /// <returns></returns>
    public static bool IsModelSlice(Type sliceType)
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
    /// 
    /// </summary>
    /// <param name="sliceType"></param>
    /// <returns></returns>
    public static (bool Any, PropertyInfo[] Nullable, PropertyInfo[] NonNullable) GetInjectableProperties(Type sliceType)
    {
        List<PropertyInfo>? nullable = null;
        List<PropertyInfo>? nonNullable = null;

        foreach (var pi in sliceType.GetProperties())
        {
            if (pi.GetCustomAttribute<RazorInjectAttribute>() is not null
                && !ExcludedServiceNames.Contains(pi.PropertyType.Name))
            {
                if (IsNullable(pi))
                {
                    nullable ??= new();
                    nullable.Add(pi);
                }
                else
                {
                    nonNullable ??= new();
                    nonNullable.Add(pi);
                }
            }
        }

        return (nullable is not null || nonNullable is not null,
                nullable?.ToArray() ?? Array.Empty<PropertyInfo>(),
                nonNullable?.ToArray() ?? Array.Empty<PropertyInfo>());
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="sliceType"></param>
    /// <param name="injectableProperties"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public static Action<RazorSlice, IServiceProvider?> GetReflectionInitAction(Type sliceType, (bool Any, PropertyInfo[] Nullable, PropertyInfo[] NonNullable) injectableProperties)
    {
        return injectableProperties.Any
            ? (s, sp) =>
            {
                if (sp is null)
                {
                    throw new InvalidOperationException($"Cannot initialize @inject properties of slice {sliceType.Name} because the ServiceProvider property is null.");
                }
                foreach (var pi in injectableProperties.NonNullable)
                {
                    pi.SetValue(s, sp.GetRequiredService(pi.PropertyType));
                }
                foreach (var pi in injectableProperties.Nullable)
                {
                    pi.SetValue(s, sp.GetService(pi.PropertyType));
                }
            }
            : _emptyInit;
    }

    private static Expression<Action<RazorSlice, IServiceProvider?>> GetExpressionInitAction(
        Type sliceType,
        (bool Any, PropertyInfo[] Nullable, PropertyInfo[] NonNullable) injectableProperties)
    {
        if (!injectableProperties.Any) throw new InvalidOperationException("Shouldn't call GetExpressionInitAction if there's no injectable properties.");

        // (RazorSlice slice, IServiceProvider? sp) =>
        // {
        //     if (sp is null) throw new InvalidOperationException("Cannot initialize @inject properties of slice because the ServiceProvider property is null.");
        //     ((MySlice)slice).SomeProp = (SomeService)sp.GetService(typeof(SomeService));
        //     ((MySlice)slice).NextProp = (SomeOtherService)sp.GetRequiredService(typeof(SomeOtherService));
        // }

        var sliceParam = Expression.Parameter(_razorSliceType, "slice");
        var spParam = Expression.Parameter(_serviceProviderType, "sp");

        var body = new List<Expression>
        {
            Expression.IfThen(
                Expression.Equal(spParam, Expression.Constant(null)),
                Expression.Throw(Expression.New(_ioeCtor, Expression.Constant("Cannot initialize @inject properties of slice because the ServiceProvider property is null."))))
        };

        var sliceParameterCast = Expression.Convert(sliceParam, sliceType);

        foreach (var ip in injectableProperties.Nullable)
        {
            var propertyAccess = Expression.MakeMemberAccess(sliceParameterCast, ip);
            var getServiceCall = Expression.Call(spParam, _getServiceMethodInfo, Expression.Constant(ip.PropertyType));
            body.Add(Expression.Assign(propertyAccess, Expression.Convert(getServiceCall, ip.PropertyType)));
        }

        foreach (var ip in injectableProperties.NonNullable)
        {
            var propertyAccess = Expression.MakeMemberAccess(sliceParameterCast, ip);
            var getServiceCall = Expression.Call(spParam, _getRequiredServiceMethodInfo, Expression.Constant(ip.PropertyType));
            body.Add(Expression.Assign(propertyAccess, Expression.Convert(getServiceCall, ip.PropertyType)));
        }

        return Expression.Lambda<Action<RazorSlice, IServiceProvider?>>(
            body: Expression.Block(body),
            parameters: new[] { sliceParam, spParam });
    }

    /// <summary>
    /// Creates a <see cref="SliceFactory"/> that can be used to create a <see cref="RazorSlice"/> of the specified <see cref="Type"/>.
    /// </summary>
    /// <param name="sliceType">The slice type.</param>
    /// <param name="modelType"></param>
    /// <param name="injectableProperties"></param>
    /// <returns>A <see cref="SliceFactory"/> that can be used to create an instance of the slice.</returns>
#if NET7_0_OR_GREATER
    [RequiresDynamicCode("Uses System.Linq.Expressions to dynamically generate delegates for creating slices")]
#endif
    public static Delegate GetSliceFactory(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
        Type sliceType,
        Type? modelType,
        (bool Any, PropertyInfo[] Nullable, PropertyInfo[] NonNullable) injectableProperties)
    {
        ArgumentNullException.ThrowIfNull(sliceType);

        if (sliceType.GetConstructor(Type.EmptyTypes) == null)
        {
            throw new ArgumentException($"Slice type {sliceType.Name} must have a parameterless constructor.", nameof(sliceType));
        }

        var body = new List<Expression>();

        // Make a delegate like:
        //
        // RazorSlice CreateSlice()
        // {
        //     var slice = new SliceType();
        //     slice.Init = ...;
        //     return slice;
        // }
        // or
        // RazorSlice<TModel> CreateSlice(MyModel model)
        // {
        //     var slice = new SliceType<MyModel>();
        //     slice.Init = ...;
        //     slice.Model = model
        //     return slice;
        // }

        var sliceVariable = Expression.Variable(sliceType, "slice");
        body.Add(Expression.Assign(sliceVariable, Expression.New(sliceType)));
        ParameterExpression[]? parameters = null;
        var factoryType = typeof(Func<RazorSlice>);

        if (injectableProperties.Any)
        {
            body.Add(Expression.Assign(
                Expression.MakeMemberAccess(sliceVariable, _razorSliceInitializeProperty),
                GetExpressionInitAction(sliceType, injectableProperties)!));
        }

        if (modelType is not null)
        {
            // Func<MyModel, RazorSlice<MyModel>>
            var modelPropInfo = sliceType.GetProperty("Model")!;
            factoryType = typeof(Func<>).MakeGenericType(modelType, sliceType);
            var modelParam = Expression.Parameter(modelType, "model");
            parameters = new[] { modelParam };
            body.Add(Expression.Assign(Expression.MakeMemberAccess(sliceVariable, modelPropInfo), modelParam));
        }

        var returnTarget = Expression.Label(sliceType);
        body.Add(Expression.Label(returnTarget, sliceVariable));

        return Expression.Lambda(
            delegateType: factoryType,
            body: Expression.Block(
                variables: new[] { sliceVariable },
                body
            ),
            name: "CreateSlice",
            parameters: parameters)
        .Compile();
    }

//    /// <summary>
//    /// Creates a <see cref="SliceFactory"/> that can be used to create a <see cref="RazorSlice"/> of the specified <see cref="Type"/>.
//    /// </summary>
//    /// <param name="sliceType">The slice type.</param>
//    /// <returns>A <see cref="SliceFactory"/> that can be used to create an instance of the slice.</returns>
//#if NET7_0_OR_GREATER
//    [RequiresDynamicCode("Uses System.Linq.Expressions to dynamically generate delegates for creating slices")]
//#endif
//    public static SliceFactory GetSliceFactory(
//        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
//        Type sliceType)
//    {
//        ArgumentNullException.ThrowIfNull(sliceType);

//        if (sliceType.GetConstructor(Type.EmptyTypes) == null)
//        {
//            throw new ArgumentException($"Slice type {sliceType.Name} must have a parameterless constructor.", nameof(sliceType));
//        }

//        return Expression.Lambda<SliceFactory>(Expression.New(sliceType)).Compile();
//    }

//    /// <summary>
//    /// Creates a <see cref="SliceFactory{TModel}"/> that can be used to create a <see cref="RazorSlice"/> of the specified <see cref="Type"/>.
//    /// </summary>
//    /// <typeparam name="TModel">The slice model.</typeparam>
//    /// <param name="sliceType">The slice type.</param>
//    /// <param name="injectableProperties">The properties to have their values injected from the DI container.</param>
//    /// <returns>A <see cref="SliceFactory{TModel}"/> that can be used to create an instance of the slice.</returns>
//#if NET7_0_OR_GREATER
//    [RequiresDynamicCode("Uses System.Linq.Expressions to dynamically generate delegates for creating slices")]
//#endif
//    public static SliceFactory<TModel> GetSliceFactory<TModel>(
//        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
//        Type sliceType,
//        IEnumerable<PropertyInfo> injectableProperties)
//    {
//        ArgumentNullException.ThrowIfNull(sliceType);

//        if (sliceType.GetConstructor(Type.EmptyTypes) == null)
//        {
//            throw new ArgumentException($"Slice type {sliceType.Name} must have a parameterless constructor.", nameof(sliceType));
//        }

//        List<ParameterExpression> parameters = new();
//        List<Expression> factoryBody = new();

//        // Create expression in a lambda that's set on the RazorSlice.Initialize property:
//        //     slice.Initialize = (s, sp) => {
//        //         ((SliceType)s).SomeNotNullProperty = (MyService)sp.GetRequiredService(typeof(MyService))
//        //         ((SliceType)s).SomeOtherProperty = (MyOtherService)sp.GetService(typeof(MyOtherService))
//        //     };
//        var initializeBody = new List<Expression>();
//        var sliceParameter = Expression.Parameter(_razorSliceType, "s");
//        var sliceParameterCast = Expression.Convert(sliceParameter, sliceType);
//        var serviceProviderParam = Expression.Parameter(_serviceProviderType, "sp");

//        foreach (PropertyInfo injectablePropertyInfo in injectableProperties)
//        {
//            if (ExcludedServiceNames.Contains(injectablePropertyInfo.PropertyType.Name))
//            {
//                continue;
//            }

//            var propertyAccess = Expression.MakeMemberAccess(sliceParameterCast, injectablePropertyInfo);
//            // Check if property type is nullable and call GetService instead of GetRequiredService appropriately
//            var getServiceCall = !IsNullable(injectablePropertyInfo)
//                ? Expression.Call(serviceProviderParam, _getServiceMethodInfo, Expression.Constant(injectablePropertyInfo.PropertyType))
//                : Expression.Call(_getRequiredServiceMethodInfo, serviceProviderParam, Expression.Constant(injectablePropertyInfo.PropertyType));
//            initializeBody.Add(Expression.Assign(propertyAccess, Expression.Convert(getServiceCall, injectablePropertyInfo.PropertyType)));
//        }
//        var initializeLambda = Expression.Lambda(
//            delegateType: _initializeDelegateType,
//            body: Expression.Block(initializeBody),
//            name: "InitializeSlice",
//            parameters: new[] { sliceParameter, serviceProviderParam });

//        var sliceVariable = Expression.Variable(sliceType, "slice");

//        // Strongly-typed model slice
//        var modelType = typeof(TModel);
//        var modelPropInfo = sliceType.GetProperty(nameof(RazorSlice<object>.Model))!;


//        // Make a SliceFactory<TModel> like:
//        //
//        // RazorSlice<TModel> CreateSlice<TModel>(TModel model)
//        // {
//        //     var slice = new SliceType<TModel>();
//        //     slice.Initialize = (s, sp) => {
//        //         ((SliceType)s).SomeNotNullProperty = sp.GetRequiredService<MyService>()
//        //         ((SliceType)s).SomeOtherProperty = sp.GetService<MyService>()
//        //     };
//        //     slice.Model = model
//        //     return slice;
//        // }

//        factoryBody.Add(Expression.Assign(Expression.MakeMemberAccess(sliceVariable, _razorSliceInitializeProperty), initializeLambda));

//        var modelParam = Expression.Parameter(modelType, "model");
//        factoryBody.Add(Expression.Assign(Expression.MakeMemberAccess(sliceVariable, modelPropInfo), modelParam));
//        var returnTarget = Expression.Label(typeof(RazorSlice<TModel>));

//        factoryBody.Add(Expression.Label(returnTarget, sliceVariable));

//        parameters.Add(modelParam);

//        return Expression.Lambda<SliceFactory<TModel>>(
//            body: Expression.Block(
//                variables: new[] { sliceVariable },
//                Expression.Assign(sliceVariable, Expression.New(sliceType)),
//                Expression.Block(factoryBody)
//            ),
//            name: "CreateSlice",
//            parameters: parameters
//        ).Compile();
//    }

//    /// <summary>
//    /// Creates a <see cref="SliceFactory"/> that can be used to create a <see cref="RazorSlice"/> of the specified <see cref="Type"/>.
//    /// </summary>
//    /// <param name="sliceType">The slice type.</param>
//    /// <param name="injectableProperties">The properties to have their values injected from the DI container.</param>
//    /// <returns>A <see cref="SliceFactory"/> that can be used to create an instance of the slice.</returns>
//#if NET7_0_OR_GREATER
//    [RequiresDynamicCode("Uses System.Linq.Expressions to dynamically generate delegates for creating slices")]
//#endif
//    public static SliceFactory GetSliceFactory(
//        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] 
//        Type sliceType,
//        IEnumerable<PropertyInfo> injectableProperties)
//    {
//        ArgumentNullException.ThrowIfNull(sliceType);

//        if (sliceType.GetConstructor(Type.EmptyTypes) == null)
//        {
//            throw new ArgumentException($"Slice type {sliceType.Name} must have a parameterless constructor.", nameof(sliceType));
//        }

//        List<ParameterExpression> parameters = new();
//        List<Expression> factoryBody = new();

//        // Create expression in a lambda that's set on the RazorSlice.Initialize property:
//        //     slice.Initialize = (s, sp) => {
//        //         ((SliceType)s).SomeNotNullProperty = (MyService)sp.GetRequiredService(typeof(MyService))
//        //         ((SliceType)s).SomeOtherProperty = (MyOtherService)sp.GetService(typeof(MyOtherService))
//        //     };
//        var initializeBody = new List<Expression>();
//        var sliceParameter = Expression.Parameter(_razorSliceType, "s");
//        var sliceParameterCast = Expression.Convert(sliceParameter, sliceType);
//        var serviceProviderParam = Expression.Parameter(_serviceProviderType, "sp");

//        foreach (PropertyInfo injectablePropertyInfo in injectableProperties)
//        {
//            if (ExcludedServiceNames.Contains(injectablePropertyInfo.PropertyType.Name))
//            {
//                continue;
//            }

//            var propertyAccess = Expression.MakeMemberAccess(sliceParameterCast, injectablePropertyInfo);
//            // Check if property type is nullable and call GetService<T> instead of GetRequiredService<T> appropriaately
//            var getServiceCall = !IsNullable(injectablePropertyInfo)
//                ? Expression.Call(serviceProviderParam, _getServiceMethodInfo, Expression.Constant(injectablePropertyInfo.PropertyType))
//                : Expression.Call(_getRequiredServiceMethodInfo, serviceProviderParam, Expression.Constant(injectablePropertyInfo.PropertyType));
//            initializeBody.Add(Expression.Assign(propertyAccess, Expression.Convert(getServiceCall, injectablePropertyInfo.PropertyType)));
//        }
//        var initializeLambda = Expression.Lambda(
//            delegateType: _initializeDelegateType,
//            body: Expression.Block(initializeBody),
//            name: "InitializeSlice",
//            parameters: new[] { sliceParameter, serviceProviderParam });

//        var sliceVariable = Expression.Variable(sliceType, "slice");

//        // Make a SliceFactory like:
//        //
//        // RazorSlice CreateSlice()
//        // {
//        //     var slice = new SliceType();
//        //     slice.Initialize = (s, sp) => {
//        //         ((SliceType)s).SomeNotNullProperty = (MyService)sp.GetRequiredService(typeof(MyService))
//        //         ((SliceType)s).SomeOtherProperty = (MyOtherService)sp.GetService(typeof(MyOtherService))
//        //     };
//        //     return slice;
//        // }
//        factoryBody.Add(Expression.Assign(Expression.MakeMemberAccess(sliceVariable, _razorSliceInitializeProperty), initializeLambda));
//        factoryBody.Add(Expression.Label(Expression.Label(typeof(RazorSlice)), sliceVariable));

//        return Expression.Lambda<SliceFactory>(
//            body: Expression.Block(
//                variables: new[] { sliceVariable },
//                Expression.Assign(sliceVariable, Expression.New(sliceType)),
//                Expression.Block(factoryBody)
//            ),
//            name: "CreateSlice",
//            parameters: parameters
//        ).Compile();
//    }

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

    /// <summary>
    /// Creates and returns a new instance of a <see cref="RazorSliceHttpResult" /> using the provided <see cref="SliceFactory" /> delegate.
    /// </summary>
    /// <param name="sliceFactory">The <see cref="SliceFactory" /> delegate.</param>
    /// <param name="statusCode">The HTTP status code to return. Defaults to <see cref="StatusCodes.Status200OK"/>.</param>
    /// <returns>The <see cref="RazorSliceHttpResult" /> instance.</returns>
    public static RazorSliceHttpResult CreateHttpResult(SliceFactory sliceFactory, int statusCode = StatusCodes.Status200OK)
    {
        var result = (RazorSliceHttpResult)sliceFactory();
        result.StatusCode = statusCode;
        return result;
    }

    /// <summary>
    /// Creates and returns a new instance of a <see cref="RazorSliceHttpResult{TModel}" /> based on the provided name.
    /// </summary>
    /// <param name="sliceFactory">The <see cref="SliceFactory" /> delegate.</param>
    /// <param name="model">The model to use for the template instance.</param>
    /// <param name="statusCode">The HTTP status code to return. Defaults to <see cref="StatusCodes.Status200OK"/>.</param>
    /// <typeparam name="TModel">The model type of the template.</typeparam>
    /// <returns>The <see cref="RazorSliceHttpResult{TModel}" /> instance.</returns>
    public static RazorSliceHttpResult<TModel> CreateHttpResult<TModel>(SliceFactory<TModel> sliceFactory, TModel model, int statusCode = StatusCodes.Status200OK)
    {
        var result = (RazorSliceHttpResult<TModel>)RazorSliceFactory.Create(sliceFactory, model);
        result.StatusCode = statusCode;
        return result;
    }
}
