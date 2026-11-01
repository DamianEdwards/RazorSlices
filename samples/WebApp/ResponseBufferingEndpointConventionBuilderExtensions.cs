namespace RazorSlices.Samples.WebApp;

public static class ResponseBufferingEndpointConventionBuilderExtensions
{
    public static TBuilder DisableResponseBuffering<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.WithMetadata(new DisableResponseBufferingAttribute());
    }
}
