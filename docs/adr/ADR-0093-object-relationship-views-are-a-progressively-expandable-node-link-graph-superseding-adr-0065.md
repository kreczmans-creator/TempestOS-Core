# ADR-0093: Object Relationship Views Are a Progressively-Expandable Node-Link Graph, Superseding ADR-0065

## Status

Accepted — `v0.10.0` "User Experience & Desktop Application", `WP 10.0A`
(User Experience Architecture), 2026-08-07. **Supersedes `ADR-0065`.**
The second supersession this Work Package records, alongside
`ADR-0092`.

## Context

`ADR-0065` (`WP 8.0A`, 2026-07-30) decided that the Digital Thread
panel would present `GetEvidenceAsync` (and each sibling framework's
own equivalent composed read) as a flat, navigable list — explicitly
rejecting a "full, interactive, multi-hop graph visualisation" because
no real, demonstrated need existed yet and because building one would
require a new transitive-traversal platform capability
`IEngineeringDocumentStore` does not provide (only direct
`GetReferencesAsync`, not transitive closure).

`WP 10.0A`'s own controlling instruction now names two explicit,
required topics: "Digital Thread visualisation" and "Object
relationship views" — listed as two distinct items, not one, the first
time this project's own governing instructions have drawn that
distinction. Both are now real, demonstrated Product Owner
requirements, satisfying the evidentiary bar `ADR-0065` itself set for
revisiting its own list-only decision. The six real Engineering
Disciplines that did not exist when `ADR-0065` was written
(`v0.9.0`, `WP 9.0A`–`WP 9.5A`) also now provide substantially richer
relationship data to visualise than the pre-implementation
architecture `ADR-0065` reasoned about in the abstract — real Product/
Assembly/Part structures, real Requirement traceability, real
Calculation dependency chains, real Verification evidence, real
Document references, and real Manufacturing Routing/Operation
sequences, every one of them already reachable through the identical
`GetRelationshipsAsync`/`GetEvidenceAsync`-shaped reads `ADR-0065`
named.

Critically, `ADR-0065`'s own rejected alternative — "a dedicated
multi-hop traversal/query platform service" — remains rejected here,
for the identical reason: no `Tempest.Core` contract change is in this
Work Package's own scope, and no real need for a *server-side*
transitive query has been demonstrated (only a *visual* one — being
able to see more than one hop without a manual "jump to" round-trip).
These are different needs, and this ADR is careful to satisfy only the
second.

## Decision

**Object Relationship Views (the evolution of the Digital Thread
panel) render exactly the same one-hop composed reads `ADR-0065`
already established (`GetEvidenceAsync` and each sibling framework's
own equivalent) as a node-link graph, built up through client-side,
on-demand progressive expansion — never a precomputed or cached
transitive traversal.** The selected object is the graph's own initial
centre node; each of its direct relationships (exactly what the
existing composed read already returns) becomes an edge to a
one-hop neighbour node. Expanding any neighbour node issues a fresh
call to that object's own `GetEvidenceAsync`-equivalent, adding its own
direct relationships to the graph in turn — the same "jump to" round
trip `ADR-0065`'s own flat list already required, made visual and
inline (no navigation away from the current View) instead of replacing
the panel's own contents with the next object's.

This is a **rendering-layer decision only.** It introduces:

- **Zero new `Tempest.Core` contract.** No transitive-closure method is
  added to `IEngineeringDocumentStore` or any framework service; every
  read this View performs already exists, unchanged, exactly as
  `ADR-0065`'s own Decision specified.
- **Zero new client-side cache, index, or persisted graph state.**
  Each expansion is a live read, discarded when the View closes,
  mirroring `WP8.0A UI Architecture.md` §3.3's own "no View introduces
  its own query, cache, or index" principle exactly as `ADR-0065`
  itself already applied it — this ADR extends that same principle to
  a graph-shaped View rather than relaxing it.
- **A genuinely new visual capability**: an engineer can now see two,
  three, or more hops of a real relationship chain in one screen,
  resolving the concrete limitation `ADR-0065`'s own Consequences
  §Negative named ("only ever one hop visible... must manually 'jump
  to' and re-open... for each successive object") — without the server-
  side capability `ADR-0065` correctly declined to build speculatively.

Full presentation detail: `WP10.0A Digital Thread & Relationship
Visualisation.md`.

## Consequences

**Positive:**

- Directly resolves `ADR-0065`'s own disclosed limitation using
  exactly the evidentiary path that ADR itself required — a real,
  demonstrated need (this Work Package's own controlling instruction),
  not a speculative build-ahead.
- Preserves `ADR-0065`'s own core architectural finding in full: no new
  traversal, query, or aggregation platform capability is needed to
  satisfy a genuinely richer relationship-visualisation requirement —
  the second time this exact claim has been re-validated at a new
  layer (first the service layer, `WP 7.3A`; then the flat-list
  presentation layer, `ADR-0065`; now the graph presentation layer,
  this decision).
- Directly complements `ADR-0092`: a node-link graph is meaningfully
  more legible inside a graphical desktop application's own pixel-precise
  canvas than it would be forced to be inside a terminal's own
  character grid — the two decisions this Work Package makes reinforce
  each other rather than being independent choices.

**Negative:**

- Expanding a densely-connected object (many direct relationships)
  produces a genuinely large graph with no automatic pruning,
  clustering, or layout optimisation designed here — an accepted,
  disclosed first-iteration limitation, not a defect; the engineer
  remains free to collapse nodes back down.
- Progressive expansion means the *engineer*, not the graph itself,
  decides how many hops to reveal — a true "show me everything three
  hops out" query still requires the same number of manual expansion
  actions a flat list's own repeated "jump to" would have required,
  just performed inline rather than via navigation. This ADR
  deliberately does not claim to have eliminated that cost, only to
  have made it visual and non-navigating.
- Should a real need for genuine server-side transitive traversal
  (e.g., "highlight every requirement transitively affected by this
  material change," computed in one platform-service call rather than
  N client-driven expansions) be demonstrated later, this decision
  would need revisiting alongside a Contract Review Work Package —
  named here as the next reversal condition, mirroring how `ADR-0065`
  itself named one.

## Alternatives Considered

**A precomputed, cached, multi-hop graph, built once per object and
held in Workspace state** — considered and rejected. This would
duplicate `IEngineeringDocumentStore`'s own durable relationship data
in presentation-layer memory, reintroducing the exact staleness risk
`WP8.0A UI Architecture.md` §5 explicitly designed the Workspace to
avoid ("the Workspace introduces no staleness risk, since it never
holds its own copy of engineering data across a session boundary").

**A new `Tempest.Core` transitive-traversal platform capability,
built now to power the graph directly** — considered and rejected, for
the identical reason `ADR-0065` already gave: no real, demonstrated
need for a *server-side* transitive query exists, only a *visual* one,
and this Work Package's own controlling instruction explicitly
forbids contract changes regardless.

**Leaving `ADR-0065` as the final word and treating "Object
relationship views" as merely a renamed reference to the same flat
list** — considered and rejected. The controlling instruction names
Digital Thread visualisation and Object relationship views as two
distinct required topics; treating them as one, unchanged capability
would not genuinely satisfy the instruction, only reword its existing
answer.

## Related Documents

`ADR-0065` (superseded by this decision); `ADR-0092` (the graphical
desktop paradigm decision this graph rendering depends on for
legibility); `WP10.0A Digital Thread & Relationship Visualisation.md`;
`WP7.2B Digital Thread Architecture.md`; `WP7.3A Digital Thread
Assessment.md`; `WP8.0A Workspace Architecture Document.md` §9;
`WP8.0A Object Relationship Diagrams.md` §3.
