using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

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

        var projectInfo = assemblyName.Combine(rootNamespace.Combine(projectDirectory));

        var texts = context.AdditionalTextsProvider
            .Where(text => text.Path.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase)
                           && !text.Path.EndsWith("_ViewImports.cshtml", StringComparison.OrdinalIgnoreCase)
                           && !text.Path.EndsWith("_ViewStart.cshtml", StringComparison.OrdinalIgnoreCase));

        var combined = projectInfo.Combine(texts.Collect());
        
        context.RegisterSourceOutput(combined, static (spc, pair) => Execute(spc, pair.Left, pair.Right));
    }

    private static void Execute(SourceProductionContext context, (string? AssemblyName, (string? RootNamespace, string? ProjectDirectory) BuildProperties) projectInfo, ImmutableArray<AdditionalText> texts)
    {
        if (string.IsNullOrEmpty(projectInfo.BuildProperties.RootNamespace) || string.IsNullOrEmpty(projectInfo.BuildProperties.ProjectDirectory))
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

        codeBuilder.AppendLine("using System.Diagnostics.CodeAnalysis;");
        codeBuilder.AppendLine("using RazorSlices;");
        codeBuilder.AppendLine();
        codeBuilder.AppendLine("#nullable enable");
        codeBuilder.AppendLine();

        foreach (var file in distinctTexts)
        {
            var fileName = Path.GetFileNameWithoutExtension(file.Path);
            var directory = Path.GetDirectoryName(file.Path);
            var relativePath = PathUtils.GetRelativePath(projectInfo.BuildProperties.ProjectDirectory!, directory);
            var subNamespace = relativePath.Replace(Path.PathSeparator, '.');

            var className = fileName;

            if (!CSharpHelpers.IsValidTypeName(className))
            {
                className = CSharpHelpers.CreateValidTypeName(className);
            }

            if (!CSharpHelpers.IsValidNamespace(subNamespace))
            {
                subNamespace = CSharpHelpers.CreateValidNamespace(subNamespace);
            }

            var subNamespaceAsClassName = subNamespace.Replace('.', '_');
            var fullNamespace = $"{projectInfo.BuildProperties.RootNamespace}.{subNamespace}";

            // Duplicate class name check

            if (generatedClasses.Contains($"{fullNamespace}.{className}"))
            {
                // TODO: Add an integer suffix to the class name so it can be generated and then log a warning?
                var descriptor = new DiagnosticDescriptor(
                    "RSG0001",
                    "Duplicate Class Name",
                    $"Generated class with name {className} already exists. File '{fileName}.cshtml' has been ignored.",
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

                codeBuilder.AppendLine($$"""
                    public sealed class {{className}} : IRazorSliceProxy
                    {
                        [DynamicDependency(DynamicallyAccessedMemberTypes.All, TypeName, "{{projectInfo.AssemblyName}}")]
                        private const string TypeName = "AspNetCoreGeneratedDocument.{{subNamespaceAsClassName}}_{{className}}, {{projectInfo.AssemblyName}}";
                        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
                        private static readonly Type _sliceType = Type.GetType(TypeName)!;
                        private static readonly SliceDefinition _sliceDefinition = new(_sliceType);

                        public static RazorSlice Create() => _sliceDefinition.CreateSlice();
                        public static RazorSlice<TModel> Create<TModel>(TModel model) => _sliceDefinition.CreateSlice(model);
                    }
                """);

                codeBuilder.AppendLine("}");

                codeBuilder.AppendLine();
            }
        }

        context.AddSource($"{projectInfo.BuildProperties.RootNamespace}.RazorSliceProxies.g.cs", SourceText.From(codeBuilder.ToString(), Encoding.UTF8));
    }

    /// <summary>
    /// Creates a relative path from one file or folder to another.
    /// </summary>
    /// <param name="fromPath">Contains the directory that defines the start of the relative path.</param>
    /// <param name="toPath">Contains the path that defines the endpoint of the relative path.</param>
    /// <returns>The relative path from the start directory to the end path.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="fromPath"/> or <paramref name="toPath"/> is <c>null</c>.</exception>
    /// <exception cref="UriFormatException"></exception>
    /// <exception cref="InvalidOperationException"></exception>
    private static string GetRelativePath(string fromPath, string toPath)
    {
        if (string.IsNullOrEmpty(fromPath))
        {
            throw new ArgumentNullException(nameof(fromPath));
        }

        if (string.IsNullOrEmpty(toPath))
        {
            throw new ArgumentNullException(nameof(toPath));
        }

        var fromUri = new Uri(AppendDirectorySeparatorChar(fromPath));
        var toUri = new Uri(AppendDirectorySeparatorChar(toPath));

        if (!string.Equals(fromUri.Scheme, toUri.Scheme, StringComparison.OrdinalIgnoreCase))
        {
            return toPath;
        }

        var relativeUri = fromUri.MakeRelativeUri(toUri);
        var relativePath = Uri.UnescapeDataString(relativeUri.ToString());

        if (string.Equals(toUri.Scheme, Uri.UriSchemeFile, StringComparison.OrdinalIgnoreCase))
        {
            relativePath = relativePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        }

        return relativePath;
    }

    private static string AppendDirectorySeparatorChar(string path)
    {
        // Append a slash only if the path is a directory and does not have a slash.
        if (!Path.HasExtension(path) && !path.EndsWith(Path.DirectorySeparatorChar.ToString()))
        {
            return path + Path.DirectorySeparatorChar;
        }

        return path;
    }

    internal static StringComparison StringComparison => IsCaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

    internal static bool IsCaseSensitive => !(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX));
}
