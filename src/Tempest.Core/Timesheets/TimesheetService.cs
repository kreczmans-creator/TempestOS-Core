using Tempest.Core.BusinessGovernance.Pricing;
using Tempest.Core.EngineeringDomain;

namespace Tempest.Core.Timesheets;

/// <summary>The concrete <see cref="ITimesheetService"/> implementation.</summary>
public sealed class TimesheetService : ITimesheetService
{
    private readonly EngineeringDomainContext _context;
    private readonly IRateCardCatalog _rateCards;
    private readonly IWorkingPatternProvider _workingPatterns;

    /// <summary>Initialises a new instance of the <see cref="TimesheetService"/> class.</summary>
    public TimesheetService(EngineeringDomainContext context, IRateCardCatalog rateCards, IWorkingPatternProvider workingPatterns)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(rateCards);
        ArgumentNullException.ThrowIfNull(workingPatterns);

        _context = context;
        _rateCards = rateCards;
        _workingPatterns = workingPatterns;
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

        await entry.MarkInvoicedAsync(requestId, cancellationToken).ConfigureAwait(false);

        return new TimesheetResult(TimesheetRefusal.None, null, entry);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TimesheetEntry>> ListForPrincipalWeekAsync(string identityId, DateOnly weekStart, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identityId);

        var all = await _context.Repository.ListByKindAsync(TimesheetEntry.CanonicalKind, cancellationToken).ConfigureAwait(false);

        return all
            .OfType<TimesheetEntry>()
            .Where(e => IsLive(e) && string.Equals(e.PrincipalIdentityId, identityId, StringComparison.Ordinal) && TimesheetWeek.WeekOf(e.Date) == weekStart)
            .OrderBy(e => e.Date)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TimesheetEntry>> ListUnbilledForProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var children = await _context.Repository.ListChildrenAsync(projectId, cancellationToken).ConfigureAwait(false);

        return children
            .OfType<TimesheetEntry>()
            .Where(e => IsLive(e) && e.Billable && e.InvoicedBy is null)
            .OrderBy(e => e.Date)
            .ToList();
    }

    private async Task<TimesheetEntry?> FindEntryAsync(Guid entryId, CancellationToken cancellationToken) =>
        await _context.Repository.FindAsync(entryId, cancellationToken).ConfigureAwait(false) as TimesheetEntry;

    private static TimesheetResult NotFound(Guid entryId) =>
        new(TimesheetRefusal.EntryNotFound, $"No timesheet entry '{entryId}' is registered.", null);

    private static TimesheetResult Invoiced(TimesheetEntry entry) =>
        new(TimesheetRefusal.EntryInvoiced, $"Timesheet entry '{entry.Id}' is already invoiced (request '{entry.InvoicedBy:N}'); it is frozen.", entry);

    private static bool IsLive(IEngineeringObject o) => o is not IDeletable { IsDeleted: true };
}
