using Velopack.Locators;

namespace Tempest.Desktop.Startup;

/// <summary>
/// The real <see cref="IInstalledAppLocator"/> (`WP 21.5A`): answers
/// "is this installed" from Velopack's own locator, never a heuristic on
/// paths, per this Work Package's own brief.
/// </summary>
/// <remarks>
/// <see cref="VelopackLocator.Current"/> is only ever meaningful once
/// <c>Velopack.VelopackApp.Build().Run()</c> has run — <see cref="Program.Main"/>'s
/// own first statement, unconditionally, for every run shape (installed,
/// <c>dotnet run</c>, the plain zip). Verified directly (not merely
/// assumed): calling <c>VelopackApp.Build().Run()</c> from a process that
/// was never packaged or installed by Velopack at all does not throw — it
/// sets <see cref="VelopackLocator.Current"/> to a locator whose own
/// <see cref="IVelopackLocator.CurrentlyInstalledVersion"/> is
/// <see langword="null"/>, which is exactly what <see cref="IsInstalled"/>
/// below reads. <see cref="VelopackLocator.IsCurrentSet"/> is still checked
/// first, purely defensively, for any code path that could reach this class
/// without that call ever having run (a future console entry point, a
/// misordered refactor) — never for <c>Tempest.Desktop</c> itself.
/// </remarks>
public sealed class VelopackInstalledAppLocator : IInstalledAppLocator
{
    /// <summary>The fixed, per-user data folder name under <see cref="Environment.SpecialFolder.LocalApplicationData"/>, outside Velopack's own <c>TempestOS</c> install root.</summary>
    public const string InstalledDataFolderName = "TempestOS-Data";

    /// <summary>The folder name builds before `v0.25.3` used: Velopack's own install root, cleared on reinstall.</summary>
    public const string LegacyDataFolderName = "TempestOS";

    /// <inheritdoc />
    public bool IsInstalled =>
        VelopackLocator.IsCurrentSet && VelopackLocator.Current.CurrentlyInstalledVersion is not null;

    /// <inheritdoc />
    public string? InstalledDataDirectory =>
        IsInstalled
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), InstalledDataFolderName)
            : null;

    /// <inheritdoc />
    public string? LegacyDataDirectory =>
        IsInstalled
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), LegacyDataFolderName)
            : null;
}
