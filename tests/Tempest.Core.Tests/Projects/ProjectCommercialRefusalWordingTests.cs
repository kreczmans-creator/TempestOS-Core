using Tempest.Core.Tests.Plugins;
using Tempest.Core.Timesheets;

namespace Tempest.Core.Tests.Projects;

/// <summary>
/// `TD-186`: a refusal the operator reads verbatim names the project the
/// way every other refusal on the surface does — never by its id. The
/// Desktop's timesheet prompt already said "No rate card is pinned on
/// Apollo Pump Redesign"; the Core refusal behind the invoicing surface read
/// "Project '8d70a987-…' has no Released rate-card pin".
/// </summary>
public sealed class ProjectCommercialRefusalWordingTests
{
    [Fact]
    public async Task ANoRateCardRefusal_NamesTheProject_NotItsId()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await ProjectCommercialTestHost.StartAsync(temp.Path);
        try
        {
            ProjectCommercialTestHost.SignIn(host);
            var projectId = await ProjectCommercialTestHost.CreateProjectAsync(host, name: "Apollo Pump Redesign");

            var refused = await ProjectCommercialTestHost.Timesheets(host)
                .RecordAsync(projectId, new DateOnly(2026, 3, 2), 2m, billable: true, "Senior", "Site visit");

            Assert.False(refused.Succeeded);
            Assert.Equal(TimesheetRefusal.NoRateCardPinned, refused.Refusal);
            Assert.Contains("'Apollo Pump Redesign'", refused.Reason);
            Assert.DoesNotContain(projectId.ToString(), refused.Reason);
        }
        finally
        {
            await manager.ShutdownAsync();
            await host.DisposeAsync();
        }
    }
}
