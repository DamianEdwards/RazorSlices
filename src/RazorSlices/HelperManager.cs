namespace RazorSlices;

internal sealed class HelperManager : IHelperManager
{
    private readonly Stack<RazorSlice> _slices = new();
    private Dictionary<Type, RazorSliceHelpers>? _helpers;

    public RazorSlice CurrentSlice => _slices.Peek();

    public void Bind(RazorSlice slice)
    {
        _slices.Push(slice);
    }

    public void Unbind(RazorSlice slice)
    {
        _slices.Pop();
    }

    public TContract GetHelper<TContract, TImplementation>(Func<TImplementation> create)
        where TContract : class
        where TImplementation : RazorSliceHelpers
    {
        ArgumentNullException.ThrowIfNull(create);
        _helpers ??= [];
        if (_helpers.TryGetValue(typeof(TImplementation), out var existing))
        {
            return (TContract)(object)existing;
        }
        var helper = RazorSliceHelperActivation<TImplementation>.Create(create)
            ?? throw new InvalidOperationException("The helper factory returned null.");
        if (helper is not TContract contract)
        {
            throw new InvalidOperationException($"Helper {helper.GetType()} does not implement {typeof(TContract)}.");
        }
        helper.Initialize(this);
        _helpers.Add(typeof(TImplementation), helper);
        return contract;
    }
}
