using System.Reflection;

namespace RazorSlices;

/// <summary>
/// A class that contain relevant details about a slice
/// </summary>
public class SliceDefinition
{
    private readonly string _identifier;
    private readonly Type _sliceType;
    private readonly Delegate _factory;
    private readonly IEnumerable<PropertyInfo> _injectableProperties;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="identifier"></param>
    /// <param name="sliceType"></param>
    /// <param name="factory"></param>
    /// <param name="injectableProperties"></param>
    public SliceDefinition(string identifier, Type sliceType, Delegate factory, IEnumerable<PropertyInfo> injectableProperties)
    {
        _identifier = identifier;
        _sliceType = sliceType;
        _factory = factory;
        _injectableProperties = injectableProperties;
    }

    /// <summary>
    /// 
    /// </summary>
    public string Identifier => _identifier;

    /// <summary>
    /// 
    /// </summary>
    public Type SliceType => _sliceType;

    /// <summary>
    /// 
    /// </summary>
    public Delegate Factory => _factory;

    /// <summary>
    /// 
    /// </summary>
    public IEnumerable<PropertyInfo> InjectableProperties => _injectableProperties;
}
