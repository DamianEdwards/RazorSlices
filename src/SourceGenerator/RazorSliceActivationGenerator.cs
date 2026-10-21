using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RazorSlices.SourceGenerator;

internal static class RazorSliceActivationGenerator
{
    internal static readonly DiagnosticDescriptor RuntimeFallback = new(
        "RSG0003", "Runtime slice activation",
        "Slice activation will use runtime discovery: {0}", "RazorSlices", DiagnosticSeverity.Info, true);

    private static readonly SymbolDisplayFormat TypeFormat = SymbolDisplayFormat.FullyQualifiedFormat
        .WithMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers |
            SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier |
            SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    private static readonly HashSet<string> ExcludedServices =
    [
with(StringComparer.OrdinalIgnoreCase),         "IModelExpressionProvider", "IUrlHelper", "IViewComponentHelper", "IJsonHelper", "IHtmlHelper`1"
    ];

    internal static string? Generate(Compilation compilation, ResolvedDirectives directives,
        string rootNamespace, string implementation, out string? reason)
    {
        reason = null;
        if (directives.InheritsDirective is null)
        {
            reason = "the base type is not known";
            return null;
        }

        var declared = new Dictionary<string, PropertyDeclarationSyntax>(StringComparer.Ordinal);
        foreach (var injection in directives.Injections)
        {
            if (SyntaxFactory.ParseMemberDeclaration("public " + injection + " { get; set; }") is not PropertyDeclarationSyntax property ||
                property.ContainsDiagnostics)
            {
                reason = "an @inject directive could not be parsed";
                return null;
            }
            // The most specific directive with a given property name wins, as in the Razor SDK.
            declared[property.Identifier.ValueText] = property;
        }

        var nullable = compilation.Options is CSharpCompilationOptions options &&
            (options.NullableContextOptions & NullableContextOptions.Annotations) != 0;
        var probe = new StringBuilder(nullable ? "#nullable enable\n" : "#nullable disable\n");
        probe.AppendLine("using System; using System.Collections.Generic; using System.Linq; using System.Threading.Tasks;");
        probe.AppendLine("using global::" + rootNamespace + ";");
        foreach (var import in directives.UsingDirectives)
        {
            probe.Append("using ");
            if (import.Alias is not null) { probe.Append(import.Alias).Append(" = "); }
            probe.Append(import.NamespaceOrType).AppendLine(";");
        }
        probe.Append("namespace ").Append(directives.NamespaceDirective ?? "AspNetCoreGeneratedDocument")
            .AppendLine(" { class __RazorSlicesActivationTypes {");
        probe.Append("public ").Append(directives.InheritsDirective).AppendLine(" __Base;");
        var directiveProperties = declared.Values.ToArray();
        for (var i = 0; i < directiveProperties.Length; i++)
        {
            probe.Append("public ").Append(directiveProperties[i].Type).Append(" __Service").Append(i).AppendLine(";");
        }
        probe.AppendLine("} }");
        var parseOptions = compilation.SyntaxTrees.FirstOrDefault()?.Options as CSharpParseOptions
            ?? new CSharpParseOptions(((CSharpCompilation)compilation).LanguageVersion);
        var tree = CSharpSyntaxTree.ParseText(probe.ToString(), parseOptions);
        var model = compilation.AddSyntaxTrees(tree).GetSemanticModel(tree);
        var fields = tree.GetRoot().DescendantNodes().OfType<FieldDeclarationSyntax>().ToArray();
        if (model.GetTypeInfo(fields[0].Declaration.Type).Type is not INamedTypeSymbol baseType ||
            !DerivesFromSlice(baseType))
        {
            reason = "the base type cannot be resolved to RazorSlice";
            return null;
        }

        var injections = new List<Injection>();
        for (var i = 0; i < directiveProperties.Length; i++)
        {
            var type = (model.GetDeclaredSymbol(fields[i + 1].Declaration.Variables[0]) as IFieldSymbol)?.Type;
            if (type is not null && ExcludedServices.Contains(type.MetadataName)) { continue; }
            if (type is null || ContainsError(type))
            {
                reason = "an injected service type cannot be resolved";
                return null;
            }
            injections.Add(new(directiveProperties[i].Identifier.ValueText, type, implementation,
                optional: IsOptional(type, nullable), accessor: true));
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var current = baseType; current is not null; current = current.BaseType)
        {
            foreach (var property in current.GetMembers().OfType<IPropertySymbol>())
            {
                if (property.IsStatic || property.DeclaredAccessibility != Accessibility.Public) { continue; }
                var injected = property.GetAttributes().Any(static attribute =>
                    attribute.AttributeClass?.ToDisplayString() == "Microsoft.AspNetCore.Mvc.Razor.Internal.RazorInjectAttribute");
                if (!seen.Add(property.Name))
                {
                    if (injected) { reason = "an inherited injection is hidden or overridden"; return null; }
                    continue;
                }
                if (!injected || ExcludedServices.Contains(property.Type.MetadataName)) { continue; }
                if (declared.ContainsKey(property.Name) || property.IsIndexer || property.SetMethod is null ||
                    property.SetMethod.IsInitOnly || ContainsError(property.Type))
                {
                    reason = "an inherited injection requires runtime property resolution";
                    return null;
                }
                var direct = property.SetMethod.DeclaredAccessibility == Accessibility.Public ||
                    (property.SetMethod.DeclaredAccessibility is Accessibility.Internal or Accessibility.ProtectedOrInternal &&
                        SymbolEqualityComparer.Default.Equals(property.ContainingAssembly, model.Compilation.Assembly));
                if (!direct && property.ContainingType.IsGenericType)
                {
                    reason = "a non-public setter on a generic base requires runtime activation";
                    return null;
                }
                if (IsBound(property.Type) &&
                    property.GetMethod?.DeclaredAccessibility != Accessibility.Public)
                {
                    reason = "a non-public inherited service getter requires runtime activation";
                    return null;
                }
                var optional = IsOptional(property.Type, property.NullableAnnotation != NullableAnnotation.None);
                if (property.GetAttributes().Any(static attribute => attribute.AttributeClass?.Name == "AllowNullAttribute")) { optional = true; }
                if (property.GetAttributes().Any(static attribute => attribute.AttributeClass?.Name == "DisallowNullAttribute")) { optional = false; }
                injections.Add(new(property.Name, property.Type, property.ContainingType.ToDisplayString(TypeFormat), optional, !direct));
            }
        }

        if (injections.Any(static injection => injection.Bound && injection.Type.IsValueType))
        {
            reason = "a slice-bound service must be a reference type";
            return null;
        }
        if (injections.Any(static injection => injection.Accessor) &&
            compilation.GetTypeByMetadataName("System.Runtime.CompilerServices.UnsafeAccessorAttribute") is null)
        {
            reason = "UnsafeAccessor is unavailable for an injected property's setter";
            return null;
        }
        return Emit(injections, implementation);
    }

    private static string Emit(List<Injection> injections, string implementation)
    {
        var code = new StringBuilder();
        var bound = injections.Where(static injection => injection.Bound).ToArray();
        code.Append("    private static readonly global::RazorSlices.RazorSliceActivation<").Append(implementation)
            .Append("> __Activation = new(");
        if (injections.Count > 0) { code.Append("__Initialize"); }
        if (bound.Length > 0) { code.Append(", __Bind, __Unbind"); }
        code.AppendLine(");");
        if (injections.Count == 0) { return code.ToString(); }

        code.AppendLine("    private static void __Initialize(global::RazorSlices.RazorSlice instance, global::System.IServiceProvider? provider, global::Microsoft.AspNetCore.Http.HttpContext? context)");
        code.AppendLine("    {");
        code.Append("        var slice = (").Append(implementation).AppendLine(")instance;");
        code.Append("        var services = provider ?? context?.RequestServices ?? throw new global::System.InvalidOperationException(")
            .Append("\"Cannot initialize @inject properties of slice \" + nameof(").Append(implementation)
            .AppendLine(") + \" because the ServiceProvider property is null.\");");
        for (var i = 0; i < injections.Count; i++)
        {
            var injection = injections[i];
            var serviceType = injection.Type.WithNullableAnnotation(NullableAnnotation.NotAnnotated).ToDisplayString(TypeFormat);
            var resolve = "global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions." +
                (injection.Optional ? "GetService" : "GetRequiredService") + "<" + serviceType + ">(services)";
            code.Append("        ");
            if (injection.Accessor) { code.Append("__Set").Append(i).Append("(slice, ").Append(resolve).AppendLine(");"); }
            else { code.Append("slice.@").Append(injection.Name).Append(" = ").Append(resolve).AppendLine(";"); }
        }
        code.AppendLine("    }");
        for (var i = 0; i < injections.Count; i++)
        {
            var injection = injections[i];
            if (!injection.Accessor) { continue; }
            code.AppendLine("    [global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Method, Name = " +
                Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral("set_" + injection.Name, true) + ")]");
            code.Append("    private static extern void __Set").Append(i).Append('(').Append(injection.Owner)
                .Append(" slice, ").Append((injection.Optional && injection.Type.IsReferenceType
                    ? injection.Type.WithNullableAnnotation(NullableAnnotation.Annotated) : injection.Type)
                    .ToDisplayString(TypeFormat)).AppendLine(" value);");
        }
        if (bound.Length == 0) { return code.ToString(); }

        code.AppendLine("    private static void __Bind(global::RazorSlices.RazorSlice instance)");
        code.AppendLine("    {");
        code.Append("        var slice = (").Append(implementation).AppendLine(")instance;");
        if (bound.Length > 1)
        {
            code.AppendLine("        var completed = 0;");
            code.AppendLine("        try");
            code.AppendLine("        {");
        }
        for (var i = 0; i < bound.Length; i++)
        {
            code.Append(bound.Length > 1 ? "            " : "        ")
                .Append("((global::RazorSlices.IRazorSliceBindable?)slice.@").Append(bound[i].Name)
                .AppendLine(")?.Bind(slice);");
            if (bound.Length > 1) { code.Append("            completed = ").Append(i + 1).AppendLine(";"); }
        }
        if (bound.Length > 1)
        {
            code.AppendLine("        }");
            code.AppendLine("        catch (global::System.Exception error)");
            code.AppendLine("        {");
            code.AppendLine("            var cleanup = __UnbindCompleted(slice, completed);");
            code.AppendLine("            if (cleanup is not null)");
            code.AppendLine("            {");
            code.AppendLine("                throw new global::System.AggregateException(error, cleanup);");
            code.AppendLine("            }");
            code.AppendLine("            throw;");
            code.AppendLine("        }");
        }
        code.AppendLine("    }");
        code.AppendLine("    private static global::System.Exception? __Unbind(global::RazorSlices.RazorSlice instance)");
        code.AppendLine("    {");
        code.Append("        return __UnbindCompleted((").Append(implementation).Append(")instance, ").Append(bound.Length).AppendLine(");");
        code.AppendLine("    }");
        code.Append("    private static global::System.Exception? __UnbindCompleted(").Append(implementation).AppendLine(" slice, int completed)");
        code.AppendLine("    {");
        code.AppendLine("        global::System.Exception? failure = null;");
        for (var i = bound.Length - 1; i >= 0; i--)
        {
            code.Append("        if (completed > ").Append(i).AppendLine(")");
            code.AppendLine("        {");
            code.AppendLine("            try");
            code.AppendLine("            {");
            code.Append("                ((global::RazorSlices.IRazorSliceBindable?)slice.@").Append(bound[i].Name)
                .AppendLine(")?.Unbind(slice);");
            code.AppendLine("            }");
            code.AppendLine("            catch (global::System.Exception error)");
            code.AppendLine("            {");
            code.AppendLine("                failure = failure is null ? error : new global::System.AggregateException(failure, error);");
            code.AppendLine("            }");
            code.AppendLine("        }");
        }
        code.AppendLine("        return failure;");
        code.AppendLine("    }");
        return code.ToString();
    }

    private static bool DerivesFromSlice(INamedTypeSymbol? type)
    {
        for (; type is not null; type = type.BaseType)
        {
            if (type.Name == "RazorSlice" && type.ContainingNamespace.ToDisplayString() == "RazorSlices") { return true; }
        }
        return false;
    }

    private static bool ContainsError(ITypeSymbol type)
    {
        return type.TypeKind is TypeKind.Error or TypeKind.Dynamic or TypeKind.Pointer or TypeKind.FunctionPointer ||
            type.IsRefLikeType ||
            type is INamedTypeSymbol named && named.TypeArguments.Any(ContainsError) ||
            type is IArrayTypeSymbol array && ContainsError(array.ElementType);
    }

    private static bool IsOptional(ITypeSymbol type, bool annotations)
    {
        return type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T ||
            type.IsReferenceType && (!annotations || type.NullableAnnotation == NullableAnnotation.Annotated);
    }

    private static bool IsBound(ITypeSymbol type)
    {
        return type is INamedTypeSymbol named &&
            (named.ToDisplayString() == "RazorSlices.IRazorSliceBindable" ||
             named.AllInterfaces.Any(static contract => contract.ToDisplayString() == "RazorSlices.IRazorSliceBindable"));
    }

    private sealed class Injection(string name, ITypeSymbol type, string owner, bool optional, bool accessor)
    {
        internal string Name { get; } = name;
        internal ITypeSymbol Type { get; } = type;
        internal string Owner { get; } = owner;
        internal bool Optional { get; } = optional;
        internal bool Accessor { get; } = accessor;
        internal bool Bound { get; } = IsBound(type);
    }
}
