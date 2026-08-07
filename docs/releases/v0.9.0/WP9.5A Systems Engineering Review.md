# WP 9.5A — Manufacturing Workspace — Systems Engineering Review

## Purpose

Reviews `WP 9.5A` from a systems-engineering standpoint: does the
shipped Manufacturing Workspace experience integrate coherently with
the platform's own existing Engineering Foundation and Workspace
extension model, or does it introduce a parallel structure.

## What Manufacturing Now Exists

A user can, inside the real Engineering Workspace: browse a
Manufacturing area rooted at five category nodes (Routings, Operations,
Supplier Operations, Work Instructions, Inspections), each containing
every live, un-parented Manufacturing object that falls into it, across
all three Manufacturing Kinds together; drill into a Routing to see its
own real, sequenced Operation steps; select an Operation and see its own
Identity/Owner/Discipline/Part/Classification/Status/Released/Revision/
Parent/BOM facets, plus real Digital Thread References/Manufactured
By/Documented By/Verified By links; select a Work Instruction or
Inspection and see the identical real facets Documents/Verification
already show for their own native Kind; create, rename, edit (revise),
delete, move, copy, and duplicate a Manufacturing object; set its own
Bill of Materials line (quantity, unit, find number, item number,
reference designator) using the pre-existing Mechanical command, unmodified;
release or archive its own lifecycle status; record a real, durable
`IVerificationRecord` against an Inspection using the pre-existing
Verification command, unmodified; search the Manufacturing area by free
text, exactly like every other Workspace area; and see the Engineering
Cockpit's own Manufacturing KPIs — Manufacturing Objects/Manufacturing
Readiness/Released Items/Open Operations/Supplier Status/Inspection
Status/Production Health — computed live from the real Manufacturing
graph, a purely additive card set with no prior placeholder to replace.

## Confirms Rather Than Redesigns

- **Reuses the Kind-keyed Workspace extension model a sixth real
  discipline's worth** (`ADR-0067`) — `ManufacturingNodeProvider`/
  `ManufacturingWorkspaceViewFactory`/
  `ManufacturingOperationPropertyFacetProvider` mirror `DocumentsNodeProvider`/
  `DocumentsWorkspaceViewFactory`/`DocumentsPropertyFacetProvider`'s own
  exact shape, over three real Domain Kinds and a purely Workspace-layer
  categorisation scheme (`ManufacturingCategory.Of`) — the third
  consecutive Work Package to prove this generalisation, after `WP 9.4A`
  and `WP 9.3A`'s own identical proofs.
- **Proves genuine cross-Work-Package read-side reuse for the first
  time in this project.** `Inspection`/`WorkInstruction` register
  `Verification`'s/`Documents`' own Property Facet Provider and
  Workspace View types directly, constructed with a different `Kind`
  string — no prior Work Package needed to reuse another discipline's own
  Workspace-layer types this way, since no prior Work Package's own
  scope named a Domain Kind that was simultaneously a subtype of a
  different, already-Workspace-integrated discipline's own base type
  (`WorkInstruction : Document`, `Inspection : VerificationActivity`).
- **Proves the Kind-agnostic command design every prior Work Package's
  own commands already claimed, empirically, for the first time.**
  `Mechanical.SetBomLineCommand` (`WP 9.0B`) and `Verification
  .RecordVerificationResultCommand` (`WP 9.3A`) are dispatched, unmodified,
  against Manufacturing Kinds — both succeed, proven by dedicated tests,
  not merely asserted compatible by inspection.
- **Reuses `ProjectExplorer.FilterAsync`** (`WP8.1B`) for Search — zero
  new code, identical to every prior discipline's own experience.
- **Reuses the existing Digital Thread reads exclusively** — real
  `"references"`/`"manufacturedBy"`/`"documentedBy"`/`"verifiedBy"`
  relationship reads, all already mapped since `WP 8.2A`/`WP 8.2B`; zero
  new traversal code, honouring this Work Package's own explicit "Reuse
  existing Engineering Objects" instruction.
- **Reuses `EngineeringObjectBase`'s own unconditional facet
  implementation exactly as every prior discipline does** —
  `IRenamable`/`IHasParent`/`IDeletable`/`IHasRevisions`/`IHasLifecycle`/
  `IHasRelationships`/`IHasBomLine` all already exist on
  `ManufacturingOperation`; every command is a cast plus a call, never a
  new mutation mechanism.

## What Remains Outside This Work Package's Own Scope

No concrete `IApprovalGate`/`IApproval`/`IReview`/`IReviewGate`
implementation exists, so a Manufacturing object's own approval state is
a `LifecycleState` reading, not a governed sign-off record (`TD-30`,
confirmed still open, not introduced here — now consequential for a
fourth real discipline). No file/URL evidence storage exists for
Tooling/Fixture Documents (mirrors `WP 9.4A`'s own already-disclosed
`TD-31`). `EngineeringCockpit.FormatCoverage`'s own zero-denominator
text is hardcoded Requirements-specific, inaccurate when reused by a
different discipline's own coverage card (`TD-33`, this Work Package's
own new finding, worked around by not reusing it, not fixed at the
shared helper itself). `"Test"` (a real, compiled `VerificationActivity`
subtype) remains uninstantiated anywhere in the platform, mirroring
`WP 9.3A`'s own already-disclosed non-use of the bare `Verification`
marker Kind. All are candidates for a future Work Package, not gaps in
this one's own delivery.

## Verdict

**Sound.** Manufacturing integrates by reuse throughout, exactly as
every prior real discipline did before it — the Kind-keyed extension
model has now been proven across six genuinely different Engineering
disciplines without a single frozen Domain contract being reopened; the
one new architectural decision (`ADR-0091`) is confined entirely to the
Workspace layer; this Work Package additionally proves, empirically, two
claims every prior Work Package's own documentation asserted but never
tested against a foreign discipline (`SetBomLineCommand`'s and
`RecordVerificationResultCommand`'s own Kind-agnosticism), and
establishes a new, disclosed pattern (cross-Work-Package facet/view
provider reuse) other future disciplines can now follow with precedent.

## Related Documents

`WP9.5A Implementation Report.md`; `ADR-0067`; `ADR-0091`; `WP9.0A
Systems Engineering Review.md`; `WP9.4A Systems Engineering Review.md`;
`WP9.3A Systems Engineering Review.md`.
