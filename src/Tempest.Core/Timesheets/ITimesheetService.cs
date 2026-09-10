namespace Tempest.Core.Timesheets;

/// <summary>
/// The acts a <see cref="TimesheetEntry"/> supports: record, amend,
/// delete, mark invoiced, and the two list queries the weekly timesheet
/// view and the unbilled-work read model need (`WP 19.0A`, `ADR-0150`).
/// Every act is one transaction with an audit row; whether an act is
/// <em>permitted</em> — whether the project has a Released rate-card pin,
/// whether the grade is on it, whether an entry is already invoiced — is
/// decided here, before <see cref="TimesheetEntry"/>'s own mutator ever
/// runs, and reported back as a refusal result rather than an exception,
/// mirroring <c>Tempest.Core.Evidence.IEvidenceService</c>.
/// </summary>
public interface ITimesheetService
{
    /// <summary>
    /// Records <paramref name="hours"/> of <paramref name="grade"/>'s own
    /// time against <paramref name="projectId"/> on <paramref name="date"/>,
    /// for the current principal — billing and cost rate resolved from the
    /// project's own pinned rate card and frozen from this moment on.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="projectId"/> does not identify a live project.</exception>
    /// <remarks>Refused, as a result, when the project has no Released rate-card pin, or the pinned card does not price <paramref name="grade"/>.</remarks>
    Task<TimesheetResult> RecordAsync(Guid projectId, DateOnly date, decimal hours, bool billable, string grade, string task, CancellationToken cancellationToken = default);

    /// <summary>Amends <paramref name="entryId"/>'s own hours, task and billable flag. Refused, as a result, once the entry carries an <see cref="TimesheetEntry.InvoicedBy"/> link.</summary>
    Task<TimesheetResult> AmendAsync(Guid entryId, decimal hours, string task, bool billable, CancellationToken cancellationToken = default);

    /// <summary>Soft-deletes <paramref name="entryId"/>'s own entry. Refused, as a result, once the entry carries an <see cref="TimesheetEntry.InvoicedBy"/> link.</summary>
    Task<TimesheetResult> DeleteAsync(Guid entryId, CancellationToken cancellationToken = default);

    /// <summary>Sets <paramref name="entryId"/>'s own <see cref="TimesheetEntry.InvoicedBy"/> link to <paramref name="requestId"/>. Refused, as a result, if the entry already carries one.</summary>
    Task<TimesheetResult> MarkInvoicedAsync(Guid entryId, Guid requestId, CancellationToken cancellationToken = default);

    /// <summary>Every live entry for <paramref name="identityId"/> in the week starting <paramref name="weekStart"/>, read as one coherent list.</summary>
    Task<IReadOnlyList<TimesheetEntry>> ListForPrincipalWeekAsync(string identityId, DateOnly weekStart, CancellationToken cancellationToken = default);

    /// <summary>Every live, billable entry for <paramref name="projectId"/> carrying no <see cref="TimesheetEntry.InvoicedBy"/> link, read as one coherent list — the unbilled-work read model.</summary>
    Task<IReadOnlyList<TimesheetEntry>> ListUnbilledForProjectAsync(Guid projectId, CancellationToken cancellationToken = default);
}
