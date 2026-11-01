using Xunit.Abstractions;

namespace RazorSlices.Samples.WebApp.PublishTests;

public class PublishSample(ITestOutputHelper testOutput)
{
    [Theory]
    [InlineData("WebApp", PublishScenario.Default, "net10.0", false)]
    [InlineData("WebApp", PublishScenario.Default, "net11.0", false)]
    [InlineData("PagesAndSlices", PublishScenario.Default, "net10.0", false)]
    [InlineData("PagesAndSlices", PublishScenario.Default, "net11.0", false)]
    [InlineData("WebApp", PublishScenario.Trimmed, "net10.0", false)]
    [InlineData("WebApp", PublishScenario.AOT, "net10.0", false)]
    [InlineData("WebApp", PublishScenario.AOT, "net11.0", false)]
    [InlineData("WebApp", PublishScenario.Default, "net11.0", true)]
    [InlineData("WebApp", PublishScenario.AOT, "net11.0", true)]
    public async Task Publish(string projectName, PublishScenario publishScenario, string tfm, bool runtimeAsync)
    {
        var projectBuilder = new ProjectBuilder(projectName, publishScenario, testOutput);
        projectBuilder.Publish(tfm, runtimeAsync);

        Assert.DoesNotContain("warning", projectBuilder.PublishResult?.Output, StringComparison.OrdinalIgnoreCase);
        await projectBuilder.VerifyEndpointsAsync(tfm);
    }

    [Theory(Skip = ".NET 11 RC3 framework IL2101 in Components.Endpoints; re-enable after dotnet/aspnetcore#69270 is fixed.")]
    [InlineData("WebApp", PublishScenario.Trimmed, "net11.0", false)]
    [InlineData("WebApp", PublishScenario.Trimmed, "net11.0", true)]
    public Task PublishTrimmedNet11(string projectName, PublishScenario publishScenario, string tfm, bool runtimeAsync)
        => Publish(projectName, publishScenario, tfm, runtimeAsync);
}
