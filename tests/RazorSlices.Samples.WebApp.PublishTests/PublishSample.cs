using System.Diagnostics;

namespace RazorSlices.Samples.WebApp.PublishTests;

public class PublishSample
{
    const string ProjectName = "RazorSlices.Samples.WebApp";

    [Theory]
    [InlineData(PublishScenario.Default, "net8.0")]
    [InlineData(PublishScenario.Default, "net9.0")]
    //[InlineData(PublishScenario.Trimmed)]
    //[InlineData(PublishScenario.AOT)]
    public void Publish(PublishScenario publishScenario, string tfm)
    {
        var projectBuilder = new ProjectBuilder(ProjectName, publishScenario);
        projectBuilder.Publish(tfm);

        Assert.DoesNotContain("warning", projectBuilder.PublishResult?.Output, StringComparison.OrdinalIgnoreCase);
    }
}
