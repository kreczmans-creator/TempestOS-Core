using System.Text;
using Tempest.Core.Deliverables;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Tests.Invoicing.Xero.Api;
using Tempest.Core.Tests.Plugins;
using Tempest.Workspace.Integration.DashboardExport;

namespace Tempest.Core.Tests.Workspace.DashboardExport;

/// <summary>
/// <c>deadlines.ics</c>: each agreed milestone is a 30-minute Outlook event
/// at 15:00 UK time, titled with its project, with a stable UID.
/// </summary>
public sealed class DeadlinesCalendarExportAdapterTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 22, 9, 0, 0, TimeSpan.Zero);

    private static async Task<string> ExportAsync(Tempest.Core.Runtime.ITempestHost host)
    {
        using var stream = new MemoryStream();
        await new DeadlinesCalendarExportAdapter(DashboardExportTestHost.Domain(host), new ManualClock(Now)).ExportAsync(stream);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static DateTimeOffset Day(int month, int day) => new(2026, month, day, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task EachAgreedMilestone_IsA30MinuteEventAt1500UkTime_TitledWithItsProject()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await DashboardExportTestHost.StartAsync(temp.Path);
        var project = await DashboardExportTestHost.CreateProjectAsync(host, "PRJ-021", "Davit redesign");
        var stage2 = await DashboardExportTestHost.CreateMilestoneAsync(host, "MS-2", "Stage 2 issue", project.Id, Day(11, 5));

        var ics = await ExportAsync(host);

        Assert.StartsWith("BEGIN:VCALENDAR\r\n", ics, StringComparison.Ordinal);
        Assert.EndsWith("END:VCALENDAR\r\n", ics, StringComparison.Ordinal);
        Assert.Contains("X-WR-CALNAME:Tempest deadlines\r\n", ics, StringComparison.Ordinal);
        Assert.Contains("BEGIN:VTIMEZONE\r\nTZID:Europe/London\r\n", ics, StringComparison.Ordinal);
        Assert.Contains($"UID:milestone-{stage2.Id:D}@tempestos\r\n", ics, StringComparison.Ordinal);
        Assert.Contains("DTSTART;TZID=Europe/London:20261105T150000\r\n", ics, StringComparison.Ordinal);
        Assert.Contains("DTEND;TZID=Europe/London:20261105T153000\r\n", ics, StringComparison.Ordinal);
        Assert.Contains("SUMMARY:PRJ-021 Davit redesign — Stage 2 issue\r\n", ics, StringComparison.Ordinal);
        Assert.Contains("DTSTAMP:20261022T090000Z\r\n", ics, StringComparison.Ordinal);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task UnquotedAndCancelledMilestones_AreNotDeadlines_AndEventsAreInDateOrder()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await DashboardExportTestHost.StartAsync(temp.Path);
        var project = await DashboardExportTestHost.CreateProjectAsync(host, "PRJ-030", "Bracket");
        await DashboardExportTestHost.CreateMilestoneAsync(host, "MS-9", "Final issue", project.Id, Day(12, 1));
        await DashboardExportTestHost.CreateMilestoneAsync(host, "MS-1", "First issue", project.Id, Day(11, 1));
        await DashboardExportTestHost.CreateMilestoneAsync(host, "MS-0", DeliverableService.UnquotedMilestoneTitle, project.Id, Day(10, 30));
        var cancelled = await DashboardExportTestHost.CreateMilestoneAsync(host, "MS-5", "Dropped stage", project.Id, Day(11, 15));
        await cancelled.TransitionAsync(LifecycleState.Cancelled);

        var ics = await ExportAsync(host);

        Assert.DoesNotContain(DeliverableService.UnquotedMilestoneTitle, ics, StringComparison.Ordinal);
        Assert.DoesNotContain("Dropped stage", ics, StringComparison.Ordinal);
        Assert.True(ics.IndexOf("First issue", StringComparison.Ordinal) < ics.IndexOf("Final issue", StringComparison.Ordinal));
        Assert.Equal(2, ics.Split("BEGIN:VEVENT").Length - 1);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task TextIsEscaped_AndLongLinesAreFoldedAt75Octets()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await DashboardExportTestHost.StartAsync(temp.Path);
        var project = await DashboardExportTestHost.CreateProjectAsync(host, "PRJ-040", "Harbour; davits, cranes and a very long project name indeed");
        await DashboardExportTestHost.CreateMilestoneAsync(host, "MS-1", "Issue", project.Id, Day(11, 2));

        var ics = await ExportAsync(host);

        Assert.Contains("Harbour\\; davits\\, cranes", ics.Replace("\r\n ", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.All(ics.Split("\r\n"), line => Assert.True(Encoding.UTF8.GetByteCount(line) <= 75, line));

        await manager.ShutdownAsync();
    }
}
