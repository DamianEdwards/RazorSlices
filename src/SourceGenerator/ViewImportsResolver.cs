using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace RazorSlices.SourceGenerator;

/// <summary>
/// Resolves the effective @inherits and @using directives for a Razor slice
/// by traversing the _ViewImports.cshtml hierarchy.
/// </summary>
internal static class ViewImportsResolver
{
    private const string ViewImportsFileName = "_ViewImports.cshtml";

    /// <summary>
    /// Builds a dictionary mapping directory paths to their parsed _ViewImports directives.
    /// </summary>
    internal static Dictionary<string, ViewImportsDirectives> BuildViewImportsMap(ImmutableArray<AdditionalText> allTexts)
    {
        var map = new Dictionary<string, ViewImportsDirectives>(StringComparer.OrdinalIgnoreCase);

        foreach (var text in allTexts)
        {
            var fileName = Path.GetFileName(text.Path);
            if (!string.Equals(fileName, ViewImportsFileName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var directory = Path.GetDirectoryName(text.Path);
            if (directory is null)
            {
                continue;
            }

            var sourceText = text.GetText();
            if (sourceText is null)
            {
                continue;
            }

            var inheritsDirective = RazorDirectiveParser.ParseInheritsDirective(sourceText);
            var usingDirectives = RazorDirectiveParser.ParseUsingDirectives(sourceText);

            map[directory] = new ViewImportsDirectives(inheritsDirective, usingDirectives);
        }

        return map;
    }

    /// <summary>
    /// Resolves the effective directives for a slice file by walking up the directory hierarchy
    /// and merging _ViewImports.cshtml directives.
    /// </summary>
    /// <param name="sliceFilePath">Full path to the .cshtml slice file.</param>
    /// <param name="projectDirectory">The project root directory.</param>
    /// <param name="viewImportsMap">Pre-built map of directory → parsed _ViewImports directives.</param>
    /// <param name="sliceSourceText">The source text of the slice file itself.</param>
    /// <returns>The resolved directives including the effective @inherits and accumulated @using directives.</returns>
    internal static ResolvedDirectives ResolveDirectives(
        string sliceFilePath,
        string projectDirectory,
        Dictionary<string, ViewImportsDirectives> viewImportsMap,
        SourceText sliceSourceText)
    {
        // Parse the slice file's own directives
        var sliceInherits = RazorDirectiveParser.ParseInheritsDirective(sliceSourceText);
        var sliceUsings = RazorDirectiveParser.ParseUsingDirectives(sliceSourceText);

        // Build the directory chain from project root down to the slice's directory
        var sliceDirectory = Path.GetDirectoryName(sliceFilePath);
        if (sliceDirectory is null)
        {
            return new ResolvedDirectives(sliceInherits, sliceUsings);
        }

        // Collect all _ViewImports directories from project root down to slice directory
        var directoryChain = GetDirectoryChain(sliceDirectory, projectDirectory);

        // Accumulate usings from outermost to innermost (parent first, child last)
        var accumulatedUsings = new List<UsingDirective>();
        string? effectiveInherits = null;

        foreach (var dir in directoryChain)
        {
            if (viewImportsMap.TryGetValue(dir, out var directives))
            {
                // @using accumulates
                accumulatedUsings.AddRange(directives.UsingDirectives);
                // @inherits: innermost (child) wins, so keep overwriting
                if (directives.InheritsDirective is not null)
                {
                    effectiveInherits = directives.InheritsDirective;
                }
            }
        }

        // Slice's own usings come last (most specific)
        accumulatedUsings.AddRange(sliceUsings);

        // Slice's own @inherits wins over any _ViewImports @inherits
        if (sliceInherits is not null)
        {
            effectiveInherits = sliceInherits;
        }

        return new ResolvedDirectives(effectiveInherits, accumulatedUsings);
    }

    /// <summary>
    /// Returns the directory chain from projectDirectory down to targetDirectory (inclusive),
    /// ordered from outermost to innermost.
    /// </summary>
    private static List<string> GetDirectoryChain(string targetDirectory, string projectDirectory)
    {
        var chain = new List<string>();
        var normalizedProject = NormalizePath(projectDirectory);
        var current = NormalizePath(targetDirectory);

        // Walk from target up to project root, collecting directories
        while (current is not null && current.Length >= normalizedProject.Length)
        {
            chain.Add(current);
            if (string.Equals(current, normalizedProject, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            var parent = Path.GetDirectoryName(current);
            if (parent is null || string.Equals(parent, current, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            current = NormalizePath(parent);
        }

        // Reverse so we go from outermost (project root) to innermost (slice directory)
        chain.Reverse();
        return chain;
    }

    private static string NormalizePath(string path)
    {
        return path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}

internal readonly struct ViewImportsDirectives(string? inheritsDirective, List<UsingDirective> usingDirectives)
{
    public string? InheritsDirective { get; } = inheritsDirective;
    public List<UsingDirective> UsingDirectives { get; } = usingDirectives;
}

internal readonly struct ResolvedDirectives(string? inheritsDirective, List<UsingDirective> usingDirectives)
{

    /// <summary>
    /// The effective @inherits directive value (from the slice or nearest _ViewImports).
    /// </summary>
    public string? InheritsDirective { get; } = inheritsDirective;

    /// <summary>
    /// All accumulated @using directives from the _ViewImports hierarchy and the slice itself.
    /// </summary>
    public List<UsingDirective> UsingDirectives { get; } = usingDirectives;
}
