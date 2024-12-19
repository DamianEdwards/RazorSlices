using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Xunit.Abstractions;

namespace RazorSlices.Samples.WebApp.PublishTests;

public class ProjectBuilder
{
    private static readonly string _dotnetFileName = "dotnet" + (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : "");

    private PublishResult? _publishResult = null;
    private long? _appStarted = null;

    public ProjectBuilder(string projectName, PublishScenario scenario, ITestOutputHelper? testOutput = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(projectName);

        ProjectName = projectName;
        PublishScenario = scenario;
        TestOutput = testOutput;
    }

    public string ProjectName { get; }

    public PublishScenario PublishScenario { get; }

    public ITestOutputHelper? TestOutput { get; }

    public PublishResult? PublishResult => _publishResult;

    /// <summary>
    /// The size of the published app in bytes.
    /// </summary>
    public int? AppPublishSize { get; }

    /// <summary>
    /// The working set of the app before it shutdown.
    /// </summary>
    public int? AppMemorySize { get; }

    /// <summary>
    /// The app process.
    /// </summary>
    public Process? AppProcess { get; private set; }

    public void Publish(string framework = "net8.0")
    {
        var runId = Enum.GetName(PublishScenario);
        _publishResult = PublishScenario switch
        {
            PublishScenario.Default => PublishDefault(ProjectName, framework: framework, runId: runId, testOutput: TestOutput),
            PublishScenario.NoAppHost => Publish(ProjectName, framework: framework, useAppHost: false, runId: runId),
            PublishScenario.ReadyToRun => Publish(ProjectName, framework: framework, readyToRun: true, runId: runId),
            PublishScenario.SelfContained => Publish(ProjectName, framework: framework, selfContained: true, trimLevel: TrimLevel.None, runId: runId),
            PublishScenario.SelfContainedReadyToRun => Publish(ProjectName, framework: framework, selfContained: true, readyToRun: true, trimLevel: TrimLevel.None, runId: runId),
            PublishScenario.SingleFile => Publish(ProjectName, framework: framework, selfContained: true, singleFile: true, trimLevel: TrimLevel.None, runId: runId),
            PublishScenario.SingleFileCompressed => Publish(ProjectName, framework: framework, selfContained: true, singleFile: true, enableCompressionInSingleFile: true, trimLevel: TrimLevel.None, runId: runId),
            PublishScenario.SingleFileReadyToRun => Publish(ProjectName, framework: framework, selfContained: true, singleFile: true, readyToRun: true, trimLevel: TrimLevel.None, runId: runId),
            PublishScenario.SingleFileReadyToRunCompressed => Publish(ProjectName, framework: framework, selfContained: true, singleFile: true, enableCompressionInSingleFile: true, readyToRun: true, trimLevel: TrimLevel.None, runId: runId),
            PublishScenario.Trimmed => Publish(ProjectName, framework: framework, selfContained: true, singleFile: true, trimLevel: TrimLevel.Full, runId: runId),
            PublishScenario.TrimmedCompressed => Publish(ProjectName, framework: framework, selfContained: true, singleFile: true, enableCompressionInSingleFile: true, trimLevel: TrimLevel.Full, runId: runId),
            PublishScenario.TrimmedReadyToRun => Publish(ProjectName, framework: framework, selfContained: true, singleFile: true, readyToRun: true, trimLevel: TrimLevel.Full, runId: runId),
            PublishScenario.TrimmedReadyToRunCompressed => Publish(ProjectName, framework: framework, selfContained: true, singleFile: true, enableCompressionInSingleFile: true, readyToRun: true, trimLevel: TrimLevel.Full, runId: runId),
            PublishScenario.AOT => PublishAot(ProjectName, framework: framework, trimLevel: TrimLevel.Default, runId: runId),
            _ => throw new InvalidOperationException($"Unrecognized publish scenario '{PublishScenario}'")
        };
    }

    public void Run()
    {
        if (_publishResult is null)
        {
            throw new InvalidOperationException($"Project must be published first by calling '{nameof(Publish)}'.");
        }

        var appExePath = _publishResult.AppFilePath;
        if (!File.Exists(appExePath))
        {
            throw new ArgumentException($"Could not find application exe '{appExePath}'", nameof(appExePath));
        }

        var isAppHost = !Path.GetExtension(appExePath)!.Equals(".dll", StringComparison.OrdinalIgnoreCase);

        var process = new Process
        {
            StartInfo =
            {
                FileName = isAppHost ? appExePath : _dotnetFileName,
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(appExePath),
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };

        if (!isAppHost)
        {
            process.StartInfo.ArgumentList.Add(appExePath);
        }

        var envVars = GetEnvVars();
        foreach (var (name, value) in envVars)
        {
            process.StartInfo.Environment.Add(name, value);
        }

        if (!process.Start())
        {
            HandleError(process, "Failed to start application process");
        }

        _appStarted = DateTime.UtcNow.Ticks;

        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            HandleError(process, $"Application process failed on exit ({process.ExitCode})");
        }

        static void HandleError(Process process, string message)
        {
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();

            var sb = new StringBuilder();
            sb.AppendLine(message);
            sb.AppendLine("Standard output:");
            sb.AppendLine(output);
            sb.AppendLine("Standard error:");
            sb.AppendLine(error);

            throw new InvalidOperationException(sb.ToString());
        }

        AppProcess = process;
    }

    public void SaveOutput()
    {
        if (_publishResult is null || AppProcess is null)
        {
            throw new InvalidOperationException($"Project must be published first by calling '{nameof(Publish)}' and then run by calling '{nameof(Run)}'.");
        }

        var outputFilePath = Path.Combine(Path.GetDirectoryName(_publishResult.AppFilePath)!, "output.txt");
        using var resultStream = new FileStream(outputFilePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
        AppProcess.StandardOutput.BaseStream.CopyTo(resultStream);
    }

    private (string, string)[] GetEnvVars()
    {
        var result = new List<(string, string)> { ("SHUTDOWN_ON_START", "true") };

        if (_publishResult?.UserSecretsId is not null)
        {
            // Set env var for JWT signing key
            var userSecretsJsonPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Microsoft", "UserSecrets", _publishResult.UserSecretsId, "secrets.json");

            if (!File.Exists(userSecretsJsonPath))
            {
                throw new InvalidOperationException($"Could not find user secrets json file at path '{userSecretsJsonPath}'. " +
                    "Project has a UserSecretsId but has not been initialized for JWT authentication." +
                    "Please run 'dotnet user-jwts create' in the '$projectName' directory.");
            }

            var userSecretsJson = JsonDocument.Parse(File.OpenRead(userSecretsJsonPath));
            var configKeyName = "Authentication:Schemes:Bearer:SigningKeys";
            var jwtSigningKey = userSecretsJson.RootElement.GetProperty(configKeyName).EnumerateArray()
                .Single(o => o.GetProperty("Issuer").GetString() == "dotnet-user-jwts")
                .GetProperty("Value").GetString();

            if (jwtSigningKey is not null)
            {
                result.Add(("JWT_SIGNING_KEY", jwtSigningKey));
            }
        }

        if (ProjectName.Contains("Sqlite"))
        {
            result.Add(("CONNECTION_STRING", Environment.GetEnvironmentVariable("CONNECTION_STRING") ?? "Data Source=todos.db;Cache=Shared"));
            result.Add(("SUPPRESS_DB_INIT", "true"));
        }

        var projectPath = Path.Combine(PathHelper.ProjectsDir, ProjectName, ProjectName + ".csproj");
        var sdkName = GetSdkName(projectPath);
        if (sdkName?.Equals("Microsoft.NET.Sdk.Web", StringComparison.OrdinalIgnoreCase) == true)
        {
            result.Add(("ASPNETCORE_URLS", "http://localhost:5079"));
        }

        return [.. result];
    }

    private static PublishResult PublishDefault(
        string projectName,
        string configuration = "Release",
        string framework = "net8.0",
        string? runId = null,
        ITestOutputHelper? testOutput = null)
    {
        var args = new List<string>
        {
            "--runtime", RuntimeInformation.RuntimeIdentifier
        };

        return PublishImpl(projectName, configuration, framework, args, runId, testOutput);
    }

    private static PublishResult Publish(
        string projectName,
        string configuration = "Release",
        string framework = "net8.0",
        bool selfContained = false,
        bool singleFile = false,
        bool enableCompressionInSingleFile = false,
        bool readyToRun = false,
        bool useAppHost = true,
        TrimLevel trimLevel = TrimLevel.None,
        string? runId = null)
    {
        var args = new List<string>
        {
            "--runtime", RuntimeInformation.RuntimeIdentifier,
            selfContained || trimLevel != TrimLevel.None ? "--self-contained" : "--no-self-contained",
            $"-p:PublishSingleFile={(singleFile ? "true" : "false")}",
            $"-p:EnableCompressionInSingleFile={(enableCompressionInSingleFile ? "true" : "false")}",
            $"-p:PublishReadyToRun={(readyToRun ? "true" : "false")}",
            "-p:PublishIISAssets=false",
            "-p:PublishAot=false"
        };

        if (trimLevel != TrimLevel.None)
        {
            args.Add("-p:PublishTrimmed=true");
            args.Add($"-p:TrimMode={GetTrimLevelPropertyValue(trimLevel)}");
        }
        else
        {
            args.Add("-p:PublishTrimmed=false");
        }

        if (!useAppHost)
        {
            args.Add("-p:UseAppHost=false");
        }

        return PublishImpl(projectName, configuration, framework, args, runId);
    }

    private readonly static List<string> _projectsSupportingAot =
    [
        "HelloWorld.Console",
        "HelloWorld.Web",
        "HelloWorld.Web.Stripped",
        "HelloWorld.KestrelDirect",
        "HelloWorld.HttpListener",
        "TrimmedTodo.Console.ApiClient",
        "TrimmedTodo.Console.PostgreSQL"
    ];

    private static PublishResult PublishAot(
        string projectName,
        string configuration = "Release",
        string framework = "net8.0",
        TrimLevel trimLevel = TrimLevel.Default,
        string? runId = null)
    {
        //if (!_projectsSupportingAot.Contains(projectName))
        //{
        //    throw new NotSupportedException($"The project '{projectName}' does not support publishing for AOT.");
        //}

        if (trimLevel == TrimLevel.None)
        {
            throw new ArgumentOutOfRangeException(nameof(trimLevel), "'TrimLevel.None' is not supported when publishing for AOT.");
        }

        var args = new List<string>
        {
            "--runtime", RuntimeInformation.RuntimeIdentifier,
            "-p:PublishIISAssets=false",
            "-p:PublishAot=true",
            "-p:PublishSingleFile=",
            "-p:PublishTrimmed="
        };

        if (trimLevel != TrimLevel.None)
        {
            args.Add($"-p:TrimMode={GetTrimLevelPropertyValue(trimLevel)}");
        }

        return PublishImpl(projectName, configuration, framework, args, runId);
    }

    private static PublishResult PublishImpl(string projectName, string configuration = "Release", string framework = "net8.0", IEnumerable<string>? args = null, string? runId = null, ITestOutputHelper? testOutput = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(projectName);

        var projectPath = Path.Combine(PathHelper.ProjectsDir, projectName, projectName + ".csproj");

        if (!File.Exists(projectPath))
        {
            throw new ArgumentException($"Project at '{projectPath}' could not be found", nameof(projectName));
        }

        runId ??= Random.Shared.NextInt64().ToString();
        var outputDir = PathHelper.GetProjectPublishDir(projectName, framework, runId);

        var cmdArgs = new List<string>
        {
            projectPath,
            "--framework", framework,
            "--configuration", configuration
        };

        //DotNetCli.Clean(cmdArgs);

        cmdArgs.AddRange([$"--output", outputDir]);
        cmdArgs.Add("--disable-build-servers");
        if (args is not null)
        {
            cmdArgs.AddRange(args);
        }

        var publishOutput = DotNetCli.Publish(cmdArgs, testOutput);

        testOutput?.WriteLine(publishOutput);

        var appFilePath = Path.Join(outputDir, projectName);
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            appFilePath += ".exe";
        }

        if (!File.Exists(appFilePath))
        {
            appFilePath = Path.Join(outputDir, projectName + ".dll");
            if (!File.Exists(appFilePath))
            {
                throw new InvalidOperationException($"Could not find application exe or dll '{appFilePath}'");
            }
        }

        if (projectName.Contains("sqlite", StringComparison.OrdinalIgnoreCase))
        {
            var dbFileName = "todos.db";
            var dbFileSrc = Path.Combine(PathHelper.RepoRoot, "scripts", dbFileName);
            File.Copy(dbFileSrc, Path.Join(outputDir, dbFileName));
        }

        return new(appFilePath, publishOutput, GetUserSecretsId(projectPath));
    }

    private static string? GetUserSecretsId(string projectFilePath)
    {
        var xml = XDocument.Load(projectFilePath);
        var userSecretsIdElement = xml.Descendants("UserSecretsId").FirstOrDefault();
        return userSecretsIdElement?.Value;
    }

    private static string? GetSdkName(string projectFilePath)
    {
        var xml = XDocument.Load(projectFilePath);
        var sdkNameElement = xml.Descendants("Project").First();
        return sdkNameElement.Attribute(XName.Get("Sdk"))?.Value;
    }

    private static string GetTrimLevelPropertyValue(TrimLevel trimLevel)
    {
        return trimLevel switch
        {
            TrimLevel.Default => "",
            _ => Enum.GetName(trimLevel)?.ToLower() ?? ""
        };
    }

    private static TrimLevel GetTrimLevel(string projectName)
    {
        if (projectName.Equals("TrimmedTodo.Console.EfCore.Sqlite")
            || projectName.Equals("TrimmedTodo.Console.Sqlite")
            || projectName.Equals("TrimmedTodo.Console.PostgreSQL"))
        {
            return TrimLevel.Full;
        }

        if (projectName.Contains("EfCore", StringComparison.OrdinalIgnoreCase)
            || projectName.Contains("Dapper", StringComparison.OrdinalIgnoreCase)
            || projectName.Contains("MinimalApi.Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            return TrimLevel.Partial;
        }

        if (projectName.Contains("Console"))
        {
            return TrimLevel.Default;
        }

        if (projectName.Contains("HelloWorld"))
        {
            return TrimLevel.Full;
        }

        return TrimLevel.Default;
    }
}

public record PublishResult(string AppFilePath, string Output, string? UserSecretsId = null);

public enum PublishScenario
{
    Default,
    NoAppHost,
    ReadyToRun,
    SelfContained,
    SelfContainedReadyToRun,
    SingleFile,
    SingleFileCompressed,
    SingleFileReadyToRun,
    SingleFileReadyToRunCompressed,
    Trimmed,
    TrimmedCompressed,
    TrimmedReadyToRun,
    TrimmedReadyToRunCompressed,
    AOT
}

enum TrimLevel
{
    None,
    Default,
    Partial,
    Full
}
