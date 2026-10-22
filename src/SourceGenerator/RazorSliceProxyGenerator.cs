using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace RazorSlices.SourceGenerator;

[Generator]
internal class RazorSliceProxyGenerator : IIncrementalGenerator
{
    internal static readonly DiagnosticDescriptor UnsupportedMinimalFeature = new(
        "RSG0004", "Unsupported minimal-slice feature",
        "Minimal slices do not support {0}; inherit RazorSlice instead", "RazorSlices", DiagnosticSeverity.Error, true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var assemblyName = context.CompilationProvider.Select(static (compilation, _) => compilation.AssemblyName);

        var rootNamespace = context.AnalyzerConfigOptionsProvider.Select(static (options, _) =>
            options.GlobalOptions.TryGetValue("build_property.RootNamespace", out var rootNamespace) ? rootNamespace : null);

        var projectDirectory = context.AnalyzerConfigOptionsProvider.Select(static (options, _) =>
            options.GlobalOptions.TryGetValue("build_property.MSBuildProjectDirectory", out var projectDir) ? projectDir : null);

        var useRecords = context.AnalyzerConfigOptionsProvider.Select(static (options, _) =>
            options.GlobalOptions.TryGetValue("build_property.RazorSliceProxiesAsRecords",
                out var recordSliceProxiesValue)
            && bool.TryParse(recordSliceProxiesValue, out var result) && result);

        // (Left.Left          , (Left.Right.Left.Left     , (Left.Right.Right.Left, Left.Right.Right.Right)))
        // (string assemblyName, (string rootNamespace     , (string projectDirectory, bool useRecords)))
        var projectInfo = assemblyName.Combine(rootNamespace.Combine(projectDirectory.Combine(useRecords)));

        // Collect all .cshtml files (both slices and _ViewImports)
        var allCshtmlFiles = context.AdditionalTextsProvider
            .Where(text => text.Path.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase));

        // Filter to only slice files (GenerateRazorSlice=true)
        var sliceTexts = allCshtmlFiles
            .Combine(context.AnalyzerConfigOptionsProvider)
            .Select((pair, _) =>
            {
                var (additionalText, optionsProvider) = pair;
                var textOptions = optionsProvider.GetOptions(additionalText);
                var generateSlice = textOptions.TryGetValue("build_metadata.RazorGenerate.GenerateRazorSlice", out var generateRazorSliceValue)
                                        && bool.TryParse(generateRazorSliceValue, out var result) && result;

                return (additionalText, generateSlice);
            })
            .Where(file => file.generateSlice)
            .Select((file, _) => file.additionalText);

        // Combine: projectInfo + sliceTexts + allCshtmlFiles (for _ViewImports) + compilation
        // NOTE: Combining with CompilationProvider means Execute re-runs on every C# source change.
        // A future optimization could separate .cshtml parsing into an earlier equatable pipeline
        // stage, so only type resolution runs when the compilation changes.
        var combined = projectInfo
            .Combine(sliceTexts.Collect())
            .Combine(allCshtmlFiles.Collect())
            .Combine(context.CompilationProvider);

        context.RegisterSourceOutput(combined, static (spc, pair) =>
        {
            var projectInfo = pair.Left.Left.Left;
            var sliceTexts = pair.Left.Left.Right;
            var allCshtmlFiles = pair.Left.Right;
            var compilation = pair.Right;

            Execute(spc,
                projectInfo.Left,
                projectInfo.Right.Left,
                projectInfo.Right.Right.Left,
                projectInfo.Right.Right.Right,
                sliceTexts,
                allCshtmlFiles,
                compilation);
        });
    }

    private static void Execute(
        SourceProductionContext context,
        string? assemblyName,
        string? rootNamespace,
        string? projectDirectory,
        bool useRecords,
        ImmutableArray<AdditionalText> sliceTexts,
        ImmutableArray<AdditionalText> allCshtmlFiles,
        Compilation compilation)
    {
        if (string.IsNullOrEmpty(rootNamespace) || string.IsNullOrEmpty(projectDirectory))
        {
            // Need to have a root namespace and project directory to generate the code
            return;
        }

        var distinctTexts = allCshtmlFiles.Where(static file =>
            !string.Equals(Path.GetFileName(file.Path), "_ViewImports.cshtml", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(Path.GetFileName(file.Path), "_ViewStart.cshtml", StringComparison.OrdinalIgnoreCase)).Distinct();

        if (!distinctTexts.Any())
        {
            // Nothing to do yet
            return;
        }

        // Build _ViewImports map once for all slices
        var viewImportsMap = ViewImportsResolver.BuildViewImportsMap(allCshtmlFiles);

        HashSet<string> generatedClasses = [];
        var generatedProxy = false;

        var codeBuilder = new StringBuilder();

        codeBuilder.AppendLine("// <auto-generated/>");
        codeBuilder.AppendLine();
        codeBuilder.AppendLine("using global::RazorSlices;");
        codeBuilder.AppendLine();
        codeBuilder.AppendLine("#nullable enable");
        codeBuilder.AppendLine();

        foreach (var file in distinctTexts)
        {
            var fileName = Path.GetFileNameWithoutExtension(file.Path);
            var directory = Path.GetDirectoryName(file.Path);
            var relativeFilePath = PathUtils.GetRelativePath(projectDirectory!, file.Path);
            var relativeDirectoryPath = PathUtils.GetRelativePath(projectDirectory!, directory);
            var subNamespace = relativeDirectoryPath == "."
                ? ""
                : relativeDirectoryPath.Replace(Path.DirectorySeparatorChar, '.');

            var className = fileName;

            if (!CSharpHelpers.IsValidTypeName(className))
            {
                className = CSharpHelpers.CreateValidTypeName(className);
            }

            if (!string.IsNullOrEmpty(subNamespace) && !CSharpHelpers.IsValidNamespace(subNamespace))
            {
                subNamespace = CSharpHelpers.CreateValidNamespace(subNamespace);
            }

            var subNamespaceAsClassName = subNamespace.Replace('.', '_');
            var fullNamespace = string.IsNullOrEmpty(subNamespace)
                ? rootNamespace
                : $"{rootNamespace}.{subNamespace}";

            var helperSource = file.GetText(context.CancellationToken);
            if (helperSource is not null)
            {
                var helperDirectives = ViewImportsResolver.ResolveDirectives(file.Path, projectDirectory!, viewImportsMap, helperSource);
                if (RazorSliceHelperGenerator.IsHelper(helperDirectives, compilation, rootNamespace!))
                {
                    if (!generatedClasses.Add($"{fullNamespace}.{className}"))
                    {
                        context.ReportDiagnostic(Diagnostic.Create(RazorSliceHelperGenerator.InvalidContract,
                            Location.Create(file.Path, default, default), $"Duplicate helper/proxy name '{fullNamespace}.{className}'"));
                        continue;
                    }
                    var helperType = string.IsNullOrEmpty(subNamespaceAsClassName) ? className : $"{subNamespaceAsClassName}_{className}";
                    RazorSliceHelperGenerator.Generate(context, file, helperDirectives, compilation,
                        fullNamespace!, className, $"global::{helperDirectives.NamespaceDirective ?? "AspNetCoreGeneratedDocument"}.{helperType}",
                        rootNamespace!);
                    continue;
                }
            }
            if (!sliceTexts.Contains(file))
            {
                continue;
            }

            // Duplicate class name check
            if (generatedClasses.Contains($"{fullNamespace}.{className}"))
            {
                // TODO: Add an integer suffix to the class name so it can be generated and then log a warning?
                var descriptor = new DiagnosticDescriptor(
                    "RSG0001",
                    "Duplicate Class Name",
                    $"Generated class with name {className} already exists. File '{relativeFilePath}' has been ignored.",
                    "Naming",
                    DiagnosticSeverity.Warning,
                    true);

                context.ReportDiagnostic(Diagnostic.Create(descriptor, Location.None));
            }
            else
            {
                generatedClasses.Add($"{fullNamespace}.{className}");
                generatedProxy = true;

                // Resolve directives (including _ViewImports hierarchy)
                var sourceText = file.GetText();
                string? resolvedModelType = null;
                bool hasModel = false;

                var templateNameSpace = "AspNetCoreGeneratedDocument";
                ResolvedDirectives? resolvedDirectives = null;

                if (sourceText is not null)
                {
                    var directives = ViewImportsResolver.ResolveDirectives(file.Path, projectDirectory!, viewImportsMap, sourceText);
                    resolvedDirectives = directives;

                    if (directives.InheritsDirective is not null)
                    {
                        var inheritsBaseTypeName = RazorDirectiveParser.ExtractBaseTypeName(directives.InheritsDirective);
                        if (IsDirectRazorSliceBaseType(inheritsBaseTypeName))
                        {
                            var modelTypeName = RazorDirectiveParser.ExtractModelType(directives.InheritsDirective);
                            if (modelTypeName is not null)
                            {
                                resolvedModelType = ModelTypeResolver.ResolveModelType(
                                    modelTypeName, directives.UsingDirectives, compilation, rootNamespace);

                                if (resolvedModelType is not null)
                                {
                                    hasModel = true;
                                }
                                else
                                {
                                    // Report diagnostic for unresolvable model type
                                    var descriptor = new DiagnosticDescriptor(
                                        "RSG0002",
                                        "Unresolvable Model Type",
                                        $"Could not resolve model type '{modelTypeName}' for slice '{relativeFilePath}'. The generated proxy will not have a strongly-typed Create method.",
                                        "TypeResolution",
                                        DiagnosticSeverity.Warning,
                                        true);
                                    context.ReportDiagnostic(Diagnostic.Create(descriptor, Location.None));
                                }
                            }
                        }
                        else
                        {
                            resolvedModelType = ModelTypeResolver.ResolveModelTypeFromSliceBaseType(
                                directives.InheritsDirective, directives.UsingDirectives, compilation, rootNamespace);

                            if (resolvedModelType is not null)
                            {
                                hasModel = true;
                            }
                        }
                    }

                    if (directives.NamespaceDirective is not null)
                    {
                        templateNameSpace = directives.NamespaceDirective;
                    }
                }

                var generatedTypeName = string.IsNullOrEmpty(subNamespaceAsClassName)
                    ? className
                    : $"{subNamespaceAsClassName}_{className}";

                var sliceTypeDeclaration = useRecords ? "record " : "class ";
                var genericParameter = hasModel && resolvedModelType is not null ? $"<{resolvedModelType}>" : "";

                var fullyQualifiedGeneratedTypeName = $"global::{templateNameSpace}.{generatedTypeName}";
                var minimal = resolvedDirectives is { } effective && IsMinimalSlice(effective, compilation, rootNamespace!);
                if (minimal && resolvedDirectives is { } minimalDirectives &&
                    GetUnsupportedMinimalFeature(minimalDirectives, compilation, rootNamespace!) is { } unsupported)
                {
                    context.ReportDiagnostic(Diagnostic.Create(UnsupportedMinimalFeature, Location.Create(file.Path, default, default), unsupported));
                    continue;
                }
                var sliceBase = minimal ? "RazorSliceBase" : "RazorSlice";
                var proxy = minimal ? "IRazorSliceBaseProxy" : "IRazorSliceProxy";
                string? activation = null;
                if (!minimal && resolvedDirectives is { } resolved)
                {
                    activation = RazorSliceActivationGenerator.Generate(compilation, resolved, rootNamespace!,
                        fullyQualifiedGeneratedTypeName, out var reason);
                    if (activation is null)
                    {
                        context.ReportDiagnostic(Diagnostic.Create(RazorSliceActivationGenerator.RuntimeFallback,
                            Location.Create(file.Path, default, default), reason));
                    }
                }
                var activationArgument = activation is null ? "" : ", __Activation";

                codeBuilder.AppendLine($$"""
                    namespace {{fullNamespace}}
                    {
                    """);

                codeBuilder.AppendLine($$"""
                        /// <summary>
                        /// Static proxy for the Razor Slice defined in <c>{{relativeFilePath}}</c>.
                        /// </summary>
                        public sealed partial {{sliceTypeDeclaration}}{{className}} : global::RazorSlices.{{proxy}}{{genericParameter}}
                        {
                    """);

                if (hasModel && resolvedModelType is not null)
                {
                    codeBuilder.AppendLine($$"""
                            /// <summary>
                            /// Creates a new instance of the Razor Slice defined in <c>{{relativeFilePath}}</c> with the given model.
                            /// </summary>
                            public static global::RazorSlices.{{sliceBase}}<{{resolvedModelType}}> Create({{resolvedModelType}} model) =>
                                global::RazorSlices.{{sliceBase}}Factory.Create(model, static model => new {{fullyQualifiedGeneratedTypeName}} { Model = model }{{activationArgument}});

                            // Explicit interface implementation, workaround for https://github.com/dotnet/runtime/issues/102796
                            static global::RazorSlices.{{sliceBase}}<{{resolvedModelType}}> global::RazorSlices.{{proxy}}<{{resolvedModelType}}>.CreateSlice({{resolvedModelType}} model) => Create(model);
                    """);
                }
                else
                {
                    codeBuilder.AppendLine($$"""
                            /// <summary>
                            /// Creates a new instance of the Razor Slice defined in <c>{{relativeFilePath}}</c>.
                            /// </summary>
                            public static global::RazorSlices.{{sliceBase}} Create() =>
                                global::RazorSlices.{{sliceBase}}Factory.Create(static () => new {{fullyQualifiedGeneratedTypeName}}(){{activationArgument}});

                            // Explicit interface implementation, workaround for https://github.com/dotnet/runtime/issues/102796
                            static global::RazorSlices.{{sliceBase}} global::RazorSlices.{{proxy}}.CreateSlice() => Create();
                    """);
                }

                if (activation is not null)
                {
                    codeBuilder.AppendLine(activation);
                }

                // Close class & namespace
                codeBuilder.AppendLine("""
                        }
                    }
                    """);

                codeBuilder.AppendLine();
            }
        }

        if (generatedProxy)
        {
            context.AddSource($"{rootNamespace}.RazorSliceProxies.g.cs", SourceText.From(codeBuilder.ToString(), Encoding.UTF8));
        }
    }

    private static bool IsDirectRazorSliceBaseType(string baseTypeName)
    {
        return string.Equals(baseTypeName, "RazorSlice", StringComparison.Ordinal) ||
        string.Equals(baseTypeName, "RazorSlices.RazorSlice", StringComparison.Ordinal) ||
        string.Equals(baseTypeName, "global::RazorSlices.RazorSlice", StringComparison.Ordinal) ||
        string.Equals(baseTypeName, "RazorSliceBase", StringComparison.Ordinal) ||
        string.Equals(baseTypeName, "RazorSlices.RazorSliceBase", StringComparison.Ordinal) ||
        string.Equals(baseTypeName, "global::RazorSlices.RazorSliceBase", StringComparison.Ordinal);
    }

    private static bool IsMinimalSlice(ResolvedDirectives directives, Compilation compilation, string rootNamespace)
    {
        if (directives.InheritsDirective is not { } inherits) { return false; }
        var name = RazorDirectiveParser.ExtractBaseTypeName(inherits);
        if (name is "RazorSliceBase" or "RazorSlices.RazorSliceBase" or "global::RazorSlices.RazorSliceBase") { return true; }
        var type = ModelTypeResolver.ResolveTypeSymbolExpression(inherits, directives.UsingDirectives, compilation, rootNamespace) as INamedTypeSymbol;
        for (; type is not null; type = type.BaseType)
        {
            if (type.ContainingNamespace.ToDisplayString() != "RazorSlices") { continue; }
            if (type.Name == "RazorSlice") { return false; }
            if (type.Name == "RazorSliceBase") { return true; }
        }
        return false;
    }

    private static string? GetUnsupportedMinimalFeature(ResolvedDirectives directives, Compilation compilation, string rootNamespace)
    {
        if (directives.Injections.Count > 0) { return "@inject"; }
        var type = ModelTypeResolver.ResolveTypeSymbolExpression(directives.InheritsDirective!,
            directives.UsingDirectives, compilation, rootNamespace) as INamedTypeSymbol;
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (current.GetMembers().OfType<IPropertySymbol>().Any(static property => property.GetAttributes().Any(static attribute =>
                attribute.AttributeClass?.ToDisplayString() == "Microsoft.AspNetCore.Mvc.Razor.Internal.RazorInjectAttribute")))
            {
                return "injected base properties";
            }
        }
        if (type?.AllInterfaces.Any(IsLayout) == true ||
            directives.Interfaces.Any(name => ModelTypeResolver.ResolveTypeSymbolExpression(name, directives.UsingDirectives,
                compilation, rootNamespace) is INamedTypeSymbol contract && (IsLayout(contract) || contract.AllInterfaces.Any(IsLayout))))
        {
            return "layouts";
        }
        return null;

        static bool IsLayout(INamedTypeSymbol contract)
        {
            return contract.Name == "IUsesLayout" && contract.ContainingNamespace.ToDisplayString() == "RazorSlices";
        }
    }
}
