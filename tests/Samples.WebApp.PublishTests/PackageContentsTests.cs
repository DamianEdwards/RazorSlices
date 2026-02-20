using System.IO.Compression;
using Xunit.Abstractions;

namespace RazorSlices.Samples.WebApp.PublishTests;

public class PackageContentsTests(ITestOutputHelper testOutput)
{
    [Fact]
    public void Package_ContainsExpectedAssets()
    {
        var projectPath = Path.Combine(PathHelper.RepoRoot, "src", "RazorSlices", "RazorSlices.csproj");
        var outputDir = Path.Combine(PathHelper.ArtifactsDir, "TestOutput", Path.GetRandomFileName(), "pack");
        Directory.CreateDirectory(outputDir);

        var packOutput = DotNetCli.Pack(projectPath, outputDir, testOutput);
        testOutput.WriteLine(packOutput);

        var nupkgFiles = Directory.GetFiles(outputDir, "*.nupkg");
        Assert.Single(nupkgFiles);

        var nupkgPath = nupkgFiles[0];
        testOutput.WriteLine($"Inspecting package: {nupkgPath}");

        using var archive = ZipFile.OpenRead(nupkgPath);
        var entries = archive.Entries.Select(e => e.FullName.Replace('\\', '/')).ToList();

        foreach (var entry in entries.OrderBy(e => e))
        {
            testOutput.WriteLine($"  {entry}");
        }

        // Source generator must be present
        Assert.Contains(entries, e => e.Equals("analyzers/dotnet/cs/RazorSlices.SourceGenerator.dll", StringComparison.OrdinalIgnoreCase));

        // Build props and targets must be present
        Assert.Contains(entries, e => e.Equals("build/RazorSlices.props", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(entries, e => e.Equals("build/RazorSlices.targets", StringComparison.OrdinalIgnoreCase));

        // Library assemblies for each targeted framework must be present
        Assert.Contains(entries, e => e.Equals("lib/net8.0/RazorSlices.dll", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(entries, e => e.Equals("lib/net10.0/RazorSlices.dll", StringComparison.OrdinalIgnoreCase));
    }
}
