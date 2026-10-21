using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace RazorSlices.SourceGenerator.Tests;

public class GeneratedActivationTests
{
    private static readonly MetadataReference[] References =
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Append(typeof(RazorSlice).Assembly.Location).Distinct()
            .Select(static path => MetadataReference.CreateFromFile(path)).ToArray();

    [Fact]
    public void PlainSlicesHaveNoInitializationOrBindingCode()
    {
        var (code, diagnostics) = Generate();
        Assert.Empty(diagnostics);
        Assert.Contains("__Activation = new();", code);
        Assert.DoesNotContain("__Initialize", code);
        Assert.DoesNotContain("__Bind", code);
        Assert.DoesNotContain("UnsafeAccessor", code);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GeneratesRequiredAndOptionalInjectionWithoutBinding(bool nullable)
    {
        var (code, diagnostics) = Generate(
            imports: "@inject Service Required\n@inject Service? Optional",
            members: "public Service Required { get; private set; } = null!; public Service? Optional { get; private set; }",
            nullable: nullable);
        Assert.Empty(diagnostics);
        Assert.Contains("GetService<global::TestApp.Service>(services)", code);
        Assert.Equal(nullable, code.Contains("GetRequiredService<global::TestApp.Service>(services)"));
        Assert.Contains("Name = \"set_Required\"", code);
        Assert.Contains("Name = \"set_Optional\"", code);
        Assert.DoesNotContain("__Bind", code);
    }

    [Fact]
    public void ChildImportOverridesParentInjectionByName()
    {
        var (code, diagnostics) = Generate(
            imports: "@inject Service Value",
            child: "@inject Alternate Value",
            members: "public Alternate Value { get; private set; } = null!;");
        Assert.Empty(diagnostics);
        Assert.Contains("GetRequiredService<global::TestApp.Alternate>", code);
        Assert.DoesNotContain("GetRequiredService<global::TestApp.Service>", code);
    }

    [Fact]
    public void InjectionTypesResolveUsingAliases()
    {
        var (code, diagnostics) = Generate(imports: "@using Alias = TestApp\n@inject Alias.Service Value",
            members: "public Service Value { get; private set; } = null!;");
        Assert.Empty(diagnostics);
        Assert.Contains("GetRequiredService<global::TestApp.Service>", code);
    }

    [Fact]
    public void BindingCallbacksAreUnrolledAndUnwindInReverseOrder()
    {
        var (code, diagnostics) = Generate(
            imports: "@inject ITracker First\n@inject ITracker? Second",
            members: "public ITracker First { get; private set; } = null!; public ITracker? Second { get; private set; }");
        Assert.Empty(diagnostics);
        Assert.Contains("__Initialize, __Bind, __Unbind", code);
        Assert.Contains("completed = 1", code);
        Assert.Contains("catch (global::System.Exception error)", code);
        Assert.True(code.IndexOf("slice.@Second)?.Unbind", StringComparison.Ordinal) <
            code.IndexOf("slice.@First)?.Unbind", StringComparison.Ordinal));
        Assert.DoesNotContain("foreach", code);
        Assert.DoesNotContain("Expression", code);
    }

    [Fact]
    public void InheritedPropertiesUseTheirDeclaringSetter()
    {
        var (code, diagnostics) = Generate(
            inherits: "CustomBase",
            custom: """
                public abstract class CustomBase : RazorSlice
                {
                    [RazorInject] public Service Required { get; private set; } = null!;
                    [RazorInject] public Service? Optional { get; set; }
                }
                """);
        Assert.Empty(diagnostics);
        Assert.Contains("global::TestApp.CustomBase slice", code);
        Assert.Contains("slice.@Optional =", code);
        Assert.Contains("Name = \"set_Required\"", code);
    }

    [Fact]
    public void NonPublicGenericBaseSettersRetainRuntimeFallback()
    {
        var (code, diagnostics) = Generate(
            inherits: "GenericBase<string>",
            custom: """
                public abstract class GenericBase<T> : RazorSlice
                {
                    [RazorInject] public T Value { get; private set; } = default!;
                }
                """);
        Assert.Contains(diagnostics, static diagnostic => diagnostic.Id == "RSG0003");
        Assert.DoesNotContain("__Activation", code);
    }

    [Theory]
    [InlineData("public")]
    [InlineData("internal")]
    public void AccessibleGenericBaseSettersAreAssignedDirectly(string access)
    {
        var (code, diagnostics) = Generate(inherits: "GenericBase<string>",
            custom: $$"""
                public abstract class GenericBase<T> : RazorSlice
                {
                    [RazorInject] public T Value { get; {{access}} set; } = default!;
                }
                """.Replace("get; public set;", "get; set;"));
        Assert.Empty(diagnostics);
        Assert.Contains("slice.@Value =", code);
        Assert.DoesNotContain("UnsafeAccessor", code);
    }

    [Fact]
    public void CommentsAndTabsDoNotInventOrLoseInjections()
    {
        var (code, diagnostics) = Generate(
            imports: "@*\n@inject Missing NotReal\n*@\n@inject\tService Value",
            members: "public Service Value { get; private set; } = null!;");
        Assert.Empty(diagnostics);
        Assert.Contains("Name = \"set_Value\"", code);
        Assert.DoesNotContain("NotReal", code);
    }

    [Fact]
    public void SemanticProbePreservesCompilationFeatureFlags()
    {
        var (_, diagnostics) = Generate(features: true);
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void UnresolvedServiceUsesVisibleRuntimeFallbackInsteadOfPlainActivation()
    {
        var (code, diagnostics) = Generate(
            imports: "@inject Missing Value", members: "public object Value { get; private set; } = null!;");
        Assert.Contains(diagnostics, static diagnostic => diagnostic.Id == "RSG0003");
        Assert.DoesNotContain("__Activation", code);
        Assert.DoesNotContain(diagnostics, static diagnostic => diagnostic.Id == "CS8785");
    }

    private static (string Code, ImmutableArray<Diagnostic> Diagnostics) Generate(
        string imports = "", string child = "", string members = "", string inherits = "RazorSlice",
        string custom = "", bool nullable = true, bool features = false)
    {
        var directory = Path.GetFullPath("ActivationProject");
        var options = new CSharpParseOptions(LanguageVersion.Preview);
        if (features) { options = options.WithFeatures([new KeyValuePair<string, string>("activation-test", "enabled")]); }
        var source = $$"""
            using RazorSlices;
            using TestApp;
            using Microsoft.AspNetCore.Mvc.Razor.Internal;
            namespace TestApp
            {
                public class Service { }
                public class Alternate { }
                public interface ITracker : IRazorSliceBindable { }
                {{custom}}
            }
            namespace AspNetCoreGeneratedDocument
            {
                internal sealed class Slices_Page : {{inherits}}
                {
                    {{members}}
                    public override System.Threading.Tasks.Task ExecuteAsync() => System.Threading.Tasks.Task.CompletedTask;
                }
            }
            """;
        var input = CSharpCompilation.Create("ActivationTests", [CSharpSyntaxTree.ParseText(source, options)], References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: nullable ? NullableContextOptions.Enable : NullableContextOptions.Disable));
        AdditionalText[] files =
        [
            new Text(Path.Combine(directory, "_ViewImports.cshtml"), "@using RazorSlices\n@using TestApp\n@inherits " + inherits + "\n" + imports),
            new Text(Path.Combine(directory, "Slices", "_ViewImports.cshtml"), child),
            new Text(Path.Combine(directory, "Slices", "Page.cshtml"), "<p>page</p>")
        ];
        GeneratorDriver driver = CSharpGeneratorDriver.Create([new RazorSliceProxyGenerator().AsSourceGenerator()],
            files, options, new OptionsProvider(directory));
        driver = driver.RunGeneratorsAndUpdateCompilation(input, out var output, out var diagnostics);
        Assert.Empty(output.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        Assert.Empty(output.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Warning &&
            diagnostic.Location.SourceTree is { } tree && !input.SyntaxTrees.Contains(tree)));
        return (string.Join("\n", driver.GetRunResult().GeneratedTrees.Select(static tree => tree.ToString())), diagnostics);
    }

    private sealed class Text(string path, string value) : AdditionalText
    {
        public override string Path => path;
        public override SourceText GetText(CancellationToken cancellationToken = default)
        {
            return SourceText.From(value);
        }
    }
    private sealed class Options(Dictionary<string, string> values) : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, out string value)
        {
            return values.TryGetValue(key, out value!);
        }
    }
    private sealed class OptionsProvider(string directory) : AnalyzerConfigOptionsProvider
    {
        public override AnalyzerConfigOptions GlobalOptions { get; } = new Options(new()
        {
            ["build_property.RootNamespace"] = "TestApp",
            ["build_property.MSBuildProjectDirectory"] = directory
        });
        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree)
        {
            return new Options([]);
        }

        public override AnalyzerConfigOptions GetOptions(AdditionalText text)
        {
            return new Options(new()
            {
                ["build_metadata.RazorGenerate.GenerateRazorSlice"] = (!text.Path.EndsWith("_ViewImports.cshtml", StringComparison.Ordinal)).ToString()
            });
        }
    }
}
