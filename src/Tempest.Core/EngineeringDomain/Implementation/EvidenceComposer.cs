namespace Tempest.Core.EngineeringDomain;

/// <summary>
/// The exact three-step recipe WP8.2B Digital Thread Contract Specification.md names: get outgoing
/// relationships, resolve Verification-category targets to <see cref="IVerificationResult"/>, resolve
/// Calculation-category targets to <see cref="ICalculationResult"/>. Both are Domain-level contracts
/// owned by the already-shipped Verification/Calculations frameworks (WP 8.2C does not give them a
/// competing concrete realisation here), so resolution only succeeds for a target the caller has itself
/// registered into the shared <see cref="IEngineeringObjectRepository"/> as an instance actually
/// implementing one of those interfaces — an empty result is the honest, expected outcome otherwise.
/// </summary>
public sealed class EvidenceComposer : IEvidenceComposer
{
    private readonly IRelationshipDiscovery _relationshipDiscovery;
    private readonly IEngineeringObjectRepository _objectRepository;

    public EvidenceComposer(IRelationshipDiscovery relationshipDiscovery, IEngineeringObjectRepository objectRepository)
    {
        ArgumentNullException.ThrowIfNull(relationshipDiscovery);
        ArgumentNullException.ThrowIfNull(objectRepository);
        _relationshipDiscovery = relationshipDiscovery;
        _objectRepository = objectRepository;
    }

    public async Task<IEvidence> ComposeAsync(Guid subjectId, CancellationToken cancellationToken = default)
    {
        var outgoing = await _relationshipDiscovery.GetOutgoingAsync(subjectId, cancellationToken).ConfigureAwait(false);

        var verificationResults = new List<IVerificationResult>();
        var calculationResults = new List<ICalculationResult>();

        foreach (var relationship in outgoing)
        {
            if (relationship.Category is not (RelationshipCategory.Verification or RelationshipCategory.Calculation))
                continue;

            var target = await _objectRepository.FindAsync(relationship.TargetId, cancellationToken).ConfigureAwait(false);

            switch (target)
            {
                case IVerificationResult verificationResult:
                    verificationResults.Add(verificationResult);
                    break;
                case ICalculationResult calculationResult:
                    calculationResults.Add(calculationResult);
                    break;
            }
        }

        return new Evidence(subjectId, outgoing, verificationResults, calculationResults);
    }

    private sealed class Evidence : IEvidence
    {
        public Guid SubjectId { get; }
        public IReadOnlyList<IEngineeringRelationship> SupportingRelationships { get; }
        public IReadOnlyList<IVerificationResult> VerificationResults { get; }
        public IReadOnlyList<ICalculationResult> CalculationResults { get; }

        public Evidence(
            Guid subjectId,
            IReadOnlyList<IEngineeringRelationship> supportingRelationships,
            IReadOnlyList<IVerificationResult> verificationResults,
            IReadOnlyList<ICalculationResult> calculationResults)
        {
            SubjectId = subjectId;
            SupportingRelationships = supportingRelationships;
            VerificationResults = verificationResults;
            CalculationResults = calculationResults;
        }
    }
}
