using Microsoft.CodeAnalysis.Text;

namespace RazorSlices.SourceGenerator.Tests;

public class RazorDirectiveParserTests
{
    [Theory]
    [InlineData("@inherits RazorSlice", "RazorSlice")]
    [InlineData("@inherits RazorSlice<Models.Todo>", "RazorSlice<Models.Todo>")]
    [InlineData("@inherits RazorSliceHttpResult<DateTime>", "RazorSliceHttpResult<DateTime>")]
    [InlineData("@inherits  RazorSlice ", "RazorSlice")]
    [InlineData("  @inherits RazorSlice", "RazorSlice")]
    public void ParseInheritsDirective_ReturnsBaseType(string line, string expected)
    {
        var sourceText = SourceText.From(line);
        var result = RazorDirectiveParser.ParseInheritsDirective(sourceText);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("@* comment *@")]
    [InlineData("<p>Hello</p>")]
    [InlineData("@using System")]
    public void ParseInheritsDirective_ReturnsNull_WhenNoInherits(string line)
    {
        var sourceText = SourceText.From(line);
        Assert.Null(RazorDirectiveParser.ParseInheritsDirective(sourceText));
    }

    [Theory]
    [InlineData("@inherits RazorSlice<Models.Todo>", "RazorSlice")]
    [InlineData("@inherits RazorSliceHttpResult", "RazorSliceHttpResult")]
    [InlineData("@inherits RazorLayoutSlice<LayoutModel>", "RazorLayoutSlice")]
    public void ExtractBaseTypeName_ReturnsCorrectName(string line, string expected)
    {
        var sourceText = SourceText.From(line);
        var inherits = RazorDirectiveParser.ParseInheritsDirective(sourceText)!;
        Assert.Equal(expected, RazorDirectiveParser.ExtractBaseTypeName(inherits));
    }

    [Theory]
    [InlineData("RazorSlice<Models.Todo>", "Models.Todo")]
    [InlineData("RazorSliceHttpResult<DateTime>", "DateTime")]
    [InlineData("RazorSlice<Func<object?, HelperResult>>", "Func<object?, HelperResult>")]
    [InlineData("RazorSlice<Models.Todo[]>", "Models.Todo[]")]
    public void ExtractModelType_ReturnsModelType(string baseType, string expected)
    {
        Assert.Equal(expected, RazorDirectiveParser.ExtractModelType(baseType));
    }

    [Theory]
    [InlineData("RazorSlice")]
    [InlineData("RazorSliceHttpResult")]
    [InlineData("RazorLayoutSlice")]
    public void ExtractModelType_ReturnsNull_WhenNoGenericArg(string baseType)
    {
        Assert.Null(RazorDirectiveParser.ExtractModelType(baseType));
    }

    [Fact]
    public void ParseUsingDirectives_ReturnsSimpleUsings()
    {
        var source = "@using System\n@using System.Linq\n@inherits RazorSlice\n";
        var sourceText = SourceText.From(source);
        var usings = RazorDirectiveParser.ParseUsingDirectives(sourceText);

        Assert.Equal(2, usings.Count);
        Assert.Equal("System", usings[0].NamespaceOrType);
        Assert.Null(usings[0].Alias);
        Assert.Equal("System.Linq", usings[1].NamespaceOrType);
    }

    [Fact]
    public void ParseUsingDirectives_ReturnsAliasUsings()
    {
        var source = "@using Models = MyApp.Models\n";
        var sourceText = SourceText.From(source);
        var usings = RazorDirectiveParser.ParseUsingDirectives(sourceText);

        Assert.Single(usings);
        Assert.Equal("MyApp.Models", usings[0].NamespaceOrType);
        Assert.Equal("Models", usings[0].Alias);
    }

    [Fact]
    public void ParseUsingDirectives_HandlesTrailingSemicolons()
    {
        var source = "@using System;\n@using System.Linq;\n";
        var sourceText = SourceText.From(source);
        var usings = RazorDirectiveParser.ParseUsingDirectives(sourceText);

        Assert.Equal(2, usings.Count);
        Assert.Equal("System", usings[0].NamespaceOrType);
        Assert.Equal("System.Linq", usings[1].NamespaceOrType);
    }

    [Fact]
    public void ParseInheritsDirective_WithMultipleLines_FindsInherits()
    {
        var source = "@using System\n@using System.Linq\n\n@inherits RazorSlice<DateTime>\n\n<p>Hello</p>";
        var sourceText = SourceText.From(source);
        var result = RazorDirectiveParser.ParseInheritsDirective(sourceText);
        Assert.Equal("RazorSlice<DateTime>", result);
    }
}
