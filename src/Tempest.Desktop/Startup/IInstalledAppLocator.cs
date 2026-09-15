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
    /// <c>%LOCALAPPDATA%\TempestOS</c>) — where the first-run marker lives,
    /// and the parent of the installed default persistence root
    /// (<c>...\TempestOS\persistence-data</c>). <see langword="null"/> when
    /// <see cref="IsInstalled"/> is <see langword="false"/>.
    /// </summary>
    /// <remarks>
    /// Deliberately not Velopack's own <see cref="Velopack.Locators.IVelopackLocator.RootAppDir"/>:
    /// that folder holds Velopack's own versioned application content and
    /// package files, which an application's own persisted user data has no
    /// business sitting beside.
    /// </remarks>
    string? InstalledDataDirectory { get; }
}
