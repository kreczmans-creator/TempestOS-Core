using System.Globalization;
using System.Text;
using Tempest.Core.Deliverables;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.ExportImport;
using Tempest.Core.Projects;
using Tempest.Workspace.Mechanical;

namespace Tempest.Workspace.Integration.DashboardExport;

/// <summary>
/// Writes <c>deadlines.ics</c>: every agreed project milestone as a 30-minute
/// Outlook event at 15:00 UK time on its target date, titled in the standard
/// export style every exported file uses — <c>ACMEE-BRIDGE-M-002</c>, customer
/// code, project reference, milestone number
/// (<see cref="ProjectNumbering.MilestoneReference"/>) — so everything in
/// Outlook says whose work it is and what to bill. The body is the
/// milestone's title, then the work its description sets out. Outlook subscribes to it as its
/// own "Tempest deadlines" calendar; Tempest-Dashboard reads the same file for
/// its due dates, so the two can never disagree.
/// </summary>
/// <remarks>
/// <para>
/// <b>Agreed only.</b> Only a numbered milestone — one set on the project
/// with a milestone number — is a deadline. The two milestones Core makes for
/// itself have no number and no agreed date: a project's default "Unquoted"
/// milestone (<see cref="DeliverableService.UnquotedMilestoneTitle"/>, a
/// placeholder date) and an accepted quote's reference milestone (dated to the
/// quote's validity expiry). A cancelled, superseded, obsolete or archived
/// milestone is no longer a deadline, and nor is one on a deleted project.
/// </para>
/// <para>
/// <b>Stable.</b> Each event's UID is the milestone's own id, so a moved date
/// or renamed milestone updates the one event in Outlook rather than adding a
/// second. Events are written in date order, so an unchanged project writes an
/// unchanged file apart from <c>DTSTAMP</c>.
/// </para>
/// </remarks>
public sealed class DeadlinesCalendarExportAdapter : IExportable, IExportableKind
{
    /// <summary>The schema version of <c>deadlines.ics</c>.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>The time of day, UK local, every deadline is due.</summary>
    public static readonly TimeOnly DueTime = new(15, 0);

    /// <summary>How long each deadline event lasts.</summary>
    public static readonly TimeSpan Duration = TimeSpan.FromMinutes(30);

    /// <summary>The calendar's name, as Outlook shows it.</summary>
    public const string CalendarName = "Tempest deadlines";

    private const string MilestoneKind = "Milestone";

    private static readonly LifecycleState[] NotADeadline =
        [LifecycleState.Cancelled, LifecycleState.Superseded, LifecycleState.Obsolete, LifecycleState.Archived];

    private readonly EngineeringDomainContext _domainContext;
    private readonly TimeProvider _time;

    /// <summary>Initialises a new instance of the <see cref="DeadlinesCalendarExportAdapter"/> class.</summary>
    /// <param name="domainContext">The Engineering Domain's own shared repository, read for projects and their milestones.</param>
    /// <param name="timeProvider">The clock for <c>DTSTAMP</c>; <see langword="null"/> uses <see cref="TimeProvider.System"/>.</param>
    public DeadlinesCalendarExportAdapter(EngineeringDomainContext domainContext, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(domainContext);
        _domainContext = domainContext;
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public string Kind => "dashboard.deadlines";

    /// <inheritdoc />
    public int SchemaVersion => CurrentSchemaVersion;

    /// <inheritdoc />
    public async Task ExportAsync(Stream destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);

        var deadlines = await ReadDeadlinesAsync(cancellationToken).ConfigureAwait(false);
        var stamp = _time.GetUtcNow().UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);

        var ics = new StringBuilder();
        Line(ics, "BEGIN:VCALENDAR");
        Line(ics, "VERSION:2.0");
        Line(ics, "PRODID:-//Tempest Design Engineering//TempestOS//EN");
        Line(ics, "CALSCALE:GREGORIAN");
        Line(ics, "METHOD:PUBLISH");
        Line(ics, $"X-WR-CALNAME:{Escape(CalendarName)}");
        Line(ics, "X-WR-TIMEZONE:Europe/London");
        foreach (var line in LondonTimeZone)
            Line(ics, line);

        foreach (var d in deadlines)
        {
            var start = d.Date.ToDateTime(DueTime);
            var end = start + Duration;
            Line(ics, "BEGIN:VEVENT");
            Line(ics, $"UID:milestone-{d.MilestoneId:D}@tempestos");
            Line(ics, $"DTSTAMP:{stamp}");
            Line(ics, $"DTSTART;TZID=Europe/London:{Local(start)}");
            Line(ics, $"DTEND;TZID=Europe/London:{Local(end)}");
            Line(ics, $"SUMMARY:{Escape(d.Summary)}");
            Line(ics, $"DESCRIPTION:{Escape(d.Description)}");
            Line(ics, "CATEGORIES:Tempest deadline");
            Line(ics, "TRANSP:OPAQUE");
            Line(ics, "END:VEVENT");
        }

        Line(ics, "END:VCALENDAR");

        var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(ics.ToString());
        await destination.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<Deadline>> ReadDeadlinesAsync(CancellationToken cancellationToken)
    {
        var repository = _domainContext.Repository;
        var projects = (await repository.ListByKindAsync(MechanicalObjectFactoryRegistry.Project, cancellationToken).ConfigureAwait(false))
            .Where(entry => !entry.IsDeleted);

        var deadlines = new List<Deadline>();
        foreach (var project in projects)
        {
            var children = await repository.ListChildrenAsync(project.Id, cancellationToken).ConfigureAwait(false);
            var milestoneEntries = children.Where(entry =>
                string.Equals(entry.Kind, MilestoneKind, StringComparison.Ordinal) &&
                !entry.IsDeleted &&
                !string.Equals(entry.DisplayName, DeliverableService.UnquotedMilestoneTitle, StringComparison.Ordinal));

            var milestones = (await repository.MaterialiseAsync<IMilestone>([.. milestoneEntries], cancellationToken).ConfigureAwait(false))
                .Where(m => !string.IsNullOrWhiteSpace(m.Identifier) && !NotADeadline.Contains(m.Status))
                .ToList();
            if (milestones.Count == 0)
                continue;

            foreach (var milestone in milestones)
            {
                var number = milestone.Identifier!.Trim();
                deadlines.Add(new Deadline(
                    milestone.Id,
                    DateOnly.FromDateTime(milestone.TargetDate.UtcDateTime),
                    ProjectNumbering.MilestoneReference(project.Identifier, number),
                    Body(number, milestone)));
            }
        }

        return [.. deadlines.OrderBy(d => d.Date).ThenBy(d => d.Summary, StringComparer.Ordinal).ThenBy(d => d.MilestoneId)];
    }

    /// <summary>
    /// The milestone's title, then the work its description sets out when one
    /// was written —
    /// not the placeholder <c>ProjectMilestoneService</c> stores when none was
    /// ("Milestone MS-2 — Stage 2 issue."), which only repeats the title.
    /// </summary>
    private static string Body(string number, IMilestone milestone)
    {
        var description = (milestone as IHasRevisions)?.Content?.Trim();
        var placeholder = $"Milestone {number} — {milestone.DisplayName.Trim()}.";
        return string.IsNullOrEmpty(description) || string.Equals(description, placeholder, StringComparison.Ordinal)
            ? milestone.DisplayName
            : $"{milestone.DisplayName}\n\n{description}";
    }

    private static string Local(DateTime value) => value.ToString("yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture);

    /// <summary>RFC 5545 §3.3.11 text escaping.</summary>
    private static string Escape(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace(";", "\\;", StringComparison.Ordinal)
            .Replace(",", "\\,", StringComparison.Ordinal)
            .Replace("\r\n", "\\n", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);

    /// <summary>Appends one content line, folded at 75 octets (RFC 5545 §3.1) without splitting a character.</summary>
    private static void Line(StringBuilder ics, string line)
    {
        var octets = 0;
        var limit = 75;
        foreach (var rune in line.EnumerateRunes())
        {
            var size = rune.Utf8SequenceLength;
            if (octets + size > limit)
            {
                ics.Append("\r\n ");
                octets = 0;
                limit = 74;
            }

            ics.Append(rune.ToString());
            octets += size;
        }

        ics.Append("\r\n");
    }

    // UK time, so 15:00 stays 15:00 across the clock changes.
    private static readonly string[] LondonTimeZone =
    [
        "BEGIN:VTIMEZONE",
        "TZID:Europe/London",
        "BEGIN:DAYLIGHT",
        "TZOFFSETFROM:+0000",
        "TZOFFSETTO:+0100",
        "TZNAME:BST",
        "DTSTART:19700329T010000",
        "RRULE:FREQ=YEARLY;BYMONTH=3;BYDAY=-1SU",
        "END:DAYLIGHT",
        "BEGIN:STANDARD",
        "TZOFFSETFROM:+0100",
        "TZOFFSETTO:+0000",
        "TZNAME:GMT",
        "DTSTART:19701025T020000",
        "RRULE:FREQ=YEARLY;BYMONTH=10;BYDAY=-1SU",
        "END:STANDARD",
        "END:VTIMEZONE",
    ];

    private sealed record Deadline(Guid MilestoneId, DateOnly Date, string Summary, string Description);
}
