# ADR-0080: Product Structure Mutation (Rename/Move/Delete) Is Three New, Additive Facet Interfaces — Never a Reopening of Any Frozen `WP8.2B` Contract

## Status

Accepted — `v0.9.0` "Mechanical Foundation", `WP 9.0A` (Mechanical Product Structure), 2026-08-05.

## Context

`WP 9.0A`'s own controlling instruction requires Create, Rename, Delete, Move, Copy, and Duplicate over `Project`/`Assembly`/`SubAssembly`/`Part`/`Component` — all six already frozen, `WP8.2C`-implemented canonical Kinds. Create already exists (`EngineeringObjectFactory<T>`, `ADR-0079`); Copy/Duplicate are pure compositions over Create (no new Domain capability). Rename/Delete/Move do not exist anywhere in the Domain: `EngineeringObjectBase.DisplayName` is constructor-only, `IEngineeringDocumentStore` has no delete or unlink operation, and `LifecycleTransitionTable`'s own XML documentation states outright that "no contract in `WP8.2B` proposes a delete operation, so a terminal state is reached and stays reached" — a gap `WP8.2C` already noticed and deliberately left open.

This is a genuine, disclosed gap in the frozen `WP8.2B` contract set — not a design the platform chose against, simply one no prior Work Package needed to fill. `WP 9.0A`'s own constraints are explicit: "No architectural redesign. No contract redesign." — but also: "Any architectural deviation shall require a new ADR." Filling this gap is a deviation; this ADR is that required record.

## Decision

**Three new facet interfaces are added to `Tempest.Core.EngineeringDomain`** (`src/Tempest.Core/EngineeringDomain/Contracts/StructuralMutation.cs`), composed only into `IProject`/`IAssembly`/`ISubAssembly`/`IPart`/`IComponent` — the same composition-over-inheritance extension model `ADR-0075` already established for the ten original facets, applied a second time, never a reopening of any existing facet's own shape:

- **`IRenamable`** — `RenameAsync(string newDisplayName, ...)`. `IHasBusinessIdentifier.DisplayName { get; }` itself is untouched; only classes composing the new facet gain a way to change the value it returns.
- **`IHasParent`** — `ParentId { get; }` + `MoveAsync(Guid? newParentId, ...)`. See `ADR-0081` for how this coexists with the frozen `IAssembly.ChildIds`/`ISubAssembly.ParentAssemblyId`.
- **`IDeletable`** — `IsDeleted { get; }` + `DeleteAsync(...)`. Soft delete only — no document, revision, or relationship is ever erased, matching every other Domain mutation's own append-only ethos. Deliberately **not** a new `LifecycleState` member: deletion is a structural fact, not a lifecycle stage, and `LifecycleState` is a platform-wide, `ADR-0074`-frozen vocabulary every canonical Kind shares — adding a `Deleted` value to it would be a real, and unnecessary, reopening of a genuinely frozen contract, where a new, narrowly-scoped facet is not.

`EngineeringObjectBase` implements all three unconditionally, mirroring how it already implements every other facet unconditionally regardless of which a concrete Kind's own interface composes. `Configuration` composes none of the three — `WP 9.0A`'s own Configuration scope is display/baseline-awareness only ("No configuration management workflows"), so it needs no structural mutation.

`DeleteAsync` rejects deletion of an object that still has live (non-deleted) children, walking `IEngineeringObjectRepository.ListAllAsync` for objects whose own `IHasParent.ParentId` equals the target — a new, disclosed validation rule (`StructuralValidationRules.NoDeleteWithLiveChildren`, `TEMPEST-VAL-007` — corrected from an initially-assigned `TEMPEST-VAL-003` by `WP 9.0B`, which collided with `IReferenceIntegrityChecker.CheckAsync`'s own pre-existing, already-shipped use of that code since `WP 8.2C`), preventing silent orphaning rather than introducing any cascading-delete behaviour.

## Consequences

**Positive:**

- Every one of the ~30 already-shipped, non-Product-Structure concrete Kinds (Requirement, VerificationRecord, Risk, Supplier, and so on) is completely unaffected — none composes any of the three new facets, and `EngineeringObjectBase`'s own unconditional implementation costs them nothing beyond three unused, unreferenced members.
- A future Work Package needing the same capability for a different object family (say, Documentation Kinds) composes the same three facets rather than inventing a parallel mechanism — exactly the reuse `ADR-0075` already intended for facets generally.
- The `DeleteAsync` has-children guard, and `MoveAsync`'s own cycle guard (`ADR-0081`), give this Work Package's own "Object validation" scope item real, tested behaviour rather than an unenforced convention.

**Negative:**

- `WP8.2B`'s own frozen ten-facet catalogue is no longer the complete list a reader of that document alone would expect — genuinely disclosed here and in the `WP 9.0A` Implementation Report, not silently left for a future reader to discover on their own.
- A concrete Kind's own interface (for example `IProject`) now composes facets from two different Work Packages' own vocabularies (`WP8.2B`'s original ten, `WP 9.0A`'s new three) — a minor provenance-tracking cost, mitigated by each facet's own XML documentation stating which Work Package introduced it.

## Alternatives Considered

**Extend `IHasBusinessIdentifier`/`IHasRelationships` directly with new members** — considered and rejected. This would be a genuine reopening of two already-frozen, already-implemented (by every one of ~38 concrete classes) `WP8.2B` interfaces — exactly what `WP 9.0A`'s own "No contract redesign" constraint forbids, and unlike this ADR's chosen approach, would force every existing Kind (Requirement, Risk, and so on) to either implement meaningless Rename/Move/Delete behaviour or throw `NotSupportedException`.

**Represent deletion as a new terminal `LifecycleState` member** — considered and rejected; see Decision, above. `LifecycleState` is shared platform-wide (`ADR-0074`); a "deleted" value would apply nonsensically to every non-structural Kind (a `Requirement`, a `VerificationRecord`) that will never have a Product Structure-style parent/child relationship to guard.

**Workspace-layer-only mutation, no Domain change** — considered and rejected. Explored during planning: Rename is fundamentally blocked (`DisplayName` has no setter anywhere); Delete could not be represented at all without either a new Domain concept or misusing an existing `LifecycleState` value to mean something it does not; Move would have no live parent pointer to update. A purely Workspace-layer implementation would either be dishonest (faking capability the Domain does not have) or simply not build.

## Related Documents

`ADR-0075`; `ADR-0074`; `ADR-0081`; `WP8.2B Dependency Rules.md`; `WP8.2C Engineering Domain Implementation Report.md`; `src/Tempest.Core/EngineeringDomain/Contracts/StructuralMutation.cs`; `src/Tempest.Core/EngineeringDomain/Implementation/EngineeringObjectBase.cs`.
