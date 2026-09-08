# WP 9.2A — Engineering Calculations Workspace — Systems Engineering Review

## Purpose

Reviews `WP 9.2A` from a systems-engineering standpoint: does the
shipped Engineering Calculations experience integrate coherently with
the platform's own existing Engineering Foundation and Workspace
extension model, or does it introduce a parallel structure.

## What Engineering Calculations Now Exists

A user can, inside the real Engineering Workspace: browse a Calculations
area rooted at a read-only "Templates" catalogue of every registered
Calculation Template (Name/Category/Description/Assumptions/
Constraints), every real Calculation Set, and every real, un-parented
Calculation; drill into a Set to find its own member Calculations; select
a Calculation and see its own Identity/Owner/Discipline/Status/Approved/
Revision/Parent facets, plus (once executed) its own Latest Result,
Latest Result Outcome, Safety Factor, Result History count, Referenced
Materials, and Digital Thread Based-On/Used-By links, all real reads;
create, rename, edit (revise), delete, move, copy, and duplicate a
Calculation or Calculation Set; execute a registered Template against a
target object, recording a real, durable `CalculationRecord` and linking
it back; recalculate; lock/unlock/request review/approve/archive a
Calculation's own lifecycle status; search the Calculations area by free
text, exactly like every other Workspace area; and see the Engineering
Cockpit's own Calculations KPIs — Total/Draft/Review/Approved/Failed/
Out-of-date/Verification Coverage/Calculation Health — computed live
from the real Calculation graph, replacing the prior placeholder card.

## Confirms Rather Than Redesigns

- **Reuses the Kind-keyed Workspace extension model a third real
  discipline's worth** (`ADR-0067`) — `CalculationsNodeProvider`/
  `CalculationsWorkspaceViewFactory`/`CalculationsPropertyFacetProvider`
  mirror `MechanicalProductStructureNodeProvider`/
  `MechanicalWorkspaceViewFactory`/`MechanicalPropertyFacetProvider`'s
  own exact shape, extended by one synthetic, registry-backed Kind
  (`"CalculationTemplate"`) for content that has no Domain identity at
  all — proof the pattern generalises even to non-`IEngineeringObject`
  content, without any change to `IWorkspaceManager`'s own frozen
  registration surface.
- **Reuses `ProjectExplorer.FilterAsync`** (`WP8.1B`) for Search — zero
  new code, identical to both prior disciplines' own experience.
- **Reuses the existing Digital Thread reads exclusively** —
  `GetRelationshipsAsync`/`GetIncomingAsync` and the pre-existing
  `"calculatedBy"`/`"basedOnCalculation"` relationship kinds
  (`RelationshipKindCategoryMap`, `WP 8.2A`/`WP 8.2B`, unchanged) carry
  every Digital Thread fact this Work Package presents; zero new
  traversal code, honouring this Work Package's own explicit "Reuse the
  existing Digital Thread" instruction.
- **Reuses `EngineeringObjectBase`'s own unconditional facet
  implementation exactly as Mechanical does** — `IRenamable`/
  `IHasParent`/`IDeletable`/`IHasRevisions`/`IHasLifecycle` all already
  exist on `Calculation`/`CalculationSet`; every command is a cast plus a
  call, never a new mutation mechanism.
- **Reuses the shared `IEngineeringDocumentStore` a second, genuinely
  different way** — `CalculationEngine` (`WP 7.1D`) already wrote every
  `CalculationRecord` into it; `CalculationRecordReader` is the
  Workspace's own read side of that exact, unmodified store — the two
  frameworks' own already-real integration point (`ADR-0056`'s own
  design), simply read from for the first time.

## What Remains Outside This Work Package's Own Scope

Calculation Templates are registered programmatically, at module-init
time, exactly as `ICalculationEngine.RegisterDefinition`'s own XML
documentation always specified ("Expected to be called only during
module initialisation") — no runtime Template-authoring UI exists, nor
was one asked for. No concrete `ICalculationResult`/`IVerificationResult`
implementation exists, so `ITraceable.GetEvidenceAsync`-based evidence
composition remains honestly empty for every Calculation (worked around
via direct relationship reads, not fixed — see Technical Debt
Assessment). No concrete `IApprovalGate`/`IApproval`/`IReview` workflow
exists — Approval State is a `LifecycleState` reading, not a governed
sign-off record. Recalculate cannot resume from a previously-executed
input, since none is retained by the Framework's own stored shape. All
are candidates for a future Work Package, not gaps in this one's own
delivery.

## Verdict

**Sound.** Engineering Calculations integrates by reuse throughout,
exactly as Mechanical and Requirements both did before it — the
Kind-keyed extension model has now been proven across three genuinely
different Engineering disciplines (one facet-composed, one
immutable-snapshot, one plain-object-plus-separate-execution-framework)
without a single frozen Domain contract being reopened; every new
capability is either a new call site of an already-proven pattern, or a
disclosed, narrowly-scoped, Workspace-layer-only additive extension
recorded in `ADR-0086`/`ADR-0087`.

## Related Documents

`WP9.2A Implementation Report.md`; `ADR-0067`; `ADR-0086`; `ADR-0087`;
`WP9.0A Systems Engineering Review.md`; `WP9.1A Systems Engineering
Review.md`; `WP7.1D-engineering-calculation-framework-implementation.md`.
