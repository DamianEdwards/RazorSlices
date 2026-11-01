using System.Diagnostics;
using System.Collections.Concurrent;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
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

    public void Publish(string framework = "net10.0", bool runtimeAsync = false)
    {
        if (runtimeAsync && (framework != "net11.0" || PublishScenario is not (PublishScenario.Default or PublishScenario.Trimmed or PublishScenario.AOT)))
        {
            throw new NotSupportedException("Runtime Async publish coverage requires net11.0 and a default, trimmed, or AOT scenario.");
        }
        var runId = Enum.GetName(PublishScenario);
        _publishResult = PublishScenario switch
        {
            PublishScenario.Default => PublishDefault(ProjectName, framework: framework, runId: runId, testOutput: TestOutput, runtimeAsync: runtimeAsync),
            PublishScenario.NoAppHost => Publish(ProjectName, framework: framework, useAppHost: false, runId: runId),
            PublishScenario.ReadyToRun => Publish(ProjectName, framework: framework, readyToRun: true, runId: runId),
            PublishScenario.SelfContained => Publish(ProjectName, framework: framework, selfContained: true, trimLevel: TrimLevel.None, runId: runId),
            PublishScenario.SelfContainedReadyToRun => Publish(ProjectName, framework: framework, selfContained: true, readyToRun: true, trimLevel: TrimLevel.None, runId: runId),
            PublishScenario.SingleFile => Publish(ProjectName, framework: framework, selfContained: true, singleFile: true, trimLevel: TrimLevel.None, runId: runId),
            PublishScenario.SingleFileCompressed => Publish(ProjectName, framework: framework, selfContained: true, singleFile: true, enableCompressionInSingleFile: true, trimLevel: TrimLevel.None, runId: runId),
            PublishScenario.SingleFileReadyToRun => Publish(ProjectName, framework: framework, selfContained: true, singleFile: true, readyToRun: true, trimLevel: TrimLevel.None, runId: runId),
            PublishScenario.SingleFileReadyToRunCompressed => Publish(ProjectName, framework: framework, selfContained: true, singleFile: true, enableCompressionInSingleFile: true, readyToRun: true, trimLevel: TrimLevel.None, runId: runId),
            PublishScenario.Trimmed => Publish(ProjectName, framework: framework, selfContained: true, singleFile: true, trimLevel: TrimLevel.Full, runId: runId, runtimeAsync: runtimeAsync),
            PublishScenario.TrimmedCompressed => Publish(ProjectName, framework: framework, selfContained: true, singleFile: true, enableCompressionInSingleFile: true, trimLevel: TrimLevel.Full, runId: runId),
            PublishScenario.TrimmedReadyToRun => Publish(ProjectName, framework: framework, selfContained: true, singleFile: true, readyToRun: true, trimLevel: TrimLevel.Full, runId: runId),
            PublishScenario.TrimmedReadyToRunCompressed => Publish(ProjectName, framework: framework, selfContained: true, singleFile: true, enableCompressionInSingleFile: true, readyToRun: true, trimLevel: TrimLevel.Full, runId: runId),
            PublishScenario.AOT => PublishAot(ProjectName, framework: framework, trimLevel: TrimLevel.Default, runId: runId, runtimeAsync: runtimeAsync),
            _ => throw new InvalidOperationException($"Unrecognized publish scenario '{PublishScenario}'")
        };
    }

    public async Task VerifyEndpointsAsync(string framework)
    {
        var published = _publishResult ?? throw new InvalidOperationException("Publish the application before verifying endpoints.");
        using var process = new Process
        {
            StartInfo =
            {
                FileName = published.AppFilePath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ? _dotnetFileName : published.AppFilePath,
                WorkingDirectory = Path.GetDirectoryName(published.AppFilePath),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };
        if (published.AppFilePath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            process.StartInfo.ArgumentList.Add(published.AppFilePath);
        }
        process.StartInfo.Environment["ASPNETCORE_URLS"] = "http://127.0.0.1:0";
        var listening = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var output = new ConcurrentQueue<string>();
        process.OutputDataReceived += (_, args) =>
        {
            if (args.Data is not { } line) { return; }
            output.Enqueue(line);
            const string marker = "Now listening on: ";
            var index = line.IndexOf(marker, StringComparison.Ordinal);
            if (index >= 0) { listening.TrySetResult(line[(index + marker.Length)..].Trim()); }
        };
        process.ErrorDataReceived += (_, args) =>
        {
            if (args.Data is { } line) { output.Enqueue(line); }
        };
        if (!process.Start()) { throw new InvalidOperationException("Failed to start published application."); }
        try
        {
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            var exited = process.WaitForExitAsync();
            var ready = await Task.WhenAny(listening.Task, exited).WaitAsync(TimeSpan.FromSeconds(30));
            if (ready == exited)
            {
                throw new InvalidOperationException($"Published application exited before listening: {string.Join(Environment.NewLine, output)}");
            }
            using var handler = new HttpClientHandler { AllowAutoRedirect = false };
            using var client = new HttpClient(handler) { BaseAddress = new Uri(await listening.Task), Timeout = TimeSpan.FromSeconds(30) };
            var endpoints = ProjectName == "WebApp"
                ? new[] { ("/", "Todos"), ("/library", "Razor Class Library"), ("/helpers", "Reusable todo helpers"), ("/minimal", "&lt;minimal&gt;"), ("/nested", "Nested : Test"), ("/templated", "templated"), ("/tag-helpers", "<"), ("/htmx-todo", "Todos using HTMX") }
                : new[] { ("/", "Hello"), ("/goodbye", "Goodbye") };
            foreach (var (path, expected) in endpoints)
            {
                var html = await client.GetStringAsync(path);
                if (!html.Contains(expected, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"Published endpoint {path} did not contain '{expected}': {html}");
                }
            }
            if (ProjectName == "WebApp")
            {
                var html = await client.GetStringAsync("/tempdata");
                var token = Regex.Match(html, """name="__RequestVerificationToken" value="([^"]+)""");
                var fields = new List<KeyValuePair<string, string>> { new("message", "Published <flash>") };
                if (framework == "net10.0")
                {
                    Assert.True(token.Success, "The .NET 10 sample must render an antiforgery token.");
                    fields.Add(new("__RequestVerificationToken", WebUtility.HtmlDecode(token.Groups[1].Value)));
                }
                else
                {
                    Assert.False(token.Success, "The .NET 11 sample must not render an antiforgery token.");
                }
                using var request = new HttpRequestMessage(HttpMethod.Post, "/tempdata")
                {
                    Content = new FormUrlEncodedContent(fields)
                };
                request.Headers.Add("Sec-Fetch-Site", "same-origin");
                using var response = await client.SendAsync(request);
                Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
                Assert.Contains("Published &lt;flash&gt;", await client.GetStringAsync("/tempdata"));

                using var untrustedRequest = new HttpRequestMessage(HttpMethod.Post, "/tempdata")
                {
                    Content = new FormUrlEncodedContent([new("message", "must not reach handler")])
                };
                untrustedRequest.Headers.Add("Sec-Fetch-Site", "cross-site");
                untrustedRequest.Headers.Add("Origin", "https://attacker.example");
                using var untrustedResponse = await client.SendAsync(untrustedRequest);
                Assert.Equal(HttpStatusCode.BadRequest, untrustedResponse.StatusCode);
                Assert.Contains("No flash message.", await client.GetStringAsync("/tempdata"));

                await VerifyHtmxTodosAsync(client, framework);
            }
        }
        finally
        {
            if (!process.HasExited) { process.Kill(entireProcessTree: true); }
            await process.WaitForExitAsync();
        }
    }

    private static async Task VerifyHtmxTodosAsync(HttpClient client, string framework)
    {
        var page = await client.GetStringAsync("/htmx-todo");
        var token = Regex.Match(page, """name="__RequestVerificationToken" value="([^"]+)""");
        Assert.Equal(framework == "net10.0", token.Success);
        var formFields = new List<KeyValuePair<string, string>>();
        if (token.Success)
        {
            formFields.Add(new("__RequestVerificationToken", WebUtility.HtmlDecode(token.Groups[1].Value)));
        }
        using var created = await MutateAsync(HttpMethod.Post, "/htmx-todo", [new("title", "Published <todo>")]);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var row = await created.Content.ReadAsStringAsync();
        Assert.Contains("Published &lt;todo&gt;", row);
        var id = Regex.Match(row, """id="todo-(\d+)""");
        Assert.True(id.Success);
        var path = $"/htmx-todo/{id.Groups[1].Value}";

        using var updated = await MutateAsync(HttpMethod.Put, path, [new("isComplete", "true")]);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Contains("<td>Complete</td>", await updated.Content.ReadAsStringAsync());
        using var deleted = await MutateAsync(HttpMethod.Delete, path, []);
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        Assert.Equal(string.Empty, await deleted.Content.ReadAsStringAsync());
        Assert.DoesNotContain($"id=\"todo-{id.Groups[1].Value}\"", await client.GetStringAsync("/htmx-todo"));

        var beforeRejection = await client.GetStringAsync("/htmx-todo");
        foreach (var method in new[] { HttpMethod.Post, HttpMethod.Put, HttpMethod.Delete })
        {
            using var request = new HttpRequestMessage(method, method == HttpMethod.Post ? "/htmx-todo" : "/htmx-todo/2")
            {
                Content = new FormUrlEncodedContent([new("title", "Must not be stored"), new("isComplete", "true")])
            };
            request.Headers.Add("Sec-Fetch-Site", "cross-site");
            request.Headers.Add("Origin", "https://attacker.example");
            using var rejected = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        }
        var afterRejection = await client.GetStringAsync("/htmx-todo");
        Assert.Equal(TodoRows(beforeRejection), TodoRows(afterRejection));

        async Task<HttpResponseMessage> MutateAsync(HttpMethod method, string path, IEnumerable<KeyValuePair<string, string>> values)
        {
            using var request = new HttpRequestMessage(method, path)
            {
                Content = new FormUrlEncodedContent(formFields.Concat(values))
            };
            request.Headers.Add("Sec-Fetch-Site", "same-origin");
            return await client.SendAsync(request);
        }

        static string TodoRows(string html)
            => Regex.Match(html, """<tbody id="todo-table">.*?</tbody>""", RegexOptions.Singleline).Value;
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
        string framework = "net10.0",
        string? runId = null,
        ITestOutputHelper? testOutput = null,
        bool runtimeAsync = false)
    {
        var args = new List<string>
        {
            "--runtime", RuntimeInformation.RuntimeIdentifier,
            "-p:PublishAot=false",
            "-p:PublishTrimmed=false"
        };

        return PublishImpl(projectName, configuration, framework, args, runId, testOutput, runtimeAsync);
    }

    private static PublishResult Publish(
        string projectName,
        string configuration = "Release",
        string framework = "net10.0",
        bool selfContained = false,
        bool singleFile = false,
        bool enableCompressionInSingleFile = false,
        bool readyToRun = false,
        bool useAppHost = true,
        TrimLevel trimLevel = TrimLevel.None,
        string? runId = null,
        bool runtimeAsync = false)
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

        return PublishImpl(projectName, configuration, framework, args, runId, runtimeAsync: runtimeAsync);
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
        string framework = "net10.0",
        TrimLevel trimLevel = TrimLevel.Default,
        string? runId = null,
        bool runtimeAsync = false)
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
            "-p:PublishSingleFile=false",
            "-p:PublishTrimmed=true"
        };

        if (trimLevel is not (TrimLevel.None or TrimLevel.Default))
        {
            args.Add($"-p:TrimMode={GetTrimLevelPropertyValue(trimLevel)}");
        }

        return PublishImpl(projectName, configuration, framework, args, runId, runtimeAsync: runtimeAsync);
    }

    private static PublishResult PublishImpl(string projectName, string configuration = "Release", string framework = "net10.0", IEnumerable<string>? args = null, string? runId = null, ITestOutputHelper? testOutput = null, bool runtimeAsync = false)
    {
        ArgumentException.ThrowIfNullOrEmpty(projectName);

        var projectPath = Path.Combine(PathHelper.ProjectsDir, projectName, projectName + ".csproj");

        if (!File.Exists(projectPath))
        {
            throw new ArgumentException($"Project at '{projectPath}' could not be found", nameof(projectName));
        }

        runId ??= Random.Shared.NextInt64().ToString();
        if (runtimeAsync) { runId += "-runtime-async"; }
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
        if (runtimeAsync)
        {
            cmdArgs.Add("-p:RazorSlicesTestRuntimeAsync=true");
            cmdArgs.Add("-p:ArtifactsPath=" + Path.Combine(outputDir, "build-artifacts"));
        }
        if (args is not null)
        {
            cmdArgs.AddRange(args);
        }

        var publishOutput = DotNetCli.Publish(cmdArgs, testOutput);

        testOutput?.WriteLine(publishOutput);

        var assemblyName = GetAssemblyName(projectPath) ?? projectName;
        var appFilePath = Path.Join(outputDir, assemblyName);
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            appFilePath += ".exe";
        }

        if (!File.Exists(appFilePath))
        {
            appFilePath = Path.Join(outputDir, assemblyName + ".dll");
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

    private static string? GetAssemblyName(string projectFilePath)
    {
        var xml = XDocument.Load(projectFilePath);
        var assemblyNameElement = xml.Descendants("AssemblyName").FirstOrDefault();
        return assemblyNameElement?.Value;
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
