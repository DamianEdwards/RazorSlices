using System.IO.Compression;
using System.Xml.Linq;
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
        Assert.DoesNotContain(entries, static entry => entry.EndsWith("Microsoft.AspNetCore.Razor.Language.dll", StringComparison.OrdinalIgnoreCase));

        using var manifest = archive.Entries.Single(e => e.FullName.EndsWith(".nuspec", StringComparison.Ordinal)).Open();
        var version = XDocument.Load(manifest).Descendants().Single(e => e.Name.LocalName == "version").Value;
        File.WriteAllText(Path.Combine(outputDir, "Directory.Build.props"), "<Project />");
        new XDocument(new XElement("configuration",
            new XElement("packageSources", new XElement("clear"),
                new XElement("add", new XAttribute("key", "local"), new XAttribute("value", outputDir))),
            new XElement("packageSourceMapping", new XElement("clear"),
                new XElement("packageSource", new XAttribute("key", "local"),
                    new XElement("package", new XAttribute("pattern", "*"))))))
            .Save(Path.Combine(outputDir, "NuGet.Config"));
        var packages = System.Security.SecurityElement.Escape(Path.Combine(outputDir, "packages"));
        var producer = Path.Combine(outputDir, "producer");
        Directory.CreateDirectory(producer);
        var producerProject = Path.Combine(producer, "SharedHelpers.csproj");
        File.WriteAllText(producerProject, $$"""
            <Project Sdk="Microsoft.NET.Sdk.Razor">
              <PropertyGroup>
                <TargetFramework>net8.0</TargetFramework>
                <LangVersion>14.0</LangVersion>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
                <AddRazorSupportForMvc>true</AddRazorSupportForMvc>
                <ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>
                <RestorePackagesPath>{{packages}}</RestorePackagesPath>
              </PropertyGroup>
              <ItemGroup>
                <FrameworkReference Include="Microsoft.AspNetCore.App" />
                <PackageReference Include="RazorSlices" Version="{{version}}" />
              </ItemGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(producer, "IUi.cs"), """
            namespace SharedHelpers;
            public interface IUi
            {
                void Greeting(string text, int count = 42);
                ValueTask GreetingAsync(string text, int count);
                Task TaskGreetingAsync(string text);
                void Values<T>(T value, params string[] rest) where T : IFormattable;
            }
            """);
        File.WriteAllText(Path.Combine(producer, "Ui.cshtml"), """
            @using RazorSlices
            @using SharedHelpers
            @inherits RazorSliceHelpers
            @implements IUi
            @functions {
                public void Greeting(string text, int count = 42) { <p>@text: @count</p> }
                public async ValueTask GreetingAsync(string text, int count) {
                    await Task.Yield();
                    <p>async @text: @count</p>
                }
                public Task TaskGreetingAsync(string text) {
                    <p>task @text</p>
                    return Task.CompletedTask;
                }
                public void Values<T>(T value, params string[] rest) where T : IFormattable {
                    <p>@value @rest.Length</p>
                }
            }
            """);
        DotNetCli.Restore([producerProject], testOutput);
        DotNetCli.Pack(producerProject, outputDir, testOutput);
        using (var package = ZipFile.OpenRead(Path.Combine(outputDir, "SharedHelpers.1.0.0.nupkg")))
        {
            Assert.DoesNotContain(package.Entries, static entry => entry.FullName.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase));
        }
        var consumer = Path.Combine(outputDir, "consumer");
        Directory.CreateDirectory(consumer);
        var consumerProject = Path.Combine(consumer, "Consumer.csproj");
        File.WriteAllText(consumerProject, $$"""
            <Project Sdk="Microsoft.NET.Sdk.Razor">
              <PropertyGroup>
                <TargetFramework>net8.0</TargetFramework>
                <LangVersion>14.0</LangVersion>
                <OutputType>Exe</OutputType>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
                <AddRazorSupportForMvc>true</AddRazorSupportForMvc>
                <ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>
                <RestorePackagesPath>{{packages}}</RestorePackagesPath>
              </PropertyGroup>
              <ItemGroup>
                <FrameworkReference Include="Microsoft.AspNetCore.App" />
                <PackageReference Include="RazorSlices" Version="{{version}}" />
                <PackageReference Include="SharedHelpers" Version="1.0.0" />
              </ItemGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(consumer, "Host.cshtml"), """
            @using RazorSlices
            @using SharedHelpers
            @inherits RazorSlice
            @inject IHelperManager Helpers
            @inject ISliceProbe Probe
            @inject IOptionalProbe? Optional
            @{ if (!Probe.Active || Optional is not null) { throw new InvalidOperationException("Generated binding failed"); } }
            @Helpers.Ui.Greeting("<package>")
            @await Helpers.Ui.GreetingAsync("loaded", 43)
            @await Helpers.Ui.TaskGreetingAsync("task")
            @Helpers.Ui.Values(42, "one", "two")
            """);
        File.WriteAllText(Path.Combine(consumer, "Program.cs"), """
            using RazorSlices;
            using SharedHelpers;
            using Microsoft.Extensions.DependencyInjection;
            using var services = new ServiceCollection().AddRazorSliceHelpers()
                .AddScoped<ISliceProbe, SliceProbe>().BuildServiceProvider();
            using var scope = services.CreateScope();
            var host = Consumer.Host.Create();
            host.ServiceProvider = scope.ServiceProvider;
            var html = await host.RenderAsync();
            if (scope.ServiceProvider.GetRequiredService<ISliceProbe>().Active)
            {
                throw new InvalidOperationException("Generated cleanup failed");
            }
            if (!html.Contains("<p>&lt;package&gt;: 42</p>") ||
                !html.Contains("async loaded: 43") || !html.Contains("task task") || !html.Contains("42 2"))
            {
                throw new InvalidOperationException(html);
            }

            var allocationSlice = (AllocationSlice)RazorSliceFactory.Create(static () => new AllocationSlice());
            allocationSlice.ServiceProvider = scope.ServiceProvider;
            await allocationSlice.RenderAsync(TextWriter.Null);
            if (allocationSlice.AllocatedBytes != 0)
            {
                throw new InvalidOperationException($"Facade calls allocated {allocationSlice.AllocatedBytes} bytes");
            }

            Console.WriteLine("Shared SDK helper rendered with zero warm per-call allocations");

            public interface ISliceProbe : IRazorSliceBindable
            {
                bool Active { get; }
            }
            public interface IOptionalProbe : IRazorSliceBindable { }
            public sealed class SliceProbe : ISliceProbe
            {
                private int _depth;
                public bool Active => _depth > 0;
                void IRazorSliceBindable.Bind(RazorSlice slice) { _depth++; }
                void IRazorSliceBindable.Unbind(RazorSlice slice) { _depth--; }
            }

            sealed class AllocationSlice : RazorSlice
            {
                [Microsoft.AspNetCore.Mvc.Razor.Internal.RazorInject]
                public IHelperManager Helpers { get; set; } = null!;
                public long AllocatedBytes;

                public override async Task ExecuteAsync()
                {
                    for (var i = 0; i < 10_000; i++)
                    {
                        Helpers.Ui.Greeting("warmup", 42);
                        await Helpers.Ui.TaskGreetingAsync("warmup");
                    }

                    var before = GC.GetAllocatedBytesForCurrentThread();
                    for (var i = 0; i < 100_000; i++)
                    {
                        Helpers.Ui.Greeting("measured", 42);
                        await Helpers.Ui.TaskGreetingAsync("measured");
                    }

                    AllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
                }
            }
            """);
        var result = DotNetCli.Run(["--project", consumerProject, "--configuration", "Release"], testOutput);
        Assert.Contains("Shared SDK helper rendered with zero warm per-call allocations", result);
    }
}
