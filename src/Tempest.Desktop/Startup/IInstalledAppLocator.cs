namespace Tempest.Desktop.Startup;

/// <summary>
/// Answers exactly two questions <see cref="PersistenceRootResolver"/>
/// needs and nothing else: is this process running from a Velopack
/// install, and if so, where does this install's own fixed data folder
/// live. A seam (`WP 21.5A`, `WP RC.0A` scope item 2) so
/// <see cref="PersistenceRootResolver"/> — and every test of it — never
/// needs a real Velopack install, or even the real <c>Velopack</c> package,
/// in scope.
/// </summary>
public interface IInstalledAppLocator
{
    /// <summary>
    /// <see langword="true"/> when this process is running from an
    /// application Velopack's own <c>Setup.exe</c> installed —
    /// <see langword="false"/> for <c>dotnet run</c>, a plain built
    /// <c>bin/</c> executable, or the plain release zip, all of which keep
    /// today's working-directory-relative persistence root unchanged.
    /// </summary>
    bool IsInstalled { get; }

    /// <summary>
    /// This install's own fixed, per-user data folder (on Windows,
    /// <c>%LOCALAPPDATA%\TempestOS-Data</c>) — where the first-run marker
    /// lives, and the parent of the installed default persistence root
    /// (<c>...\TempestOS-Data\persistence-data</c>). <see langword="null"/>
    /// when <see cref="IsInstalled"/> is <see langword="false"/>.
    /// </summary>
    /// <remarks>
    /// Outside Velopack's own install root (<c>%LOCALAPPDATA%\TempestOS</c>,
    /// named by the package id), which <c>Setup.exe</c> clears when it
    /// reinstalls over an existing install: data kept there was lost on
    /// every reinstall (`v0.25.2` independent test, P1).
    /// </remarks>
    string? InstalledDataDirectory { get; }

    /// <summary>
    /// Where builds before `v0.25.3` kept the first-run marker and default
    /// data (<c>%LOCALAPPDATA%\TempestOS</c>, inside Velopack's install
    /// root), so <see cref="PersistenceRootResolver.MigrateLegacyChoice"/>
    /// can carry them out; <see langword="null"/> when there is none.
    /// </summary>
    string? LegacyDataDirectory => null;
}
