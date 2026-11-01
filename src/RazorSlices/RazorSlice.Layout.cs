namespace RazorSlices;

public partial class RazorSlice
{
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
    protected internal virtual Task ExecuteSectionAsync(string sectionName)
    {
        ArgumentNullException.ThrowIfNull(sectionName);

        return Task.CompletedTask;
    }
}
