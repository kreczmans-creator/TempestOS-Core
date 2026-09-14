using System.Globalization;
using System.Text.Json;
using Tempest.Core.Invoicing;
using Tempest.Core.Persistence;
using Tempest.Core.Quotations;

namespace Tempest.Workspace.Projects;

/// <summary>A project's own computed status (`WP 19.5C`, Product Owner comment item 6) — see <see cref="ProjectStatusReadModel.RuleFor"/> for the one place every rule is defined.</summary>
public enum ProjectHealthStatus
{
    /// <summary>On hold — <see cref="Tempest.Core.EngineeringDomain.Project.Held"/>.</summary>
    OnHold,

    /// <summary>An accepted quote with no deliverable started, or a Sent invoice request Unavailable/Reauthorise.</summary>
    Blocked,

    /// <summary>Any deliverable or milestone past its target date and not complete.</summary>
    Overdue,

    /// <summary>A deliverable or milestone due within seven days and not complete, or hours recorded exceeding the quote's hours.</summary>
    AtRisk,

    /// <summary>A completion with no live invoice request.</summary>
    ReadyToInvoice,

    /// <summary>None of the above.</summary>
    OnTrack,
}

/// <summary>One milestone on a project's own schedule (`WP 19.5C`) — what the Gantt and "upcoming milestones" panels need.</summary>
public sealed record ProjectScheduleMilestone(Guid MilestoneId, string Title, DateOnly TargetDate);

/// <summary>
/// One open project's own computed status, with the reason next to it, and
/// the schedule fields a Gantt needs (`WP 19.5C`).
/// </summary>
/// <param name="ProjectId">The project's own id.</param>
/// <param name="ProjectName">The project's own display name.</param>
/// <param name="Status">This project's computed status — see <see cref="ProjectStatusReadModel.RuleFor"/>.</param>
/// <param name="Reason">Why <see cref="Status"/> is what it is, in one sentence — printed next to the status.</param>
/// <param name="StartDate">The project's own start date, or <see langword="null"/> if unset.</param>
/// <param name="TargetDate">The project's own target (end) date, or <see langword="null"/> if unset.</param>
/// <param name="QuotedHours">The sum of every Hourly line's own hours across this project's Accepted quotations, or <see langword="null"/> when it has none.</param>
/// <param name="RecordedHours">The sum of every live timesheet entry's own hours recorded against this project.</param>
/// <param name="Milestones">Every live milestone under this project, by target date — the Gantt's own rows.</param>
public sealed record ProjectStatusRow(
    Guid ProjectId,
    string ProjectName,
    ProjectHealthStatus Status,
    string Reason,
    DateOnly? StartDate,
    DateOnly? TargetDate,
    decimal? QuotedHours,
    decimal RecordedHours,
    IReadOnlyList<ProjectScheduleMilestone> Milestones);

/// <summary>The Project dashboard's own read (`WP 19.5C`): every open project's status, with counts.</summary>
/// <param name="Counts">How many open projects carry each <see cref="ProjectHealthStatus"/>.</param>
/// <param name="Projects">Every open project's own row, by project name.</param>
public sealed record ProjectStatusSnapshot(
    IReadOnlyDictionary<ProjectHealthStatus, int> Counts,
    IReadOnlyList<ProjectStatusRow> Projects);

/// <summary>
/// Reads the Project dashboard's own status snapshot in one coherent
/// persistence read (`WP 19.5C`, Product Owner comment item 6) — the
/// <c>Tempest.Workspace.Kpi.KpiSnapshotService</c>/<c>WorkspaceSnapshotReader.ReadKpiAsync</c>
/// shape, as a sibling reader over the identical durable
/// <c>EngineeringDomain.ObjectState</c> collection, rather than a new
/// <c>WorkspaceSnapshotKind</c> member (this Work Package's own files do
/// not touch <c>SnapshotReader.cs</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>"Project health" here is genuinely per-project — not
/// <c>EngineeringCockpit.Health</c>.</b> That rollup is platform-wide,
/// across five engineering disciplines, with no project-id anywhere in its
/// computation chain — the exact mislabelling Product Owner comment item 1
/// found ("Project health: Unknown — no Engineering data yet" on a card
/// that cannot possibly know about one project). This class computes a
/// real per-project figure instead, from the project's own hold state, its
/// own quotations, milestones, deliverables, completions, invoice requests
/// and timesheet entries.
/// </para>
/// <para>
/// <b>Rule order is priority order.</b> <see cref="RuleFor"/> evaluates On
/// hold, then Blocked, then Overdue, then At risk, then Ready to invoice,
/// else On track — the exact order Work Package 19.5C's own brief lists
/// them in. A project could satisfy more than one at once (on hold <em>and</em>
/// overdue); the first match wins, deliberately, so the status a dashboard
/// shows is never ambiguous.
/// </para>
/// </remarks>
public interface IProjectStatusReadModel
{
    /// <summary>Reads the status of every open project, as of now.</summary>
    Task<ProjectStatusSnapshot> ReadAsync(CancellationToken cancellationToken = default);
}

/// <summary>The concrete <see cref="IProjectStatusReadModel"/> implementation.</summary>
public sealed class ProjectStatusReadModel : IProjectStatusReadModel
{
    /// <summary>
    /// A deliverable or milestone due within this many days, and not
    /// complete, is At risk (Work Package 19.5C's own brief: "due within
    /// seven days").
    /// </summary>
    public const int AtRiskWithinDays = 7;

    private const string StateCollectionName = "EngineeringDomain.ObjectState";
    private static readonly JsonSerializerOptions DeserialiseOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly IQueryablePersistenceStore _store;
    private readonly TimeProvider _time;

    /// <summary>Initialises a new instance of the <see cref="ProjectStatusReadModel"/> class.</summary>
    /// <param name="timeProvider">The clock "as of now" is read from. <see langword="null"/> — the default — is <see cref="TimeProvider.System"/>; a test supplies a controllable one.</param>
    public ProjectStatusReadModel(IQueryablePersistenceStore store, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(store);

        _store = store;
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public Task<ProjectStatusSnapshot> ReadAsync(CancellationToken cancellationToken = default) =>
        _store.ExecuteInReadTransactionAsync(
            async (transaction, token) =>
            {
                var records = await transaction.ReadAllAsync(StateCollectionName, token).ConfigureAwait(false);

                var projects = new List<ProjectFact>();
                var quotations = new List<QuotationFact>();
                var milestones = new List<MilestoneFact>();
                var deliverables = new List<DeliverableFact>();
                var completions = new List<CompletionFact>();
                var invoiceRequests = new List<InvoiceRequestStatusFact>();
                var timesheetHours = new Dictionary<Guid, decimal>();

                foreach (var (key, json) in records)
                {
                    if (!Guid.TryParseExact(key, "N", out var objectId))
                        continue;

                    if (Deserialise(json) is not { } state || state.IsDeleted)
                        continue;

                    var typeState = state.TypeState ?? EmptyTypeState;

                    switch (state.Kind)
                    {
                        case "Project":
                            projects.Add(new ProjectFact(
                                objectId, state.DisplayName ?? string.Empty,
                                TypeBool(typeState, "Held"),
                                TypeJson<DateOnly?>(typeState, "StartDate"),
                                TypeJson<DateOnly?>(typeState, "TargetDate"),
                                TypeJson<DateOnly?>(typeState, "ClosedOn")));
                            break;

                        case Quotation.CanonicalKind:
                            if (state.ParentId is { } quoteProjectId)
                            {
                                quotations.Add(new QuotationFact(
                                    quoteProjectId,
                                    Enum.TryParse<QuotationStatus>(TypeString(typeState, "Status"), out var qs) ? qs : QuotationStatus.Draft,
                                    TypeJson<List<QuotationLine>>(typeState, "Lines") ?? []));
                            }

                            break;

                        case "Milestone":
                            if (state.ParentId is { } milestoneProjectId)
                            {
                                var targetDate = TypeDate(typeState, "TargetDate");
                                if (targetDate is { } td)
                                    milestones.Add(new MilestoneFact(objectId, state.DisplayName ?? string.Empty, milestoneProjectId, td));
                            }

                            break;

                        case "Deliverable":
                            if (state.ParentId is { } milestoneId)
                                deliverables.Add(new DeliverableFact(objectId, milestoneId));

                            break;

                        case Tempest.Core.Deliverables.DeliverableCompletion.CanonicalKind:
                            if (state.ParentId is { } completionProjectId)
                            {
                                completions.Add(new CompletionFact(
                                    completionProjectId,
                                    TypeGuid(typeState, "DeliverableId") ?? Guid.Empty,
                                    TypeGuid(typeState, "InvoicedBy")));
                            }

                            break;

                        case InvoiceRequest.CanonicalKind:
                            if (state.ParentId is { } invoiceProjectId)
                            {
                                invoiceRequests.Add(new InvoiceRequestStatusFact(
                                    invoiceProjectId,
                                    Enum.TryParse<InvoiceRequestStatus>(TypeString(typeState, "Status"), out var invStatus) ? invStatus : InvoiceRequestStatus.Draft));
                            }

                            break;

                        case Tempest.Core.Timesheets.TimesheetEntry.CanonicalKind:
                            if (state.ParentId is { } timesheetProjectId)
                            {
                                timesheetHours[timesheetProjectId] =
                                    timesheetHours.GetValueOrDefault(timesheetProjectId) + TypeDecimal(typeState, "Hours");
                            }

                            break;
                    }
                }

                var completedDeliverableIds = completions.Select(c => c.DeliverableId).ToHashSet();
                var deliverablesByMilestone = deliverables.ToLookup(d => d.MilestoneId);

                var asOf = _time.GetUtcNow();
                var today = DateOnly.FromDateTime(asOf.UtcDateTime);

                var rows = new List<ProjectStatusRow>();

                foreach (var project in projects.Where(p => p.ClosedOn is null).OrderBy(p => p.DisplayName, StringComparer.Ordinal))
                {
                    var projectMilestones = milestones.Where(m => m.ProjectId == project.Id).ToList();
                    var projectQuotations = quotations.Where(q => q.ProjectId == project.Id).ToList();
                    var projectCompletions = completions.Where(c => c.ProjectId == project.Id).ToList();
                    var projectInvoiceRequests = invoiceRequests.Where(r => r.ProjectId == project.Id).ToList();
                    var recordedHours = timesheetHours.GetValueOrDefault(project.Id);

                    var quotedHours = QuotedHoursOf(projectQuotations);

                    var (status, reason) = RuleFor(
                        project, projectMilestones, deliverablesByMilestone, completedDeliverableIds, projectQuotations,
                        projectCompletions, projectInvoiceRequests, recordedHours, quotedHours, today);

                    rows.Add(new ProjectStatusRow(
                        project.Id, project.DisplayName, status, reason, project.StartDate, project.TargetDate,
                        quotedHours, recordedHours,
                        projectMilestones.OrderBy(m => m.TargetDate).Select(m => new ProjectScheduleMilestone(m.Id, m.Title, m.TargetDate)).ToList()));
                }

                var counts = Enum.GetValues<ProjectHealthStatus>().ToDictionary(s => s, s => rows.Count(r => r.Status == s));

                return new ProjectStatusSnapshot(counts, rows);
            },
            cancellationToken);

    /// <summary>
    /// The one place every status rule is defined, with a remark, so the
    /// dashboard can print the reason next to the status (Work Package
    /// 19.5C's own brief). Evaluated in priority order — the first rule
    /// that matches is the project's status.
    /// </summary>
    private static (ProjectHealthStatus Status, string Reason) RuleFor(
        ProjectFact project,
        IReadOnlyList<MilestoneFact> projectMilestones,
        ILookup<Guid, DeliverableFact> deliverablesByMilestone,
        IReadOnlySet<Guid> completedDeliverableIds,
        IReadOnlyList<QuotationFact> projectQuotations,
        IReadOnlyList<CompletionFact> projectCompletions,
        IReadOnlyList<InvoiceRequestStatusFact> projectInvoiceRequests,
        decimal recordedHours,
        decimal? quotedHours,
        DateOnly today)
    {
        // On hold — the project's own explicit pause (IProjectLifecycleService.HoldAsync).
        if (project.Held)
            return (ProjectHealthStatus.OnHold, "On hold.");

        // Blocked — (a) an Accepted quote whose lines' own created
        // deliverables carry no completion at all yet: accepted, nothing
        // started. (b) a Sent invoice request the connector reports
        // Unavailable or Reauthorise (Unavailable is a sentinel that is
        // never actually persisted per `InvoiceRequestStatus`'s own
        // remarks; checked anyway, disclosed, rather than assumed
        // unreachable).
        var acceptedNotStarted = projectQuotations
            .Where(q => q.Status == QuotationStatus.Accepted)
            .FirstOrDefault(q => q.Lines.Count > 0 && q.Lines.All(l => l.DeliverableId is not { } id || !completedDeliverableIds.Contains(id)));
        if (acceptedNotStarted is not null)
            return (ProjectHealthStatus.Blocked, "Blocked: a quote was accepted but no deliverable has been started.");

        var stuckInvoice = projectInvoiceRequests.FirstOrDefault(
            r => r.Status is InvoiceRequestStatus.Unavailable or InvoiceRequestStatus.Reauthorise);
        if (stuckInvoice is not null)
            return (ProjectHealthStatus.Blocked, $"Blocked: an invoice request is {stuckInvoice.Status}.");

        // Overdue / At risk — any deliverable or milestone past (Overdue)
        // or within AtRiskWithinDays of (At risk) its target date and not
        // complete. A deliverable's own "due" is inherited from its parent
        // milestone (Deliverable carries no date of its own — a disclosed
        // heuristic already established by
        // Tempest.Workspace.Projects.ProjectTaskRegister.ResolveTargetAsync);
        // a milestone counts as "not complete" when it has no deliverables
        // at all, or any live deliverable under it lacks a completion —
        // itself a disclosed heuristic, since Milestone carries no
        // "achieved" flag of its own.
        DateOnly? earliestOverdue = null;
        DateOnly? earliestAtRisk = null;
        string? overdueWhat = null;
        string? atRiskWhat = null;

        foreach (var milestone in projectMilestones)
        {
            var underMilestone = deliverablesByMilestone[milestone.Id].ToList();
            var notComplete = underMilestone.Count == 0 || underMilestone.Any(d => !completedDeliverableIds.Contains(d.Id));
            if (!notComplete)
                continue;

            if (milestone.TargetDate < today && (earliestOverdue is null || milestone.TargetDate < earliestOverdue))
            {
                earliestOverdue = milestone.TargetDate;
                overdueWhat = milestone.Title;
            }
            else if (milestone.TargetDate >= today && (milestone.TargetDate.DayNumber - today.DayNumber) <= AtRiskWithinDays
                     && (earliestAtRisk is null || milestone.TargetDate < earliestAtRisk))
            {
                earliestAtRisk = milestone.TargetDate;
                atRiskWhat = milestone.Title;
            }
        }

        if (earliestOverdue is { } overdueDate)
            return (ProjectHealthStatus.Overdue, $"Overdue: milestone '{overdueWhat}' was due {overdueDate:yyyy-MM-dd}.");

        if (earliestAtRisk is { } atRiskDate)
            return (ProjectHealthStatus.AtRisk, $"At risk: milestone '{atRiskWhat}' is due {atRiskDate:yyyy-MM-dd}.");

        if (quotedHours is { } quoted && recordedHours > quoted)
        {
            return (
                ProjectHealthStatus.AtRisk,
                $"At risk: {recordedHours:0.##} hour(s) recorded against a {quoted:0.##}-hour quote.");
        }

        // Ready to invoice — a completion with no live invoice request.
        // `DeliverableCompletion.InvoicedBy` is set only once a request
        // reaches Sent (`ADR-0151` §5), so this is a direct read of that
        // one field — no separate scan of invoice requests is needed.
        var unInvoiced = projectCompletions.FirstOrDefault(c => c.InvoicedBy is null);
        if (unInvoiced is not null)
            return (ProjectHealthStatus.ReadyToInvoice, "Ready to invoice: a completion has not yet been invoiced.");

        return (ProjectHealthStatus.OnTrack, "On track.");
    }

    private static decimal? QuotedHoursOf(IReadOnlyList<QuotationFact> projectQuotations)
    {
        var accepted = projectQuotations.Where(q => q.Status == QuotationStatus.Accepted).ToList();
        if (accepted.Count == 0)
            return null;

        return accepted
            .SelectMany(q => q.Lines)
            .Where(l => l.Basis == QuotationLineBasis.Hourly && l.Hours is not null)
            .Sum(l => l.Hours!.Value);
    }

    private sealed record ProjectFact(Guid Id, string DisplayName, bool Held, DateOnly? StartDate, DateOnly? TargetDate, DateOnly? ClosedOn);

    private sealed record QuotationFact(Guid ProjectId, QuotationStatus Status, IReadOnlyList<QuotationLine> Lines);

    private sealed record MilestoneFact(Guid Id, string Title, Guid ProjectId, DateOnly TargetDate);

    private sealed record DeliverableFact(Guid Id, Guid MilestoneId);

    private sealed record CompletionFact(Guid ProjectId, Guid DeliverableId, Guid? InvoicedBy);

    private sealed record InvoiceRequestStatusFact(Guid ProjectId, InvoiceRequestStatus Status);

    // ================================================================
    // Plain-data parsing helpers — mirror WorkspaceSnapshotReader's own,
    // deliberately duplicated rather than shared (`TD-60`'s own passive-
    // read discipline: each reader owns its own degrade-to-default parse).
    // ================================================================

    private static readonly Dictionary<string, string?> EmptyTypeState = new(StringComparer.Ordinal);

    private static string? TypeString(IReadOnlyDictionary<string, string?> typeState, string key) =>
        typeState.TryGetValue(key, out var value) ? value : null;

    private static Guid? TypeGuid(IReadOnlyDictionary<string, string?> typeState, string key) =>
        Guid.TryParse(TypeString(typeState, key), out var value) ? value : null;

    private static bool TypeBool(IReadOnlyDictionary<string, string?> typeState, string key) =>
        bool.TryParse(TypeString(typeState, key), out var value) && value;

    private static decimal TypeDecimal(IReadOnlyDictionary<string, string?> typeState, string key) =>
        decimal.TryParse(TypeString(typeState, key), NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : 0m;

    private static DateOnly? TypeDate(IReadOnlyDictionary<string, string?> typeState, string key) =>
        DateTimeOffset.TryParse(TypeString(typeState, key), CultureInfo.InvariantCulture, out var value)
            ? DateOnly.FromDateTime(value.UtcDateTime)
            : null;

    private static TValue? TypeJson<TValue>(IReadOnlyDictionary<string, string?> typeState, string key)
    {
        var raw = TypeString(typeState, key);
        if (string.IsNullOrEmpty(raw))
            return default;

        try
        {
            return JsonSerializer.Deserialize<TValue>(raw);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    private static StateProjection? Deserialise(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<StateProjection>(json, DeserialiseOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed class StateProjection
    {
        public string? Kind { get; set; }

        public string? DisplayName { get; set; }

        public Guid? ParentId { get; set; }

        public bool IsDeleted { get; set; }

        public Dictionary<string, string?>? TypeState { get; set; }
    }
}
