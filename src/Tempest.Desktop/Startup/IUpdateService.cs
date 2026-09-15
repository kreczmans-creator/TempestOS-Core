namespace Tempest.Desktop.Startup;

/// <summary>
/// Checks the release feed and applies an update (`WP 21.5A`, `WP RC.0A`
/// scope item 1) — a seam over Velopack's own <c>UpdateManager</c>, kept
/// narrow (two methods) and Avalonia-free so Settings → Updates and the
/// on-launch check can both be exercised by a test double, per this Work
/// Package's own "a seam you can substitute" brief.
/// </summary>
public interface IUpdateService
{
    /// <summary>
    /// <see langword="true"/> only for a real Velopack install — a
    /// not-installed run (<c>dotnet run</c>, a plain <c>bin/</c> exe, the
    /// plain release zip) can never check for or apply an update, since it
    /// has no local install for Velopack's own update-and-restart mechanism
    /// to operate on.
    /// </summary>
    bool IsInstalled { get; }

    /// <summary>
    /// Checks the release feed. Returns the newer version's own version
    /// string when one is available, or <see langword="null"/> when this
    /// build is already current, when <see cref="IsInstalled"/> is
    /// <see langword="false"/>, or when the feed could not be reached (a
    /// network failure is reported here as "nothing found", never thrown —
    /// an update check is opportunistic, never something a launch or a
    /// Settings visit should fail over).
    /// </summary>
    Task<string?> CheckForUpdateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads the update <see cref="CheckForUpdateAsync"/> most recently
    /// found, then applies it and restarts the application. Does nothing
    /// if no update was found by the most recent <see cref="CheckForUpdateAsync"/>
    /// call.
    /// </summary>
    Task DownloadAndApplyUpdateAsync(CancellationToken cancellationToken = default);
}

/// <summary>A running process's own shared record of the last update check's result — see this Work Package's own report for why a small shared object, not a toast, carries this to Settings → Updates.</summary>
public sealed class UpdateAvailability
{
    /// <summary>The newer version found by the most recent check, or <see langword="null"/> when none is known.</summary>
    public string? AvailableVersion { get; set; }
}
