using Tempest.Core.Commands;
using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Verification;

namespace Tempest.App.Workspace.Verification;

/// <summary>
/// Records a real <see cref="IVerificationRecord"/> against one
/// Verification Activity, via the existing, unmodified
/// <see cref="IVerificationService.RecordAsync"/> — this Work Package's
/// own realisation of "Execute," "Record Result," and "Attach Evidence"
/// together (`ADR-0089`). The Verification Framework has exactly one
/// action (a caller-driven assertion, not a computed dispatch like
/// <see cref="Tempest.Core.Calculations.ICalculationEngine.ExecuteAsync{TInput,TResult}"/>),
/// so no second, separate "Execute" mechanism is invented, and evidence
/// is supplied inline, at record time, exactly as
/// <see cref="VerificationContext"/>'s own shape requires — there is no
/// "attach evidence to an already-recorded result" capability anywhere
/// in the Framework.
/// </summary>
/// <remarks>
/// Reuses <see cref="TargetObjectId"/> as <see cref="IVerificationService.RecordAsync"/>'s
/// own <c>subjectDocumentId</c> — the resulting record is linked back to
/// the Verification Activity itself via the existing, unmodified
/// <c>"verifiedBy"</c> relationship kind (<see cref="VerificationService.VerifiedByRelationshipKind"/>),
/// the identical mechanism <c>Tempest.Samples.RequirementsWorkspaceSampleModule</c>'s
/// own directly-against-a-Requirement recording already establishes, one
/// link-hop earlier — never a new relationship kind.
/// </remarks>
public sealed class RecordVerificationResultCommand : IWorkspaceCommand
{
    public RecordVerificationResultCommand(
        Guid targetObjectId, string targetKind, VerificationOutcome outcome, string method,
        IReadOnlyList<VerificationCriterion>? criteria = null,
        IReadOnlyList<VerificationEvidenceEntry>? evidence = null,
        IReadOnlyList<Guid>? linkedDocumentIds = null,
        IReadOnlyList<Guid>? linkedCalculationRecordIds = null,
        IReadOnlyList<string>? referencedMaterialIds = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetKind);
        ArgumentException.ThrowIfNullOrWhiteSpace(method);

        TargetObjectId = targetObjectId;
        TargetKind = targetKind;
        Outcome = outcome;
        Method = method;
        Criteria = criteria ?? [];
        Evidence = evidence ?? [];
        LinkedDocumentIds = linkedDocumentIds ?? [];
        LinkedCalculationRecordIds = linkedCalculationRecordIds ?? [];
        ReferencedMaterialIds = referencedMaterialIds ?? [];
    }

    /// <inheritdoc />
    /// <remarks>The Verification Activity the resulting record is linked back to — also <see cref="IVerificationService.RecordAsync"/>'s own <c>subjectDocumentId</c>.</remarks>
    public Guid TargetObjectId { get; }

    /// <inheritdoc />
    public string TargetKind { get; }

    /// <summary>Gets whether the engineering claim was demonstrated.</summary>
    public VerificationOutcome Outcome { get; }

    /// <summary>Gets the verification method used for this specific recording.</summary>
    public string Method { get; }

    /// <summary>Gets every explicit criterion checked.</summary>
    public IReadOnlyList<VerificationCriterion> Criteria { get; }

    /// <summary>Gets every piece of supporting evidence — Test/Inspection/Analysis/Demonstration evidence, and Witness information, are all carried here as plain description/reference pairs; no dedicated field exists for any one evidence category (disclosed, `WP9.3A Technical Debt Assessment.md`).</summary>
    public IReadOnlyList<VerificationEvidenceEntry> Evidence { get; }

    /// <summary>Gets every additional linked engineering document Id.</summary>
    public IReadOnlyList<Guid> LinkedDocumentIds { get; }

    /// <summary>Gets every linked calculation record Id.</summary>
    public IReadOnlyList<Guid> LinkedCalculationRecordIds { get; }

    /// <summary>Gets every referenced material Id.</summary>
    public IReadOnlyList<string> ReferencedMaterialIds { get; }
}

/// <summary>Handles <see cref="RecordVerificationResultCommand"/>.</summary>
public sealed class RecordVerificationResultCommandHandler : ICommandHandler<RecordVerificationResultCommand>
{
    private readonly IVerificationService _verificationService;

    public RecordVerificationResultCommandHandler(IVerificationService verificationService)
    {
        ArgumentNullException.ThrowIfNull(verificationService);

        _verificationService = verificationService;
    }

    public async Task<CommandResult> HandleAsync(RecordVerificationResultCommand command, CancellationToken cancellationToken)
    {
        var context = new VerificationContext();

        foreach (var criterion in command.Criteria)
            context.RecordCriterion(criterion.Description, criterion.IsSatisfied, criterion.Detail);

        foreach (var entry in command.Evidence)
            context.RecordEvidence(entry.Description, entry.Reference);

        foreach (var documentId in command.LinkedDocumentIds)
            context.LinkDocument(documentId);

        foreach (var calculationRecordId in command.LinkedCalculationRecordIds)
            context.LinkCalculationRecord(calculationRecordId);

        foreach (var materialId in command.ReferencedMaterialIds)
            context.ReferenceMaterial(materialId);

        IVerificationRecord record;

        try
        {
            record = await _verificationService.RecordAsync(
                command.TargetObjectId, command.Outcome, command.Method, context, cancellationToken).ConfigureAwait(false);
        }
        catch (EngineeringDocumentNotFoundException ex)
        {
            return CommandResult.Failure(ex.Message);
        }

        return CommandResult.Success($"Recorded verification '{record.Id}' ({command.Outcome}) for '{command.TargetObjectId}'.");
    }
}
