using System.Diagnostics;
using Mono.Cecil;

namespace RazorSlices.BuildTasks.Tests;

public class BuildIntegrationTests
{
    private static readonly string[] Templates =
        ["Hello", "Minimal", "MinimalModel", "FullModel", "CustomMinimal", "CustomFull", "Markup", "Composed"];

    [Theory]
    [InlineData("net8.0")]
    [InlineData("net10.0")]
    public async Task OptimizationDefaultsAndOptOutWorkWithoutCleaning(string framework)
    {
        var root = FindRoot();
        var artifacts = Path.Combine(root, "artifacts", "OptimizerTests", Guid.NewGuid().ToString("N"));
        var project = Path.Combine(root, "tests", "BuildTasks", "Integration", "OptimizerSample.csproj");
        Directory.CreateDirectory(artifacts);
        try
        {
            await Build("Debug");
            Assert.Equal(5, CountProperties("debug"));
            await Build("Release");
            Assert.Equal(0, CountProperties("release"));
            var lastWrite = File.GetLastWriteTimeUtc(AssemblyPath("release"));
            await Build("Release");
            Assert.Equal(lastWrite, File.GetLastWriteTimeUtc(AssemblyPath("release")));
            await Build("Release", "-p:RazorSlicesRemoveMvcDefaultInjectedProperties=false");
            Assert.Equal(5, CountProperties("release"));
            await Build("Release");
            Assert.Equal(0, CountProperties("release"));
            await Build("Release", "-p:Optimize=false", "-p:RazorSlicesRemoveMvcDefaultInjectedProperties=true");
            Assert.Equal(5, CountProperties("release"));
            await Build("Release", "-p:OptimizerTestDebugType=none");
            Assert.Equal(0, CountProperties("release"));
            await Build("Release", "-p:OptimizerTestDebugType=embedded");
            Assert.Equal(0, CountProperties("release"));

            Assert.Contains("Optimized full, minimal, model, custom-base, and helper templates rendered successfully.",
                await Run(AssemblyPath("release")));
        }
        finally
        {
            Directory.Delete(artifacts, recursive: true);
        }

        string AssemblyPath(string configuration) => Path.Combine(artifacts, "bin", "OptimizerSample", configuration, "OptimizerSample.dll");

        int CountProperties(string configuration)
        {
            using var module = ModuleDefinition.ReadModule(AssemblyPath(configuration));
            var counts = Templates.Select(name =>
            {
                var template = module.Types.Single(t => t.FullName == "AspNetCoreGeneratedDocument." + name);
                Assert.Equal(template.Properties.Count, template.Fields.Count);
                return template.Properties.Count;
            }).ToArray();
            Assert.All(counts, count => Assert.Equal(counts[0], count));
            return counts[0];
        }

        async Task Build(string configuration, params string[] extra)
        {
            await Run(new[]
            {
                "build", project, "-c", configuration, "--disable-build-servers", "--nologo", "-v:q",
                "-p:ArtifactsPath=" + artifacts, "-p:OptimizerTestTargetFramework=" + framework
            }.Concat(extra).ToArray());
        }

        async Task<string> Run(params string[] arguments)
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo("dotnet")
                {
                    WorkingDirectory = root,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                }
            };
            foreach (var argument in arguments)
            {
                process.StartInfo.ArgumentList.Add(argument);
            }
            Assert.True(process.Start());
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                process.Kill(entireProcessTree: true);
                throw;
            }
            var output = await stdout + Environment.NewLine + await stderr;
            Assert.True(process.ExitCode == 0, output);
            return output;
        }
    }

    private static string FindRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "RazorSlices.slnx")))
            {
                return directory.FullName;
            }
        }
        throw new DirectoryNotFoundException("Could not locate the RazorSlices repository.");
    }
}
