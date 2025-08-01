﻿using System;
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
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var assemblyName = context.CompilationProvider.Select(static (compilation, _) => compilation.AssemblyName);

        var rootNamespace = context.AnalyzerConfigOptionsProvider.Select(static (options, _) =>
            options.GlobalOptions.TryGetValue("build_property.RootNamespace", out var rootNamespace) ? rootNamespace : null);

        var projectDirectory = context.AnalyzerConfigOptionsProvider.Select(static (options, _) =>
            options.GlobalOptions.TryGetValue("build_property.MSBuildProjectDirectory", out var projectDir) ? projectDir : null);

        var sealSliceProxies = context.AnalyzerConfigOptionsProvider.Select(static (options, _) =>
            options.GlobalOptions.TryGetValue("build_property.RazorSliceProxiesSealed", out var sealSliceProxiesValue)
                && bool.TryParse(sealSliceProxiesValue, out var result) && result);

        // (Left.Left          , (Left.Right.Left.Left     , (Left.Right.Right.Left, Left.Right.Right.Right))
        // (string assemblyName, (string rootNamespace     , (string projectDirectory, bool sealSliceProxies))
        var projectInfo = assemblyName.Combine(rootNamespace.Combine(projectDirectory.Combine(sealSliceProxies)));

        var texts = context.AdditionalTextsProvider
            .Where(text => text.Path.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase))
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

        // (() projectInfo, texts)
        var combined = projectInfo.Combine(texts.Collect());
        
        context.RegisterSourceOutput(combined, static (spc, pair) => Execute(spc, pair.Left.Left, pair.Left.Right.Left, pair.Left.Right.Right.Left, pair.Left.Right.Right.Right, pair.Right));
    }

    private static void Execute(SourceProductionContext context, string? assemblyName, string? rootNamespace, string? projectDirectory, bool sealSliceProxies, ImmutableArray<AdditionalText> texts)
    {
        if (string.IsNullOrEmpty(rootNamespace) || string.IsNullOrEmpty(projectDirectory))
        {
            // Need to have a root namespace and project directory to generate the code
            return;
        }

        var distinctTexts = texts.Distinct();

        if (!distinctTexts.Any())
        {
            // Nothing to do yet
            return;
        }

        HashSet<string> generatedClasses = [];

        var codeBuilder = new StringBuilder();

        codeBuilder.AppendLine("// <auto-generated/>");
        codeBuilder.AppendLine();
        codeBuilder.AppendLine("using global::System.Diagnostics.CodeAnalysis;");
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

                codeBuilder.AppendLine($$"""
                namespace {{fullNamespace}}
                {
                """);

                var generatedTypeName = string.IsNullOrEmpty(subNamespaceAsClassName)
                    ? className
                    : $"{subNamespaceAsClassName}_{className}";

                var sealedValue = sealSliceProxies ? "sealed " : "partial ";

                codeBuilder.AppendLine($$"""
                    /// <summary>
                    /// Static proxy for the Razor Slice defined in <c>{{relativeFilePath}}</c>.
                    /// </summary>
                    public {{ sealedValue }}class {{className}} : global::RazorSlices.IRazorSliceProxy
                    {
                        [global::System.Diagnostics.CodeAnalysis.DynamicDependency(global::System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All, TypeName, "{{assemblyName}}")]
                        private const string TypeName = "AspNetCoreGeneratedDocument.{{generatedTypeName}}, {{assemblyName}}";
                        [global::System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(global::System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All)]
                        private static readonly global::System.Type _sliceType = global::System.Type.GetType(TypeName)
                            ?? throw new global::System.InvalidOperationException($"Razor view type '{TypeName}' was not found. This is likely a bug in the RazorSlices source generator.");
                        private static readonly global::RazorSlices.SliceDefinition _sliceDefinition = new(_sliceType);

                        /// <summary>
                        /// Creates a new instance of the Razor Slice defined in <c>{{relativeFilePath}}</c> .
                        /// </summary>
                        public static global::RazorSlices.RazorSlice Create() => _sliceDefinition.CreateSlice();

                        /// <summary>
                        /// Creates a new instance of the Razor Slice defined in <c>{{relativeFilePath}}</c> with the given model.
                        /// </summary>
                        public static global::RazorSlices.RazorSlice<TModel> Create<TModel>(TModel model) => _sliceDefinition.CreateSlice(model);

                        // Explicit interface implementation, workaround for https://github.com/dotnet/runtime/issues/102796
                        static global::RazorSlices.RazorSlice global::RazorSlices.IRazorSliceProxy.CreateSlice() => Create();

                        // Explicit interface implementation, workaround for https://github.com/dotnet/runtime/issues/102796
                        static global::RazorSlices.RazorSlice<TModel> global::RazorSlices.IRazorSliceProxy.CreateSlice<TModel>(TModel model) => Create(model);
                    }
                """);

                codeBuilder.AppendLine("}");

                codeBuilder.AppendLine();
            }
        }

        context.AddSource($"{rootNamespace}.RazorSliceProxies.g.cs", SourceText.From(codeBuilder.ToString(), Encoding.UTF8));
    }
}
