using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace RazorSlices.SourceGenerator;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class RazorSliceHelperAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Unsupported = new(
        "RSG0101", "Helpers must be stateless", "{0}", "RazorSlices", DiagnosticSeverity.Error, true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Unsupported);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.RegisterCompilationStartAction(start =>
        {
            var helperBase = start.Compilation.GetTypeByMetadataName("RazorSlices.RazorSliceHelpers");
            if (helperBase is null)
            {
                return;
            }

            bool IsHelper(INamedTypeSymbol? type)
            {
                for (; type is not null; type = type.BaseType)
                {
                    if (SymbolEqualityComparer.Default.Equals(type, helperBase))
                    {
                        return true;
                    }
                }
                return false;
            }

            start.RegisterSymbolAction(symbolContext =>
            {
                var type = (INamedTypeSymbol)symbolContext.Symbol;
                if (SymbolEqualityComparer.Default.Equals(type, helperBase) || !IsHelper(type))
                {
                    return;
                }

                foreach (var member in type.GetMembers())
                {
                    if (member.IsStatic || member.IsImplicitlyDeclared)
                    {
                        continue;
                    }

                    var authored = member.Locations.FirstOrDefault(static location => location.IsInSource);
                    if (member is IPropertySymbol property && HasInjection(property) &&
                        !member.Locations.Any(IsAuthored))
                    {
                        continue;
                    }

                    if (authored is not null &&
                        (member is IFieldSymbol or IPropertySymbol or IEventSymbol ||
                         member is IMethodSymbol { MethodKind: MethodKind.Constructor or MethodKind.Destructor }))
                    {
                        symbolContext.ReportDiagnostic(Diagnostic.Create(Unsupported, authored,
                            $"Helper member '{member.Name}' introduces instance state; use arguments or static utilities instead"));
                    }
                }
                foreach (var method in type.GetMembers("ExecuteAsync").OfType<IMethodSymbol>())
                {
                    foreach (var reference in method.DeclaringSyntaxReferences)
                    {
                        if (reference.GetSyntax(symbolContext.CancellationToken) is MethodDeclarationSyntax { Body: { } body } &&
                            body.Statements.Any(statement => !IsWhitespace(statement)))
                        {
                            symbolContext.ReportDiagnostic(Diagnostic.Create(Unsupported, method.Locations[0],
                                "A helper cannot have a top-level slice body; put markup in its interface methods"));
                        }
                    }
                }
            }, SymbolKind.NamedType);

            start.RegisterOperationAction(operationContext =>
            {
                if (!IsHelper(operationContext.ContainingSymbol.ContainingType) ||
                    SymbolEqualityComparer.Default.Equals(operationContext.ContainingSymbol.ContainingType, helperBase))
                {
                    return;
                }

                var operation = operationContext.Operation;
                if (!operation.Syntax.GetLocation().IsInSource)
                {
                    return;
                }

                var forbidden = operation switch
                {
                    IInstanceReferenceOperation instance => !instance.IsImplicit,
                    IMethodReferenceOperation method => method.Instance is not null && IsHelper(method.Method.ContainingType),
                    IPropertyReferenceOperation property => HasInjection(property.Property),
                    _ => false
                };
                if (forbidden)
                {
                    operationContext.ReportDiagnostic(Diagnostic.Create(Unsupported, operation.Syntax.GetLocation(),
                        "Helpers cannot use injected members or retain/escape their instance"));
                }
            }, OperationKind.InstanceReference, OperationKind.MethodReference, OperationKind.PropertyReference);
        });
    }

    private static bool IsAuthored(Location location)
    {
        return location.IsInSource && location.GetMappedLineSpan().HasMappedPath &&
        location.GetMappedLineSpan().Path.EndsWith(".cshtml", System.StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasInjection(IPropertySymbol property)
    {
        return property.GetAttributes().Any(static attribute => attribute.AttributeClass?.Name == "RazorInjectAttribute");
    }

    private static bool IsWhitespace(StatementSyntax statement)
    {
        return statement is ExpressionStatementSyntax { Expression: InvocationExpressionSyntax invocation } &&
            invocation.Expression is IdentifierNameSyntax { Identifier.ValueText: "WriteLiteral" } &&
            invocation.ArgumentList.Arguments.Count == 1 &&
            invocation.ArgumentList.Arguments[0].Expression is LiteralExpressionSyntax literal && string.IsNullOrWhiteSpace(literal.Token.ValueText);
    }
}
