using Xunit.Abstractions;

namespace RazorSlices.Samples.WebApp.PublishTests;

public class PublishSample(ITestOutputHelper testOutput)
{
    const string ProjectName = "RazorSlices.Samples.WebApp";

    [Theory]
    [InlineData(PublishScenario.Default, "net8.0")]
    [InlineData(PublishScenario.Default, "net9.0")]
    //[InlineData(PublishScenario.Trimmed)]
    //[InlineData(PublishScenario.AOT, "net8.0")]
    //[InlineData(PublishScenario.AOT, "net9.0")]
    public void Publish(PublishScenario publishScenario, string tfm)
    {
        var projectBuilder = new ProjectBuilder(ProjectName, publishScenario, testOutput);
        projectBuilder.Publish(tfm);

        Assert.DoesNotContain("warning", projectBuilder.PublishResult?.Output, StringComparison.OrdinalIgnoreCase);
    }
}
