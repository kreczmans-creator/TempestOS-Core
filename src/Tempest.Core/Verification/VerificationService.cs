using System.Text.Json;
using Tempest.Core.EngineeringData;
using Tempest.Core.Identity;
using Tempest.Core.Logging;

namespace Tempest.Core.Verification;

/// <summary>
/// The concrete <see cref="IVerificationService"/> implementation.
/// </summary>
/// <remarks>
/// <para>
/// <b>Verification history is queried through the Data Model's own
/// existing reference mechanism</b> (`ADR-0057`), not a new index:
/// <see cref="RecordAsync"/> creates each verification as its own
/// <see cref="IEngineeringDocument"/> of
/// <c>Kind = "VerificationRecord"</c>, then links the subject document to
/// it (<see cref="VerifiedByRelationshipKind"/>) via
/// <see cref="IEngineeringDocumentStore.LinkAsync"/>.
/// <see cref="GetVerificationHistoryAsync"/> reads exactly this back via
/// <see cref="IEngineeringDocumentStore.GetReferencesAsync"/> — no direct
/// <see cref="Persistence.IPersistenceStore"/> dependency is needed at
/// all, unlike <see cref="Materials.MaterialCatalog"/>, since this
/// framework never looks anything up by an arbitrary caller-chosen
/// string key, only by <see cref="IEngineeringDocument"/> Id.
/// </para>
/// <para>
/// <b>Permission-gated read access</b>, mirroring
/// <see cref="Audit.AuditQuery"/>'s own identical pattern: every call to
/// <see cref="GetVerificationHistoryAsync"/> requires
/// <see cref="ReadPermission"/>, checked against the current principal
/// via <see cref="IPermissionEvaluator.RequirePermission"/>. Recording
/// (<see cref="RecordAsync"/>) is not permission-gated, mirroring
/// <see cref="Audit.IAuditRecorder.RecordAsync"/>'s own identical,
/// already-established asymmetry (only the read side is gated).
/// </para>
/// <para>
/// <b>Error handling reuses <see cref="EngineeringDocumentNotFoundException"/>
/// directly</b> — no parallel Verification-specific exception type
/// exists, exactly as `WP7.0C Engineering Foundation Contracts.md`
/// itself specified and `WP7.1A Future Capability Recommendations.md`
/// Recommendation 2 anticipated. <see cref="RecordAsync"/> checks
/// <paramref name="subjectDocumentId" /> exists before creating any
/// document, avoiding an orphaned "VerificationRecord" document on the
/// common failure path.
/// </para>
/// </remarks>
public sealed class VerificationService : IVerificationService
{
    /// <summary>The <see cref="IEngineeringDocument.Kind"/> every verification's own backing document carries.</summary>
    public const string VerificationRecordDocumentKind = "VerificationRecord";

    /// <summary>The relationship kind recorded from a subject document to each of its own verification records.</summary>
    public const string VerifiedByRelationshipKind = "verifiedBy";

    /// <summary>The relationship kind recorded from a verification record to each additional linked document.</summary>
    public const string ReferencesRelationshipKind = "references";

    /// <summary>The relationship kind recorded from a verification record to each linked calculation record.</summary>
    public const string BasedOnCalculationRelationshipKind = "basedOnCalculation";

    /// <summary>The <see cref="IVerificationRecord.VerifiedByPrincipalId"/> recorded when no principal is currently established.</summary>
    public const string UnknownVerifierPrincipalId = "unknown";

    /// <summary>The permission a principal must hold to call <see cref="GetVerificationHistoryAsync"/>.</summary>
    public static readonly Permission ReadPermission = new("verification.read");

    private readonly IEngineeringDocumentStore _documentStore;
    private readonly ICurrentPrincipalAccessor _currentPrincipalAccessor;
    private readonly IPermissionEvaluator _permissionEvaluator;
    private readonly ILogger? _logger;

    /// <summary>
    /// Initialises a new instance of the <see cref="VerificationService"/> class.
    /// </summary>
    /// <param name="documentStore">The store this instance's own verification records are durably held in.</param>
    /// <param name="currentPrincipalAccessor">The service this instance resolves the acting principal from.</param>
    /// <param name="permissionEvaluator">The service this instance checks <see cref="ReadPermission"/> against.</param>
    /// <param name="logger">An optional logger for diagnostic output.</param>
    /// <exception cref="ArgumentNullException"><paramref name="documentStore"/>, <paramref name="currentPrincipalAccessor"/>, or <paramref name="permissionEvaluator"/> is <see langword="null"/>.</exception>
    public VerificationService(
        IEngineeringDocumentStore documentStore,
        ICurrentPrincipalAccessor currentPrincipalAccessor,
        IPermissionEvaluator permissionEvaluator,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(documentStore);
        ArgumentNullException.ThrowIfNull(currentPrincipalAccessor);
        ArgumentNullException.ThrowIfNull(permissionEvaluator);

        _documentStore = documentStore;
        _currentPrincipalAccessor = currentPrincipalAccessor;
        _permissionEvaluator = permissionEvaluator;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IVerificationRecord> RecordAsync(
        Guid subjectDocumentId,
        VerificationOutcome outcome,
        string method,
        VerificationContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(method);
        ArgumentNullException.ThrowIfNull(context);

        if (await _documentStore.FindAsync(subjectDocumentId, cancellationToken).ConfigureAwait(false) is null)
            throw new EngineeringDocumentNotFoundException(subjectDocumentId);

        var verifiedAt = DateTimeOffset.UtcNow;
        var verifiedBy = ResolveVerifierPrincipalId();

        var dto = new VerificationRecordDto(
            subjectDocumentId, outcome, method, context.Criteria, context.Evidence,
            context.LinkedDocumentIds, context.LinkedCalculationRecordIds, context.ReferencedMaterialIds,
            verifiedBy, verifiedAt);

        var document = await _documentStore.CreateAsync(VerificationRecordDocumentKind, JsonSerializer.Serialize(dto), cancellationToken)
            .ConfigureAwait(false);

        await _documentStore.LinkAsync(subjectDocumentId, document.Id, VerifiedByRelationshipKind, cancellationToken).ConfigureAwait(false);

        foreach (var linkedDocumentId in context.LinkedDocumentIds)
            await _documentStore.LinkAsync(document.Id, linkedDocumentId, ReferencesRelationshipKind, cancellationToken).ConfigureAwait(false);

        foreach (var calculationRecordId in context.LinkedCalculationRecordIds)
            await _documentStore.LinkAsync(document.Id, calculationRecordId, BasedOnCalculationRelationshipKind, cancellationToken).ConfigureAwait(false);

        _logger?.Information($"Verification recorded: '{document.Id}' for subject '{subjectDocumentId}' (outcome {outcome}).");

        return new VerificationRecord(
            document.Id, subjectDocumentId, outcome, method, context.Criteria, context.Evidence,
            context.LinkedDocumentIds, context.LinkedCalculationRecordIds, context.ReferencedMaterialIds,
            verifiedBy, verifiedAt, document.CurrentRevisionNumber);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<IVerificationRecord>> GetVerificationHistoryAsync(Guid subjectDocumentId, CancellationToken cancellationToken = default)
    {
        var principal = _currentPrincipalAccessor.Current
            ?? new PlatformPrincipal(new PlatformIdentity(UnknownVerifierPrincipalId, "Unauthenticated"), []);
        _permissionEvaluator.RequirePermission(principal, ReadPermission);

        var references = await _documentStore.GetReferencesAsync(subjectDocumentId, cancellationToken).ConfigureAwait(false);

        var records = new List<IVerificationRecord>();

        foreach (var reference in references)
        {
            if (!string.Equals(reference.RelationshipKind, VerifiedByRelationshipKind, StringComparison.Ordinal))
                continue;

            var history = await _documentStore.GetRevisionHistoryAsync(reference.TargetDocumentId, cancellationToken).ConfigureAwait(false);
            var currentRevision = history[^1];
            VerificationRecordDto dto;
            try
            {
                dto = JsonSerializer.Deserialize<VerificationRecordDto>(currentRevision.Content)
                    ?? throw new EngineeringDataException($"Verification record '{reference.TargetDocumentId}' could not be deserialised.");
            }
            catch (JsonException ex)
            {
                // Controlled failure for malformed stored content (`TD-60`).
                throw new EngineeringDataException($"Verification record '{reference.TargetDocumentId}' could not be deserialised.", ex);
            }

            records.Add(new VerificationRecord(
                reference.TargetDocumentId, dto.SubjectDocumentId, dto.Outcome, dto.Method, dto.Criteria, dto.Evidence,
                dto.LinkedDocumentIds, dto.LinkedCalculationRecordIds, dto.ReferencedMaterialIds,
                dto.VerifiedByPrincipalId, dto.VerifiedAt, currentRevision.RevisionNumber));
        }

        _logger?.Information($"Verification history returned {records.Count} record(s) for subject '{subjectDocumentId}'.");

        return records.OrderBy(r => r.VerifiedAt).ToList();
    }

    private string ResolveVerifierPrincipalId() =>
        _currentPrincipalAccessor.Current?.Identity.Id ?? UnknownVerifierPrincipalId;
}
