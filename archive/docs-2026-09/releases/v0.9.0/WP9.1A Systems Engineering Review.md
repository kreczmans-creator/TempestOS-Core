# WP 9.1A — Requirements Management Workspace — Systems Engineering Review

## Purpose

Reviews `WP 9.1A` from a systems-engineering standpoint: does the
shipped Requirements Management experience integrate coherently with
the platform's own existing Systems Engineering Foundation and Workspace
extension model, or does it introduce a parallel structure.

## What Requirements Management Now Exists

A user can, inside the real Engineering Workspace: browse a Requirements
area rooted at real Requirement Sets and a real Group hierarchy; drill
into a Group to find its own sub-groups and directly-grouped
requirements, or into a Collection to find its own members; select a
requirement and see its own Identifier/Statement/Category/Status/Owner/
Priority/Revision/Group, plus Verification Coverage and Allocation
targets, all real reads; create, revise, set status/owner/priority,
delete, move, duplicate, and link a requirement; create, move, and
delete a group; create, delete, and add-to a collection; apply the same
status/owner/priority change to many requirements at once; search the
Requirements area by free text, exactly like every other Workspace area;
and see the Engineering Cockpit's own Requirements KPIs — Total/Draft/
Review/Approved/Released/Verification Coverage/Allocation Coverage/
Requirement Health/Outstanding Actions — computed live from the real
Requirements graph, replacing every prior placeholder card.

## Confirms Rather Than Redesigns

- **Reuses the Kind-keyed Workspace extension model a second real
  discipline's worth** (`ADR-0067`) — `RequirementsNodeProvider`/
  `RequirementsWorkspaceViewFactory`/`RequirementsPropertyFacetProvider`
  mirror `MechanicalProductStructureNodeProvider`/
  `MechanicalWorkspaceViewFactory`/`MechanicalPropertyFacetProvider`'s
  own exact shape — a second, independent proof the pattern
  generalises, exactly as `WP 9.0B`'s own Lessons Learned anticipated.
- **Reuses `ProjectExplorer.FilterAsync`** (`WP8.1B`) for Search — the
  entire "Search" scope item required zero new code once the node
  provider was registered; no second filter/index mechanism was built.
- **Reuses the existing Digital Thread reads exclusively** — every
  Verification Coverage/Allocation facet and KPI is `GetRelationshipsAsync`-
  or `GetEvidenceAsync`-derived (the latter corrected away from, for
  availability reasons — see Security Review); zero new traversal code,
  honouring this Work Package's own explicit "Do not implement new
  traceability mechanisms."
- **Reuses the `WP 6.7` Export/Import framework exactly** —
  `RequirementCollectionExportAdapter` is the same `IExportable`/
  `IExportableKind`/`IImportable` triad `RequirementExportAdapter`
  (`WP 7.3A`) already proved, scaled to Requirement Set granularity.
- **Reuses `SetStatusAsync`'s own mutation shape seven more times** —
  every new `IRequirementsService` method follows the identical `dto with
  {...}` + `IEngineeringDocumentStore.ReviseAsync` pattern; no second
  mutation mechanism was invented for a framework whose own architecture
  (immutable snapshots, not facet-composed objects) genuinely differs
  from `EngineeringDomain`'s.

## What Remains Outside This Work Package's Own Scope

No Domain-level `ISearchQuery`/`ISearchResult` implementation exists —
Search is satisfied entirely at the Workspace layer (see Engineering
Review Report). No "remove from collection" capability exists —
`IEngineeringDocumentStore` has no unlink primitive. No cross-discipline
traceability visualisation beyond what the Property Inspector's own flat
facet list already shows — a Digital Thread graph view remains
`WP8.0A`'s own already-disclosed placeholder, unchanged by this Work
Package. All are candidates for a future Work Package, not gaps in this
one's own delivery.

## Verdict

**Sound.** Requirements Management integrates by reuse throughout,
exactly as both `WP 9.0` Work Packages did — the Kind-keyed extension
model has now been proven across two genuinely different Engineering
disciplines (one facet-composed, one immutable-snapshot) without a
single frozen contract being reopened; every new capability is either a
new call site of an already-proven pattern, or a disclosed, narrowly-
scoped additive extension recorded in `ADR-0084`/`ADR-0085`.

## Related Documents

`WP9.1A Implementation Report.md`; `ADR-0067`; `ADR-0084`; `ADR-0085`;
`WP9.0A Systems Engineering Review.md`; `WP9.0B Systems Engineering
Review.md`; `WP8.2A Engineering Domain Architecture.md`.
