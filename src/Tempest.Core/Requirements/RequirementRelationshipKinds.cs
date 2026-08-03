namespace Tempest.Core.Requirements;

/// <summary>
/// Reserved <see cref="EngineeringData.DocumentReference.RelationshipKind"/>
/// constants for the Requirements Platform (<c>WP7.2C Relationship
/// Model.md</c>). Six of the seven relationship kinds this Platform
/// reviewed belong here; the seventh ("Verified By") is deliberately
/// absent — it already exists, unmodified, as
/// <see cref="Verification.VerificationService.VerifiedByRelationshipKind"/>,
/// created by <see cref="Verification.IVerificationService.RecordAsync"/>
/// itself, never by this class.
/// </summary>
public static class RequirementRelationshipKinds
{
    /// <summary>Requirement Group hierarchy — recorded from a child group or requirement to its own parent group.</summary>
    public const string GroupedUnder = "groupedUnder";

    /// <summary>Requirement Collection membership — recorded from a collection to each member requirement.</summary>
    public const string CollectedIn = "collects";

    /// <summary>Requirement Relationship — a general dependency between two requirements.</summary>
    public const string DependsOn = "dependsOn";

    /// <summary>Requirement Trace Link — backward traceability (derivation).</summary>
    public const string DerivesFrom = "derivesFrom";

    /// <summary>Requirement Allocation — recorded from a requirement to an allocation target of any kind.</summary>
    public const string AllocatedTo = "allocatedTo";

    /// <summary>Requirement Relationship — a non-owning cross-reference.</summary>
    public const string References = "references";

    /// <summary>Requirement Trace Link — forward traceability (satisfaction), recorded from a satisfying target to the requirement.</summary>
    public const string Satisfies = "satisfies";
}
