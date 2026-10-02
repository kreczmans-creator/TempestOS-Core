using System.Text.Json;
using System.Text.RegularExpressions;
using Tempest.Core.Tests.Templates;

namespace Tempest.Core.Tests.ReleaseEngineering;

/// <summary>
/// ADR-0160 (PO decision 2026-10-01): every project in the solution commits
/// a <c>packages.lock.json</c>, every lock carries the win-x64 and
/// linux-x64 runtime sections Directory.Build.props declares (so a RID-less
/// restore, the installer's <c>dotnet publish -r win-x64</c> and the Linux
/// smoke job all match the same lock), and every <c>dotnet restore</c> in
/// the CI and release workflows runs with <c>--locked-mode</c>.
/// </summary>
public sealed partial class NuGetLockFileTests
{
    private static readonly string Root = RepositoryPaths.RepositoryRoot;

    public static TheoryData<string> SolutionProjects()
    {
        var data = new TheoryData<string>();
        var slnx = File.ReadAllText(Path.Combine(Root, "src", "TempestOS.slnx"));
        foreach (Match match in ProjectPathPattern().Matches(slnx))
        {
            data.Add(match.Groups[1].Value);
        }

        return data;
    }

    [Fact]
    public void DirectoryBuildProps_EnablesLockFiles_LockedModeOnCi_AndBothRuntimeIdentifiers()
    {
        var props = File.ReadAllText(Path.Combine(Root, "Directory.Build.props"));

        Assert.Contains("<RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>", props, StringComparison.Ordinal);
        Assert.Contains("<RestoreLockedMode Condition=\"'$(CI)' == 'true'\">true</RestoreLockedMode>", props, StringComparison.Ordinal);
        Assert.Contains("<RuntimeIdentifiers>win-x64;linux-x64</RuntimeIdentifiers>", props, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(SolutionProjects))]
    public void EverySolutionProject_CommitsALockFile_WithBothRuntimeSections(string relativeProjectPath)
    {
        var projectPath = Path.GetFullPath(Path.Combine(Root, "src", relativeProjectPath.Replace('/', Path.DirectorySeparatorChar)));
        var lockPath = Path.Combine(Path.GetDirectoryName(projectPath)!, "packages.lock.json");

        Assert.True(File.Exists(lockPath), $"'{lockPath}' is missing. Run `dotnet restore src/TempestOS.slnx --force-evaluate` and commit it.");

        using var lockFile = JsonDocument.Parse(File.ReadAllText(lockPath));
        var targets = lockFile.RootElement.GetProperty("dependencies").EnumerateObject().Select(p => p.Name).ToArray();
        Assert.Contains("net10.0", targets);
        Assert.Contains("net10.0/win-x64", targets);
        Assert.Contains("net10.0/linux-x64", targets);
    }

    [Theory]
    [InlineData("ci.yml")]
    [InlineData("release.yml")]
    public void EveryWorkflowRestore_IsLocked(string workflow)
    {
        var lines = File.ReadAllLines(Path.Combine(Root, ".github", "workflows", workflow));
        var restores = lines
            .Where(l => l.TrimStart().StartsWith("run: dotnet restore", StringComparison.Ordinal))
            .ToArray();

        Assert.NotEmpty(restores);
        Assert.All(restores, l => Assert.Contains("--locked-mode", l, StringComparison.Ordinal));
    }

    [GeneratedRegex("<Project Path=\"([^\"]+)\"")]
    private static partial Regex ProjectPathPattern();
}
