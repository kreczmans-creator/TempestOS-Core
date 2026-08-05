# WP 9.0B — Product Configuration & BOM Management — Architecture Conformance Review

## Purpose

Independently re-verifies that every new or changed piece of this Work
Package sits in its own correct architectural layer, introduces no
circular dependency, and follows the frozen Dependency Rules exactly
where each already applies.

## 1. Layering

| Component | Layer | Depends on | Verdict |
|---|---|---|---|
| `IHasBomLine` | `Tempest.Core.EngineeringDomain` (Contracts) | `IEngineeringObject` only | Conforms |
| `EngineeringObjectBase` (extended) | `Tempest.Core.EngineeringDomain` (Implementation) | Already-existing dependencies only | Conforms — no new dependency added |
| Five new `IValidationRule`s | `Tempest.Core.EngineeringDomain` (Implementation) | `IEngineeringObjectRepository` (two of five; the other three need none) | Conforms — identical shape to `ReferenceIntegrityChecker`'s own existing dependency |
| `SetBomLineCommand`/`CompareBaselinesCommand`/`ValidateConfigurationCommand` | `Tempest.App.Workspace.Mechanical` | `Tempest.Core.EngineeringDomain` directly (the Engineering Discipline integration layer, per `WP 9.0A`'s own precedent) | Conforms |
| `MechanicalObjectFactoryRegistry`/`MechanicalPropertyFacetProvider`/`MechanicalProductStructureNodeProvider` (extended) | `Tempest.App.Workspace.Mechanical` | Unchanged dependency shape | Conforms |

No new project reference was added anywhere.

## 2. Circular Dependency Analysis

None introduced. `ValidateConfigurationCommandHandler` takes
`IReferenceIntegrityChecker` as a fourth constructor dependency,
resolved from `host.Services` in `Program.cs` exactly as
`EngineeringDomainContext`/`ICommandDispatcher`/`ICommandRegistry`
already are — no new resolution pattern.

## 3. Extension-Point Conformance

`ValidationRuleSet.Register` and `IReferenceIntegrityChecker.CheckBaselineMembersAsync`
are both consumed exactly as their own `WP8.2B`/`WP8.2C` XML
documentation already anticipated — verified by direct inspection of
both call sites (`MechanicalProductStructureSampleModule.InitialiseAsync`
for the former, `ValidateConfigurationCommandHandler.HandleAsync` for
the latter) against that documentation's own stated intent. Neither
required any signature change.

## 4. Two In-Place Fixes — Conformance-Specific Verification

**`TEMPEST-VAL` renumbering.** Confirmed via `grep` across the full
repository (`src/`, `tests/`, `docs/`) that no reference to the old
`TEMPEST-VAL-002`/`-003` meaning (circular parent / has children)
survives anywhere; the `ReferenceIntegrityChecker`-owned meaning of
those two codes is unchanged and now uses the same named constants.

**`ReviseAsync` structural-state copy.** Confirmed the fix accesses only
`private` fields of `EngineeringObjectBase` on a second instance of the
*same* class — ordinary C# field-access scoping, not a new internal
surface, not a reflection-based workaround, and not a change to any
public or protected member.

## 5. API Stability Classification

| Member | Classification | Rationale |
|---|---|---|
| `IHasBomLine` | **Provisional** | First release carrying it |
| `MechanicalObjectFactoryRegistry.CreateAsync`'s new `memberRevisions` parameter | **Provisional, additive** | Optional, defaulted; no existing call site's own meaning changed |
| Five new `IValidationRule` types, three new commands | **Internal** | `Tempest.App`/`Tempest.Core` implementation detail, not a published contract surface |
| `IConfiguration`/`IBaseline`/`IRelease`, `IHasParent`, `IRenamable`, `IDeletable` | **Stable, unchanged** | Confirmed byte-for-byte identical to `v0.9.0`'s own `WP 9.0A` shape |

## 6. Overall Verdict

**Fully conformant.** Every new dependency edge already existed in shape
elsewhere in the platform; the two in-place fixes touch only this
session's own not-yet-committed code and correct, rather than introduce,
a layering/consistency concern.

## Related Documents

`ADR-0080`; `ADR-0083`; `WP9.0A Architecture Conformance Review.md`;
`WP8.0B Dependency Rules.md`; `WP8.2B Dependency Rules.md`.
