using System.Diagnostics;
using System.Runtime.Loader;
using Mono.Cecil;

namespace RazorSlices.BuildTasks.Tests;

public class BuildIntegrationTests
{
    [Fact]
    public async Task OptimizationDefaultsAndOptOutWorkWithoutCleaning()
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

            var context = new AssemblyLoadContext("OptimizedSdkTemplate", isCollectible: true);
            try
            {
                using var stream = new MemoryStream(File.ReadAllBytes(AssemblyPath("release")));
                var assembly = context.LoadFromStream(stream);
                using var instance = (RazorSlice)Activator.CreateInstance(
                    assembly.GetType("AspNetCoreGeneratedDocument.Hello")!, nonPublic: true)!;
                Assert.Equal("Hello 42", (await instance.RenderAsync()).Trim());
            }
            finally
            {
                context.Unload();
            }
        }
        finally
        {
            Directory.Delete(artifacts, recursive: true);
        }

        string AssemblyPath(string configuration) => Path.Combine(artifacts, "bin", "OptimizerSample", configuration, "OptimizerSample.dll");

        int CountProperties(string configuration)
        {
            using var module = ModuleDefinition.ReadModule(AssemblyPath(configuration));
            var template = module.Types.Single(t => t.FullName == "AspNetCoreGeneratedDocument.Hello");
            Assert.Equal(template.Properties.Count, template.Fields.Count);
            return template.Properties.Count;
        }

        async Task Build(string configuration, params string[] extra)
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
            foreach (var argument in new[]
            {
                "build", project, "-c", configuration, "--disable-build-servers", "--nologo", "-v:q",
                "-p:ArtifactsPath=" + artifacts
            }.Concat(extra))
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
            Assert.True(process.ExitCode == 0, await stdout + Environment.NewLine + await stderr);
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
