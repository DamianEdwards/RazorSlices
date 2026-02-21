using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace RazorSlices.SourceGenerator.Tests;

public class ModelTypeResolverTests
{
    private static readonly CSharpCompilation TestCompilation = CreateTestCompilation();

    private static CSharpCompilation CreateTestCompilation()
    {
        var source = @"
namespace TestNs
{
    public class Outer
    {
        public class Inner { }

        public class Middle
        {
            public class Deep { }
        }

        public class GenericInner<T> { }

        public class MultiGenericInner<T, U> { }
    }

    public struct ValueOuter
    {
        public struct ValueInner { }
    }

    public class _Odd_Name
    {
        public class _Inner_Type { }
    }

    public class Container123
    {
        public class Item456 { }
    }
}

namespace AliasNs
{
    public class Host
    {
        public class Nested { }

        public class Level2
        {
            public class Level3 { }
        }

        public class GenericNested<T> { }
    }
}

namespace RootNs
{
    public class Parent
    {
        public class Child { }
    }
}
";
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        var references = new MetadataReference[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(Path.Combine(runtimeDir, "System.Runtime.dll")),
        };

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        // Verify compilation has no errors
        var diagnostics = compilation.GetDiagnostics();
        var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "Test compilation has errors: " + string.Join(", ", errors.Select(e => e.GetMessage())));
        }

        return compilation;
    }

    private static string? Resolve(
        string modelTypeName,
        List<UsingDirective>? usings = null,
        string? rootNamespace = null)
    {
        return ModelTypeResolver.ResolveModelType(
            modelTypeName,
            usings ?? [],
            TestCompilation,
            rootNamespace);
    }

    // ===== Basic nested type resolution via @using namespace =====

    [Fact]
    public void NestedType_ResolvedViaUsing()
    {
        var usings = new List<UsingDirective> { new("TestNs", null) };
        var result = Resolve("Outer.Inner", usings);
        Assert.Equal("global::TestNs.Outer.Inner", result);
    }

    [Fact]
    public void NestedType_ThreeLevels_ResolvedViaUsing()
    {
        var usings = new List<UsingDirective> { new("TestNs", null) };
        var result = Resolve("Outer.Middle.Deep", usings);
        Assert.Equal("global::TestNs.Outer.Middle.Deep", result);
    }

    [Fact]
    public void NestedValueType_ResolvedViaUsing()
    {
        var usings = new List<UsingDirective> { new("TestNs", null) };
        var result = Resolve("ValueOuter.ValueInner", usings);
        Assert.Equal("global::TestNs.ValueOuter.ValueInner", result);
    }

    // ===== Nested type resolution via fully-qualified name =====

    [Fact]
    public void NestedType_FullyQualified()
    {
        var result = Resolve("TestNs.Outer.Inner");
        Assert.Equal("global::TestNs.Outer.Inner", result);
    }

    [Fact]
    public void NestedType_ThreeLevels_FullyQualified()
    {
        var result = Resolve("TestNs.Outer.Middle.Deep");
        Assert.Equal("global::TestNs.Outer.Middle.Deep", result);
    }

    // ===== Nested type resolution via root namespace =====

    [Fact]
    public void NestedType_ResolvedViaRootNamespace()
    {
        var result = Resolve("Parent.Child", rootNamespace: "RootNs");
        Assert.Equal("global::RootNs.Parent.Child", result);
    }

    // ===== Nested type resolution via using alias =====

    [Fact]
    public void NestedType_ResolvedViaAlias()
    {
        var usings = new List<UsingDirective> { new("AliasNs", "M") };
        var result = Resolve("M.Host.Nested", usings);
        Assert.Equal("global::AliasNs.Host.Nested", result);
    }

    [Fact]
    public void NestedType_ThreeLevels_ResolvedViaAlias()
    {
        var usings = new List<UsingDirective> { new("AliasNs", "M") };
        var result = Resolve("M.Host.Level2.Level3", usings);
        Assert.Equal("global::AliasNs.Host.Level2.Level3", result);
    }

    [Fact]
    public void NestedType_AliasOnly_NoMatchingNamespaceUsing()
    {
        // Only an alias, no non-alias @using that could find the type via namespace prefixing
        var usings = new List<UsingDirective> { new("AliasNs", "Models") };
        var result = Resolve("Models.Host.Nested", usings);
        Assert.Equal("global::AliasNs.Host.Nested", result);
    }

    // ===== Nested types with generics =====

    [Fact]
    public void NestedGenericType_ResolvedViaUsing()
    {
        var usings = new List<UsingDirective> { new("TestNs", null) };
        var result = Resolve("Outer.GenericInner<string>", usings);
        Assert.Equal("global::TestNs.Outer.GenericInner<global::System.String>", result);
    }

    [Fact]
    public void NestedGenericType_MultipleTypeArgs()
    {
        var usings = new List<UsingDirective> { new("TestNs", null) };
        var result = Resolve("Outer.MultiGenericInner<string, int>", usings);
        Assert.Equal("global::TestNs.Outer.MultiGenericInner<global::System.String, global::System.Int32>", result);
    }

    [Fact]
    public void NestedType_AsGenericArgument()
    {
        var usings = new List<UsingDirective> { new("TestNs", null) };
        var result = Resolve("List<Outer.Inner>", usings);
        Assert.Equal("global::System.Collections.Generic.List<global::TestNs.Outer.Inner>", result);
    }

    [Fact]
    public void NestedType_AsMultipleGenericArguments()
    {
        var usings = new List<UsingDirective> { new("TestNs", null) };
        var result = Resolve("Dictionary<string, Outer.Inner>", usings);
        Assert.Equal("global::System.Collections.Generic.Dictionary<global::System.String, global::TestNs.Outer.Inner>", result);
    }

    [Fact]
    public void NestedType_BothGenericArgsAreNested()
    {
        var usings = new List<UsingDirective> { new("TestNs", null) };
        var result = Resolve("Dictionary<Outer.Inner, Outer.Middle.Deep>", usings);
        Assert.Equal("global::System.Collections.Generic.Dictionary<global::TestNs.Outer.Inner, global::TestNs.Outer.Middle.Deep>", result);
    }

    [Fact]
    public void NestedGenericType_ViaAlias()
    {
        var usings = new List<UsingDirective> { new("AliasNs", "M") };
        var result = Resolve("M.Host.GenericNested<string>", usings);
        Assert.Equal("global::AliasNs.Host.GenericNested<global::System.String>", result);
    }

    [Fact]
    public void NestedType_AsGenericArgViaAlias()
    {
        var usings = new List<UsingDirective> { new("AliasNs", "M") };
        var result = Resolve("List<M.Host.Nested>", usings);
        Assert.Equal("global::System.Collections.Generic.List<global::AliasNs.Host.Nested>", result);
    }

    // ===== Nested types with arrays =====

    [Fact]
    public void NestedType_Array()
    {
        var usings = new List<UsingDirective> { new("TestNs", null) };
        var result = Resolve("Outer.Inner[]", usings);
        Assert.Equal("global::TestNs.Outer.Inner[]", result);
    }

    [Fact]
    public void NestedType_MultiDimensionalArray()
    {
        var usings = new List<UsingDirective> { new("TestNs", null) };
        var result = Resolve("Outer.Inner[,]", usings);
        Assert.Equal("global::TestNs.Outer.Inner[,]", result);
    }

    [Fact]
    public void NestedType_JaggedArray()
    {
        var usings = new List<UsingDirective> { new("TestNs", null) };
        var result = Resolve("Outer.Inner[][]", usings);
        Assert.Equal("global::TestNs.Outer.Inner[][]", result);
    }

    // ===== Nested types with nullable =====

    [Fact]
    public void NestedValueType_Nullable()
    {
        var usings = new List<UsingDirective> { new("TestNs", null) };
        var result = Resolve("ValueOuter.ValueInner?", usings);
        Assert.Equal("global::TestNs.ValueOuter.ValueInner?", result);
    }

    // ===== Odd but valid identifiers =====

    [Fact]
    public void NestedType_UnderscoreIdentifiers()
    {
        var usings = new List<UsingDirective> { new("TestNs", null) };
        var result = Resolve("_Odd_Name._Inner_Type", usings);
        Assert.Equal("global::TestNs._Odd_Name._Inner_Type", result);
    }

    [Fact]
    public void NestedType_NumericSuffixIdentifiers()
    {
        var usings = new List<UsingDirective> { new("TestNs", null) };
        var result = Resolve("Container123.Item456", usings);
        Assert.Equal("global::TestNs.Container123.Item456", result);
    }

    // ===== Negative tests =====

    [Fact]
    public void NestedType_NonExistent_ReturnsNull()
    {
        var usings = new List<UsingDirective> { new("TestNs", null) };
        var result = Resolve("Outer.DoesNotExist", usings);
        Assert.Null(result);
    }

    [Fact]
    public void NestedType_WrongNamespace_ReturnsNull()
    {
        var usings = new List<UsingDirective> { new("WrongNamespace", null) };
        var result = Resolve("Outer.Inner", usings);
        Assert.Null(result);
    }

    // ===== Regression tests: non-nested types still work =====

    [Fact]
    public void NonNestedType_ResolvedViaUsing()
    {
        var usings = new List<UsingDirective> { new("TestNs", null) };
        var result = Resolve("Outer", usings);
        Assert.Equal("global::TestNs.Outer", result);
    }

    [Fact]
    public void NonNestedType_Primitive()
    {
        var result = Resolve("string");
        Assert.Equal("global::System.String", result);
    }

    [Fact]
    public void NonNestedType_FullyQualified()
    {
        var result = Resolve("TestNs.Outer");
        Assert.Equal("global::TestNs.Outer", result);
    }

    [Fact]
    public void NonNestedType_GenericWithPrimitive()
    {
        var result = Resolve("List<string>");
        Assert.Equal("global::System.Collections.Generic.List<global::System.String>", result);
    }
}
