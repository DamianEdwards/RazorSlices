using System.Reflection;

namespace RazorSlices;

/// <summary>
/// A class that contains relevant details about a slice.
/// </summary>
internal sealed class SliceDefinition
{
    private readonly string _identifier;
    private readonly Type _sliceType;
    private readonly Delegate _factory;
    private readonly IEnumerable<PropertyInfo> _injectableProperties;

    /// <summary>
    /// Initializes a new instance of the <see cref="SliceDefinition"/> class with the specified identifier, slice type, factory, and injectable properties.
    /// </summary>
    /// <param name="identifier">The unique identifier of the slice.</param>
    /// <param name="sliceType">The type of the slice.</param>
    /// <param name="factory">The factory delegate used to create instances of the slice.</param>
    /// <param name="injectableProperties">The properties of the slice that can be injected.</param>
    internal SliceDefinition(string identifier, Type sliceType, Delegate factory, IEnumerable<PropertyInfo> injectableProperties)
    {
        _identifier = identifier;
        _sliceType = sliceType;
        _factory = factory;
        _injectableProperties = injectableProperties;
        HasInjectableProperties = injectableProperties.Any();
    }

    /// <summary>
    /// Gets the unique identifier of the slice.
    /// </summary>
    internal string Identifier => _identifier;

    /// <summary>
    /// Gets the type of the slice.
    /// </summary>
    internal Type SliceType => _sliceType;

    /// <summary>
    /// Gets the factory delegate used to create instances of the slice.
    /// </summary>
    internal Delegate Factory => _factory;

    /// <summary>
    /// Gets the dependency-injected properties of the slice.
    /// </summary>
    internal IEnumerable<PropertyInfo> InjectableProperties => _injectableProperties;

    /// <summary>
    /// Gets a value indicating whether this slice definition has any dependency-injected properties
    /// </summary>
    public bool HasInjectableProperties { get; }
}
