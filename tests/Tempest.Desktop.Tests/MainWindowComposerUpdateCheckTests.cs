using Tempest.Desktop.Composition;
using Tempest.Desktop.Startup;

namespace Tempest.Desktop.Tests;

/// <summary>
/// <see cref="MainWindowComposer.CheckForUpdatesInBackgroundAsync"/>'s own
/// claims (`WP 21.5A`, `WP RC.0A` scope item 1): a found update lands in
/// the shared <see cref="UpdateAvailability"/>, and a feed failure never
/// escapes as an exception. <see cref="MainWindowComposer.BuildViews"/>'s
/// own gate around the call site — <c>UserSettings.CheckForUpdatesOnLaunch</c>,
/// <see langword="false"/> by default — is what actually makes "off by
/// default" true; this class proves the method the gate calls behaves
/// correctly once it does run, which a real window's own Settings status
/// text cannot distinguish from "never ran at all" in any test process
/// (see this method's own remarks — no test ever calls
/// <c>Velopack.VelopackApp.Build().Run()</c>, so a real
/// <see cref="VelopackUpdateService.IsInstalled"/> is always
/// <see langword="false"/> regardless of whether a check happened).
/// </summary>
public sealed class MainWindowComposerUpdateCheckTests
{
    [Fact]
    public async Task CheckForUpdatesInBackgroundAsync_AnAvailableUpdate_LandsInTheSharedAvailability()
    {
        var updateService = new FakeUpdateService { NextCheckResult = "9.9.9" };
        var availability = new UpdateAvailability();

        await MainWindowComposer.CheckForUpdatesInBackgroundAsync(updateService, availability);

        Assert.Equal("9.9.9", availability.AvailableVersion);
        Assert.Equal(1, updateService.CheckCallCount);
    }

    [Fact]
    public async Task CheckForUpdatesInBackgroundAsync_NoUpdateAvailable_LeavesAvailabilityNull()
    {
        var updateService = new FakeUpdateService { NextCheckResult = null };
        var availability = new UpdateAvailability { AvailableVersion = "should be overwritten" };

        await MainWindowComposer.CheckForUpdatesInBackgroundAsync(updateService, availability);

        Assert.Null(availability.AvailableVersion);
    }

    [Fact]
    public async Task CheckForUpdatesInBackgroundAsync_TheCheckThrows_NeverEscapesAsAnException()
    {
        var updateService = new FakeUpdateService { ThrowOnCheck = true };
        var availability = new UpdateAvailability();

        // The whole point: an on-launch check's own feed failure must never
        // surface as an unobserved exception, let alone fault startup.
        await MainWindowComposer.CheckForUpdatesInBackgroundAsync(updateService, availability);

        Assert.Null(availability.AvailableVersion);
    }

    private sealed class FakeUpdateService : IUpdateService
    {
        public string? NextCheckResult { get; set; }
        public bool ThrowOnCheck { get; set; }
        public int CheckCallCount { get; private set; }

        public bool IsInstalled => true;

        public Task<string?> CheckForUpdateAsync(CancellationToken cancellationToken = default)
        {
            CheckCallCount++;

            if (ThrowOnCheck)
                throw new InvalidOperationException("Simulated feed failure.");

            return Task.FromResult(NextCheckResult);
        }

        public Task DownloadAndApplyUpdateAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
