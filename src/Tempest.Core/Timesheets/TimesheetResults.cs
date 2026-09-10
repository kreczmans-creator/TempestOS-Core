namespace Tempest.Core.Timesheets;

/// <summary>Why an <see cref="ITimesheetService"/> act was refused, or <see cref="None"/> if it was not.</summary>
/// <remarks>
/// A refusal is a first-class answer here, exactly as
/// <c>Tempest.Core.Evidence.EvidenceRefusal</c> is for citing an
/// unreleased reference record: recording against a project with no
/// Released rate-card pin, or against a grade that card does not price,
/// are ordinary engineering-governance findings a surface should show,
/// not error conditions.
/// </remarks>
public enum TimesheetRefusal
{
    /// <summary>Nothing was refused.</summary>
    None,

    /// <summary>No timesheet entry is registered under the requested id.</summary>
    EntryNotFound,

    /// <summary>The project has no Released rate-card pin, so no grade can be priced.</summary>
    NoRateCardPinned,

    /// <summary>The pinned rate card's own grade is not priced on it.</summary>
    GradeNotOnCard,

    /// <summary>This entry already carries an <see cref="TimesheetEntry.InvoicedBy"/> link — its rate, hours, task and billable flag are frozen.</summary>
    EntryInvoiced,
}

/// <summary>The outcome of an <see cref="ITimesheetService"/> act: either it happened, or a refusal that says why it did not.</summary>
/// <param name="Refusal">Why the act was refused, or <see cref="TimesheetRefusal.None"/>.</param>
/// <param name="Reason">The refusal in a sentence an engineer can read. <see langword="null"/> when nothing was refused.</param>
/// <param name="Entry">The entry acted on, when it could be resolved.</param>
public sealed record TimesheetResult(TimesheetRefusal Refusal, string? Reason, TimesheetEntry? Entry)
{
    /// <summary>Whether the act actually happened.</summary>
    public bool Succeeded => Refusal == TimesheetRefusal.None;
}
