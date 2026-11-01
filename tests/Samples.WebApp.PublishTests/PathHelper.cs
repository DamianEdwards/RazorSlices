namespace RazorSlices.Samples.WebApp.PublishTests;

public class PathHelper
{
    public static string RepoRoot { get; } = GetRepoRoot();
    public static string ProjectsDir { get; } = Path.Combine(RepoRoot, "samples");
    public static string ArtifactsDir { get; } = Path.Combine(RepoRoot, "artifacts");

    public static string GetProjectPublishDir(string projectName, string framework, string? runId)
    {
        return Path.Combine(ArtifactsDir, "TestOutput", Path.GetRandomFileName(), projectName, framework, runId ?? "");
    }

    private static string GetRepoRoot()
    {
        var currentDir = new DirectoryInfo(Directory.GetCurrentDirectory());
        DirectoryInfo? repoDir = null;

        while (true)
        {
            if (currentDir is null)
            {
                // We hit the file system root
                break;
            }

            if (File.Exists(Path.Join(currentDir.FullName, "RazorSlices.slnx")))
            {
                // We're in the repo root
                repoDir = currentDir;
                break;
            }

            currentDir = currentDir.Parent;
        }

        return repoDir is null ? throw new InvalidOperationException("Couldn't find repo directory") : repoDir.FullName;
    }
}
