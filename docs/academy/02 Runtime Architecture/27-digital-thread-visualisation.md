# Digital Thread Visualisation

## 1. Introduction

`WP 10.4A`'s own concept guide — how TempestOS turned `ADR-0093`'s own
architectural decision (a progressively-expandable node-link graph,
never a precomputed transitive traversal) into a real, working,
interactive control, and why its own composed-read design choice
deliberately diverges from `ADR-0093`'s own literal example.

## 2. Purpose

Explains why "the graph shall be a presentation over the existing
Digital Thread" led to reusing `ObjectEditorView`'s own bidirectional
relationship read rather than `IEvidenceComposer`, how expand/collapse
correctly preserves shared reachability without precomputing anything,
and how a two-release-old disclosed debt item (`TD-32`) became visible,
for the first time, as a real graph node.

## 3. Background

Every prior `v0.10.0` Work Package through `WP 10.3B` explicitly
excluded a Digital Thread graph ("No Digital Thread graph"). `WP 10.4A`
reverses that exclusion directly — the same "genuine, disclosed
reversal, not a contradiction" pattern `WP 10.3B`'s own ribbon
already established one release earlier. Unlike the ribbon case,
though, a governing ADR already existed before this Work Package began:
`ADR-0093` (superseding `ADR-0065`'s own earlier "flat, one-hop list"
decision) was written during `WP 10.0A`, specifically to authorise
exactly this graph, once built. This Work Package's own instruction
says so directly — "Honour `ADR-0093`" — so there is no ADR tension to
resolve here, only an ADR to realise.

## 4. The Problem

`ADR-0065` had decided, correctly for its own time, that a flat,
one-hop relationship list was enough — no demonstrated need existed for
a full interactive graph, and no platform capability existed for
transitive traversal. `ObjectEditorView`'s own Relationship summary
(`WP 10.3A`) built exactly that flat list, and built it well — but a
Digital Thread genuinely is a graph, and an engineer following a
requirement through its verification, through its calculation, through
its manufacturing record, benefits from seeing the shape of that chain,
not re-opening a new tab at every hop.

## 5. The Design

**Progressive, client-side, on-demand expansion — never precomputed.**
`DigitalThreadGraphModel.ExpandNode` issues a fresh, live read every
time; nothing about the graph's own shape is ever cached, indexed, or
persisted. Collapsing a node removes exactly what that node's own
expansion added, unless another still-expanded path keeps a shared
node reachable — implemented as a general fixpoint reachability sweep
from the centre, not fragile "who added this" bookkeeping.

**Reused reads, not a new one.** Every edge comes from
`IHasRelationships.GetRelationshipsAsync` (outgoing) +
`RelationshipRepository.GetIncomingAsync` (incoming) — the identical
pair `ObjectEditorView` already proved, generalised from "one flat
list" to "one hop of a graph, repeatable." `IEvidenceComposer` was
considered and rejected (§6).

**`TD-32`, made visible for the first time.** A Verification Activity's
own `"verifiedBy"` link has been invisible to `RelationshipRepository`
since `WP 9.3A` — durable, correctly readable via the raw store, but
never through the relationship graph. `WP10.0A Digital Thread &
Relationship Visualisation.md` itself predicted this and asked that the
debt item's own disposition be revisited once a graph view existed to
actually surface it. This Work Package does exactly that: expanding a
Verification Activity node additionally calls `VerificationRecordReader`
and renders the result as a real, visible, non-editable leaf.

**One generic engine, three layouts.** `Hierarchical`, `Engineering`
(concentric rings), and `ForceDirected` (a seeded spring simulation)
all operate on the same node/edge data — real, distinct, deterministic
algorithms, not three static arrangements wearing different labels.

## 6. Alternatives Considered

- **`IEvidenceComposer`/`IEvidence` as the composed read** — rejected.
  Outgoing-only (would silently omit every incoming relationship), and
  its own Verification/Calculation-result enrichment resolves
  structurally empty today (`TD-30`) — strictly less complete than the
  bidirectional pair already proven in `ObjectEditorView`.
- **A new dockable `IWorkspacePanel`, per `WP10.0A`'s own "two
  independently dockable panels" language** — rejected. `DockingGrid`
  has exactly three slots, all occupied; a fourth would itself be a
  Workspace docking change, out of this Work Package's own "No
  Workspace redesign" scope. A Document Area tab reuses everything
  already built instead.
- **A precomputed/cached multi-hop graph** — rejected, by `ADR-0093`
  itself, before this Work Package began: staleness risk, and no
  demonstrated need for a platform-level transitive-traversal
  capability.

## 7. Why This Solution Was Chosen

Every alternative considered either widened scope beyond what the
instruction explicitly permitted (a new dock slot, a new Core
capability) or produced a strictly less complete result (outgoing-only
reads). The chosen design realises every named scope item using
exclusively reads and patterns that already existed before this Work
Package began.

## 8. Architectural Principles

- **A rendering-layer decision changes nothing about the data it
  renders** — `ADR-0093`'s own central discipline, re-proven here: zero
  new Tempest.Core contract, zero new persisted state.
- **Reuse the proven pattern over the literally-named one** when the
  literally-named one is demonstrably less complete (§6) — a real,
  disclosed, defensible judgment call, not a shortcut.
- **A found gap gets disclosed twice** — once where it was found
  (`TD-32`'s own register entry), and once in the class that newly
  depends on the workaround (`DigitalThreadGraphModel`'s own remarks).

## 9. Benefits

An engineer can now see a requirement's own verification chain, a
part's own BOM neighbourhood, or a document's own reference web as a
real, navigable shape — not a sequence of separately-opened flat lists.
Expand/collapse means the engineer controls how much of the thread is
visible at once, never an unmanageable, pre-expanded wall of nodes.

## 10. Trade-offs

- Filtering hides edges, not nodes — a deliberate, disclosed choice for
  a stable, predictable graph shape over a stricter filter.
- No automatic pruning/clustering for dense objects after a few
  expansions — `WP10.0A`'s own already-disclosed first-iteration
  limitation, unchanged.
- No dedicated test proves the "shared reachability preserved" case
  against a specific two-path topology (real sample data does not
  reliably produce one) — the general correctness argument (fixpoint
  reachability) stands in its place, disclosed directly.

## 11. Common Mistakes

- Assuming a bigger, more literally-named contract (`IEvidenceComposer`)
  is automatically the more "correct" composed read — completeness and
  proven precedent matter more than name-matching an ADR's own
  illustrative example.
- Tracking "which expansion added this node" imperatively, rather than
  recomputing reachability from first principles on every collapse —
  the imperative approach is more fragile and harder to prove correct.
- Coupling a shared "rebuild the graph" helper to one caller's own
  breadcrumb semantics (the defect this Work Package found and fixed,
  see the Retrospective) — a shared helper needs an explicit parameter
  for behaviour that genuinely differs by caller, not an implicit
  assumption.

## 12. Future Evolution

- Server-side transitive traversal, if a real, demonstrated need
  emerges — explicitly named by `ADR-0093` itself as the condition that
  would warrant revisiting this decision via a Contract Review Work
  Package.
- Automatic clustering/pruning for dense graphs — disclosed, not yet
  designed.
- Real per-command icons for graph legend rows once `FCR-0069` (real
  authored command icons) lands, for visual consistency with the
  Ribbon.

## 13. Key Takeaways

A graph view over an existing composed read is a presentation decision,
not a data-model decision — and choosing which existing read to reuse
is itself an architectural judgment worth writing down, not an
afterthought.

## Related Documents

- `ADR-0093` — Object Relationship Views Are a Progressively-Expandable
  Node-Link Graph, Superseding `ADR-0065`.
- `WP10.0A Digital Thread & Relationship Visualisation.md`.
- `WP10.4A Implementation Report.md`, `WP10.4A Architecture Review.md`.
- `25-engineering-object-editors.md` — the bidirectional relationship
  read this Work Package reuses.
