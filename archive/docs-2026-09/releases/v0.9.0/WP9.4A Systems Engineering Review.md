# WP 9.4A — Engineering Documents Workspace — Systems Engineering Review

## Purpose

Reviews `WP 9.4A` from a systems-engineering standpoint: does the
shipped Engineering Documents experience integrate coherently with the
platform's own existing Engineering Foundation and Workspace extension
model, or does it introduce a parallel structure.

## What Engineering Documents Now Exists

A user can, inside the real Engineering Workspace: browse a Documents
area rooted at nine category nodes (Drawings, CAD Models,
Specifications, Reports, Procedures, Standards, Datasheets, External
References, Uncategorized), each containing every live, un-parented
Document that falls into it; drill into a Document to find its own real
`IHasParent`-nested children (proven by the Detail Drawing nested under
the GA Drawing); select a Document and see its own Identity/Owner/
Discipline/Classification/Status/Approved/Revision/Parent/Attachments
facets, plus Drawing Number or Model Format where the Kind carries one,
and real Digital Thread References/Documents links; create, rename,
edit (revise), delete, move, copy, and duplicate a Document, Drawing, or
CAD Model; attach a file reference; transition a Document's own status
through Draft/InReview/Approved/Released (and on to Superseded/Obsolete/
Archived/Cancelled, the platform-wide closed lifecycle); search the
Documents area by free text, exactly like every other Workspace area;
and see the Engineering Cockpit's own Documents KPIs — Total Documents/
Draft/Review/Approved/Released/Outstanding Reviews/Missing Evidence/
Documentation Health — computed live from the real Document graph,
replacing the prior placeholder card.

## Confirms Rather Than Redesigns

- **Reuses the Kind-keyed Workspace extension model a fourth real
  discipline's worth** (`ADR-0067`) — `DocumentsNodeProvider`/
  `DocumentsWorkspaceViewFactory`/`DocumentsPropertyFacetProvider`
  mirror `CalculationsNodeProvider`/`CalculationsWorkspaceViewFactory`/
  `CalculationsPropertyFacetProvider`'s own exact shape, extended by a
  synthetic, purely-Workspace-layer categorisation scheme
  (`DocumentCategory.Of`) over one real Domain Kind, rather than a
  synthetic Kind of its own — proof the pattern generalises to
  "many display categories over one real Kind" just as readily as it
  already proved "one synthetic Kind with no Domain identity at all"
  (`WP 9.2A`'s own Calculation Template).
- **Reuses `ProjectExplorer.FilterAsync`** (`WP8.1B`) for Search — zero
  new code, identical to every prior discipline's own experience.
- **Reuses the existing Digital Thread reads exclusively** —
  `GetRelationshipsAsync`/`GetIncomingAsync` and the pre-existing
  `"documentedBy"`/`"references"` relationship kinds
  (`RelationshipKindCategoryMap`, `WP 8.2A`/`WP 8.2B`, unchanged) carry
  every Digital Thread fact this Work Package presents; zero new
  traversal code, honouring this Work Package's own explicit "Reuse the
  existing Digital Thread" instruction.
- **Reuses `EngineeringObjectBase`'s own unconditional facet
  implementation exactly as every prior discipline does** —
  `IRenamable`/`IHasParent`/`IDeletable`/`IHasRevisions`/`IHasLifecycle`/
  `IHasAttachments`/`IHasRelationships` all already exist on `Document`/
  `Drawing`/`CadModel`; every command is a cast plus a call, never a new
  mutation mechanism.
- **Reuses the existing, closed `LifecycleState` vocabulary without any
  aliasing** — unlike Calculations' Lock/Unlock/Review/Approve/Archive
  aliasing over a five-name-to-four-state mapping (`ADR-0087`), this
  Work Package's own named statuses (Draft/Review/Approved/Released) map
  one-for-one onto `LifecycleState`'s own existing values — the simplest
  possible integration of the four real-discipline Work Packages so far.

## What Remains Outside This Work Package's Own Scope

No file/URL storage service exists anywhere in this platform — an
Attachment is metadata only (`TD-31`), and an External Reference
Document's own Content field is a descriptive placeholder, never a
resolvable path or fetched resource. No concrete Verification Domain
object exists anywhere in the platform (a direct consequence of the
disclosed `WP 9.3A` numbering gap — see the Implementation Report),
so Documents↔Verification traceability is demonstrated structurally
(the same generic relationship mechanism) but not against a live
Verification object today. No concrete `ICalculationResult`/
`IVerificationResult`/`IApprovalGate` implementation exists, so
`ITraceable.GetEvidenceAsync`-based evidence composition remains
honestly empty for every Document (worked around via direct
relationship/attachment reads, not fixed — see Technical Debt
Assessment). All are candidates for a future Work Package, not gaps in
this one's own delivery.

## Verdict

**Sound.** Engineering Documents integrates by reuse throughout, exactly
as Mechanical, Requirements, and Calculations all did before it — the
Kind-keyed extension model has now been proven across four genuinely
different Engineering disciplines without a single frozen Domain
contract being reopened; the one new capability
(`AttachDocumentCommand`) is a narrow, additive wrapper over an
already-existing, already-compiled facet; the one new architectural
decision (`ADR-0088`) is confined entirely to how six display categories
map onto one existing, open Domain facet, never a new Domain type.

## Related Documents

`WP9.4A Implementation Report.md`; `ADR-0067`; `ADR-0088`; `WP9.0A
Systems Engineering Review.md`; `WP9.1A Systems Engineering Review.md`;
`WP9.2A Systems Engineering Review.md`.
