using RazorSlices;

namespace OptimizerSample;

public abstract class CustomMinimalBase<T> : RazorSliceBase<T>;

public abstract class CustomFullBase<T> : RazorSlice<T>;

public abstract class CustomHelperBase : RazorSliceHelpers;

public interface IMarkup
{
    void Text(string value);
}
