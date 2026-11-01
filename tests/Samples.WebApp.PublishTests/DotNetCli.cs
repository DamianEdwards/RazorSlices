using System.Diagnostics;
using System.Runtime.InteropServices;
using Xunit.Abstractions;

namespace RazorSlices.Samples.WebApp.PublishTests;

public class DotNetCli
{
    private static readonly string _fileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "dotnet.exe" : "dotnet";
    private static readonly TimeSpan _timeout = TimeSpan.FromMinutes(5);

    public static string Clean(IEnumerable<string> args)
    {
        return RunCommand("clean", args);
    }

    public static string Publish(IEnumerable<string> args, ITestOutputHelper? testOutput = null, string? workingDirectory = null)
    {
        return RunCommand("publish", args, testOutput, workingDirectory);
    }

    public static string Run(IEnumerable<string> args, ITestOutputHelper? testOutput = null, string? workingDirectory = null)
    {
        return RunCommand("run", args, testOutput, workingDirectory);
    }

    public static string Restore(IEnumerable<string> args, ITestOutputHelper? testOutput = null, string? workingDirectory = null)
    {
        return RunCommand("restore", args.Append("--disable-build-servers"), testOutput, workingDirectory);
    }

    public static string Pack(string projectPath, string outputDir, ITestOutputHelper? testOutput = null, params string[] additionalArguments)
    {
        var args = new List<string>
        {
            projectPath,
            "--configuration", "Release",
            "--output", outputDir,
            "--no-restore",
            "--disable-build-servers"
        };
        args.AddRange(additionalArguments);
        return RunCommand("pack", args, testOutput);
    }

    internal static string RunCommand(string commandName, IEnumerable<string> args, ITestOutputHelper? testOutput = null, string? workingDirectory = null)
    {
        var process = new Process
        {
            StartInfo =
            {
                FileName = _fileName,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };
        if (workingDirectory is not null)
        {
            process.StartInfo.WorkingDirectory = workingDirectory;
            // Let the fixture's global.json select its SDK instead of inheriting the test runner's SDK.
            foreach (var variable in new[] { "MSBuildSDKsPath", "MSBUILD_EXE_PATH", "MSBuildExtensionsPath", "MSBuildToolsPath", "MSBUILD_NUGET_PATH", "DOTNET_MSBUILD_SDK_RESOLVER_SDKS_DIR", "DOTNET_MSBUILD_SDK_RESOLVER_SDKS_VER" })
            {
                process.StartInfo.Environment.Remove(variable);
            }
        }
        process.StartInfo.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        process.StartInfo.Environment["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0";
        process.StartInfo.Environment["MSBUILDTERMINALLOGGER"] = "off";

        process.StartInfo.ArgumentList.Add(commandName);
        foreach (var arg in args)
        {
            process.StartInfo.ArgumentList.Add(arg);
        }

        var cmdLine = $"{process.StartInfo.FileName} {string.Join(' ', process.StartInfo.ArgumentList)}";
        testOutput?.WriteLine("Running dotnet CLI with cmd line:");
        testOutput?.WriteLine(cmdLine);
        testOutput?.WriteLine("");

        if (!process.Start())
        {
            throw new InvalidOperationException($"dotnet {commandName} failed");
        }

        var stdOutTask = process.StandardOutput.ReadToEndAsync();
        var stdErrTask = process.StandardError.ReadToEndAsync();

        if (!process.WaitForExit(_timeout))
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();

            throw new InvalidOperationException($"dotnet {commandName} took longer than the allowed time of {_timeout}");
        }

        var stdOut = stdOutTask.WaitAsync(_timeout).GetAwaiter().GetResult();
        var stdErr = stdErrTask.WaitAsync(_timeout).GetAwaiter().GetResult();

        return process.ExitCode != 0
            ? throw new InvalidOperationException($"""
                dotnet {commandName} failed on exit ({process.ExitCode})
                Error:  {stdErr}
                Output: {stdOut}
                """)
            : stdOut;
    }
}
