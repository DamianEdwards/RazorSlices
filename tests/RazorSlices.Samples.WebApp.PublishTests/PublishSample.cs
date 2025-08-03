using Xunit.Abstractions;

namespace RazorSlices.Samples.WebApp.PublishTests;

public class PublishSample(ITestOutputHelper testOutput)
{
    [Theory]
    [InlineData("RazorSlices.Samples.WebApp", PublishScenario.Default, "net8.0")]
    [InlineData("RazorSlices.Samples.WebApp", PublishScenario.Default, "net9.0")]
    [InlineData("RazorSlices.Samples.PagesAndSlices", PublishScenario.Default, "net8.0")]
    [InlineData("RazorSlices.Samples.PagesAndSlices", PublishScenario.Default, "net9.0")]
    //[InlineData(PublishScenario.Trimmed)]
    //[InlineData(PublishScenario.AOT, "net8.0")]
    //[InlineData(PublishScenario.AOT, "net9.0")]
    public void Publish(string projectName, PublishScenario publishScenario, string tfm)
    {
        var projectBuilder = new ProjectBuilder(projectName, publishScenario, testOutput);
        projectBuilder.Publish(tfm);

        Assert.DoesNotContain("warning", projectBuilder.PublishResult?.Output, StringComparison.OrdinalIgnoreCase);
    }
}
