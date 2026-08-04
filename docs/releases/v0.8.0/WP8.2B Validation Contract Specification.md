# WP 8.2B — Engineering Domain Contracts — Validation Contract Specification

## Purpose

The contract shape realising `WP8.2A Validation Specification.md` —
rule evaluation, constraint reporting, diagnostic information, error
and warning collection. Proposed, uncompiled C#,
`Tempest.Core.EngineeringDomain` namespace throughout.

## 1. `IValidationResult`

```csharp
public interface IValidationResult
{
    bool IsValid { get; }

    IReadOnlyList<IValidationDiagnostic> Errors { get; }
    IReadOnlyList<IValidationDiagnostic> Warnings { get; }
}

public interface IValidationDiagnostic
{
    /// <summary>A stable, caller-facing code — never a raw exception message.</summary>
    string Code { get; }

    string Message { get; }

    /// <summary>The object or relationship this diagnostic concerns, if any.</summary>
    Guid? SubjectId { get; }
}
```

**Error vs. Warning, resolved:** an `Error` means `IsValid` is
`false` — the object or relationship fails a rule this platform
structurally enforces (§3, below — currently, only "no self-reference,"
`WP8.2A Digital Thread Specification.md` §5). A `Warning` means the
object or relationship is structurally accepted but violates a
*recommended* rule (a mismatched `RelationshipKind`/`Category`, an
approval-gate rule with no shipped enforcement yet, §4) —
`IsValid` remains `true` when only warnings are present. This mirrors
`CalculationValidationOutcome`'s own shipped `Valid`/`Conditional`
distinction (`Tempest.Core.Calculations`) generalised platform-wide.

## 2. Rule Evaluation

```csharp
public interface IValidationRule
{
    string RuleCode { get; }

    Task<IValidationResult> EvaluateAsync(IEngineeringObject subject, CancellationToken cancellationToken = default);
}

/// <summary>Composes every applicable rule for one object's own Kind — mirrors ICommandRegistry's own Kind-keyed lookup shape.</summary>
public interface IValidationRuleSet
{
    IReadOnlyList<IValidationRule> GetRulesFor(string kind);

    Task<IValidationResult> ValidateAsync(IEngineeringObject subject, CancellationToken cancellationToken = default);
}
```

`IValidatable.ValidateAsync` (`Interface Catalogue.md` §1) is the
per-object entry point; `IValidationRuleSet` is how a future
implementation composes every rule applicable to one `Kind` without
each object's own concrete type needing to know its own full rule list
in advance — a module registering a new canonical object (`ADR-0072`'s
own extensibility consequence) also registers its own rules here,
never modifying a platform switch statement.

## 3. Structural Constraints (Always Enforced)

```csharp
/// <summary>The complete list of rules this platform structurally enforces today — deliberately short (WP8.2A Validation Specification.md §3.1, §7.1).</summary>
public static class StructuralValidationRules
{
    public const string NoSelfReference = "TEMPEST-VAL-001";
}
```

Every other rule named in `WP8.2A Validation Specification.md`
(approval gates, lifecycle-blocking dependencies, composition cascade)
is a **recommended** rule (§4, below) — this catalogue keeps the
structurally-enforced list explicit and short, matching what the real,
shipped `IEngineeringDocumentStore.LinkAsync` actually rejects today
(only self-reference), rather than implying more enforcement exists
than does.

## 4. Recommended (Non-Structural) Constraints

```csharp
/// <summary>A rule this platform names as architecture but does not yet structurally enforce (WP8.2A Validation Specification.md §5's own disclosed gap).</summary>
public interface IRecommendedValidationRule : IValidationRule
{
    /// <summary>Why this rule is recommended rather than enforced — surfaced to a caller deciding whether to adopt it.</summary>
    string Rationale { get; }
}
```

Concrete rules a future implementation Work Package could register
against this contract: an `Approved` transition with no resolvable
`Approved By` link (`WP8.2A Validation Specification.md` §5); an object
with an unresolved incoming `Blocks` link attempting to advance past
`Approved` (§4.3); a `RelationshipKind` whose own conventional
`RelationshipCategory` mapping (`Relationship Contract
Specification.md` §3) does not match the `Category` actually recorded.
None of these is implemented by this Work Package — the contract shape
exists so a future Work Package has somewhere to register them.

## 5. Reference Integrity

```csharp
public interface IReferenceIntegrityChecker
{
    /// <summary>A read-time check, never a write-time rejection (WP8.2A Validation Specification.md §7.2) — a dangling target is reported, not prevented.</summary>
    Task<IValidationResult> CheckAsync(IEngineeringRelationship relationship, CancellationToken cancellationToken = default);

    /// <summary>The one create-time check this platform does perform: a Baseline's own members must resolve at creation (WP8.2A Validation Specification.md §7.3).</summary>
    Task<IValidationResult> CheckBaselineMembersAsync(IBaseline baseline, CancellationToken cancellationToken = default);
}
```

## 6. Deletion Rules (Restated)

No `DeleteAsync`/`IDeletionRule` contract exists anywhere in this
catalogue — `WP8.2A Validation Specification.md` §6's own "no
Engineering Object is ever physically deleted" rule is enforced simply
by the absence of any such method, not by a rule that rejects deletion
attempts. There is nothing to validate against, because there is
nothing to call.

## Related Documents

`WP8.2B Engineering Domain Contracts.md`; `WP8.2B Interface
Catalogue.md`; `WP8.2B Lifecycle Contract Specification.md`; `WP8.2B
Relationship Contract Specification.md`; `WP8.2A Validation
Specification.md`; `CalculationValidationOutcome`
(`src/Tempest.Core/Calculations/`).
