using Avalonia.Headless.XUnit;
using Tempest.Companion.Branding;
using Tempest.Companion.Contracts;
using Tempest.Companion.Views;
using static Tempest.Companion.Tests.CompanionViewTestHelpers;

namespace Tempest.Companion.Tests;

// Proves the individual Companion pages headlessly: the Cockpit renders
// every populated region and discloses placeholders honestly; Attention
// renders the actionable pending-review rows with confirm-gated actions;
// empty states are honest empty states; and the brand mark renders as
// real vector geometry.
public class CompanionPageTests
{
    [AvaloniaFact]
    public async Task CockpitPage_RendersThePopulatedRegions()
    {
        using var temp = new TempDirectory();
        var page = new CockpitPage(BuildDataService(new FakeCompanionApiClient(), temp));
        var window = ShowInPhoneWindow(page);

        await page.RefreshAsync();
        window.UpdateLayout();

        AssertShowsText(window, "PROJECT HEALTH");
        AssertShowsText(window, "ATTENTION");
        AssertShowsText(window, "CONTINUE WHERE I LEFT OFF");
        AssertShowsText(window, "OPEN DECISIONS");
        AssertShowsText(window, "BLOCKED ITEMS");
        AssertShowsText(window, "UPCOMING MILESTONES");
        AssertShowsText(window, "RISK SUMMARY");
        AssertShowsText(window, "REQ-0042");
        AssertShowsText(window, "DEC-001");
        // The honest overdue disclosure - never a fabricated "overdue" figure.
        AssertShowsText(window, "due-date");
    }

    [AvaloniaFact]
    public async Task AttentionPage_NothingOutstanding_ShowsTheHonestEmptyState()
    {
        using var temp = new TempDirectory();
        var page = new AttentionPage(BuildDataService(new FakeCompanionApiClient(), temp));
        var window = ShowInPhoneWindow(page);

        await page.RefreshAsync();
        window.UpdateLayout();

        AssertShowsText(window, "Nothing needs your attention");
    }

    [AvaloniaFact]
    public async Task AttentionPage_PendingReview_RendersActionableRow()
    {
        using var temp = new TempDirectory();
        var client = new FakeCompanionApiClient
        {
            Attention = new(
                DateTimeOffset.UtcNow,
                AttentionItems: [],
                BlockedItems: [],
                OpenDecisions: [],
                OpenTaskCount: 1,
                UpcomingMilestones: [],
                PendingReviews: [new(Guid.NewGuid(), "Document", "DOC-9 Interface Control", "InReview")]),
        };
        var page = new AttentionPage(BuildDataService(client, temp));
        var window = ShowInPhoneWindow(page);

        await page.RefreshAsync();
        window.UpdateLayout();

        AssertShowsText(window, "REVIEWS AWAITING DECISION");
        AssertShowsText(window, "DOC-9 Interface Control");
        AssertShowsText(window, "Approve");
        AssertShowsText(window, "Return to Draft");
    }

    [AvaloniaFact]
    public async Task ProjectsPage_Empty_PointsAtTheDesktopWorkspace()
    {
        using var temp = new TempDirectory();
        var page = new ProjectsPage(BuildDataService(new FakeCompanionApiClient(), temp), _ => { });
        var window = ShowInPhoneWindow(page);

        await page.RefreshAsync();
        window.UpdateLayout();

        AssertShowsText(window, "No Projects exist yet");
    }

    [AvaloniaFact]
    public async Task MorePage_RendersNotificationsSettingsAndIdentity()
    {
        using var temp = new TempDirectory();
        var client = new FakeCompanionApiClient
        {
            Notifications = new(DateTimeOffset.UtcNow,
            [
                new(DateTimeOffset.UtcNow.AddMinutes(-2), "Requirements", "Warning", "REQ-0007 failed validation."),
            ]),
        };
        var page = new MorePage(
            BuildDataService(client, temp),
            new("http://127.0.0.1:5080", "tester", "Light"),
            _ => { },
            () => { });
        var window = ShowInPhoneWindow(page);

        await page.RefreshAsync();
        window.UpdateLayout();

        AssertShowsText(window, "NOTIFICATIONS (1)");
        AssertShowsText(window, "REQ-0007");
        AssertShowsText(window, "CONNECTION");
        AssertShowsText(window, "Clear Local Data");
        AssertShowsText(window, "TEMPEST OS");
    }

    [AvaloniaFact]
    public void TempestLogo_MeasuresSquare_AndRendersWithoutError()
    {
        var logo = new TempestLogoControl { Width = 48, Height = 48 };
        var window = ShowInPhoneWindow(logo, width: 100, height: 100);

        Assert.Equal(48, logo.Bounds.Width);
        Assert.Equal(48, logo.Bounds.Height);
    }
}
