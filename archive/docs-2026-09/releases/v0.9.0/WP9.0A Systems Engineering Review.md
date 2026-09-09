# WP 9.0A — Mechanical Product Structure — Systems Engineering Review

## Purpose

Reviews `WP 9.0A` from a systems-engineering standpoint: does the shipped
Mechanical Product Structure integrate coherently with the platform's own
existing Systems Engineering Foundation (Requirements, Verification,
Calculations, Materials, the Engineering Domain generally), or does it
introduce a parallel, competing structure the rest of the platform will
eventually need reconciling with.

## What Mechanical Product Structure Now Exists

A user can, inside the real Engineering Workspace: browse a Project's
own Assembly/Sub-Assembly/Part/Component hierarchy to arbitrary depth;
select any object and see its real Engineering Identifier, Name,
Revision, Status, Owner, Discipline, Classification, Tags, and Notes;
create, rename, move, copy, duplicate, and (with a live-children guard)
delete any of the five structural Kinds; and see which `Configuration`
baseline(s), if any, reference a given object, and whether it has reached
`Released`. This is the first of `WP 8.2A`'s own ~13 canonical object
families to receive real Workspace presentation — a concrete, working
precedent every later family (Documentation & Design, Requirements &
Verification, Supply Chain, and so on) can follow.

## Confirms Rather Than Redesigns

- **Reuses the same `EngineeringObjectFactory<T>`/`EngineeringObjectBase`
  machinery** every other canonical Kind already reuses (`ADR-0079`) —
  Create introduces no new pattern.
- **Reuses the Relationship framework** (`IHasRelationships.LinkAsync`,
  `RelationshipCategory`, `ADR-0073`/`ADR-0076`) for both Move's own
  history and the shared-Component cross-reference — no second reference
  mechanism was introduced.
- **Reuses the Command Framework** (`ICommandDispatcher`/
  `ICommandRegistry`, unchanged) — the six Mechanical commands are the
  Command Framework's own first real, non-sample `IWorkspaceCommand`
  implementations, proving `WP5.1A`/`WP5.1B`'s own design against a real
  discipline for the first time.
- **Reuses the Kind-keyed Workspace extension model** (`ADR-0067`) a
  third time (`IPropertyFacetProvider`, `ADR-0082`), rather than
  inventing a fourth, different extensibility mechanism.
- **Reuses `Configuration`'s own existing shape** for baseline display —
  no second "which objects are baselined" mechanism was introduced.

## What Remains Outside This Work Package's Own Scope

No Requirements traceability from a Part/Assembly to the Requirements it
satisfies was built — `Requirement`'s own relationships remain exactly as
`WP7.3A` shipped them, reachable only through the generic
`GetRelationshipsAsync`/Digital Thread mechanisms, not through any
Mechanical-specific UI. No Verification or Calculation result is shown
against a Part/Assembly in the Workspace — `EngineeringCockpit`'s own
`RequirementsStatus`/`VerificationStatus`/`CalculationStatus` remain
`Unknown`, honestly. No Manufacturing Operation, Supplier, or Purchase
Item is presented in the Workspace, though all three already exist as
real `WP8.2C` Kinds. No Configuration Management workflow (create,
approve, release a baseline) exists — display only, per this Work
Package's own explicit constraint. All are candidates for a future
Engineering Discipline Module, not gaps in this one's own delivery.

## Verdict

**Sound.** Mechanical Product Structure integrates by reuse throughout —
zero new parallel mechanisms, zero platform duplication — and
establishes a real, working, tested precedent for every later Engineering
Discipline Module's own Workspace integration to follow.

## Related Documents

`WP9.0A Implementation Report.md`; `ADR-0067`; `ADR-0073`; `ADR-0076`;
`ADR-0079`; `ADR-0082`; `WP8.2A Engineering Domain Architecture.md`;
`WP7.2A Strategic Roadmap Selection and Programme Architecture.md`.
