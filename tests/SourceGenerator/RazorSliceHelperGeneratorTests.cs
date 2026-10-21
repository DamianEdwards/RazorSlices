using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace RazorSlices.SourceGenerator.Tests;

public class RazorSliceHelperGeneratorTests
{
    private static readonly MetadataReference[] References =
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Append(typeof(RazorSlice).Assembly.Location).Distinct()
            .Select(static path => MetadataReference.CreateFromFile(path)).ToArray();

    [Fact]
    public void GeneratesFacadeWithoutSdkImplementationInInputCompilation()
    {
        var result = Generate("""
            public interface IBase { void Common(); }
            public interface IHelpers : IBase
            {
                void Button(string text, int id = 42, bool disabled = false);
                System.Threading.Tasks.ValueTask LoadAsync(string value);
                System.Threading.Tasks.Task TaskAsync();
                void Values<T>(T? value, params string[] rest) where T : class;
                void Update(ref int value, out string result);
                void Names(string _helper, string _context);
                void Defaults<T>(T value = default!, string text = null!, decimal amount = 1.25m,
                    System.DayOfWeek day = System.DayOfWeek.Monday, double number = double.PositiveInfinity);
                void Caller(int value, [System.Runtime.CompilerServices.CallerArgumentExpression("value")] string? expression = null);
            }
            """);

        Assert.Empty(result.Diagnostics);
        var facade = Assert.Single(result.Sources, static source => source.Contains("readonly struct"));
        Assert.Contains("global::Microsoft.AspNetCore.Html.HtmlString @Button", facade);
        Assert.Contains("int @id = 42, bool @disabled = false", facade);
        Assert.Contains("global::RazorSlices.HelperCompletion @LoadAsync", facade);
        Assert.Contains("where @T : class", facade);
        Assert.Contains("ref @value, out @result", facade);
        Assert.Contains("HtmlString.Empty", facade);
        Assert.Contains("extension(global::RazorSlices.IHelperManager manager)", string.Join("\n", result.Sources));
        Assert.DoesNotContain("CreateSlice()", string.Join("\n", result.Sources));
        var tree = CSharpSyntaxTree.ParseText(facade, new CSharpParseOptions(LanguageVersion.Preview));
        Assert.Empty(result.Input.AddSyntaxTrees(tree).GetDiagnostics().Where(static d => d.Severity == DiagnosticSeverity.Error));
    }

    [Theory]
    [InlineData("public interface IHelpers { int Invalid(); }")]
    [InlineData("internal interface IHelpers { void Invalid(); }")]
    [InlineData("public interface IHelpers { string Value { get; } }")]
    [InlineData("public interface IHelpers { System.Threading.Tasks.ValueTask<int> Invalid(); }")]
    public void RejectsUnsupportedContracts(string contract)
    {
        var result = Generate(contract);
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Id == "RSG0100");
        Assert.DoesNotContain(result.Diagnostics, static diagnostic => diagnostic.Id == "CS8785");
    }

    [Fact]
    public void InjectionIsDiagnosedByAnalyzerAndMissingContractByGenerator()
    {
        Assert.Empty(Generate("public interface IHelpers { void Render(); }", "@inject object Service").Diagnostics);
        Assert.Contains(Generate("public interface IHelpers { void Render(); }", implements: "").Diagnostics,
            static d => d.Id == "RSG0100");
    }

    [Theory]
    [InlineData("Helpers.cshtml")]
    [InlineData("_ViewImports.cshtml")]
    public async Task AnalyzerExplainsHowToAvoidInheritedHelperInjection(string file)
    {
        var source = $$"""
            using RazorSlices;
            using Microsoft.AspNetCore.Mvc.Razor.Internal;
            class SdkHelper : RazorSliceHelpers
            {
            #line 1 "{{file}}"
                [RazorInject] public IHelperManager Injected { get; private set; } = null!;
            #line default
                public override System.Threading.Tasks.Task ExecuteAsync() => System.Threading.Tasks.Task.CompletedTask;
            }
            """;
        var input = CSharpCompilation.Create("Analyzer", [CSharpSyntaxTree.ParseText(source)], References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var diagnostics = await input.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new RazorSliceHelperAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();
        var diagnostic = Assert.Single(diagnostics, static diagnostic => diagnostic.Id == "RSG0102");
        Assert.Contains("~/Helpers", diagnostic.GetMessage());
    }
    [Fact]
    public void HelperDiscoveryDoesNotDependOnOrdinarySliceGeneration()
    {
        var result = Generate("public interface IHelpers { void Render(); }", generateSlice: false);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(2, result.Sources.Length);
    }

    [Theory]
    [InlineData("Write(42);", false)]
    [InlineData("Write(Html);", true)]
    [InlineData("object escaped = this;", true)]
    public async Task AnalyzerAllowsSdkProtocolButRejectsInjectedAccessAndEscape(string body, bool invalid)
    {
        var source = $$"""
            using RazorSlices;
            using Microsoft.AspNetCore.Mvc.Razor.Internal;
            class SdkHelper : RazorSliceHelpers
            {
                [RazorInject] public object Html { get; private set; } = null!;
                public override async System.Threading.Tasks.Task ExecuteAsync() { WriteLiteral("\r\n"); }
            #line 1 "Helpers.cshtml"
                public void Render() { {{body}} }
            #line default
            }
            """;
        var input = CSharpCompilation.Create("Analyzer", [CSharpSyntaxTree.ParseText(source)], References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var diagnostics = await input.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new RazorSliceHelperAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();
        Assert.Equal(invalid, diagnostics.Any(static diagnostic => diagnostic.Id == "RSG0101"));
    }

    [Theory]
    [InlineData("public int Count;", "")]
    [InlineData("public string State { get; set; } = \"\";", "")]
    [InlineData("", "WriteLiteral(\"not whitespace\");")]
    public async Task AnalyzerRejectsStateAndTopLevelBodies(string state, string body)
    {
        var source = $$"""
            using RazorSlices;
            class SdkHelper : RazorSliceHelpers
            {
            #line 1 "Helpers.cshtml"
                {{state}}
            #line default
                public override async System.Threading.Tasks.Task ExecuteAsync()
                {
                    {{body}}
                }
            }
            """;
        var input = CSharpCompilation.Create("Analyzer", [CSharpSyntaxTree.ParseText(source)], References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var diagnostics = await input.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new RazorSliceHelperAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();
        Assert.Contains(diagnostics, static d => d.Id == "RSG0101");
    }

    private static (Compilation Input, string[] Sources, ImmutableArray<Diagnostic> Diagnostics) Generate(
        string contract, string extra = "", string implements = "@implements IHelpers", bool generateSlice = true)
    {
        var directory = Path.GetFullPath("HelperProject");
        var options = new CSharpParseOptions(LanguageVersion.Preview);
        var input = CSharpCompilation.Create("Helpers",
            [CSharpSyntaxTree.ParseText("#nullable enable\n" + contract, options)], References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        AdditionalText[] files = [new Text(Path.Combine(directory, "MyHelpers.cshtml"),
            "@using RazorSlices\n@inherits RazorSliceHelpers\n" + implements + "\n" + extra)];
        GeneratorDriver driver = CSharpGeneratorDriver.Create([new RazorSliceProxyGenerator().AsSourceGenerator()],
            files, options, new OptionsProvider(directory, generateSlice));
        driver = driver.RunGeneratorsAndUpdateCompilation(input, out _, out var diagnostics);
        return (input, driver.GetRunResult().GeneratedTrees.Select(static tree => tree.ToString()).ToArray(), diagnostics);
    }

    private sealed class Text(string path, string content) : AdditionalText
    {
        public override string Path => path;
        public override SourceText GetText(CancellationToken cancellationToken = default)
        {
            return SourceText.From(content);
        }
    }
    private sealed class Options(Dictionary<string, string> values) : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, out string value)
        {
            return values.TryGetValue(key, out value!);
        }
    }
    private sealed class OptionsProvider(string directory, bool generateSlice) : AnalyzerConfigOptionsProvider
    {
        public override AnalyzerConfigOptions GlobalOptions { get; } = new Options(new()
        {
            ["build_property.RootNamespace"] = "HelperProject",
            ["build_property.MSBuildProjectDirectory"] = directory
        });
        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree)
        {
            return new Options([]);
        }

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile)
        {
            return new Options(new()
            {
                ["build_metadata.RazorGenerate.GenerateRazorSlice"] = generateSlice ? "true" : "false"
            });
        }
    }
}
