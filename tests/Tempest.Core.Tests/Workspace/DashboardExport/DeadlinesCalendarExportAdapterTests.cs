using System.Text;
using Tempest.Core.Deliverables;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Tests.Invoicing.Xero.Api;
using Tempest.Core.Tests.Plugins;
using Tempest.Workspace.Integration.DashboardExport;
using Tempest.Workspace.Projects;

namespace Tempest.Core.Tests.Workspace.DashboardExport;

/// <summary>
/// <c>deadlines.ics</c>: each numbered milestone is a 30-minute Outlook event
/// at 15:00 UK time, named in the standard export style
/// (<c>HARBR-DAVITS-M-002</c>), with the milestone's title and work in the
/// body and a stable UID.
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
    public async Task EachNumberedMilestone_IsA30MinuteEventAt1500UkTime_NamedInTheStandardExportStyle()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await DashboardExportTestHost.StartAsync(temp.Path);
        var project = await DashboardExportTestHost.CreateProjectAsync(host, "HARBR-DAVITS", "Davit redesign");
        var milestones = new ProjectMilestoneService(DashboardExportTestHost.Domain(host));
        var stage2 = await milestones.CreateMilestoneAsync(project.Id, "2", "Stage 2 issue", Day(11, 5), "Issue GA, calcs and fabrication details for the davit arm.");

        var ics = await ExportAsync(host);

        Assert.StartsWith("BEGIN:VCALENDAR\r\n", ics, StringComparison.Ordinal);
        Assert.EndsWith("END:VCALENDAR\r\n", ics, StringComparison.Ordinal);
        Assert.Contains("X-WR-CALNAME:Tempest deadlines\r\n", ics, StringComparison.Ordinal);
        Assert.Contains("BEGIN:VTIMEZONE\r\nTZID:Europe/London\r\n", ics, StringComparison.Ordinal);
        Assert.Contains($"UID:milestone-{stage2.Id:D}@tempestos\r\n", ics, StringComparison.Ordinal);
        Assert.Contains("DTSTART;TZID=Europe/London:20261105T150000\r\n", ics, StringComparison.Ordinal);
        Assert.Contains("DTEND;TZID=Europe/London:20261105T153000\r\n", ics, StringComparison.Ordinal);
        Assert.Contains("SUMMARY:HARBR-DAVITS-M-002\r\n", ics, StringComparison.Ordinal);
        Assert.Contains("DESCRIPTION:Stage 2 issue\\n\\nIssue GA\\, calcs and fabrication details for the davit arm.", Unfold(ics), StringComparison.Ordinal);
        Assert.Contains("DTSTAMP:20261022T090000Z\r\n", ics, StringComparison.Ordinal);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task WithNoWrittenDescription_TheBodyIsTheTitle_NotThePlaceholderThatRepeatsIt()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await DashboardExportTestHost.StartAsync(temp.Path);
        var project = await DashboardExportTestHost.CreateProjectAsync(host, "CRANE-BASE01", "Crane base");
        var milestones = new ProjectMilestoneService(DashboardExportTestHost.Domain(host));
        await milestones.CreateMilestoneAsync(project.Id, "MS-1", "Design freeze", Day(11, 4));

        var ics = await ExportAsync(host);

        Assert.Contains("SUMMARY:CRANE-BASE01-M-001\r\nDESCRIPTION:Design freeze\r\n", ics, StringComparison.Ordinal);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task AMilestoneWithNoNumber_SuchAsAQuotesReferenceMilestone_IsNotADeadline()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await DashboardExportTestHost.StartAsync(temp.Path);
        var project = await DashboardExportTestHost.CreateProjectAsync(host, "DAVIT-REFIT1", "Davit refit");
        await DashboardExportTestHost.CreateMilestoneAsync(host, null!, "DAVIT-REFIT1-Q-001", project.Id, Day(11, 20));

        var ics = await ExportAsync(host);

        Assert.DoesNotContain("BEGIN:VEVENT", ics, StringComparison.Ordinal);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task UnquotedAndCancelledMilestones_AreNotDeadlines_AndEventsAreInDateOrder()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await DashboardExportTestHost.StartAsync(temp.Path);
        var project = await DashboardExportTestHost.CreateProjectAsync(host, "BRACK-ETFTG1", "Bracket");
        await DashboardExportTestHost.CreateMilestoneAsync(host, "9", "Final issue", project.Id, Day(12, 1));
        await DashboardExportTestHost.CreateMilestoneAsync(host, "1", "First issue", project.Id, Day(11, 1));
        await DashboardExportTestHost.CreateMilestoneAsync(host, null!, DeliverableService.UnquotedMilestoneTitle, project.Id, Day(10, 30));
        var cancelled = await DashboardExportTestHost.CreateMilestoneAsync(host, "5", "Dropped stage", project.Id, Day(11, 15));
        await cancelled.TransitionAsync(LifecycleState.Cancelled);

        var ics = await ExportAsync(host);

        Assert.DoesNotContain(DeliverableService.UnquotedMilestoneTitle, ics, StringComparison.Ordinal);
        Assert.DoesNotContain("Dropped stage", ics, StringComparison.Ordinal);
        Assert.True(ics.IndexOf("BRACK-ETFTG1-M-001", StringComparison.Ordinal) < ics.IndexOf("BRACK-ETFTG1-M-009", StringComparison.Ordinal));
        Assert.Equal(2, ics.Split("BEGIN:VEVENT").Length - 1);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task TextIsEscaped_AndLongLinesAreFoldedAt75Octets()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await DashboardExportTestHost.StartAsync(temp.Path);
        var project = await DashboardExportTestHost.CreateProjectAsync(host, "HARBR-CRANES", "Cranes");
        var milestones = new ProjectMilestoneService(DashboardExportTestHost.Domain(host));
        await milestones.CreateMilestoneAsync(project.Id, "1", "Harbour; davits, cranes and a very long milestone title indeed", Day(11, 2));

        var ics = await ExportAsync(host);

        Assert.Contains("Harbour\\; davits\\, cranes", Unfold(ics), StringComparison.Ordinal);
        Assert.All(ics.Split("\r\n"), line => Assert.True(Encoding.UTF8.GetByteCount(line) <= 75, line));

        await manager.ShutdownAsync();
    }

    private static string Unfold(string ics) => ics.Replace("\r\n ", string.Empty, StringComparison.Ordinal);
}
