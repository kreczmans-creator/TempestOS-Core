namespace Tempest.Core.EngineeringDomain;

public sealed class ReferenceIntegrityChecker : IReferenceIntegrityChecker
{
    private readonly IEngineeringObjectRepository _repository;

    public ReferenceIntegrityChecker(IEngineeringObjectRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        _repository = repository;
    }

    public async Task<IValidationResult> CheckAsync(IEngineeringRelationship relationship, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(relationship);

        var errors = new List<IValidationDiagnostic>();

        if (await _repository.FindAsync(relationship.SourceId, cancellationToken).ConfigureAwait(false) is null)
            errors.Add(new ValidationDiagnostic(StructuralValidationRules.RelationshipSourceMustExist, $"Relationship source '{relationship.SourceId}' does not exist.", relationship.SourceId));

        if (await _repository.FindAsync(relationship.TargetId, cancellationToken).ConfigureAwait(false) is null)
            errors.Add(new ValidationDiagnostic(StructuralValidationRules.RelationshipTargetMustExist, $"Relationship target '{relationship.TargetId}' does not exist.", relationship.TargetId));

        return errors.Count == 0 ? ValidationResult.Valid : new ValidationResult(errors, Array.Empty<IValidationDiagnostic>());
    }

    public async Task<IValidationResult> CheckBaselineMembersAsync(IBaseline baseline, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(baseline);

        var errors = new List<IValidationDiagnostic>();

        foreach (var member in baseline.MemberRevisions)
        {
            var found = await _repository.FindAsync(member.ObjectId, cancellationToken).ConfigureAwait(false);

            if (found is null)
            {
                errors.Add(new ValidationDiagnostic(StructuralValidationRules.BaselineMemberMustExist, $"Baseline member '{member.ObjectId}' does not exist.", member.ObjectId));
            }
            else if (found.CurrentRevisionNumber < member.RevisionNumber)
            {
                errors.Add(new ValidationDiagnostic(
                    StructuralValidationRules.BaselineMemberRevisionMustExist,
                    $"Baseline member '{member.ObjectId}' references revision {member.RevisionNumber}, but only {found.CurrentRevisionNumber} exist(s).",
                    member.ObjectId));
            }
        }

        return errors.Count == 0 ? ValidationResult.Valid : new ValidationResult(errors, Array.Empty<IValidationDiagnostic>());
    }
}
