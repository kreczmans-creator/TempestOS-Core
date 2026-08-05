namespace Tempest.Core.EngineeringDomain;

public interface IValidationResult
{
    bool IsValid { get; }
    IReadOnlyList<IValidationDiagnostic> Errors { get; }
    IReadOnlyList<IValidationDiagnostic> Warnings { get; }
}

public interface IValidationDiagnostic
{
    string Code { get; }
    string Message { get; }
    Guid? SubjectId { get; }
}

public interface IValidationRule
{
    string RuleCode { get; }
    Task<IValidationResult> EvaluateAsync(IEngineeringObject subject, CancellationToken cancellationToken = default);
}

public interface IValidationRuleSet
{
    IReadOnlyList<IValidationRule> GetRulesFor(string kind);
    Task<IValidationResult> ValidateAsync(IEngineeringObject subject, CancellationToken cancellationToken = default);
}

/// <summary>The only rule structurally enforced platform-wide today (WP8.2B Validation Contract Specification.md).</summary>
public static class StructuralValidationRules
{
    public const string NoSelfReference = "TEMPEST-VAL-001";

    /// <summary>`WP 8.2C` — <see cref="IReferenceIntegrityChecker.CheckAsync"/> rejects a relationship whose own source object does not exist. Catalogued here by `WP 9.0B`; the code itself is unchanged, already shipped since `WP 8.2C`.</summary>
    public const string RelationshipSourceMustExist = "TEMPEST-VAL-002";

    /// <summary>`WP 8.2C` — <see cref="IReferenceIntegrityChecker.CheckAsync"/> rejects a relationship whose own target object does not exist. Catalogued here by `WP 9.0B`; the code itself is unchanged, already shipped since `WP 8.2C`.</summary>
    public const string RelationshipTargetMustExist = "TEMPEST-VAL-003";

    /// <summary>`WP 8.2C` — <see cref="IReferenceIntegrityChecker.CheckBaselineMembersAsync"/> rejects a baseline member whose own object does not exist. Catalogued here by `WP 9.0B`; the code itself is unchanged, already shipped since `WP 8.2C`.</summary>
    public const string BaselineMemberMustExist = "TEMPEST-VAL-004";

    /// <summary>`WP 8.2C` — <see cref="IReferenceIntegrityChecker.CheckBaselineMembersAsync"/> rejects a baseline member referencing a revision number higher than the object's own current revision. Catalogued here by `WP 9.0B`; the code itself is unchanged, already shipped since `WP 8.2C`.</summary>
    public const string BaselineMemberRevisionMustExist = "TEMPEST-VAL-005";

    /// <summary>`WP 9.0A` — <see cref="IHasParent.MoveAsync"/> rejects a target parent that is this object itself or one of its own descendants. Numbered `-006`, not `-002`: `-002`/`-003` were already in use by <see cref="IReferenceIntegrityChecker.CheckAsync"/> (relationship source/target existence, `WP 8.2C`) — a genuine collision found and corrected during `WP 9.0B`'s own validation work, before either code was ever referenced by a committed/tagged release.</summary>
    public const string NoCircularParent = "TEMPEST-VAL-006";

    /// <summary>`WP 9.0A` — <see cref="IDeletable.DeleteAsync"/> rejects deleting an object that still has live (non-deleted) children. Numbered `-007`, not `-003` — see <see cref="NoCircularParent"/>'s own remarks for why.</summary>
    public const string NoDeleteWithLiveChildren = "TEMPEST-VAL-007";

    /// <summary>`WP 9.0B` — a BOM sibling group (same live <see cref="IHasParent.ParentId"/>) has two or more live objects sharing the same non-null <see cref="IHasBomLine.ItemNumber"/>.</summary>
    public const string NoDuplicateItemNumber = "TEMPEST-VAL-008";

    /// <summary>`WP 9.0B` — a BOM sibling group (same live <see cref="IHasParent.ParentId"/>) has two or more live objects sharing the same non-null <see cref="IHasBomLine.FindNumber"/>.</summary>
    public const string NoDuplicateFindNumber = "TEMPEST-VAL-009";

    /// <summary>`WP 9.0B` — <see cref="IHasBomLine.Quantity"/> must be positive.</summary>
    public const string QuantityMustBePositive = "TEMPEST-VAL-010";

    /// <summary>`WP 9.0B` — <see cref="IHasParent.ParentId"/> is set but does not resolve to a live object via <see cref="IEngineeringObjectRepository"/> — a read-time confirmation `MoveAsync`'s own write-time guard already prevents in the ordinary case.</summary>
    public const string ParentMustExist = "TEMPEST-VAL-011";

    /// <summary>`WP 9.0B` — walking <see cref="IHasParent.ParentId"/> from this object leads back to itself — a read-time confirmation `MoveAsync`'s own write-time guard (<see cref="NoCircularParent"/>) already prevents in the ordinary case.</summary>
    public const string NoCircularHierarchy = "TEMPEST-VAL-012";
}

public interface IRecommendedValidationRule : IValidationRule
{
    string Rationale { get; }
}

public interface IReferenceIntegrityChecker
{
    Task<IValidationResult> CheckAsync(IEngineeringRelationship relationship, CancellationToken cancellationToken = default);
    Task<IValidationResult> CheckBaselineMembersAsync(IBaseline baseline, CancellationToken cancellationToken = default);
}
