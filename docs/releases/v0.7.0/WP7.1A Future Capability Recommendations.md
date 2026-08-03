# WP 7.1A — Engineering Data Model — Future Capability Recommendations

## Purpose

Recommendations for future Work Packages, arising directly from what
this Work Package's own implementation found — mirroring the eight
`WP6.x Future Capability Recommendations.md` documents' own format from
`v0.6.0`.

## Recommendation 1 — Candidate `G` (Materials) Should Represent a Material Specification as an `IEngineeringDocument` of `Kind = "MaterialSpecification"` Directly, Never Its Own Storage

**What.** When Candidate `G` begins, `IMaterialCatalog` should be a
thin, typed index over `IEngineeringDocumentStore`, exactly as
`WP7.0C Engineering Foundation Contracts.md` originally proposed — this
Work Package's own implementation confirms the underlying create/
revise/find operations all work correctly and are ready to build on
directly.

**Why this matters.** Avoids Materials reinventing document storage,
mirroring this project's own repeated "reuse before invention"
discipline.

## Recommendation 2 — Candidate `H` (Verification) Should Use `EngineeringDocumentNotFoundException` Directly, Never a Parallel Exception

**What.** When a verification subject document does not exist,
Candidate `H`'s own implementation should let
`EngineeringDocumentStore`'s own `EngineeringDocumentNotFoundException`
propagate (via whatever lookup it performs against the Data Model),
rather than catching it and re-throwing a new,
Verification-specific exception type.

**Why this matters.** `WP7.0C Engineering Foundation Contracts.md`
already proposed this reuse explicitly; this Work Package's own real
implementation confirms `EngineeringDocumentNotFoundException` carries
enough information (`DocumentId`) for a Verification consumer to handle
it meaningfully without needing its own wrapper type.

## Recommendation 3 — A Future Consumer Needing Structured `Content` Should Layer Its Own Schema Above This Framework, Not Request a Contract Change

**What.** If a future consumer needs `Content` to be more than an
opaque string (a real JSON schema, a specific document format), it
should serialize/deserialize its own structure within the `Content`
string itself (mirroring how `AuditRecorder`/`SettingsProvider` already
serialize their own DTOs into `IPersistenceStore`'s own opaque string
values), rather than asking `IEngineeringDocumentStore` itself to
understand or validate that structure.

**Why not build it now.** No real consumer has a concrete structured-
content need yet (`TD-17`); building speculative schema support ahead
of one would violate this project's own standing discipline.

## Recommendation 4 — `GetReferencesAsync`'s Own Per-Source-Document Collection Design Should Be Reused, Not Redesigned, by Any Future Bidirectional-Reference Need

**What.** If a future consumer needs to find every document that
references a *given* document (the reverse direction —
"who points at me," not "who do I point at"), the natural extension is
a second, symmetric per-target-document collection, populated
alongside the existing per-source one in the same `LinkAsync` call —
not a redesign of the existing forward-reference storage.

**Why not build it now.** No current consumer needs reverse-reference
lookup; `WP7.0C`'s own approved contract does not name it, and building
it speculatively would be exactly the kind of anticipatory complexity
this project's governance discipline discourages.

## Not Recommended

- **Extending `IPersistenceStore` with native query support to serve
  this framework specifically.** `TD-12`/`FCR-0007` already name this as
  a cross-cutting, unscheduled capability — this framework's own
  sequential-key-read design (`ADR-0053`) already avoids needing it for
  its own access patterns; a future Work Package should not couple
  Engineering Data Model's own timeline to that unrelated, unscheduled
  extension.
- **Adding permission-gating to `IEngineeringDocumentStore` itself.**
  The approved contract names no such requirement; if a future
  consumer needs access control over specific documents, that belongs
  at the calling layer (mirroring every `v0.6.0` sample module's own
  permission-check-then-call pattern), not inside this framework.

## Related Documents

`WP7.1A Implementation Report.md`; `ADR-0053`; `docs/releases/v0.7.0/
WP7.0C Engineering Foundation Contracts.md`; `docs/governance/Quality/
Technical Debt Register.md` (`TD-12`, `TD-17`, `TD-18`).
