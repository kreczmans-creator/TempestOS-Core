using System.Diagnostics;
using Tempest.Core.Tests.Templates;

namespace Tempest.Core.Tests.ReleaseEngineering;

/// <summary>
/// `scripts/package-installer.ps1`'s own claims (`WP 21.5A`, `WP RC.0A`
/// scope item 1) — validated without `act` or any other GitHub-Actions-in-
/// a-container tool, per this Work Package's own brief: the script is
/// syntactically valid PowerShell (parsed by the real PowerShell parser,
/// the same one that will run it in CI), and it names the assets
/// <c>release.yml</c> is contracted to publish. Running the script itself
/// (a real self-contained publish plus a real <c>vpk pack</c>, several
/// minutes and ~120 MB of output) is exercised once, by hand, per this
/// Work Package's own report — not on every `dotnet test` run, which
/// mirrors how `scripts/governance-healthcheck.ps1` and
/// `scripts/new-release.ps1` are already treated: real scripts this suite
/// checks the shape of, never re-runs wholesale in-process.
/// </summary>
public sealed class PackagingScriptTests
{
    private static readonly string ScriptPath = Path.Combine(RepositoryPaths.RepositoryRoot, "scripts", "package-installer.ps1");

    [Fact]
    public void PackageInstallerScript_Exists()
    {
        Assert.True(File.Exists(ScriptPath), $"Expected to find '{ScriptPath}'.");
    }

    [Fact]
    public void PackageInstallerScript_IsSyntacticallyValidPowerShell()
    {
        var (exitCode, output) = RunPowerShellSyntaxCheck(ScriptPath);

        Assert.True(exitCode == 0, $"PowerShell syntax check failed for '{ScriptPath}':{Environment.NewLine}{output}");
    }

    [Theory]
    [InlineData("dotnet publish")]
    [InlineData("--runtime win-x64")]
    [InlineData("--self-contained true")]
    [InlineData("Tempest.Desktop.exe")]
    [InlineData("vpk pack")]
    [InlineData("--packId TempestOS")]
    [InlineData("TempestOS-win-Setup.exe")]
    [InlineData("TempestOS-$Version-Setup.exe")]
    public void PackageInstallerScript_NamesTheExpectedCommandsAndAssets(string expectedSubstring)
    {
        var content = File.ReadAllText(ScriptPath);

        Assert.Contains(expectedSubstring, content, StringComparison.Ordinal);
    }

    /// <summary>
    /// Runs the script's own text through PowerShell's real language parser
    /// (<c>System.Management.Automation.Language.Parser</c>), out-of-process
    /// via <c>powershell.exe</c> — the same interpreter <c>release.yml</c>'s
    /// own <c>shell: pwsh</c> steps and this repository's own
    /// <c>scripts/governance-healthcheck.ps1</c> gate step already run
    /// under — rather than adding a <c>System.Management.Automation</c>
    /// package reference this Work Package's brief does not name, purely
    /// to parse one file in-process.
    /// </summary>
    private static (int ExitCode, string Output) RunPowerShellSyntaxCheck(string scriptPath)
    {
        var command =
            "$parseErrors = $null; " +
            $"[System.Management.Automation.Language.Parser]::ParseFile('{scriptPath.Replace("'", "''")}', [ref]$null, [ref]$parseErrors) | Out-Null; " +
            "if ($parseErrors.Count -gt 0) { $parseErrors | ForEach-Object { Write-Output $_.Message }; exit 1 } else { exit 0 }";

        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell",
            ArgumentList = { "-NoProfile", "-NonInteractive", "-Command", command },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start powershell.");
        var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();

        return (process.ExitCode, output);
    }
}
