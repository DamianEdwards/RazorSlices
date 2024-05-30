using System.Diagnostics;

namespace RazorSlices.Samples.WebApp.PublishTests;

public class PublishSample
{
    const string ProjectName = "RazorSlices.Samples.WebApp";

    [Theory]
    [InlineData(PublishScenario.Default)]
    [InlineData(PublishScenario.Trimmed)]
    //[InlineData(PublishScenario.AOT)]
    public void Publish(PublishScenario publishScenario)
    {
        var projectBuilder = new ProjectBuilder(ProjectName, publishScenario);
        projectBuilder.Publish("net8.0");

        Assert.DoesNotContain("warning", projectBuilder.PublishResult?.Output, StringComparison.OrdinalIgnoreCase);
    }
}
