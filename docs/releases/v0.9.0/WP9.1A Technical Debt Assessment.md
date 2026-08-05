# WP 9.1A — Requirements Management Workspace — Technical Debt Assessment

## Purpose

Reviews the Technical Debt Register for items this Work Package's own
implementation created, extended, or should have created and did not.

## New Item

### `TD-28` — Bulk Requirements Commands Do Not Trigger an Automatic View Refresh

**What.** `IWorkspaceCommand` carries `TargetObjectId`/`TargetKind` so
generic Workspace infrastructure can call `IWorkspaceView.RefreshAsync`
on whatever open view matches, after any successful dispatch.
`BulkSetRequirementStatusCommand`/`BulkSetRequirementOwnerCommand`/
`BulkSetRequirementPriorityCommand` are deliberately plain `ICommand` —
they touch many requirements, none more "the" target than another,
mirroring `CompareBaselinesCommand`'s (`WP 9.0B`) own identical two-target
reasoning. A consequence, not separately considered when that shape was
chosen: no open view for any of the touched requirements is
automatically refreshed after a bulk operation succeeds.

**How it was found.** Reviewed directly while writing
`RequirementsWorkspaceRegistration`'s own XML documentation and
comparing the three Bulk commands against `IWorkspaceCommand`'s own
stated purpose — not found via a failing test, since no test in this
Work Package's own suite happens to hold an open view across a bulk
operation.

**Disposition — disclosed, not fixed.** A caller that has a Requirement
open in a view, then bulk-updates it via the Command Palette, will see
a stale view until it is next explicitly re-selected or re-opened. No
data-correctness issue — the underlying `IRequirement` state is correct
immediately; only the already-open view's own cached display can lag.

**Why this is debt, not merely a limitation.** The generic refresh
mechanism already exists and already works correctly for every
single-item command in both disciplines; a bulk operation is the one
class of mutation that currently falls outside it, for a reason
(single-target-per-command) that does not, on its own, explain why a
bulk operation cannot refresh *multiple* targets.

**Revisit trigger.** A real user report of a stale view after a bulk
operation, or a future Work Package extending `IWorkspaceCommand` (or a
sibling contract) with a multi-target refresh shape.

**Disposition.** Open.

## Existing Items Reviewed for Extension or Change

- **`TD-22`/`TD-24`/`WP 9.0A`'s and `WP 9.0B`'s own equivalent findings**
  (`ListAllAsync`/list-and-filter reads scale with total object count) —
  the same pattern recurs in `ListCollectionsAsync`/`ListGroupsAsync`/
  `ListAsync` and every Cockpit KPI/validation read built on them. Not
  separately re-registered; see `WP9.1A Security Review Report.md`.
- **`TD-26`** (Runtime Host module-initialisation timing) — unaffected by
  this Work Package; the same test-level `HasRegistered` wait continues
  to be sufficient, confirmed by four consecutive full clean runs with
  zero flakes on that dimension, including the new cross-module
  dependency (`RequirementsWorkspaceSampleModule` → `MechanicalProductStructureSampleModule`).
- **`TD-27`** (unspecified `ConcurrentDictionary`/`IPersistenceStore`
  iteration order) — this Work Package's own new node-provider ordering
  (`RequirementsNodeProvider`, Collections/Groups sorted by Name,
  Requirements by title, all via explicit `OrderBy`) was written with
  `TD-27`'s own lesson already in mind — no reliance on iteration order
  anywhere, confirmed by four consecutive full clean runs with zero
  flakes. No recurrence.

## Two Findings Fixed, Not Registered as Debt

The permission-gated `GetEvidenceAsync` reachable from three passive
surfaces, and the `RequirementGroupDto` parent-resolution ambiguity, are
**not** registered as Technical Debt items — both are genuine
implementation defects in not-yet-committed code, fully fixed with
regression coverage, not accepted, ongoing trade-offs. Recorded in
`WP9.1A Implementation Report.md` and `WP9.1A Lessons Learned.md`
instead, matching how this project distinguishes a fixed bug from a
disclosed, accepted limitation.

## Items Considered and Not Raised

- **No "remove from collection" capability** — not Technical Debt:
  `IEngineeringDocumentStore` has no unlink primitive to build one on;
  explicitly reasoned through and disclosed in `WP9.1A Engineering
  Review Report.md` as a scope decision, not an oversight.
- **`RequirementCollectionExportAdapter` does not replay Status/Owner/
  Priority on import** — not newly raised here: already fully disclosed
  and reasoned in the adapter's own XML documentation, mirroring
  `RequirementExportAdapter`'s (`WP 7.3A`) own identical, already-accepted
  precedent.
- **`RequirementValidationService`'s orphan detection remains
  outgoing-only** — not newly raised: inherited, already-disclosed
  behaviour from before this Work Package (`IEngineeringDocumentStore`
  has no incoming-reference capability); unaffected by any change made
  here.
- **`DeleteGroupAsync`'s guard cannot see live sub-*collections*** — not
  raised: Collections have no parent-group concept by design
  (`WP7.2C Requirements Platform Contracts.md` §3 — collections own
  membership, not hierarchy), so there is no such relationship for the
  guard to fail to check.

## Verdict

**One new item (`TD-28`), formally registered**, a disclosed
availability/freshness characteristic, not a correctness defect. Two
genuine defects found and fully fixed, not registered as debt. No
existing item's own disposition worsened; `TD-27`'s own lesson was
applied proactively, with zero recurrence.

## Related Documents

`docs/governance/Quality/Technical Debt Register.md`; `WP9.0A Technical
Debt Assessment.md` (`TD-26`); `WP9.0B Technical Debt Assessment.md`
(`TD-27`); `ADR-0084`; `ADR-0085`; `WP9.1A Lessons Learned.md`.
