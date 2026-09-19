using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Http;
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
    private static readonly PropertyInfo _requestServicesProperty = typeof(HttpContext).GetProperty(nameof(HttpContext.RequestServices))
        ?? throw new InvalidOperationException("Could not find HttpContext.RequestServices. Likely a bug in Razor Slices itself.");
    private static readonly MethodInfo _getServiceMethod = typeof(IServiceProvider).GetMethod(nameof(IServiceProvider.GetService))
        ?? throw new InvalidOperationException("Could not find IServiceProvider.GetService. Likely a bug in Razor Slices itself.");
    private static readonly MethodInfo _getRequiredServiceMethod = typeof(ServiceProviderServiceExtensions).GetMethod(nameof(ServiceProviderServiceExtensions.GetRequiredService), [typeof(IServiceProvider), typeof(Type)])
        ?? throw new InvalidOperationException("Could not find ServiceProviderServiceExtensions.GetRequirdService. Likely a bug in Razor Slices itself.");

    private static readonly ConstructorInfo _ioeCtor = typeof(InvalidOperationException).GetConstructor([typeof(string)])!;
    private static readonly NullabilityInfoContext _nullabilityContext = new();

    /// <summary>
    /// Creates a slice using its generated constructor callback and configures deferred property injection.
    /// </summary>
    /// <typeparam name="TSlice">The compiled Razor template type.</typeparam>
    /// <param name="createSlice">A callback that directly constructs the template.</param>
    /// <returns>A new slice, or its Hot Reload replacement.</returns>
    public static RazorSlice Create<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TSlice>(Func<TSlice> createSlice)
        where TSlice : RazorSlice
    {
        ArgumentNullException.ThrowIfNull(createSlice);
        var activation = SliceCache<TSlice>.Current;
        var slice = activation.CreateReplacement is { } createReplacement ? createReplacement() : createSlice();
        slice.Initialize = activation.Initialize;
        return slice;
    }

    /// <summary>
    /// Creates a model slice using its generated constructor callback and configures deferred property injection.
    /// </summary>
    /// <typeparam name="TSlice">The compiled Razor template type.</typeparam>
    /// <typeparam name="TModel">The model type.</typeparam>
    /// <param name="model">The model for the slice.</param>
    /// <param name="createSlice">A callback that directly constructs the template with its model.</param>
    /// <returns>A new slice, or its Hot Reload replacement, with the specified model.</returns>
    public static RazorSlice<TModel> Create<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TSlice, TModel>(
        TModel model, Func<TModel, TSlice> createSlice)
        where TSlice : RazorSlice<TModel>
    {
        ArgumentNullException.ThrowIfNull(createSlice);
        var activation = SliceCache<TSlice>.Current;
        RazorSlice<TModel> slice;
        if (activation.CreateReplacement is { } createReplacement)
        {
            slice = (RazorSlice<TModel>)createReplacement();
            slice.Model = model;
        }
        else
        {
            slice = createSlice(model);
        }
        slice.Initialize = activation.Initialize;
        return slice;
    }

    internal sealed record SliceActivation(
        Action<RazorSlice, IServiceProvider?, HttpContext?>? Initialize,
        Func<RazorSlice>? CreateReplacement = null);

    internal static class SliceCache<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TSlice>
        where TSlice : RazorSlice
    {
        internal static volatile SliceActivation Current = new(GetInitializer(typeof(TSlice)));

        static SliceCache()
        {
            if (HotReloadService.IsSupported)
            {
                HotReloadService.ClearCacheEvent += ReplaceSliceType;
            }
        }

        [UnconditionalSuppressMessage("Trimming", "IL2072",
            Justification = "Replacement types are only used during Hot Reload, which is not supported in trimmed applications.")]
        internal static void ReplaceSliceType(Type[]? changedTypes)
        {
            if (HotReloadService.TryGetUpdatedType(changedTypes, typeof(TSlice), out var updatedType))
            {
                // Publish construction and injection together so a render cannot mix two generations of the template.
                Current = new(GetInitializer(updatedType), () => (RazorSlice)Activator.CreateInstance(updatedType)!);
            }
        }
    }

    [UnconditionalSuppressMessage("AOT", "IL3050",
        Justification = "Guarded by RuntimeFeature.IsDynamicCodeCompiled.")]
    internal static Action<RazorSlice, IServiceProvider?, HttpContext?>? GetInitializer(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type sliceType)
    {
        var properties = GetInjectableProperties(sliceType);
        if (!properties.Any)
        {
            return null;
        }

        return RuntimeFeature.IsDynamicCodeCompiled
            ? GetExpressionInitAction(sliceType, properties).Compile()
            : GetReflectionInitAction(sliceType, properties);
    }

    internal static (bool Any, PropertyInfo[] Nullable, PropertyInfo[] NonNullable) GetInjectableProperties(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
        Type sliceType)
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
                    nullable ??= [];
                    nullable.Add(pi);
                }
                else
                {
                    nonNullable ??= [];
                    nonNullable.Add(pi);
                }
            }
        }

        return (nullable is not null || nonNullable is not null,
                nullable?.ToArray() ?? [],
                nonNullable?.ToArray() ?? []);
    }

    internal static Action<RazorSlice, IServiceProvider?, HttpContext?> GetReflectionInitAction(
        Type sliceType, (bool Any, PropertyInfo[] Nullable, PropertyInfo[] NonNullable) properties)
    {
        return (slice, serviceProvider, httpContext) =>
        {
            var services = (serviceProvider ?? httpContext?.RequestServices)
                ?? throw new InvalidOperationException($"Cannot initialize @inject properties of slice {sliceType.Name} because the ServiceProvider property is null.");

            foreach (var pi in properties.NonNullable)
            {
                pi.SetValue(slice, services.GetRequiredService(pi.PropertyType));
            }

            foreach (var pi in properties.Nullable)
            {
                pi.SetValue(slice, services.GetService(pi.PropertyType));
            }
        };
    }

    [RequiresDynamicCode("Uses System.Linq.Expressions to dynamically generate delegates for initializing slices")]
    private static Expression<Action<RazorSlice, IServiceProvider?, HttpContext?>> GetExpressionInitAction(
        Type sliceType, (bool Any, PropertyInfo[] Nullable, PropertyInfo[] NonNullable) properties)
    {
        // Make a delegate like:
        //
        // (RazorSlice slice, IServiceProvider? sp, HttpContext? httpContext) =>
        // {
        //     var services = sp;
        //     if (services == null && httpContext != null)
        //     {
        //         services = httpContext.RequestServices;
        //     }
        //     if (services == null) throw new InvalidOperationException("Cannot initialize @inject properties of slice because the ServiceProvider property is null.");
        //     var s = (MySlice)slice;
        //     s.SomeProp = (SomeService)services.GetService(typeof(SomeService));
        //     s.NextProp = (SomeOtherService)services.GetRequiredService(typeof(SomeOtherService));
        // }

        var sliceParam = Expression.Parameter(typeof(RazorSlice), "slice");
        var spParam = Expression.Parameter(typeof(IServiceProvider), "sp");
        var httpContextParam = Expression.Parameter(typeof(HttpContext), "httpContext");
        var servicesVar = Expression.Variable(typeof(IServiceProvider), "services");
        var castSliceVar = Expression.Variable(sliceType, "s");

        var body = new List<Expression>
        {
            // var services = sp;
            Expression.Assign(servicesVar, spParam),
            // if
            Expression.IfThen(
                Expression.And(
                    // services == null
                    Expression.Equal(servicesVar, Expression.Constant(null)),
                    // httpContext != null
                    Expression.NotEqual(httpContextParam, Expression.Constant(null))),
                // services = httpContext.RequestServices
                Expression.Assign(servicesVar, Expression.MakeMemberAccess(httpContextParam, _requestServicesProperty))),
            Expression.IfThen(
                // services == null
                Expression.Equal(servicesVar, Expression.Constant(null)),
                // throw new InvalidOperationException
                Expression.Throw(Expression.New(_ioeCtor, Expression.Constant("Cannot initialize @inject properties of slice because the ServiceProvider property is null.")))),
            // var s = (MySlice)slice;
            Expression.Assign(castSliceVar, Expression.Convert(sliceParam, sliceType))
        };

        foreach (var ip in properties.Nullable)
        {
            // s.SomeProp = (SomeService)services.GetService(typeof(SomeService));
            var propertyAccess = Expression.MakeMemberAccess(castSliceVar, ip);
            var getServiceCall = Expression.Call(servicesVar, _getServiceMethod, Expression.Constant(ip.PropertyType));
            body.Add(Expression.Assign(propertyAccess, Expression.Convert(getServiceCall, ip.PropertyType)));
        }

        foreach (var ip in properties.NonNullable)
        {
            // s.SomeProp = (SomeService)services.GetRequiredService(typeof(SomeService));
            var propertyAccess = Expression.MakeMemberAccess(castSliceVar, ip);
            var getServiceCall = Expression.Call(null, _getRequiredServiceMethod, servicesVar, Expression.Constant(ip.PropertyType));
            body.Add(Expression.Assign(propertyAccess, Expression.Convert(getServiceCall, ip.PropertyType)));
        }

        return Expression.Lambda<Action<RazorSlice, IServiceProvider?, HttpContext?>>(
            body: Expression.Block(
                variables: [servicesVar, castSliceVar],
                body),
            parameters: [sliceParam, spParam, httpContextParam]);
    }

    private static bool IsNullable(PropertyInfo info) =>
        Nullable.GetUnderlyingType(info.PropertyType) is not null
            || _nullabilityContext.Create(info).WriteState is not NullabilityState.NotNull;
}
