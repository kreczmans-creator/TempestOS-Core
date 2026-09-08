using System.Text.Json;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Verification;

namespace Tempest.App.Workspace.Verification;

/// <summary>
/// Reads a Verification Activity Domain object's own recorded results back
/// — every <c>"verifiedBy"</c>-linked <see cref="IVerificationRecord"/>
/// document — mirroring
/// <see cref="Calculations.CalculationRecordReader"/>'s own identical
/// shape and reasoning exactly. <see cref="VerificationRecordDto"/> (the
/// real, shipped serialised shape) is <see langword="internal"/> to
/// <see cref="Tempest.Core.Verification"/> — confirmed by direct read,
/// the identical situation <see cref="Tempest.Core.Calculations.CalculationRecordDto{TResult}"/>
/// already established — so this reads the same shared
/// <see cref="EngineeringData.IEngineeringDocumentStore"/> content
/// <see cref="VerificationService"/> itself already wrote, parsed with
/// <see cref="JsonDocument"/> rather than a typed contract.
/// </summary>
/// <remarks>
/// Deliberately never calls <see cref="IVerificationService.GetVerificationHistoryAsync"/>
/// — that method is permission-gated
/// (<see cref="VerificationService.ReadPermission"/>, confirmed by direct
/// read) — avoiding, from the start, the exact class of passive-surface
/// availability defect `WP 9.1A` found and fixed for
/// <see cref="ITraceable.GetEvidenceAsync"/>, mirroring `WP 9.2A`'s own
/// already-disclosed identical avoidance for Calculations.
/// </remarks>
public static class VerificationRecordReader
{
    /// <summary>
    /// Every <c>"verifiedBy"</c>-linked record for <paramref name="activityId"/>,
    /// oldest first.
    /// </summary>
    /// <remarks>
    /// Reads <see cref="EngineeringData.IEngineeringDocumentStore.GetReferencesAsync"/>
    /// directly — the same raw store read
    /// <see cref="VerificationService.RecordAsync"/>/
    /// <see cref="IVerificationService.GetVerificationHistoryAsync"/>
    /// themselves use internally — rather than
    /// <see cref="EngineeringDomainContext.RelationshipRepository"/>. A
    /// disclosed, genuine finding: <see cref="VerificationService.RecordAsync"/>
    /// links its own subject to the new record via
    /// <c>IEngineeringDocumentStore.LinkAsync</c> directly, never through
    /// <see cref="IHasRelationships.LinkAsync"/> on an
    /// <see cref="EngineeringObjectBase"/>-derived object — unlike
    /// <c>CalculationTemplateRegistry.ExecuteAsync</c>, which explicitly
    /// calls the Calculation Domain object's own <c>LinkAsync</c> for this
    /// exact reason — so the Activity→Record link this Work Package's own
    /// <see cref="RecordVerificationResultCommand"/> produces is visible
    /// via the raw store, but never via <c>RelationshipRepository</c>
    /// (`WP9.3A Technical Debt Assessment.md`).
    /// </remarks>
    public static async Task<IReadOnlyList<VerificationRecordSnapshot>> GetResultHistoryAsync(
        EngineeringDomainContext context, Guid activityId, CancellationToken cancellationToken = default)
    {
        var references = await context.Store.GetReferencesAsync(activityId, cancellationToken).ConfigureAwait(false);

        var recordLinks = references
            .Where(r => string.Equals(r.RelationshipKind, VerificationService.VerifiedByRelationshipKind, StringComparison.Ordinal))
            .ToList();

        var snapshots = new List<VerificationRecordSnapshot>();
        foreach (var link in recordLinks)
        {
            if (await ReadAsync(context, link.TargetDocumentId, cancellationToken).ConfigureAwait(false) is { } snapshot)
                snapshots.Add(snapshot);
        }

        return snapshots.OrderBy(s => s.VerifiedAt).ToList();
    }

    /// <summary>The most recent <c>"verifiedBy"</c>-linked record for <paramref name="activityId"/>, or <see langword="null"/> if no result has ever been recorded.</summary>
    public static async Task<VerificationRecordSnapshot?> GetLatestAsync(
        EngineeringDomainContext context, Guid activityId, CancellationToken cancellationToken = default)
    {
        var history = await GetResultHistoryAsync(context, activityId, cancellationToken).ConfigureAwait(false);
        return history.Count > 0 ? history[^1] : null;
    }

    private static async Task<VerificationRecordSnapshot?> ReadAsync(EngineeringDomainContext context, Guid recordId, CancellationToken cancellationToken)
    {
        var revisions = await context.Store.GetRevisionHistoryAsync(recordId, cancellationToken).ConfigureAwait(false);
        if (revisions.Count == 0)
            return null;

        using var document = JsonDocument.Parse(revisions[^1].Content);
        var root = document.RootElement;

        var outcome = (VerificationOutcome)root.GetProperty("Outcome").GetInt32();
        var method = root.GetProperty("Method").GetString() ?? string.Empty;
        var verifiedBy = root.GetProperty("VerifiedByPrincipalId").GetString() ?? "unknown";
        var verifiedAt = root.GetProperty("VerifiedAt").GetDateTimeOffset();

        var criteria = root.GetProperty("Criteria").EnumerateArray()
            .Select(c => new VerificationCriterion(
                c.GetProperty("Description").GetString() ?? string.Empty,
                c.GetProperty("IsSatisfied").GetBoolean(),
                c.TryGetProperty("Detail", out var detail) && detail.ValueKind != JsonValueKind.Null ? detail.GetString() : null))
            .ToList();

        var evidence = root.GetProperty("Evidence").EnumerateArray()
            .Select(e => new VerificationEvidenceEntry(
                e.GetProperty("Description").GetString() ?? string.Empty,
                e.TryGetProperty("Reference", out var reference) && reference.ValueKind != JsonValueKind.Null ? reference.GetString() : null))
            .ToList();

        var linkedDocumentIds = root.GetProperty("LinkedDocumentIds").EnumerateArray().Select(e => e.GetGuid()).ToList();
        var linkedCalculationRecordIds = root.GetProperty("LinkedCalculationRecordIds").EnumerateArray().Select(e => e.GetGuid()).ToList();
        var referencedMaterialIds = root.GetProperty("ReferencedMaterialIds").EnumerateArray().Select(e => e.GetString() ?? string.Empty).ToList();

        return new VerificationRecordSnapshot(
            recordId, outcome, method, criteria, evidence, linkedDocumentIds, linkedCalculationRecordIds,
            referencedMaterialIds, verifiedBy, verifiedAt);
    }
}

/// <summary>
/// One generically-parsed <see cref="IVerificationRecord"/>, read back for
/// display — see <see cref="VerificationRecordReader"/>.
/// </summary>
/// <param name="RecordId">The record's own document Id.</param>
/// <param name="Outcome">Whether the engineering claim was demonstrated.</param>
/// <param name="Method">The verification method used.</param>
/// <param name="Criteria">Every explicit criterion checked.</param>
/// <param name="Evidence">Every piece of supporting evidence.</param>
/// <param name="LinkedDocumentIds">Every additional linked document Id.</param>
/// <param name="LinkedCalculationRecordIds">Every linked calculation record Id.</param>
/// <param name="ReferencedMaterialIds">Every material Id referenced.</param>
/// <param name="VerifiedByPrincipalId">Who performed this verification.</param>
/// <param name="VerifiedAt">When this verification was performed.</param>
public sealed record VerificationRecordSnapshot(
    Guid RecordId,
    VerificationOutcome Outcome,
    string Method,
    IReadOnlyList<VerificationCriterion> Criteria,
    IReadOnlyList<VerificationEvidenceEntry> Evidence,
    IReadOnlyList<Guid> LinkedDocumentIds,
    IReadOnlyList<Guid> LinkedCalculationRecordIds,
    IReadOnlyList<string> ReferencedMaterialIds,
    string VerifiedByPrincipalId,
    DateTimeOffset VerifiedAt);
