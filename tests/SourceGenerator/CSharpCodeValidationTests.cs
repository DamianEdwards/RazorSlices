namespace RazorSlices.SourceGenerator.Tests;

public class CSharpCodeValidationTests
{
    [Theory]
    [InlineData("_Footer")]
    [InlineData("Todo_")]
    [InlineData("Todo_Row")]
    [InlineData("lorem")]
    public void ValidTypeName_ReturnTrue(string typeName)
    {
        Assert.True(CSharpHelpers.IsValidTypeName(typeName));
    }

    [Theory]
    [InlineData("1Footer")] // starts with number
    [InlineData("*Todo_")] // contains special characters
    [InlineData("lorem ipsum")] // contains space
    [InlineData("lorem-ipsum")] // contains -
    [InlineData("lorem.ipsum")] // contains .
    public void InvalidTypeName_ReturnFalse(string typeName)
    {
        Assert.False(CSharpHelpers.IsValidTypeName(typeName));
    }

    [Theory]
    [InlineData("1Footer", "_1Footer")]
    [InlineData("*Todo_", "_Todo_")]
    [InlineData("lorem-ipsum", "lorem_ipsum")]
    [InlineData("lorem ipsum", "lorem_ipsum")]
    [InlineData("lorem&*$^^-ipsum", "lorem______ipsum")]
    public void CreateValidTypeName(string input, string expected)
    {
        Assert.Equal(expected, CSharpHelpers.CreateValidTypeName(input));
    }
}
