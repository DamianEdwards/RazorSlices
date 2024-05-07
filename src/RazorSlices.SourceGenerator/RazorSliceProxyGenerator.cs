using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;

namespace RazorSlices.SourceGenerator;

/// <summary>
/// 
/// </summary>
[Generator]
public class RazorSliceProxyGenerator : IIncrementalGenerator
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="context"></param>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var assemblyName = context.CompilationProvider.Select(static (c, _) => c.AssemblyName);

        var texts = context.AdditionalTextsProvider
            .Where(f => f.Path.EndsWith(".cshtml") && !f.Path.EndsWith("_ViewImports.cshtml"));

        var combined = assemblyName.Combine(texts.Collect());
        
        context.RegisterSourceOutput(combined,
            static (spc, pair) =>
            {
                if (pair.Left is not null) Execute(spc, pair.Left, pair.Right);
            });
    }

    private static void Execute(SourceProductionContext context, string assemblyName, ImmutableArray<AdditionalText> texts)
    {
        HashSet<string> generatedClasses = [];

        var codeBuilder = new StringBuilder();

        codeBuilder.AppendLine("using System.Diagnostics.CodeAnalysis;");
        codeBuilder.AppendLine("#nullable enable");
        codeBuilder.AppendLine($"namespace {assemblyName}.Slices;");

        foreach (var file in texts)
        {
            var fileName = Path.GetFileNameWithoutExtension(file.Path);
            
            var className = fileName;

            if (!CSharpHelpers.IsValidTypeName(className))
            {
                className = CSharpHelpers.CreateValidTypeName(className);
            }

            // Duplicate class name check

            if (generatedClasses.Contains(className))
            {
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
                generatedClasses.Add(className);

                codeBuilder.AppendLine($$"""
                public sealed class {{className}} : IRazorSliceProxy
                {
                    [DynamicDependency(DynamicallyAccessedMemberTypes.All, TypeName, "{{assemblyName}}")]
                    private const string TypeName = "AspNetCoreGeneratedDocument.Slices_{{className}}, {{assemblyName}}";
                    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
                    private static readonly Type _sliceType = Type.GetType(TypeName)!;
                    private static readonly SliceDefinition _sliceDefinition = new(_sliceType);

                    public static RazorSlice Create() => _sliceDefinition.CreateSlice();
                    public static RazorSlice<TModel> Create<TModel>(TModel model) => _sliceDefinition.CreateSlice(model);
                }
                """);

                codeBuilder.AppendLine();
            }
        }

        context.AddSource("Slices.g.cs", codeBuilder.ToString());
    }
}
