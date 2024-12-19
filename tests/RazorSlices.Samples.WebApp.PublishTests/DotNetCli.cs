using System.Diagnostics;
using System.Runtime.InteropServices;
using Xunit.Abstractions;

namespace RazorSlices.Samples.WebApp.PublishTests;

public class DotNetCli
{
    private static readonly string _fileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "dotnet.exe" : "dotnet";
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(60);

    public static string Clean(IEnumerable<string> args)
    {
        return RunCommand("clean", args);
    }

    public static string Publish(IEnumerable<string> args, ITestOutputHelper? testOutput =  null)
    {
        return RunCommand("publish", args, testOutput);
    }

    private static string RunCommand(string commandName, IEnumerable<string> args, ITestOutputHelper? testOutput = null)
    {
        var process = new Process
        {
            StartInfo =
            {
                FileName = _fileName,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };

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

        process.WaitForExit();

        if (!process.WaitForExit(_timeout))
        {
            process.Kill();

            throw new InvalidOperationException($"dotnet {commandName} took longer than the allowed time of {_timeout}");
        }

        var stdOut = process.StandardOutput.ReadToEnd();

        if (process.ExitCode != 0)
        {
            var stdErr = process.StandardError.ReadToEnd();

            throw new InvalidOperationException($"""
                dotnet {commandName} failed on exit ({process.ExitCode})
                Error:  {stdErr}
                Output: {stdOut}
                """);
        }

        return stdOut;
    }
}
