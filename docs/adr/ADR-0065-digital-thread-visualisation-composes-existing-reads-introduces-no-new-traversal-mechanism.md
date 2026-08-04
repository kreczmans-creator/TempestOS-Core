# ADR-0065: Digital Thread Visualisation Composes Existing Reads, Introducing No New Traversal Mechanism

## Status

Accepted — `v0.8.0` "Engineering Workspace", `WP 8.0A` (Engineering
Workspace Architecture), 2026-07-30. Resolves how the Workspace's own
Digital Thread panel obtains the data it presents.

## Context

`WP7.2B Digital Thread Architecture.md` argued, at the architecture
stage, that a digital thread requires no dedicated traversal mechanism
— only composed reads over relationships and revision history that
`IEngineeringDocumentStore` already provides. `WP 7.3A` proved this
claim in running code: `IRequirementsService.GetEvidenceAsync` composes
`IVerificationService.GetVerificationHistoryAsync` with
`IEngineeringDocumentStore.GetReferencesAsync` into one read, with zero
new index, cache, or query mechanism introduced. The Workspace's own
Digital Thread panel (`WP8.0A Workspace Architecture Document.md` §9)
must decide whether to reuse this proof directly or build its own,
richer traversal/query capability (a multi-hop graph query, a
server-side aggregation service) to power a more elaborate
visualisation.

## Decision

**The Workspace's own Digital Thread panel is a View over
`GetEvidenceAsync` (and each sibling framework's own equivalent composed
read, where one exists) — it introduces no new traversal, query, or
aggregation platform capability.** The panel presents exactly what the
composed read already returns: a requirement's own verification history
plus its own linked references, as a navigable, flat list, each entry
carrying its own relationship kind and a "jump to" action opening the
target object in a new Document Area tab
(`WP8.0A UI Architecture.md` §4).

A full, interactive, multi-hop graph visualisation (rendering a
requirement's own transitive dependency chain, not only its own direct
relationships) is explicitly not designed here — it would require a new
traversal capability `IEngineeringDocumentStore` does not provide today
(only direct `GetReferencesAsync`, not transitive closure), and no real,
demonstrated need for one exists yet.

## Consequences

**Positive:**

- Zero new platform capability required before the Digital Thread panel
  can be built — implementation can proceed directly against
  `GetEvidenceAsync` as it exists today, with no Contract Review needed
  for this specific View.
- Directly validates `WP7.2B Digital Thread Architecture.md`'s own
  central claim a second time, at the presentation layer, reinforcing
  that the composed-read pattern generalises beyond the service layer
  it was first proven in.

**Negative:**

- A requirement with a deep chain of transitive relationships (a
  requirement that depends on a requirement that depends on another) is
  only ever one hop visible in the Digital Thread panel — the engineer
  must manually "jump to" and re-open the Digital Thread panel for each
  successive object to trace a longer chain. Disclosed as a genuine,
  accepted limitation, not a defect: no real requirement set with this
  depth of chaining exists yet to demonstrate the limitation matters in
  practice.

## Alternatives Considered

**Build a dedicated multi-hop traversal/query platform service ahead of
this Work Package's own scope** — considered and rejected. This would
require a Contract Review of its own, be architecture beyond what
`WP 8.0A`'s own controlling instruction asks for ("no implementation"
applies equally to "no new platform capability designed speculatively"),
and has no real, demonstrated need behind it yet — the identical
reasoning `WP7.2B Digital Thread Architecture.md` and `WP 7.3A` already
applied to reject the same idea at the service layer.

**A client-side, Workspace-only cache that walks multiple
`GetReferencesAsync` calls to build a deeper graph in memory** —
considered and rejected. This would be new traversal logic living in
presentation code rather than a platform capability, violating
`WP8.0A UI Architecture.md` §3.3's own "no View introduces its own
query, cache, or index" principle — if a multi-hop capability is ever
needed, it belongs in `Tempest.Core`, not duplicated ad hoc in
`Tempest.App`.

## Related Documents

`WP7.2B Digital Thread Architecture.md`; `WP7.3A Digital Thread
Assessment.md`; `WP8.0A Workspace Architecture Document.md` §9;
`WP8.0A Object Relationship Diagrams.md` §3.
