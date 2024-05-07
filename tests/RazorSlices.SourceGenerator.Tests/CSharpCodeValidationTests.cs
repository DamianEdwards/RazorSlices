namespace RazorSlices.SourceGenerator.Tests;

public class CSharpCodeValidationTests
{
    [Fact]
    public void ValidTypeName_ReturnTrue()
    {
        string[] validNames = new string[] 
        { 
            "_Footer", 
            "Todo_", 
            "lorem" 
        };
        
        bool isValid = true;

        foreach (string name in validNames)
        {
            isValid = isValid && CSharpHelpers.IsValidTypeName(name);
        }

        Assert.True(isValid);
    }

    [Fact]
    public void InvalidTypeName_ReturnFalse()
    {
        Assert.False(CSharpHelpers.IsValidTypeName("1Footer"));      // starts with number.
        Assert.False(CSharpHelpers.IsValidTypeName("*Todo_"));       // contains special characters
        Assert.False(CSharpHelpers.IsValidTypeName("lorem ipsum"));  // contains space
        Assert.False(CSharpHelpers.IsValidTypeName("lorem-ipsum"));  // contains -
    }

    [Fact]
    public void CreateValidTypeName()
    {
        Assert.Equal("_1Footer", CSharpHelpers.CreateValidTypeName("1Footer"));
        Assert.Equal("_Todo_", CSharpHelpers.CreateValidTypeName("*Todo_"));
        Assert.Equal("lorem_ipsum", CSharpHelpers.CreateValidTypeName("lorem-ipsum"));
        Assert.Equal("lorem_ipsum", CSharpHelpers.CreateValidTypeName("lorem ipsum"));
        Assert.Equal("lorem______ipsum", CSharpHelpers.CreateValidTypeName("lorem&*$^^-ipsum"));
    }
}
