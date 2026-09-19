namespace RazorSlices.Samples.WebApp;

public interface IDisableResponseBufferingMetadata
{
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class DisableResponseBufferingAttribute : Attribute, IDisableResponseBufferingMetadata
{
}
