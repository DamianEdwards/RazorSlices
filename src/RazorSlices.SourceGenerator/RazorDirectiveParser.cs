using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.Text;

namespace RazorSlices.SourceGenerator;

/// <summary>
/// Parses Razor directives (@inherits, @using) from .cshtml file content.
/// </summary>
internal static class RazorDirectiveParser
{
    /// <summary>
    /// Parses the @inherits directive value from the given source text.
    /// Returns null if no @inherits directive is found.
    /// </summary>
    internal static string? ParseInheritsDirective(SourceText sourceText)
    {
        foreach (var line in sourceText.Lines)
        {
            var lineText = line.ToString().TrimStart();
            if (lineText.StartsWith("@inherits ", StringComparison.Ordinal))
            {
                var value = lineText.Substring("@inherits ".Length).Trim();
                if (value.Length > 0)
                {
                    return value;
                }
            }
        }
        return null;
    }

    /// <summary>
    /// Parses all @using directives from the given source text.
    /// Returns a list of (namespace, alias) tuples. alias is null for non-aliased usings.
    /// </summary>
    internal static List<UsingDirective> ParseUsingDirectives(SourceText sourceText)
    {
        var usings = new List<UsingDirective>();
        foreach (var line in sourceText.Lines)
        {
            var lineText = line.ToString().TrimStart();
            if (lineText.StartsWith("@using ", StringComparison.Ordinal))
            {
                var value = lineText.Substring("@using ".Length).Trim();
                // Remove trailing semicolons if present
                if (value.EndsWith(";", StringComparison.Ordinal))
                {
                    value = value.Substring(0, value.Length - 1).Trim();
                }
                if (value.Length == 0) continue;

                // Check for alias: @using Alias = Namespace.Type
                var equalsIndex = value.IndexOf('=');
                if (equalsIndex > 0)
                {
                    var alias = value.Substring(0, equalsIndex).Trim();
                    var target = value.Substring(equalsIndex + 1).Trim();
                    if (alias.Length > 0 && target.Length > 0)
                    {
                        usings.Add(new UsingDirective(target, alias));
                    }
                }
                else
                {
                    usings.Add(new UsingDirective(value, null));
                }
            }
        }
        return usings;
    }

    /// <summary>
    /// Extracts the model type from a base type string.
    /// For example, "RazorSlice&lt;Models.Todo&gt;" returns "Models.Todo".
    /// Returns null if the base type is not generic (no model).
    /// </summary>
    internal static string? ExtractModelType(string baseType)
    {
        // Find the first '<' and last '>' for the generic type argument
        var openAngle = baseType.IndexOf('<');
        if (openAngle < 0) return null;

        var closeAngle = baseType.LastIndexOf('>');
        if (closeAngle <= openAngle) return null;

        var modelType = baseType.Substring(openAngle + 1, closeAngle - openAngle - 1).Trim();
        return modelType.Length > 0 ? modelType : null;
    }

    /// <summary>
    /// Extracts the base type name (without generic arguments) from a base type string.
    /// For example, "RazorSlice&lt;Models.Todo&gt;" returns "RazorSlice".
    /// </summary>
    internal static string ExtractBaseTypeName(string baseType)
    {
        var openAngle = baseType.IndexOf('<');
        return openAngle >= 0 ? baseType.Substring(0, openAngle).Trim() : baseType.Trim();
    }
}

internal readonly struct UsingDirective(string namespaceOrType, string? alias) : IEquatable<UsingDirective>
{

    /// <summary>
    /// The namespace or fully qualified type (for alias usings).
    /// </summary>
    public string NamespaceOrType { get; } = namespaceOrType;

    /// <summary>
    /// The alias, or null for non-aliased usings.
    /// </summary>
    public string? Alias { get; } = alias;

    public bool Equals(UsingDirective other) =>
        string.Equals(NamespaceOrType, other.NamespaceOrType, StringComparison.Ordinal) &&
        string.Equals(Alias, other.Alias, StringComparison.Ordinal);

    public override bool Equals(object obj) => obj is UsingDirective other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            return (NamespaceOrType.GetHashCode() * 397) ^ (Alias?.GetHashCode() ?? 0);
        }
    }
}
