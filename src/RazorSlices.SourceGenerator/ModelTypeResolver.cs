using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis;

namespace RazorSlices.SourceGenerator;

/// <summary>
/// Resolves model type names from Razor @inherits directives to fully qualified C# type names
/// using the compilation's type information and @using directives.
/// </summary>
internal static class ModelTypeResolver
{
    // C# keyword to System type mapping
    private static readonly Dictionary<string, string> PrimitiveTypeMap = new(StringComparer.Ordinal)
    {
        { "bool", "global::System.Boolean" },
        { "byte", "global::System.Byte" },
        { "sbyte", "global::System.SByte" },
        { "char", "global::System.Char" },
        { "decimal", "global::System.Decimal" },
        { "double", "global::System.Double" },
        { "float", "global::System.Single" },
        { "int", "global::System.Int32" },
        { "uint", "global::System.UInt32" },
        { "long", "global::System.Int64" },
        { "ulong", "global::System.UInt64" },
        { "short", "global::System.Int16" },
        { "ushort", "global::System.UInt16" },
        { "string", "global::System.String" },
        { "object", "global::System.Object" },
    };

    // Implicit namespaces available in Razor files (from the Razor compiler and ASP.NET Core)
    private static readonly string[] ImplicitNamespaces =
    [
        "System",
        "System.Collections.Generic",
        "System.Linq",
        "System.Threading.Tasks",
        "Microsoft.AspNetCore.Mvc.Razor",
    ];

    /// <summary>
    /// Resolves a model type name to a fully qualified C# type name.
    /// </summary>
    /// <param name="modelTypeName">The model type name as it appears in the @inherits directive.</param>
    /// <param name="usingDirectives">The accumulated @using directives.</param>
    /// <param name="compilation">The Roslyn compilation for type lookup.</param>
    /// <param name="rootNamespace">The root namespace of the project, used as a resolution candidate.</param>
    /// <returns>The fully qualified type name with global:: prefix, or null if resolution fails.</returns>
    internal static string? ResolveModelType(string modelTypeName, List<UsingDirective> usingDirectives, Compilation compilation, string? rootNamespace = null)
    {
        return ResolveTypeExpression(modelTypeName.Trim(), usingDirectives, compilation, rootNamespace);
    }

    private static string? ResolveTypeExpression(string typeName, List<UsingDirective> usingDirectives, Compilation compilation, string? rootNamespace)
    {
        // Handle nullable value types: T?
        if (typeName.EndsWith("?", StringComparison.Ordinal))
        {
            var innerType = typeName.Substring(0, typeName.Length - 1).Trim();
            var resolved = ResolveTypeExpression(innerType, usingDirectives, compilation, rootNamespace);
            return resolved != null ? resolved + "?" : null;
        }

        // Handle array types: T[], T[,], etc.
        if (typeName.EndsWith("]", StringComparison.Ordinal))
        {
            var bracketStart = FindArrayBracketStart(typeName);
            if (bracketStart >= 0)
            {
                var elementType = typeName.Substring(0, bracketStart).Trim();
                var arraySuffix = typeName.Substring(bracketStart);
                var resolved = ResolveTypeExpression(elementType, usingDirectives, compilation, rootNamespace);
                return resolved != null ? resolved + arraySuffix : null;
            }
        }

        // Handle generic types: Type<T1, T2>
        var genericOpen = FindTopLevelGenericOpen(typeName);
        if (genericOpen >= 0)
        {
            return ResolveGenericType(typeName, genericOpen, usingDirectives, compilation, rootNamespace);
        }

        // Handle simple type names
        return ResolveSimpleType(typeName, usingDirectives, compilation, rootNamespace: rootNamespace);
    }

    private static string? ResolveGenericType(string typeName, int genericOpen, List<UsingDirective> usingDirectives, Compilation compilation, string? rootNamespace)
    {
        var outerType = typeName.Substring(0, genericOpen).Trim();
        var genericClose = typeName.LastIndexOf('>');
        if (genericClose <= genericOpen) return null;

        var argsString = typeName.Substring(genericOpen + 1, genericClose - genericOpen - 1);
        var args = SplitGenericArguments(argsString);

        // Resolve each generic argument
        var resolvedArgs = new List<string>();
        foreach (var arg in args)
        {
            var resolved = ResolveTypeExpression(arg.Trim(), usingDirectives, compilation, rootNamespace);
            if (resolved == null) return null;
            resolvedArgs.Add(resolved);
        }

        // Resolve the outer type with the correct arity
        // For compilation lookup, generic types use backtick notation: Type`N
        var metadataName = outerType + "`" + args.Count;
        var resolvedOuter = ResolveSimpleType(outerType, usingDirectives, compilation, metadataName, stripGenericParams: true, rootNamespace: rootNamespace);
        if (resolvedOuter == null) return null;

        var sb = new StringBuilder();
        sb.Append(resolvedOuter);
        sb.Append('<');
        for (int i = 0; i < resolvedArgs.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(resolvedArgs[i]);
        }
        sb.Append(">");
        return sb.ToString();
    }

    private static string? ResolveSimpleType(string typeName, List<UsingDirective> usingDirectives, Compilation compilation, string? metadataNameOverride = null, bool stripGenericParams = false, string? rootNamespace = null)
    {
        // 1. Check primitive types
        if (PrimitiveTypeMap.TryGetValue(typeName, out var primitiveType))
        {
            return primitiveType;
        }

        // 2. Check using aliases
        var dotIndex = typeName.IndexOf('.');
        if (dotIndex > 0)
        {
            var prefix = typeName.Substring(0, dotIndex);
            var suffix = typeName.Substring(dotIndex + 1);
            foreach (var ud in usingDirectives)
            {
                if (ud.Alias != null && string.Equals(ud.Alias, prefix, StringComparison.Ordinal))
                {
                    // Replace alias with actual namespace/type
                    var expandedType = ud.NamespaceOrType + "." + suffix;
                    // For generic types, extract the arity-qualified suffix from metadataNameOverride
                    string? expandedLookup = null;
                    if (metadataNameOverride != null)
                    {
                        var metaDotIndex = metadataNameOverride.IndexOf('.');
                        var metaSuffix = metaDotIndex >= 0
                            ? metadataNameOverride.Substring(metaDotIndex + 1)
                            : metadataNameOverride;
                        expandedLookup = ud.NamespaceOrType + "." + metaSuffix;
                    }
                    var resolved = TryResolveViaCompilation(expandedType, expandedLookup, compilation, stripGenericParams);
                    if (resolved != null) return resolved;
                }
            }
        }

        // 3. Try as fully-qualified name first
        var lookupName = metadataNameOverride ?? typeName;
        var result = TryResolveViaCompilation(typeName, lookupName, compilation, stripGenericParams);
        if (result != null) return result;

        // 4. Try with each @using namespace prefix
        foreach (var ud in usingDirectives)
        {
            if (ud.Alias != null) continue; // Skip aliases, handled above

            var candidateName = ud.NamespaceOrType + "." + typeName;
            var candidateLookup = metadataNameOverride != null
                ? ud.NamespaceOrType + "." + metadataNameOverride
                : candidateName;

            result = TryResolveViaCompilation(candidateName, candidateLookup, compilation, stripGenericParams);
            if (result != null) return result;
        }

        // 5. Try with implicit namespaces (System, System.Collections.Generic, etc.)
        foreach (var implicitNs in ImplicitNamespaces)
        {
            var candidateName = implicitNs + "." + typeName;
            var candidateLookup = metadataNameOverride != null
                ? implicitNs + "." + metadataNameOverride
                : candidateName;

            result = TryResolveViaCompilation(candidateName, candidateLookup, compilation, stripGenericParams);
            if (result != null) return result;
        }

        // 6. Try with the project's root namespace
        if (!string.IsNullOrEmpty(rootNamespace))
        {
            var candidateName = rootNamespace + "." + typeName;
            var candidateLookup = metadataNameOverride != null
                ? rootNamespace + "." + metadataNameOverride
                : candidateName;

            result = TryResolveViaCompilation(candidateName, candidateLookup, compilation, stripGenericParams);
            if (result != null) return result;
        }

        return null;
    }

    private static string? TryResolveViaCompilation(string displayName, string? metadataName, Compilation compilation, bool stripGenericParams = false)
    {
        var lookup = metadataName ?? displayName;
        var symbol = compilation.GetTypeByMetadataName(lookup);
        if (symbol != null)
        {
            var result = "global::" + symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat
                .WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.OmittedAsContaining));

            // For generic types being resolved as outer types, strip the generic type parameters
            // e.g. "global::System.Func<T, TResult>" → "global::System.Func"
            if (stripGenericParams)
            {
                var angleIndex = result.IndexOf('<');
                if (angleIndex >= 0)
                {
                    result = result.Substring(0, angleIndex);
                }
            }

            return result;
        }
        return null;
    }

    /// <summary>
    /// Splits generic arguments at the top level, respecting nested angle brackets.
    /// "int, List&lt;string&gt;" → ["int", "List&lt;string&gt;"]
    /// </summary>
    private static List<string> SplitGenericArguments(string args)
    {
        var result = new List<string>();
        int depth = 0;
        int start = 0;

        for (int i = 0; i < args.Length; i++)
        {
            var c = args[i];
            if (c == '<') depth++;
            else if (c == '>') depth--;
            else if (c == ',' && depth == 0)
            {
                result.Add(args.Substring(start, i - start));
                start = i + 1;
            }
        }

        result.Add(args.Substring(start));
        return result;
    }

    /// <summary>
    /// Finds the opening '[' for an array suffix, skipping nested brackets.
    /// </summary>
    private static int FindArrayBracketStart(string typeName)
    {
        // Find the last ']' and then walk back to find the matching '['
        int depth = 0;
        for (int i = typeName.Length - 1; i >= 0; i--)
        {
            if (typeName[i] == ']') depth++;
            else if (typeName[i] == '[')
            {
                depth--;
                if (depth == 0) return i;
            }
            else if (depth == 0) return -1; // Non-bracket character before finding match
        }
        return -1;
    }

    /// <summary>
    /// Finds the index of the first top-level '&lt;' (not nested within other angle brackets).
    /// </summary>
    private static int FindTopLevelGenericOpen(string typeName)
    {
        for (int i = 0; i < typeName.Length; i++)
        {
            if (typeName[i] == '<') return i;
        }
        return -1;
    }
}
