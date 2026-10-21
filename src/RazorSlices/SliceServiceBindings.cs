namespace RazorSlices;

internal sealed class SliceServiceBindings
{
    private readonly Func<RazorSlice, IRazorSliceBindable?>[]? _services;
    internal Action<RazorSlice> Bind { get; }
    internal Func<RazorSlice, Exception?> Unbind { get; }

    internal SliceServiceBindings(Func<RazorSlice, IRazorSliceBindable?>[] services)
    {
        _services = services;
        Bind = BindRuntime;
        Unbind = UnbindRuntime;
    }

    internal SliceServiceBindings(Action<RazorSlice> bind, Func<RazorSlice, Exception?> unbind)
    {
        Bind = bind;
        Unbind = unbind;
    }

    private void BindRuntime(RazorSlice slice)
    {
        var completed = 0;
        try
        {
            for (; completed < _services!.Length; completed++)
            {
                _services[completed](slice)?.Bind(slice);
            }
        }
        catch (Exception error)
        {
            var cleanup = UnbindCompleted(slice, completed);
            if (cleanup is not null)
            {
                throw new AggregateException(error, cleanup);
            }
            throw;
        }
    }

    private Exception? UnbindRuntime(RazorSlice slice)
    {
        return UnbindCompleted(slice, _services!.Length);
    }

    private Exception? UnbindCompleted(RazorSlice slice, int count)
    {
        List<Exception>? failures = null;
        for (var i = count - 1; i >= 0; i--)
        {
            try
            {
                _services![i](slice)?.Unbind(slice);
            }
            catch (Exception error)
            {
                (failures ??= []).Add(error);
            }
        }
        return failures switch
        {
            null => null,
            { Count: 1 } => failures[0],
            _ => new AggregateException(failures)
        };
    }
}
