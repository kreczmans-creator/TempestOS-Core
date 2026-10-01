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
    [InlineData("dotnet tool restore")]
    [InlineData("dotnet vpk pack")]
    [InlineData("--packId TempestOS")]
    [InlineData("TempestOS-win-Setup.exe")]
    [InlineData("TempestOS-$Version-Setup.exe")]
    public void PackageInstallerScript_NamesTheExpectedCommandsAndAssets(string expectedSubstring)
    {
        var content = File.ReadAllText(ScriptPath);

        Assert.Contains(expectedSubstring, content, StringComparison.Ordinal);
    }

    /// <summary>
    /// `v0.23.0` CI board G-07 (ADR-0160): <c>vpk</c> is pinned in the
    /// repository's tool manifest at the same version as the Velopack
    /// runtime package Tempest.Desktop references, and the script runs it
    /// only as <c>dotnet vpk</c>, so a different <c>vpk</c> on PATH can
    /// never package a release.
    /// </summary>
    [Fact]
    public void Vpk_IsPinnedInTheToolManifest_AtTheVelopackPackageVersion()
    {
        var manifestPath = Path.Combine(RepositoryPaths.RepositoryRoot, ".config", "dotnet-tools.json");
        using var manifest = System.Text.Json.JsonDocument.Parse(File.ReadAllText(manifestPath));
        var pinned = manifest.RootElement.GetProperty("tools").GetProperty("vpk").GetProperty("version").GetString();

        var csproj = File.ReadAllText(Path.Combine(RepositoryPaths.RepositoryRoot, "src", "Tempest.Desktop", "Tempest.Desktop.csproj"));
        var velopack = System.Text.RegularExpressions.Regex.Match(csproj, "<PackageReference Include=\"Velopack\" Version=\"([^\"]+)\"");
        Assert.True(velopack.Success, "Tempest.Desktop.csproj no longer references Velopack.");
        Assert.Equal(velopack.Groups[1].Value, pinned);

        var script = File.ReadAllText(ScriptPath);
        Assert.Contains($"$expectedVpkVersion = \"{pinned}\"", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Get-Command vpk", script, StringComparison.Ordinal);
        Assert.DoesNotContain("dotnet tool install", script, StringComparison.Ordinal);
    }

    [Fact]
    public void PackageInstallerScript_IsAsciiOnly()
    {
        var bytes = File.ReadAllBytes(ScriptPath);
        var offset = Array.FindIndex(bytes, b => b > 0x7F);

        Assert.True(offset < 0, $"'{ScriptPath}' contains a non-ASCII byte at offset {offset}; Windows PowerShell 5.1 reads a BOM-less script as ANSI.");
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
    internal static (int ExitCode, string Output) RunPowerShellSyntaxCheck(string scriptPath)
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
