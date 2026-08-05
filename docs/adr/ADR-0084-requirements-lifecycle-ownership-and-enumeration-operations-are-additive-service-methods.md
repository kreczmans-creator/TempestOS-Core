# ADR-0084: Requirements Lifecycle, Ownership, Priority, and Enumeration Operations Are Additive `IRequirementsService` Methods — Never a Facet-Composition Retrofit

## Status

Accepted — `v0.9.0` "Mechanical Foundation", `WP 9.1A` (Requirements Management Workspace), 2026-08-05.

## Context

`WP 9.1A`'s own controlling instruction requires a complete Requirements Management experience — Create/Edit/Delete/Duplicate/Move/Group/Bulk editing, Owner, Priority, and full Workspace integration (Explorer tree rooted at Requirement Sets and Groups, Property Inspector, Command Palette) — over the already-real `Tempest.Core.Requirements` framework (`WP 7.3A`). Its own explicit constraints match `WP 9.0A`'s: "No architectural redesign. No contract redesign. No duplicate framework."

`ADR-0080` already established the pattern for exactly this situation in `Tempest.Core.EngineeringDomain` — new, additive facet interfaces, composed into existing Kinds, filling a genuine gap left open by an earlier Work Package. Requirements needed the identical class of gap filled (no delete, no owner, no priority, no group/collection deletion, no way to enumerate a Group or a Collection at all), but `Requirement`/`RequirementGroup`/`RequirementCollection` (`src/Tempest.Core/Requirements/`) are **not** `EngineeringObjectBase`-derived, facet-composable objects — confirmed directly: they are `internal sealed`, fully immutable snapshot classes, reconstructed fresh by `RequirementsService` on every read, with no base class and no facet-composition seam to extend into. `ADR-0080`'s own *mechanism* (new facet interfaces) does not fit; its own *principle* (extend additively, never reopen a frozen shape) does.

A second, disclosed gap surfaced during implementation: `RequirementGroupDto`'s own original documentation stated a group's parent was "recorded entirely through `LinkAsync`... never duplicated into this content," and `FindGroupAsync` resolved `ParentGroupId` via `.FirstOrDefault()` over `GetReferencesAsync`'s own returned list — order-dependent, and `IPersistenceStore` carries no ordering guarantee (the same class of risk `WP 9.0B`'s own `TD-27` already found and fixed for a different repository). This was never triggered before this Work Package, because nothing ever recorded a second `groupedUnder` link for the same group — but a real `MoveGroupAsync` needs to, making the existing resolution genuinely ambiguous the moment it exists.

A third, disclosed gap: no enumeration of Groups or Collections existed at all. `IEngineeringDocumentStore` has no "list every document of a Kind" capability (confirmed directly), and unlike Requirements, Groups and Collections were never identifier-indexed. The Engineering Workspace's own Project Explorer needs to root its tree at every live Requirement Set and every live root Group — an enumeration capability, not a mutation one, but the identical "genuine gap, no mechanism yet exists" shape.

## Decision

**Every new capability is an additive method on the existing `IRequirementsService`/`RequirementsService`, never a new interface, base class, or storage mechanism:**

- **Lifecycle/ownership/priority:** `SetOwnerAsync`, `SetPriorityAsync`, `DeleteAsync` (requirement, soft), `MoveToGroupAsync`, `MoveGroupAsync`, `DeleteGroupAsync`, `DeleteCollectionAsync`. Each follows the exact `dto with { ... }` + `IEngineeringDocumentStore.ReviseAsync` shape `SetStatusAsync`/`ReviseAsync` already established — no new mutation pattern, only new call sites of the one that already exists. `IRequirement`/`IRequirementGroup`/`IRequirementCollection` each gain `IsDeleted`; `IRequirement` gains `Owner` and `Priority` (a new, small `RequirementPriority` enum — Low/Medium/High/Critical, mirroring `RequirementStatus`'s own placement) and `GroupId` (the live, current group membership).
- **`RequirementGroupDto` storage-model fix:** `ParentGroupId` is now stored directly on the DTO (`internal`, not a public contract — `IRequirementGroup`'s own shape is unchanged), resolved via the same safe `dto with {...}` pattern every other mutation already uses. The `groupedUnder` relationship link is still recorded on every create/move, for Digital Thread history — it stops being the *resolution* mechanism, without stopping being part of the historical record.
- **Enumeration:** `ListCollectionsAsync`, `ListGroupsAsync` — each backed by a second, small `IPersistenceStore`-direct registry (`Requirements.CollectionRegistry`, `Requirements.GroupRegistry`), mirroring `FindByIdentifierAsync`'s own already-approved `ADR-0059` precedent for the identical reason (`IEngineeringDocumentStore` has no list-by-Kind capability to build on otherwise). `ListGroupsAsync`'s addition also let `DeleteGroupAsync`'s own has-children guard start checking live sub-groups, not only live grouped requirements — closing a narrower gap the guard's own exception type originally, briefly, disclosed before this capability existed to close it.

`IRequirementValidationService`/`RequirementValidationService` (a new, small, Requirements-scoped validation contract — reusing `IValidationResult`/`IValidationDiagnostic`'s own generic, type-agnostic result shape, never `IValidationRule` itself, which is scoped to `IEngineeringObject` and structurally cannot validate an `IRequirement`) is added the same way: new capability, existing result vocabulary, no new mechanism.

## Consequences

**Positive:**

- Every existing `IRequirementsService` consumer (`RequirementsSampleModule`, every `WP 7.3A` test) is completely unaffected — every new member is additive, nothing existing changed shape.
- The Engineering Workspace's own Requirements area can root its tree at real, enumerable Requirement Sets and Groups, exactly as `WP 9.0A`'s Mechanical area roots at real, enumerable Projects.
- The `RequirementGroupDto` fix removes a real, latent correctness bug before it was ever triggered in a committed/tagged release — disclosed, not silently patched.

**Negative:**

- `IRequirementsService` grows from 13 to 20 methods in one Work Package — a real surface-area increase, mitigated by every new method's own narrow, single-purpose shape and full XML documentation.
- `CountLiveGroupChildrenAsync`'s own has-children guard still cannot discover a group's own live sub-*collections* (collections have no parent-group concept to begin with, by design — `WP7.2C Requirements Platform Contracts.md` §3's own "collections own membership, not hierarchy" model), a boundary this ADR does not attempt to change.

## Alternatives Considered

**Force Requirements into `EngineeringObjectBase`'s own facet-composition model** — considered and rejected. `Requirement`/`RequirementGroup`/`RequirementCollection` are immutable snapshot classes by original `WP 7.3A` design, reconstructed on every read; retrofitting them onto `EngineeringObjectBase` would be exactly the "architectural redesign" this Work Package's own controlling instruction forbids, for a framework `WP 7.3A` already deliberately built differently.

**Leave `RequirementGroupDto`'s `.FirstOrDefault()` resolution as-is, since it was never actually wrong until now** — considered and rejected. `WP 9.1A`'s own scope explicitly requires `MoveGroupAsync`; shipping a `Move` operation over a resolution mechanism already known to become ambiguous the moment it is used would be building a defect on delivery, not disclosing and fixing one.

**Add a Domain-level "list every document of a Kind" capability to `IEngineeringDocumentStore` instead of a per-service registry** — considered and rejected. That would be a genuine contract redesign of a shared, `WP 8.2C`-frozen Domain interface, affecting every consumer, to solve a need only Requirements has today; the same class of "add a narrow, service-owned index instead of reopening a shared contract" reasoning `ADR-0059` already applied to `FindByIdentifierAsync`.

## Related Documents

`ADR-0080`; `ADR-0059`; `ADR-0073`; `WP7.2C Requirements Platform Contracts.md`; `WP7.3A Requirements Engine Implementation Report.md`; `WP9.1A Technical Debt Assessment.md`; `src/Tempest.Core/Requirements/IRequirementsService.cs`; `src/Tempest.Core/Requirements/RequirementsService.cs`; `src/Tempest.Core/Requirements/RequirementGroupDto.cs`.
