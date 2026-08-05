namespace Tempest.Core.EngineeringDomain;

/// <summary>
/// Five small `WP 9.0B` <see cref="IValidationRule"/> implementations —
/// registered via <see cref="ValidationRuleSet.Register"/> at composition
/// time exactly as that type's own XML documentation anticipates ("a
/// future discipline module registers its own rules here without this
/// type changing shape"), never baked into a mutator's own contract.
/// Two of the five (<see cref="MissingParentValidationRule"/>,
/// <see cref="CircularHierarchyValidationRule"/>) are deliberately
/// read-time, defence-in-depth confirmations of guards
/// <see cref="EngineeringObjectBase.MoveAsync"/> already enforces at
/// write time — reachable through <see cref="IValidatable.ValidateAsync"/>
/// exactly as the Validation framework's own model expects, not a second,
/// competing enforcement point.
/// </summary>
public sealed class InvalidQuantityValidationRule : IValidationRule
{
    public string RuleCode => StructuralValidationRules.QuantityMustBePositive;

    public Task<IValidationResult> EvaluateAsync(IEngineeringObject subject, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subject);

        IValidationResult result = subject is IHasBomLine { Quantity: <= 0 } bomLine
            ? ValidationResult.SingleError(RuleCode, $"Quantity {bomLine.Quantity} is not positive.", subject.Id)
            : ValidationResult.Valid;

        return Task.FromResult(result);
    }
}

public sealed class MissingParentValidationRule : IValidationRule
{
    private readonly IEngineeringObjectRepository _repository;

    public MissingParentValidationRule(IEngineeringObjectRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        _repository = repository;
    }

    public string RuleCode => StructuralValidationRules.ParentMustExist;

    public async Task<IValidationResult> EvaluateAsync(IEngineeringObject subject, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subject);

        if (subject is not IHasParent { ParentId: { } parentId })
            return ValidationResult.Valid;

        var parent = await _repository.FindAsync(parentId, cancellationToken).ConfigureAwait(false);

        return parent is null
            ? ValidationResult.SingleError(RuleCode, $"Parent '{parentId}' does not exist.", subject.Id)
            : ValidationResult.Valid;
    }
}

public sealed class CircularHierarchyValidationRule : IValidationRule
{
    private readonly IEngineeringObjectRepository _repository;

    public CircularHierarchyValidationRule(IEngineeringObjectRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        _repository = repository;
    }

    public string RuleCode => StructuralValidationRules.NoCircularHierarchy;

    public async Task<IValidationResult> EvaluateAsync(IEngineeringObject subject, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subject);

        if (subject is not IHasParent { ParentId: { } parentId })
            return ValidationResult.Valid;

        var visited = new HashSet<Guid> { subject.Id };
        var current = parentId;

        while (visited.Add(current))
        {
            var candidate = await _repository.FindAsync(current, cancellationToken).ConfigureAwait(false);

            if (candidate is not IHasParent { ParentId: { } nextParentId })
                return ValidationResult.Valid;

            if (nextParentId == subject.Id)
                return ValidationResult.SingleError(RuleCode, $"'{subject.Id}' is its own ancestor, reached via '{current}'.", subject.Id);

            current = nextParentId;
        }

        return ValidationResult.Valid;
    }
}

public sealed class DuplicateItemNumberValidationRule : IValidationRule
{
    private readonly IEngineeringObjectRepository _repository;

    public DuplicateItemNumberValidationRule(IEngineeringObjectRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        _repository = repository;
    }

    public string RuleCode => StructuralValidationRules.NoDuplicateItemNumber;

    public async Task<IValidationResult> EvaluateAsync(IEngineeringObject subject, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subject);

        if (subject is not IHasBomLine { ItemNumber: { } itemNumber } || subject is not IHasParent { ParentId: { } parentId })
            return ValidationResult.Valid;

        var siblings = await GetLiveSiblingsAsync(parentId, subject.Id, cancellationToken).ConfigureAwait(false);

        var collision = siblings.OfType<IHasBomLine>().Any(s => s.ItemNumber == itemNumber);

        return collision
            ? ValidationResult.SingleError(RuleCode, $"Item Number '{itemNumber}' is already used by another live object under parent '{parentId}'.", subject.Id)
            : ValidationResult.Valid;
    }

    private async Task<IReadOnlyList<IEngineeringObject>> GetLiveSiblingsAsync(Guid parentId, Guid excludingId, CancellationToken cancellationToken)
    {
        var all = await _repository.ListAllAsync(cancellationToken).ConfigureAwait(false);

        return all.Where(o =>
            o.Id != excludingId &&
            o is IHasParent { ParentId: { } pid } && pid == parentId &&
            o is not IDeletable { IsDeleted: true }).ToList();
    }
}

public sealed class DuplicateFindNumberValidationRule : IValidationRule
{
    private readonly IEngineeringObjectRepository _repository;

    public DuplicateFindNumberValidationRule(IEngineeringObjectRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        _repository = repository;
    }

    public string RuleCode => StructuralValidationRules.NoDuplicateFindNumber;

    public async Task<IValidationResult> EvaluateAsync(IEngineeringObject subject, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subject);

        if (subject is not IHasBomLine { FindNumber: { } findNumber } || subject is not IHasParent { ParentId: { } parentId })
            return ValidationResult.Valid;

        var all = await _repository.ListAllAsync(cancellationToken).ConfigureAwait(false);

        var collision = all.Any(o =>
            o.Id != subject.Id &&
            o is IHasParent { ParentId: { } pid } && pid == parentId &&
            o is not IDeletable { IsDeleted: true } &&
            o is IHasBomLine sibling && sibling.FindNumber == findNumber);

        return collision
            ? ValidationResult.SingleError(RuleCode, $"Find Number '{findNumber}' is already used by another live object under parent '{parentId}'.", subject.Id)
            : ValidationResult.Valid;
    }
}
