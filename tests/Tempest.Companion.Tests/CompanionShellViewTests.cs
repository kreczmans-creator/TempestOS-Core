using Avalonia.Headless.XUnit;
using Tempest.Companion.Client;
using Tempest.Companion.Views;
using static Tempest.Companion.Tests.CompanionViewTestHelpers;

namespace Tempest.Companion.Tests;

// Proves the Companion shell against real Avalonia headless rendering
// (the ADR-0094 strategy): branded app bar, cockpit-first landing,
// bottom-tab navigation between every section, the Command Palette as a
// global entry point, project drill-down and back, and the offline error
// state when the platform is unreachable with nothing cached.
public class CompanionShellViewTests
{
    private static readonly CompanionClientSettings Settings =
        new("http://127.0.0.1:5080", "tester", "Light");

    [AvaloniaFact]
    public void Launch_LandsOnTheCockpit_WithBrandChrome()
    {
        using var temp = new TempDirectory();
        var shell = new CompanionShellView(BuildDataService(new FakeCompanionApiClient(), temp), Settings);
        var window = ShowInPhoneWindow(shell);

        Assert.Equal(CompanionSection.Cockpit, shell.CurrentSection);
        Assert.IsType<CockpitPage>(shell.ActivePage);
        AssertShowsText(window, "TEMPEST OS");
        AssertShowsText(window, "COMPANION");
        AssertShowsText(window, "PROJECT HEALTH");
    }

    [AvaloniaFact]
    public void BottomNavigation_SwitchesEverySection()
    {
        using var temp = new TempDirectory();
        var shell = new CompanionShellView(BuildDataService(new FakeCompanionApiClient(), temp), Settings);
        var window = ShowInPhoneWindow(shell);

        shell.Navigate(CompanionSection.Projects);
        Assert.IsType<ProjectsPage>(shell.ActivePage);

        shell.Navigate(CompanionSection.Attention);
        Assert.IsType<AttentionPage>(shell.ActivePage);

        shell.Navigate(CompanionSection.Activity);
        Assert.IsType<ActivityPage>(shell.ActivePage);

        shell.Navigate(CompanionSection.More);
        Assert.IsType<MorePage>(shell.ActivePage);

        shell.Navigate(CompanionSection.Cockpit);
        Assert.IsType<CockpitPage>(shell.ActivePage);
    }

    [AvaloniaFact]
    public void CommandPalette_OpensFiltersAndNavigates()
    {
        using var temp = new TempDirectory();
        var shell = new CompanionShellView(BuildDataService(new FakeCompanionApiClient(), temp), Settings);
        var window = ShowInPhoneWindow(shell);

        shell.Palette.Open();
        Assert.True(shell.Palette.IsVisible);
        Assert.Contains(shell.Palette.VisibleEntries, e => e.Title == "Go to Attention");

        var entry = shell.Palette.VisibleEntries.Single(e => e.Title == "Go to Attention");
        entry.Execute();
        shell.Palette.Close();

        Assert.Equal(CompanionSection.Attention, shell.CurrentSection);
        Assert.False(shell.Palette.IsVisible);
    }

    [AvaloniaFact]
    public void ProjectDrillDown_OpensDetail_AndReturns()
    {
        using var temp = new TempDirectory();
        var client = new FakeCompanionApiClient
        {
            Projects = new(DateTimeOffset.UtcNow,
            [
                new(Guid.NewGuid(), "Tidal Turbine", "PRJ-001", "Draft", DateTimeOffset.UtcNow.AddDays(-3), 1, 4),
            ]),
        };
        var shell = new CompanionShellView(BuildDataService(client, temp), Settings);
        var window = ShowInPhoneWindow(shell);

        shell.OpenProject(client.Projects.Projects[0]);
        var detail = Assert.IsType<ProjectDetailPage>(shell.ActivePage);
        Assert.Equal("Tidal Turbine", detail.Title);
        window.UpdateLayout();
        AssertShowsText(window, "PRJ-001");

        shell.Navigate(CompanionSection.Projects);
        Assert.IsType<ProjectsPage>(shell.ActivePage);
    }

    [AvaloniaFact]
    public async Task OfflineWithNothingCached_ShowsTheErrorState_WithRetry()
    {
        using var temp = new TempDirectory();
        var client = new FakeCompanionApiClient
        {
            Failure = new CompanionApiException(CompanionApiFailureReason.Unreachable, "TempestOS could not be reached."),
        };
        var shell = new CompanionShellView(BuildDataService(client, temp), Settings);
        var window = ShowInPhoneWindow(shell);

        await shell.ActivePage!.RefreshAsync();
        window.UpdateLayout();

        AssertShowsText(window, "could not be reached");
        AssertShowsText(window, "Retry");
    }

    [AvaloniaFact]
    public async Task OfflineWithCache_ShowsDataUnderAFreshnessBanner()
    {
        using var temp = new TempDirectory();
        var client = new FakeCompanionApiClient();
        var data = BuildDataService(client, temp);
        var shell = new CompanionShellView(data, Settings);
        var window = ShowInPhoneWindow(shell);

        // First refresh succeeds and caches; then the platform goes away.
        await shell.ActivePage!.RefreshAsync();
        client.Failure = new CompanionApiException(CompanionApiFailureReason.Unreachable, "TempestOS could not be reached.");
        await shell.ActivePage!.RefreshAsync();
        window.UpdateLayout();

        AssertShowsText(window, "CACHED");
        AssertShowsText(window, "PROJECT HEALTH");
    }

    [AvaloniaFact]
    public void SmallPhone_LaysOutWithoutHorizontalOverflow()
    {
        using var temp = new TempDirectory();
        var shell = new CompanionShellView(BuildDataService(new FakeCompanionApiClient(), temp), Settings);
        var window = ShowInPhoneWindow(shell, width: 320, height: 568);

        Assert.True(shell.Bounds.Width <= 320.5, $"Shell width {shell.Bounds.Width} exceeded the small-phone viewport.");
        AssertShowsText(window, "PROJECT HEALTH");
    }
}
