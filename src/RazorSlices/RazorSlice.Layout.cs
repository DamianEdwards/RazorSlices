namespace RazorSlices;

public partial class RazorSlice
{
    /// <summary>
    /// Gets the <see cref="RazorSlice"/> to use as the layout when rendering.
    /// </summary>
    /// <remarks>
    /// Override this method and return a slice instance that derives from <see cref="RazorLayoutSlice"/> or
    /// <see cref="RazorLayoutSlice{TModel}"/>
    /// </remarks>
    /// <returns>The <see cref="RazorSlice"/> to use as layout.</returns>
    protected virtual RazorSlice? GetLayout() => null;

    /// <summary>
    /// Executes the section with the given name to the layout.
    /// </summary>
    /// <remarks>
    /// Override this method in the <c>@functions</c> block of your <c>.cshtml</c> file to define section content, e.g.<br/>
    /// <example>
    /// <code>
    /// @functions {
    ///     protected override Task ExecuteSectionAsync(string sectionName) {
    ///         if (sectionName == "scripts")
    ///         {
    ///             &lt;script src="js/fancyeffect.js" /&gt;
    ///         }
    ///     }
    /// }
    /// </code>
    /// </example>
    /// </remarks>
    /// <param name="sectionName">The name of the section.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="sectionName"/> is <c>null</c>.</exception>
    protected virtual Task ExecuteSectionAsync(string sectionName)
    {
        ArgumentNullException.ThrowIfNull(sectionName);

        return Task.CompletedTask;
    }
}
