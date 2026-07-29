namespace Tempest.Core.Tests.Templates;

// Locates the repository root from the test assembly's own runtime
// location (walking upward until global.json - the repo root marker
// Directory.Build.props itself relies on - is found), so template
// source files can be read directly from src/Templates/ without a
// hand-maintained relative path from the test output directory.
internal static class RepositoryPaths
{
    public static string RepositoryRoot { get; } = FindRepositoryRoot();

    public static string ModuleTemplateDirectory { get; } =
        Path.Combine(RepositoryRoot, "src", "Templates", "Tempest.Templates.Module");

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "global.json")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"Could not locate the repository root (global.json) above '{AppContext.BaseDirectory}'.");
    }
}
