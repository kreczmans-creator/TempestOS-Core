using Tempest.Core.Audit;
using Tempest.Core.Configuration;
using Tempest.Core.Identity;
using Tempest.Core.Logging;
using Tempest.Core.ReferenceData.Review;

namespace Tempest.Core.ReferenceData.Seeding;

/// <summary>
/// The Product Owner's standing instruction that shipped reference data is
/// released for engineering use the moment it is seeded, and the one
/// governed path by which that happens.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists (PO decision 2026-10-01).</b> Until this decision a
/// seeded record always landed <see cref="ReferenceValidationState.Draft"/>,
/// and every calculator reads released records only — so a freshly
/// installed product could populate its libraries and still calculate
/// nothing. The runbook step that bends a beam in S355J2 could not be run.
/// The Product Owner ruled that shipped libraries must be "fully usable from
/// day 1, otherwise it's a hard block against v1.0.0". This type is how that
/// ruling is carried out without weakening any governance rule the platform
/// already enforces.
/// </para>
/// <para>
/// <b>It walks the ordinary path, it does not route around it.</b> Release
/// still requires provenance verified by a named principal on a recorded
/// date (<see cref="ReferenceValidationStates.DescribeProvenanceShortfall"/>),
/// and still goes Draft → Checked → Validated → Released through
/// <see cref="ReferenceReviewService"/>, with the same permission checks
/// and the same audit rows a person's review writes. Nothing here sets a
/// validation state or a verification field directly.
/// </para>
/// <para>
/// <b>The reviewer is named for what it is.</b> The acting principal is
/// <see cref="SeedPrincipalId"/> — a fixed, non-human identity that holds
/// exactly the verify and release permissions and nothing else. Nobody's
/// name is borrowed. Anyone reading a record's provenance sees that it was
/// released by the seed process under this decision, and the statement the
/// review records says, in so many words, that the check was against the
/// cited secondary source as transcribed and that the primary standard must
/// be consulted before issue. A person who later checks a record against the
/// primary standard supersedes it with their own verified revision.
/// </para>
/// </remarks>
public sealed class ReferenceSeedReleasePolicy
{
    /// <summary>The principal id every release-at-seed is attributed to.</summary>
    public const string SeedPrincipalId = "tempest.reference-seed";

    /// <summary>The display name of <see cref="SeedPrincipalId"/>.</summary>
    public const string SeedPrincipalDisplayName = "Tempest reference seed (release at seed, PO decision 2026-10-01)";

    /// <summary>The decision this policy carries out, as recorded on every record it releases.</summary>
    public const string DecisionReference = "PO decision 2026-10-01";

    /// <summary>
    /// The configuration key that switches release-at-seed off. Absent, or any
    /// value other than <c>false</c>, leaves it on — the Product Owner's
    /// default. Setting it to <c>false</c> is the stricter choice: every
    /// shipped record then lands Draft and waits for a person's review, as it
    /// did before the decision (for example
    /// <c>--ReferenceData:ReleaseAtSeed=false</c> on the command line, or
    /// <c>TEMPEST_ReferenceData__ReleaseAtSeed=false</c>).
    /// </summary>
    public const string EnabledConfigurationKey = "ReferenceData:ReleaseAtSeed";

    private readonly IAuditRecorder? _auditRecorder;
    private readonly ILogger? _logger;
    private readonly TimeProvider _time;

    /// <summary>Initialises a new instance of the <see cref="ReferenceSeedReleasePolicy"/> class.</summary>
    /// <param name="auditRecorder">Where the verify and release rows go; the same recorder a person's review writes to.</param>
    /// <param name="logger">An optional logger.</param>
    /// <param name="configuration">Where <see cref="EnabledConfigurationKey"/> is read from; absent means enabled.</param>
    public ReferenceSeedReleasePolicy(IAuditRecorder? auditRecorder = null, ILogger? logger = null, IConfigurationProvider? configuration = null)
        : this(auditRecorder, logger, TimeProvider.System, ReadEnabled(configuration))
    {
    }

    private ReferenceSeedReleasePolicy(IAuditRecorder? auditRecorder, ILogger? logger, TimeProvider time, bool enabled)
    {
        _auditRecorder = auditRecorder;
        _logger = logger;
        _time = time;
        IsEnabled = enabled;
    }

    /// <summary>Whether this policy releases at seed (see <see cref="EnabledConfigurationKey"/>).</summary>
    public bool IsEnabled { get; }

    private static bool ReadEnabled(IConfigurationProvider? configuration) =>
        configuration is null
        || !configuration.TryGetValue(EnabledConfigurationKey, out var value)
        || !string.Equals(value?.Trim(), "false", StringComparison.OrdinalIgnoreCase);

    /// <summary>Creates a policy whose verification date is taken from <paramref name="time"/> — for tests that pin the clock.</summary>
    /// <param name="time">The clock the verification date is read from.</param>
    /// <param name="auditRecorder">An optional audit recorder.</param>
    /// <returns>A policy reading its date from <paramref name="time"/>.</returns>
    public static ReferenceSeedReleasePolicy WithClock(TimeProvider time, IAuditRecorder? auditRecorder = null)
    {
        ArgumentNullException.ThrowIfNull(time);
        return new ReferenceSeedReleasePolicy(auditRecorder, null, time, enabled: true);
    }

    /// <summary>
    /// The statement recorded against a record released at seed — the words
    /// the Product Owner's decision asked for, with the source named.
    /// </summary>
    /// <param name="provenance">The seeded record's own provenance.</param>
    /// <returns>The release statement.</returns>
    public static string ReleaseStatement(ReferenceProvenance provenance)
    {
        ArgumentNullException.ThrowIfNull(provenance);

        return $"Seeded from {DescribeSource(provenance)}; released at seed for day-one use ({DecisionReference}); "
            + "verify against the primary standard before issue.";
    }

    /// <summary>
    /// Verifies and releases one freshly seeded record through
    /// <see cref="ReferenceReviewService"/>, as <see cref="SeedPrincipalId"/>.
    /// </summary>
    /// <typeparam name="TDefinition">The library's own definition type.</typeparam>
    /// <param name="catalog">The library holding the record.</param>
    /// <param name="recordId">The record to release.</param>
    /// <param name="cancellationToken">A token observed while writing.</param>
    /// <returns>The released record.</returns>
    public async Task<IReferenceRecord<TDefinition>> ReleaseAsync<TDefinition>(
        IReferenceDataCatalog<TDefinition> catalog,
        string recordId,
        CancellationToken cancellationToken = default)
        where TDefinition : class
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentException.ThrowIfNullOrWhiteSpace(recordId);

        var record = await catalog.FindAsync(recordId, cancellationToken).ConfigureAwait(false)
            ?? throw new ReferenceRecordNotFoundException(catalog.LibraryName, recordId);

        var statement = ReleaseStatement(record.Provenance);
        var review = CreateReviewer();

        await review.VerifyAsync(
            catalog,
            recordId,
            new ReferenceReviewStatement(
                DescribeSource(record.Provenance),
                statement + " The check this verification records is against the cited secondary source "
                + "document as transcribed at acquisition, not against the primary standard itself."),
            cancellationToken).ConfigureAwait(false);

        var released = await review.ReleaseAsync(catalog, recordId, statement, cancellationToken).ConfigureAwait(false);

        _logger?.Debug($"{catalog.LibraryName} record '{recordId}' released at seed ({DecisionReference}).");

        return released;
    }

    private ReferenceReviewService CreateReviewer() =>
        new(new FixedPrincipalAccessor(SeedPrincipal), _time, _logger, _auditRecorder, new PermissionEvaluator());

    private static readonly IPrincipal SeedPrincipal = new PlatformPrincipal(
        new PlatformIdentity(SeedPrincipalId, SeedPrincipalDisplayName),
        [ReferenceReviewService.VerifyPermission, ReferenceReviewService.ReleasePermission]);

    private static string DescribeSource(ReferenceProvenance provenance) =>
        string.IsNullOrWhiteSpace(provenance.SourceDocument)
            ? provenance.SourceOrganisation ?? "an unnamed source"
            : $"{provenance.SourceOrganisation} — {provenance.SourceDocument}";

    private sealed class FixedPrincipalAccessor(IPrincipal principal) : ICurrentPrincipalAccessor
    {
        public IPrincipal? Current { get; } = principal;
    }
}
