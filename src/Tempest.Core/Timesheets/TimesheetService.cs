using Tempest.Core.BusinessGovernance.Pricing;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Projects;

namespace Tempest.Core.Timesheets;

/// <summary>The concrete <see cref="ITimesheetService"/> implementation.</summary>
public sealed class TimesheetService : ITimesheetService
{
    private readonly EngineeringDomainContext _context;
    private readonly IRateCardCatalog _rateCards;
    private readonly IWorkingPatternProvider _workingPatterns;
    private readonly TimeProvider _time;

    /// <summary>Initialises a new instance of the <see cref="TimesheetService"/> class.</summary>
    /// <param name="timeProvider">The clock the archived-project guard reads "now" from (`WP 19.5C`). <see langword="null"/> — the default — is <see cref="TimeProvider.System"/>.</param>
    public TimesheetService(
        EngineeringDomainContext context, IRateCardCatalog rateCards, IWorkingPatternProvider workingPatterns, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(rateCards);
        ArgumentNullException.ThrowIfNull(workingPatterns);

        _context = context;
        _rateCards = rateCards;
        _workingPatterns = workingPatterns;
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public async Task<TimesheetResult> RecordAsync(
        Guid projectId, DateOnly date, decimal hours, bool billable, string grade, string task, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(grade);
        ArgumentException.ThrowIfNullOrWhiteSpace(task);

        if (await _context.Repository.FindAsync(projectId, cancellationToken).ConfigureAwait(false) is not Project project)
            throw new ArgumentException($"'{projectId}' does not identify a live project.", nameof(projectId));

        if (project.RateCardPin is not { } pin)
        {
            return new TimesheetResult(
                TimesheetRefusal.NoRateCardPinned,
                $"Project '{projectId}' has no Released rate-card pin; time cannot be priced against it.",
                null);
        }

        var cardAtPin = await _rateCards.GetRevisionAsync(pin.RecordId, pin.RevisionNumber, cancellationToken).ConfigureAwait(false);
        var resolution = cardAtPin.Definition.ResolveRates(grade);

        if (!resolution.Succeeded)
            return new TimesheetResult(TimesheetRefusal.GradeNotOnCard, resolution.Reason, null);

        if (ProjectArchival.IsArchived(project, _time.GetUtcNow()))
        {
            return new TimesheetResult(
                TimesheetRefusal.ProjectArchived, $"Project '{projectId}' is archived (closed {project.ClosedOn:O}); no new time can be recorded against it.", null);
        }

        var principalId = _context.ResolveCurrentPrincipalId();

        await _workingPatterns.EnsureRegisteredAsync(principalId, cancellationToken).ConfigureAwait(false);

        var created = await new EngineeringObjectFactory<TimesheetEntry>(
            TimesheetEntry.CanonicalKind,
            _context,
            (doc, rev) => new TimesheetEntry(
                doc, rev, _context, identifier: null, $"{task} — {date:yyyy-MM-dd}", EngineeringObjectMetadata.Empty,
                principalId, projectId, task, date, hours, billable, grade, resolution.Billing!.Value, resolution.Cost))
            .CreateAsync($"Timesheet entry recorded — {task}.", cancellationToken)
            .ConfigureAwait(false);

        if (created is IHasParent hasParent)
            await hasParent.MoveAsync(projectId, cancellationToken).ConfigureAwait(false);

        return new TimesheetResult(TimesheetRefusal.None, null, (TimesheetEntry)created);
    }

    /// <inheritdoc />
    public async Task<TimesheetResult> AmendAsync(Guid entryId, decimal hours, string task, bool billable, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(task);

        var entry = await FindEntryAsync(entryId, cancellationToken).ConfigureAwait(false);
        if (entry is null)
            return NotFound(entryId);

        if (entry.InvoicedBy is not null)
            return Invoiced(entry);

        if (await ArchivedAsync(entry, cancellationToken).ConfigureAwait(false) is { } archived)
            return archived;

        await entry.AmendAsync(hours, task, billable, cancellationToken).ConfigureAwait(false);

        return new TimesheetResult(TimesheetRefusal.None, null, entry);
    }

    /// <inheritdoc />
    public async Task<TimesheetResult> DeleteAsync(Guid entryId, CancellationToken cancellationToken = default)
    {
        var entry = await FindEntryAsync(entryId, cancellationToken).ConfigureAwait(false);
        if (entry is null)
            return NotFound(entryId);

        if (entry.InvoicedBy is not null)
            return Invoiced(entry);

        if (await ArchivedAsync(entry, cancellationToken).ConfigureAwait(false) is { } archived)
            return archived;

        await entry.DeleteAsync(cancellationToken).ConfigureAwait(false);

        return new TimesheetResult(TimesheetRefusal.None, null, entry);
    }

    /// <inheritdoc />
    public async Task<TimesheetResult> MarkInvoicedAsync(Guid entryId, Guid requestId, CancellationToken cancellationToken = default)
    {
        var entry = await FindEntryAsync(entryId, cancellationToken).ConfigureAwait(false);
        if (entry is null)
            return NotFound(entryId);

        if (entry.InvoicedBy is not null)
            return Invoiced(entry);

        if (await ArchivedAsync(entry, cancellationToken).ConfigureAwait(false) is { } archived)
            return archived;

        await entry.MarkInvoicedAsync(requestId, cancellationToken).ConfigureAwait(false);

        return new TimesheetResult(TimesheetRefusal.None, null, entry);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TimesheetEntry>> ListForPrincipalWeekAsync(string identityId, DateOnly weekStart, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identityId);

        // `TD-88`/`WP 21.5B`: principal/date are `TimesheetEntry`-own
        // fields, not on the index row.
        var entries = await _context.Repository.ListByKindAsync(TimesheetEntry.CanonicalKind, cancellationToken).ConfigureAwait(false);
        var all = await _context.Repository.MaterialiseAsync<TimesheetEntry>(entries, cancellationToken).ConfigureAwait(false);

        return all
            .Where(e => IsLive(e) && string.Equals(e.PrincipalIdentityId, identityId, StringComparison.Ordinal) && TimesheetWeek.WeekOf(e.Date) == weekStart)
            .OrderBy(e => e.Date)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TimesheetEntry>> ListUnbilledForProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        // `TD-88`/`WP 21.5B`: billable/invoiced-by are `TimesheetEntry`-own
        // fields, not on the index row.
        var childEntries = await _context.Repository.ListChildrenAsync(projectId, cancellationToken).ConfigureAwait(false);
        var children = await _context.Repository.MaterialiseAsync<TimesheetEntry>(childEntries, cancellationToken).ConfigureAwait(false);

        return children
            .Where(e => IsLive(e) && e.Billable && e.InvoicedBy is null)
            .OrderBy(e => e.Date)
            .ToList();
    }

    private async Task<TimesheetEntry?> FindEntryAsync(Guid entryId, CancellationToken cancellationToken) =>
        await _context.Repository.FindAsync(entryId, cancellationToken).ConfigureAwait(false) as TimesheetEntry;

    /// <summary>The archived-project guard (`WP 19.5C`): every mutating command on an archived project's objects is refused, here, before its own mutator ever runs.</summary>
    private async Task<TimesheetResult?> ArchivedAsync(TimesheetEntry entry, CancellationToken cancellationToken)
    {
        if (entry.ParentId is not { } projectId
            || await _context.Repository.FindAsync(projectId, cancellationToken).ConfigureAwait(false) is not Project project)
        {
            return null;
        }

        return ProjectArchival.IsArchived(project, _time.GetUtcNow())
            ? new TimesheetResult(TimesheetRefusal.ProjectArchived, $"Project '{projectId}' is archived (closed {project.ClosedOn:O}); this entry is read-only.", entry)
            : null;
    }

    private static TimesheetResult NotFound(Guid entryId) =>
        new(TimesheetRefusal.EntryNotFound, $"No timesheet entry '{entryId}' is registered.", null);

    private static TimesheetResult Invoiced(TimesheetEntry entry) =>
        new(TimesheetRefusal.EntryInvoiced, $"Timesheet entry '{entry.Id}' is already invoiced (request '{entry.InvoicedBy:N}'); it is frozen.", entry);

    private static bool IsLive(IEngineeringObject o) => o is not IDeletable { IsDeleted: true };
}
