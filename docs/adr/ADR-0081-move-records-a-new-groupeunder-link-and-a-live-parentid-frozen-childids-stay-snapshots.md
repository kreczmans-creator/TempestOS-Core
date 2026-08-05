# ADR-0081: `Move` Records a New `groupedUnder` Relationship Link and Updates a Live `ParentId` Field — It Never Removes History, and Never Mutates the Frozen `ChildIds`/`ParentAssemblyId`

## Status

Accepted — `v0.9.0` "Mechanical Foundation", `WP 9.0A` (Mechanical Product Structure), 2026-08-05.

## Context

`ADR-0080` adds `IHasParent.MoveAsync`. Two shapes were available for representing "this object's current parent": mutate the frozen, `WP8.2B`-shaped `IAssembly.ChildIds`/`ISubAssembly.ParentAssemblyId` in place, or introduce a new, separate live pointer. `ChildIds` is a plain `IReadOnlyList<Guid>` set once, at construction, by the object's own constructor — there is no reasonable way to make it "live" without changing its own type shape (from a snapshot list to something query-backed), which would be exactly the kind of frozen-contract reopening `ADR-0080` already declined for `IHasBusinessIdentifier`/`IHasRelationships`.

Separately, the existing Relationship framework (`IHasRelationships.LinkAsync`, reused throughout the Domain to record a `"groupedUnder"` composition link — see `EngineeringDomainSampleModule`'s own precedent) is strictly append-only: `IEngineeringDocumentStore.LinkAsync`/`IEngineeringRelationshipRepository.Record` have no corresponding "unlink" operation (by design — every other Domain history, lifecycle transitions and revisions alike, is append-only too).

## Decision

**`IHasParent.ParentId` is a new, live field on `EngineeringObjectBase`, updated only by `MoveAsync`, completely independent of `IAssembly.ChildIds`/`ISubAssembly.ParentAssemblyId`, both of which are left exactly as `WP8.2C` shipped them — untouched, undeprecated, disclosed as a construction-time snapshot only.** Every `MoveAsync` call that moves to a non-null parent also calls the object's own existing `LinkAsync(newParentId, "groupedUnder")` — recording a **new** relationship link to the new parent. The previous `"groupedUnder"` link, if any, is never removed: the Relationship framework's own append-only Digital Thread history therefore accumulates a complete move history (every parent an object has ever had, in order), while `ParentId` itself, and everything the Workspace tree/property panel render from, always reflects only the latest move.

`MoveAsync` also validates against cycles before committing: walking the candidate new parent's own ancestry (via `IEngineeringObjectRepository.FindAsync` and each ancestor's own `IHasParent.ParentId`) and rejecting (`CircularParentAssignmentException`) if the object being moved is found anywhere in that chain, or is the candidate parent itself.

## Consequences

**Positive:**

- Digital Thread compatibility (this Work Package's own explicit Quality requirement) is satisfied by construction: nothing is ever destructively overwritten, matching every other Domain mutation's own precedent (lifecycle `History`, revision `GetRevisionHistoryAsync`).
- `IAssembly.ChildIds`/`ISubAssembly.ParentAssemblyId` remain exactly as frozen — any code written against `WP8.2B`'s own documented shape (none exists yet outside this platform's own tests, but a future external consumer is exactly the audience contract-freezing protects) is unaffected by `WP 9.0A`'s own extension.
- The Project Explorer's own tree (`MechanicalProductStructureNodeProvider`) and the Property Inspector's own "Parent" facet both derive structure from one single, unambiguous source (`ParentId`), never needing to reconcile it against a second, potentially stale one.

**Negative:**

- `IAssembly.ChildIds` is now honestly stale the moment any child is moved after construction — a real, disclosed inconsistency a future reader inspecting `ChildIds` directly (rather than deriving structure from `ParentId`, as this Work Package's own Workspace code does throughout) could be misled by. Both classes' own XML documentation states this explicitly.
- The Relationship framework now carries `"groupedUnder"` links that no longer describe the current structure once an object has moved more than once — a minor semantic broadening of what a `"groupedUnder"` link means (history, not necessarily current fact), disclosed here rather than left implicit.
- A moved object's full move history is only discoverable by querying its own `GetRelationshipsAsync` and filtering by `"groupedUnder"` kind and creation order — no dedicated "move history" read exists. Named as a Future Capability, not built speculatively ahead of a real need.

## Alternatives Considered

**Remove the old `"groupedUnder"` link when recording the new one (an implicit "unlink")** — considered and rejected. `IEngineeringDocumentStore`/`IEngineeringRelationshipRepository` have no unlink operation, and adding one would be a second, larger contract extension solely to support a destructive operation nothing else on this platform needs — directly against the append-only precedent every other Domain history already sets.

**Mutate `ChildIds`/`ParentAssemblyId` in place (require them to become live)** — considered and rejected; see Context, above. Would reopen two already-implemented, frozen `WP8.2B` interface members' own shape and every one of their existing concrete implementations, a strictly larger deviation than adding one new field.

## Related Documents

`ADR-0080`; `ADR-0073` (open, string `DocumentReference`s platform-wide); `WP8.2C Engineering Domain Implementation Report.md`; `src/Tempest.Core/EngineeringDomain/Contracts/StructuralMutation.cs`; `src/Tempest.Core/EngineeringDomain/Implementation/EngineeringObjectBase.cs`; `src/Tempest.App/Workspace/Mechanical/MechanicalProductStructureNodeProvider.cs`.
