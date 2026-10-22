using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace RazorSlices.SourceGenerator;

internal static class RazorSliceHelperGenerator
{
    internal static readonly DiagnosticDescriptor InvalidContract = new(
        "RSG0100", "Invalid helper contract", "{0}", "RazorSlices", DiagnosticSeverity.Error, true);

    private static readonly SymbolDisplayFormat TypeFormat = SymbolDisplayFormat.FullyQualifiedFormat
        .WithMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers |
            SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier |
            SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    internal static bool IsHelper(ResolvedDirectives directives, Compilation compilation, string rootNamespace)
    {
        if (directives.InheritsDirective is null)
        {
            return false;
        }
        if (directives.InheritsDirective is "RazorSliceHelpers" or "RazorSlices.RazorSliceHelpers" or "global::RazorSlices.RazorSliceHelpers")
        {
            return true;
        }
        var type = ModelTypeResolver.ResolveTypeSymbolExpression(directives.InheritsDirective,
            directives.UsingDirectives, compilation, rootNamespace) as INamedTypeSymbol;
        for (; type is not null; type = type.BaseType)
        {
            if (type.ToDisplayString() == "RazorSlices.RazorSliceHelpers")
            {
                return true;
            }
        }
        return false;
    }

    internal static void Generate(SourceProductionContext context, AdditionalText file,
        ResolvedDirectives directives, Compilation compilation, string fullNamespace,
        string name, string implementation, string rootNamespace)
    {
        var location = Location.Create(file.Path, default, default);
        if ((int)((CSharpCompilation)compilation).LanguageVersion < 1400)
        {
            Error("Generated helper extension properties require C# 14 or newer; set LangVersion to 14.0 or newer");
            return;
        }
        var interfaceNames = directives.Interfaces.Distinct(StringComparer.Ordinal).ToArray();
        if (interfaceNames.Length != 1 ||
            ModelTypeResolver.ResolveTypeSymbolExpression(interfaceNames[0], directives.UsingDirectives,
                compilation, rootNamespace) is not INamedTypeSymbol contract ||
            contract.TypeKind != TypeKind.Interface || !IsPublic(contract))
        {
            Error("A helper must declare exactly one public @implements export interface");
            return;
        }

        var methods = new List<IMethodSymbol>();
        var signatures = new Dictionary<string, IMethodSymbol>(StringComparer.Ordinal);
        foreach (var type in new[] { contract }.Concat(contract.AllInterfaces))
        {
            foreach (var member in type.GetMembers())
            {
                if (member is not IMethodSymbol method || method.MethodKind != MethodKind.Ordinary ||
                    method.IsStatic || method.DeclaredAccessibility != Accessibility.Public ||
                    method.ReturnsByRef || method.ReturnsByRefReadonly || !SupportedReturn(method.ReturnType) ||
                    (method.Name == "ExecuteAsync" && method.Parameters.Length == 0))
                {
                    Error($"Helper contract member '{member.Name}' must be an instance method returning void, Task, or ValueTask; ExecuteAsync is reserved");
                    return;
                }
                var key = method.Name + "`" + method.Arity + "(" +
                    string.Join(",", method.Parameters.Select(static parameter => parameter.RefKind + ":" + SignatureType(parameter.Type))) + ")";
                if (signatures.TryGetValue(key, out var previous))
                {
                    if (!SymbolEqualityComparer.Default.Equals(previous.ReturnType, method.ReturnType))
                    {
                        Error($"Inherited helper method '{method.Name}' has conflicting return types");
                        return;
                    }
                    if (!SymbolEqualityComparer.Default.Equals(previous.ContainingType, contract) &&
                        !SymbolEqualityComparer.Default.Equals(previous.ContainingType, method.ContainingType))
                    {
                        Error($"Inherited helper method '{method.Name}' is ambiguous; redeclare it on the export interface");
                        return;
                    }
                    continue;
                }
                signatures.Add(key, method);
                methods.Add(method);
            }
        }
        var contractName = contract.ToDisplayString(TypeFormat);
        var renderer = name + "Renderer";
        while (methods.Any(method => method.Name == renderer))
        {
            renderer += "_";
        }

        var code = new StringBuilder();
        code.AppendLine("// <auto-generated/>");
        code.AppendLine("#nullable enable");
        code.AppendLine("#pragma warning disable CS1591");
        code.AppendLine($"namespace {fullNamespace};");
        code.AppendLine();
        code.AppendLine($"public readonly struct {renderer}");
        code.AppendLine("{");
        code.AppendLine($"    private readonly {contractName} _helper;");
        code.AppendLine();
        code.AppendLine($"    internal {renderer}({contractName} helper)");
        code.AppendLine("    {");
        code.AppendLine("        _helper = helper;");
        code.AppendLine("    }");
        foreach (var method in methods)
        {
            var parameters = string.Join(", ", method.Parameters.Select(Parameter));
            var generic = method.TypeParameters.Length == 0 ? "" : "<" +
                string.Join(", ", method.TypeParameters.Select(static parameter => "@" + parameter.Name)) + ">";
            var returnType = method.ReturnsVoid ? "global::Microsoft.AspNetCore.Html.HtmlString" : "global::RazorSlices.HelperCompletion";
            code.AppendLine();
            code.AppendLine($"    public {returnType} @{method.Name}{generic}({parameters})");
            foreach (var parameter in method.TypeParameters)
            {
                var constraints = new List<string>();
                if (parameter.HasUnmanagedTypeConstraint)
                {
                    constraints.Add("unmanaged");
                }
                else if (parameter.HasValueTypeConstraint)
                {
                    constraints.Add("struct");
                }
                else if (parameter.HasReferenceTypeConstraint)
                {
                    constraints.Add(parameter.ReferenceTypeConstraintNullableAnnotation == NullableAnnotation.Annotated ? "class?" : "class");
                }
                else if (parameter.HasNotNullConstraint)
                {
                    constraints.Add("notnull");
                }

                constraints.AddRange(parameter.ConstraintTypes.Select(static type => type.ToDisplayString(TypeFormat)));
                if (parameter.HasConstructorConstraint && !parameter.HasValueTypeConstraint)
                {
                    constraints.Add("new()");
                }

                if (constraints.Count != 0)
                {
                    code.AppendLine($"        where @{parameter.Name} : {string.Join(", ", constraints)}");
                }
            }
            code.AppendLine("    {");
            var arguments = string.Join(", ", method.Parameters.Select(static parameter => RefPrefix(parameter.RefKind, argument: true) + "@" + parameter.Name));
            var call = $"(({method.ContainingType.ToDisplayString(TypeFormat)})this._helper).@{method.Name}{generic}({arguments})";
            if (method.ReturnsVoid)
            {
                code.AppendLine($"        {call};");
                code.AppendLine("        return global::Microsoft.AspNetCore.Html.HtmlString.Empty;");
            }
            else
            {
                code.AppendLine($"        return new global::RazorSlices.HelperCompletion({call});");
            }
            code.AppendLine("    }");
        }
        code.AppendLine("}");
        context.AddSource($"{fullNamespace}.{name}.HelperRenderer.g.cs", code.ToString());

        var accessor = $$"""
            // <auto-generated/>
            #nullable enable
            #pragma warning disable CS1591
            namespace {{fullNamespace}};

            public static class {{name}}HelperExtensions
            {
                extension(global::RazorSlices.IHelperContext context)
                {
                    public {{renderer}} {{name}} =>
                        new(context.GetHelper<{{contractName}}, {{implementation}}>(
                            static () => new {{implementation}}()));
                }
            }
            """;
        context.AddSource($"{fullNamespace}.{name}.HelperAccessor.g.cs", accessor);

        void Error(string message)
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidContract, location, message));
        }
    }

    private static bool IsPublic(INamedTypeSymbol type)
    {
        for (var current = type; current is not null; current = current.ContainingType)
        {
            if (current.DeclaredAccessibility != Accessibility.Public)
            {
                return false;
            }
        }

        return true;
    }

    private static string SignatureType(ITypeSymbol type)
    {
        return type switch
        {
            ITypeParameterSymbol { TypeParameterKind: TypeParameterKind.Method } parameter => "!!" + parameter.Ordinal,
            IArrayTypeSymbol array => SignatureType(array.ElementType) + "[" + array.Rank + "]",
            INamedTypeSymbol { IsGenericType: true } named => named.OriginalDefinition.ToDisplayString() +
                "<" + string.Join(",", named.TypeArguments.Select(SignatureType)) + ">",
            _ => type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
        };
    }

    private static bool SupportedReturn(ITypeSymbol type)
    {
        return type.SpecialType == SpecialType.System_Void ||
        type is INamedTypeSymbol { Arity: 0 } named &&
        named.ContainingNamespace.ToDisplayString() == "System.Threading.Tasks" &&
        named.Name is "Task" or "ValueTask";
    }

    private static string Parameter(IParameterSymbol parameter)
    {
        var attributes = new StringBuilder();
        foreach (var attribute in parameter.GetAttributes())
        {
            var name = attribute.AttributeClass?.ToDisplayString();
            if (name is "System.Runtime.CompilerServices.CallerArgumentExpressionAttribute" or
                "System.Runtime.CompilerServices.CallerMemberNameAttribute" or
                "System.Runtime.CompilerServices.CallerFilePathAttribute" or
                "System.Runtime.CompilerServices.CallerLineNumberAttribute")
            {
                attributes.Append("[global::").Append(name).Append('(')
                    .Append(string.Join(", ", attribute.ConstructorArguments.Select(static value =>
                        Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral((string)value.Value!, quote: true))))
                    .Append(")] ");
            }
        }
        var prefix = parameter.IsParams ? "params " : "";
        if (parameter.ScopedKind != ScopedKind.None)
        {
            prefix += "scoped ";
        }

        prefix += RefPrefix(parameter.RefKind, argument: false);
        var result = attributes + prefix + parameter.Type.ToDisplayString(TypeFormat) + " @" + parameter.Name;
        if (parameter.HasExplicitDefaultValue)
        {
            result += " = " + DefaultValue(parameter);
        }

        return result;
    }

    private static string RefPrefix(RefKind kind, bool argument)
    {
        return kind switch
        {
            RefKind.Ref => "ref ",
            RefKind.Out => "out ",
            RefKind.In => "in ",
            RefKind.RefReadOnlyParameter => argument ? "in " : "ref readonly ",
            _ => ""
        };
    }

    private static string DefaultValue(IParameterSymbol parameter)
    {
        var value = parameter.ExplicitDefaultValue;
        if (value is null)
        {
            return parameter.Type.TypeKind == TypeKind.TypeParameter
                ? "default!"
                : parameter.Type.IsValueType && parameter.Type.OriginalDefinition.SpecialType != SpecialType.System_Nullable_T
                ? "default"
                : parameter.NullableAnnotation == NullableAnnotation.Annotated ? "null" : "null!";
        }
        var literal = value switch
        {
            string text => Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(text, quote: true),
            char character => Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(character, quote: true),
            bool boolean => boolean ? "true" : "false",
            float number when float.IsNaN(number) => "global::System.Single.NaN",
            float number when float.IsPositiveInfinity(number) => "global::System.Single.PositiveInfinity",
            float number when float.IsNegativeInfinity(number) => "global::System.Single.NegativeInfinity",
            double number when double.IsNaN(number) => "global::System.Double.NaN",
            double number when double.IsPositiveInfinity(number) => "global::System.Double.PositiveInfinity",
            double number when double.IsNegativeInfinity(number) => "global::System.Double.NegativeInfinity",
            float number => number.ToString("R", CultureInfo.InvariantCulture) + "F",
            double number => number.ToString("R", CultureInfo.InvariantCulture) + "D",
            decimal number => number.ToString(CultureInfo.InvariantCulture) + "M",
            long number => number.ToString(CultureInfo.InvariantCulture) + "L",
            ulong number => number.ToString(CultureInfo.InvariantCulture) + "UL",
            uint number => number.ToString(CultureInfo.InvariantCulture) + "U",
            _ => Convert.ToString(value, CultureInfo.InvariantCulture)!
        };
        return parameter.Type.TypeKind == TypeKind.Enum ? $"({parameter.Type.ToDisplayString(TypeFormat)}){literal}" : literal;
    }
}
