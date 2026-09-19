using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace RazorSlices.SourceGenerator.Tests;

public class RazorSliceProxyGeneratorTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    public void Proxy_CompilesWithDirectConstructor(bool hasModel, bool useRecords, bool customBase)
    {
        var (source, compilation) = Generate(hasModel, useRecords, customBase);

        Assert.Contains("new global::AspNetCoreGeneratedDocument.Slices_Hello", source);
        Assert.DoesNotContain("SliceDefinition", source);
        Assert.DoesNotContain("typeof(", source);
        Assert.DoesNotContain("DynamicallyAccessedMembers", source);
        Assert.Contains(useRecords ? "partial record Hello" : "partial class Hello", source);
        if (hasModel)
        {
            Assert.Contains("static model => new global::AspNetCoreGeneratedDocument.Slices_Hello { Model = model }", source);
            Assert.Contains(customBase ? "IRazorSliceProxy<global::TestApp.Model>.CreateSlice" : "IRazorSliceProxy<global::System.String>.CreateSlice", source);
        }
        else
        {
            Assert.Contains("static () => new global::AspNetCoreGeneratedDocument.Slices_Hello()", source);
            Assert.Contains("IRazorSliceProxy.CreateSlice()", source);
        }
        Assert.Empty(compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));
    }

    [Fact]
    public void Proxy_ReportsInaccessibleConstructorAtCompileTime()
    {
        var (_, compilation) = Generate(false, false, false, privateConstructor: true);

        Assert.Contains(compilation.GetDiagnostics(), d => d.Id == "CS0122");
    }

    private static (string Source, Compilation Compilation) Generate(
        bool hasModel, bool useRecords, bool customBase, bool privateConstructor = false)
    {
        var projectDirectory = Path.GetFullPath("TestProject");
        var baseType = customBase ? "TestApp.CustomSlice" : hasModel ? "RazorSlices.RazorSlice<string>" : "RazorSlices.RazorSlice";
        var source = $$"""
            #nullable enable
            namespace RazorSlices
            {
                public abstract class RazorSlice { }
                public abstract class RazorSlice<TModel> : RazorSlice
                {
                    public required TModel Model { get; set; }
                }
                public interface IRazorSliceProxy
                {
                    static abstract RazorSlice CreateSlice();
                }
                public interface IRazorSliceProxy<TModel>
                {
                    static abstract RazorSlice<TModel> CreateSlice(TModel model);
                }
                public static class RazorSliceFactory
                {
                    public static RazorSlice Create<TSlice>(System.Func<TSlice> create) where TSlice : RazorSlice => create();
                    public static RazorSlice<TModel> Create<TSlice, TModel>(TModel model, System.Func<TModel, TSlice> create)
                        where TSlice : RazorSlice<TModel> => create(model);
                }
            }
            namespace TestApp
            {
                public class Model { }
                public abstract class CustomSlice : RazorSlices.RazorSlice<Model> { }
            }
            namespace AspNetCoreGeneratedDocument
            {
                internal sealed class Slices_Hello : {{baseType}}
                {
                    {{(privateConstructor ? "private" : "public")}} Slices_Hello() { }
                }
            }
            """;
        var runtimeDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        var compilation = CSharpCompilation.Create(
            "TestApp",
            [CSharpSyntaxTree.ParseText(source)],
            [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(Path.Combine(runtimeDirectory, "System.Runtime.dll"))
            ],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        ImmutableArray<AdditionalText> texts =
        [
            new TestText(Path.Combine(projectDirectory, "Slices", "_ViewImports.cshtml"), $"@inherits {baseType}"),
            new TestText(Path.Combine(projectDirectory, "Slices", "Hello.cshtml"), "<p>Hello</p>")
        ];
        var options = new TestOptionsProvider(projectDirectory, useRecords);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new RazorSliceProxyGenerator().AsSourceGenerator()], texts, optionsProvider: options);

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);

        Assert.Empty(diagnostics);
        return (Assert.Single(driver.GetRunResult().GeneratedTrees).ToString(), output);
    }

    private sealed class TestText(string path, string content) : AdditionalText
    {
        public override string Path => path;
        public override SourceText GetText(CancellationToken cancellationToken = default) => SourceText.From(content);
    }

    private sealed class TestOptions(Dictionary<string, string> values) : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, out string value) => values.TryGetValue(key, out value!);
    }

    private sealed class TestOptionsProvider(string projectDirectory, bool useRecords) : AnalyzerConfigOptionsProvider
    {
        public override AnalyzerConfigOptions GlobalOptions { get; } = new TestOptions(new()
        {
            ["build_property.RootNamespace"] = "TestApp",
            ["build_property.MSBuildProjectDirectory"] = projectDirectory,
            ["build_property.RazorSliceProxiesAsRecords"] = useRecords.ToString()
        });

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => new TestOptions([]);

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => new TestOptions(new()
        {
            ["build_metadata.RazorGenerate.GenerateRazorSlice"] =
                (Path.GetFileName(textFile.Path) != "_ViewImports.cshtml").ToString()
        });
    }
}
