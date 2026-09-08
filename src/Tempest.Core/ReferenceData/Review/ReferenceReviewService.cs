using Tempest.Core.Audit;
using Tempest.Core.Identity;
using Tempest.Core.Logging;

namespace Tempest.Core.ReferenceData.Review;

/// <summary>What a reviewer states they did when verifying a record.</summary>
/// <param name="SourceConsulted">
/// The document the reviewer actually opened, in their own words. Required:
/// a verification that cannot name what was checked against is not a
/// verification.
/// </param>
/// <param name="Notes">Anything else the reviewer wants on the record. <see langword="null"/> if none.</param>
public sealed record ReferenceReviewStatement(string SourceConsulted, string? Notes = null);

/// <summary>
/// The governed act of a person verifying a reference record against its
/// own source, and of releasing a verified record.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this type exists.</b> Reaching
/// <see cref="ReferenceValidationState.Released"/> requires writing a
/// reviewer's principal id, a verification date and
/// <see cref="ReferenceVerificationStatus.VerifiedAgainstSource"/> into a
/// record's provenance. Before this service, those were three fields any
/// caller could set to any value: a reviewer's name was a string, so a
/// review could be asserted by anybody with write access and by any
/// process, including an automated one. The integration phase raised that
/// as a finding and deferred it; this closes it.
/// </para>
/// <para>
/// <b>What it changes.</b> The reviewer is taken from
/// <see cref="ICurrentPrincipalAccessor"/> and the date from an injected
/// <see cref="TimeProvider"/>. Neither is a parameter, so neither can be
/// supplied — a caller can no longer name somebody else as the reviewer,
/// and cannot backdate the review. With nobody signed in the act is
/// refused outright rather than attributed to an "unknown" principal,
/// because an unattributed verification is worth less than none: it looks
/// like assurance and carries none.
/// </para>
/// <para>
/// <b>What it deliberately does not do.</b> It does not decide whether a
/// record is correct, and it does not read the source. A person opens the
/// document, compares the values, and then records that they did so. This
/// service is the pen, not the reviewer — which is why
/// <see cref="ReferenceReviewStatement.SourceConsulted"/> is required and
/// stored verbatim: the record must say what was checked against, in the
/// words of whoever checked it.
/// </para>
/// <para>
/// <b>Release is a second, separate act.</b>
/// <see cref="VerifyAsync{TDefinition}"/> records the review and moves the
/// record to <see cref="ReferenceValidationState.Checked"/>;
/// <see cref="ReleaseAsync{TDefinition}"/> walks it to
/// <see cref="ReferenceValidationState.Released"/>. Splitting them keeps
/// the door open for the two to be done by different people, and means
/// verifying a record is not the same keystroke as making engineering work
/// depend on it.
/// </para>
/// </remarks>
public sealed class ReferenceReviewService
{
    /// <summary>The <see cref="Audit.IAuditRecord.Action"/> recorded by <see cref="VerifyAsync{TDefinition}"/> (`WP 17.2A`).</summary>
    public const string ReferenceVerifiedActionName = "reference.verified";

    /// <summary>The <see cref="Audit.IAuditRecord.Action"/> recorded by <see cref="ReleaseAsync{TDefinition}"/> (`WP 17.2A`).</summary>
    public const string ReferenceReleasedActionName = "reference.released";

    private readonly ICurrentPrincipalAccessor _principals;
    private readonly TimeProvider _time;
    private readonly ILogger? _logger;
    private readonly IAuditRecorder? _auditRecorder;

    /// <summary>Initialises a new instance of the <see cref="ReferenceReviewService"/> class.</summary>
    /// <param name="principals">Where the reviewer's identity comes from. Never a parameter of the review itself.</param>
    /// <param name="timeProvider">Where the verification date comes from. Injectable so a test can state the date rather than depend on the day it runs.</param>
    /// <param name="logger">An optional logger.</param>
    /// <param name="auditRecorder">
    /// Records a <c>reference.verified</c>/<c>reference.released</c> audit
    /// row for every review act (`WP 17.2A`, ADR-0146). Optional and
    /// nullable, defaulting to <see langword="null"/>, so a hand-assembled
    /// test context keeps working unchanged; without it, review behaves
    /// exactly as before and writes no audit row.
    /// </param>
    public ReferenceReviewService(
        ICurrentPrincipalAccessor principals,
        TimeProvider? timeProvider = null,
        ILogger? logger = null,
        IAuditRecorder? auditRecorder = null)
    {
        ArgumentNullException.ThrowIfNull(principals);

        _principals = principals;
        _time = timeProvider ?? TimeProvider.System;
        _logger = logger;
        _auditRecorder = auditRecorder;
    }

    /// <summary>
    /// Records that the signed-in principal has checked
    /// <paramref name="recordId"/> against its own source, and moves it to
    /// <see cref="ReferenceValidationState.Checked"/>.
    /// </summary>
    /// <typeparam name="TDefinition">The library's own definition type.</typeparam>
    /// <param name="catalog">The library holding the record.</param>
    /// <param name="recordId">The record being verified.</param>
    /// <param name="statement">What the reviewer states they consulted.</param>
    /// <param name="cancellationToken">A token observed while writing.</param>
    /// <returns>The record, verified and Checked.</returns>
    /// <exception cref="ReferenceReviewException">Nobody is signed in, or the record already claims verification.</exception>
    /// <exception cref="ReferenceRecordNotFoundException"><paramref name="recordId"/> is not registered.</exception>
    public async Task<IReferenceRecord<TDefinition>> VerifyAsync<TDefinition>(
        IReferenceDataCatalog<TDefinition> catalog,
        string recordId,
        ReferenceReviewStatement statement,
        CancellationToken cancellationToken = default)
        where TDefinition : class
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentException.ThrowIfNullOrWhiteSpace(recordId);
        ArgumentNullException.ThrowIfNull(statement);
        ArgumentException.ThrowIfNullOrWhiteSpace(statement.SourceConsulted);

        var reviewer = RequireReviewer(catalog.LibraryName, recordId);

        var record = await catalog.FindAsync(recordId, cancellationToken).ConfigureAwait(false)
            ?? throw new ReferenceRecordNotFoundException(catalog.LibraryName, recordId);

        if (record.Provenance.IsVerified)
        {
            throw new ReferenceReviewException(
                catalog.LibraryName,
                recordId,
                $"it is already verified by '{record.Provenance.ReviewerPrincipalId}' on "
                + $"{record.Provenance.VerificationDate:yyyy-MM-dd}. Revise the record to withdraw that "
                + "verification before recording a new one.");
        }

        if (!record.Provenance.IdentifiesASource)
        {
            throw new ReferenceReviewException(
                catalog.LibraryName,
                recordId,
                "its provenance names neither a source organisation nor a source document, so there is "
                + "nothing for a reviewer to have checked it against.");
        }

        var verifiedOn = DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime);

        var verified = await catalog.ReviseAsync(
            recordId,
            record.Definition,
            record.Provenance with
            {
                VerificationStatus = ReferenceVerificationStatus.VerifiedAgainstSource,
                ReviewerPrincipalId = reviewer,
                VerificationDate = verifiedOn,
                Notes = Append(record.Provenance.Notes, BuildReviewNote(reviewer, verifiedOn, statement)),
            },
            $"Verified against source by '{reviewer}' on {verifiedOn:yyyy-MM-dd}.",
            cancellationToken).ConfigureAwait(false);

        _logger?.Information(
            $"{catalog.LibraryName} record '{recordId}' verified by '{reviewer}' against '{statement.SourceConsulted}'.");

        var checkedRecord = await catalog.SetValidationStateAsync(
            recordId,
            ReferenceValidationState.Checked,
            $"Checked following verification by '{reviewer}'.",
            cancellationToken).ConfigureAwait(false);

        if (_auditRecorder is not null)
        {
            await _auditRecorder.RecordAsync(
                ReferenceVerifiedActionName,
                new Dictionary<string, string>
                {
                    ["Subject"] = recordId,
                    ["Revision"] = checkedRecord.RevisionNumber.ToString(),
                },
                cancellationToken).ConfigureAwait(false);
        }

        return checkedRecord;
    }

    /// <summary>
    /// Walks a verified record from
    /// <see cref="ReferenceValidationState.Checked"/> to
    /// <see cref="ReferenceValidationState.Released"/>, so engineering work
    /// may rely on it.
    /// </summary>
    /// <typeparam name="TDefinition">The library's own definition type.</typeparam>
    /// <param name="catalog">The library holding the record.</param>
    /// <param name="recordId">The record being released.</param>
    /// <param name="rationale">Why it is being released. Required — a release with no stated reason is not reviewable.</param>
    /// <param name="cancellationToken">A token observed while writing.</param>
    /// <returns>The released record.</returns>
    /// <exception cref="ReferenceReviewException">Nobody is signed in, or the record has not been verified.</exception>
    public async Task<IReferenceRecord<TDefinition>> ReleaseAsync<TDefinition>(
        IReferenceDataCatalog<TDefinition> catalog,
        string recordId,
        string rationale,
        CancellationToken cancellationToken = default)
        where TDefinition : class
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentException.ThrowIfNullOrWhiteSpace(recordId);
        ArgumentException.ThrowIfNullOrWhiteSpace(rationale);

        var releaser = RequireReviewer(catalog.LibraryName, recordId);

        var record = await catalog.FindAsync(recordId, cancellationToken).ConfigureAwait(false)
            ?? throw new ReferenceRecordNotFoundException(catalog.LibraryName, recordId);

        if (!record.Provenance.IsVerified)
        {
            throw new ReferenceReviewException(
                catalog.LibraryName,
                recordId,
                "it has not been verified against its source. Call VerifyAsync first — being imported, "
                + "revised or approved is not being verified.");
        }

        // Checked -> Validated -> Released. The lifecycle refuses any jump,
        // and this walks it rather than routing around it.
        if (record.ValidationState == ReferenceValidationState.Checked)
        {
            await catalog.SetValidationStateAsync(
                recordId,
                ReferenceValidationState.Validated,
                $"Validated by '{releaser}': {rationale}",
                cancellationToken).ConfigureAwait(false);
        }

        var released = await catalog.SetValidationStateAsync(
            recordId,
            ReferenceValidationState.Released,
            $"Released by '{releaser}': {rationale}",
            cancellationToken).ConfigureAwait(false);

        _logger?.Information($"{catalog.LibraryName} record '{recordId}' released by '{releaser}'.");

        if (_auditRecorder is not null)
        {
            await _auditRecorder.RecordAsync(
                ReferenceReleasedActionName,
                new Dictionary<string, string>
                {
                    ["Subject"] = recordId,
                    ["Revision"] = released.RevisionNumber.ToString(),
                },
                cancellationToken).ConfigureAwait(false);
        }

        return released;
    }

    private string RequireReviewer(string library, string recordId)
    {
        var reviewer = _principals.Current?.Identity.Id;

        if (string.IsNullOrWhiteSpace(reviewer))
        {
            throw new ReferenceReviewException(
                library,
                recordId,
                "no principal is signed in. A verification nobody can be held to is not a verification, so "
                + "the act is refused rather than attributed to an unknown principal.");
        }

        return reviewer;
    }

    private static string BuildReviewNote(string reviewer, DateOnly verifiedOn, ReferenceReviewStatement statement) =>
        $"VERIFIED {verifiedOn:yyyy-MM-dd} by principal '{reviewer}' against: {statement.SourceConsulted}."
        + (string.IsNullOrWhiteSpace(statement.Notes) ? string.Empty : $" Reviewer notes: {statement.Notes}");

    private static string Append(string? existing, string addition) =>
        string.IsNullOrWhiteSpace(existing) ? addition : $"{existing} {addition}";
}
