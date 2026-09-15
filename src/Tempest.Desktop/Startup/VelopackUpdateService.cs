using Velopack;
using Velopack.Locators;
using Velopack.Sources;

namespace Tempest.Desktop.Startup;

/// <summary>
/// The real <see cref="IUpdateService"/> (`WP 21.5A`): checks and applies
/// updates from this repository's own GitHub Releases feed, through
/// Velopack's own <see cref="UpdateManager"/>/<see cref="GithubSource"/>.
/// </summary>
/// <remarks>
/// <see cref="UpdateManager"/>'s own constructor reads
/// <see cref="VelopackLocator.Current"/> immediately, and
/// <see cref="VelopackLocator.Current"/> throws
/// <see cref="InvalidOperationException"/> outright when
/// <c>Velopack.VelopackApp.Build().Run()</c> has never run — verified
/// directly (a scratch probe project), not merely assumed. This class is
/// constructed unconditionally, by every <see cref="Composition.MainWindowComposer.BuildViews"/>
/// call, including every Desktop journey test's own <c>MainWindow</c>
/// construction, none of which ever calls <c>VelopackApp.Build().Run()</c>
/// (only <see cref="Program.Main"/> does) — so <see cref="UpdateManager"/>
/// itself is built lazily, in <see cref="CheckForUpdateAsync"/>, strictly
/// after <see cref="IsInstalled"/> has already confirmed
/// <see cref="VelopackLocator.IsCurrentSet"/>. Constructing this class, and
/// reading <see cref="IsInstalled"/> on it, is therefore always safe,
/// exactly like every other run shape this Work Package's brief asks for.
/// </remarks>
public sealed class VelopackUpdateService : IUpdateService
{
    /// <summary>The GitHub repository <c>release.yml</c> publishes the installer and update feed assets to.</summary>
    public const string RepositoryUrl = "https://github.com/kreczmans-creator/TempestOS-Core";

    private UpdateManager? _manager;
    private UpdateInfo? _pendingUpdate;

    /// <inheritdoc />
    public bool IsInstalled =>
        VelopackLocator.IsCurrentSet && VelopackLocator.Current.CurrentlyInstalledVersion is not null;

    /// <inheritdoc />
    public async Task<string?> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        if (!IsInstalled)
        {
            _pendingUpdate = null;
            return null;
        }

        try
        {
            _manager ??= new UpdateManager(new GithubSource(RepositoryUrl, accessToken: null, prerelease: false));
            _pendingUpdate = await _manager.CheckForUpdatesAsync().ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Opportunistic, per this interface's own contract: no network,
            // a rate-limited GitHub API call, a malformed feed — none of
            // these is a failure the caller (a launch, or an operator
            // visiting Settings) should ever see as an exception.
            _pendingUpdate = null;
            return null;
        }

        return _pendingUpdate?.TargetFullRelease.Version.ToString();
    }

    /// <inheritdoc />
    public async Task DownloadAndApplyUpdateAsync(CancellationToken cancellationToken = default)
    {
        if (_manager is null || _pendingUpdate is not { } update)
            return;

        await _manager.DownloadUpdatesAsync(update, cancelToken: cancellationToken).ConfigureAwait(false);
        _manager.ApplyUpdatesAndRestart(update.TargetFullRelease);
    }
}
