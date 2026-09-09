# WP 7.1A — Engineering Data Model — Implementation

## 1. Introduction

`WP 7.1A` is the first implementation Work Package of the Engineering
Foundation phase (`v0.7.0`) — the first Work Package since `WP 6.6`
(the final `v0.6.0` implementation Work Package) to ship real production
code, following three consecutive architecture-and-planning Work
Packages (`WP 7.0A`, `WP 7.0B`, `WP 7.0C`) that produced no code at all.
It implements `Tempest.Core.EngineeringData` — the Engineering Data
Model — exactly as `WP7.0C Engineering Foundation Contracts.md`
proposed.

## 2. Purpose

To give every future Engineering Foundation framework and Engineering
Module a shared, canonical way to represent engineering-domain
documents — identity, revision history, and typed relationships — so
none of them invents its own incompatible storage shape, mirroring
`ADR-0041`'s own precedent for Settings and Audit.

## 3. Background

`WP 7.0A` identified the aspiration (a Requirements Engine, a Project
Engine) without a shared data layer to build either on. `WP 7.0B`
identified the Engineering Data Model (`FCR-0029`) as the single
highest-priority Engineering Foundation capability — nearly everything
else in the programme depends on it. `WP 7.0C` proposed its full public
contract. This Work Package is the first to turn that contract into
working, tested code.

## 4. The Problem

An engineering document (a requirement, a material specification, a
project record) needs three things no existing `v0.6.0` Platform
Service provides together: a permanent identity independent of its
content, an append-only history of every past version of that content,
and typed, directed relationships to other documents. `IPersistenceStore`
provides durable key-value storage but no revision or reference concept
at all.

## 5. The Design

`EngineeringDocumentStore` is built directly on `IPersistenceStore`
(`ADR-0053`), using three collections: one for document identity
records, one for revisions (keyed so that revision history can be read
by direct, sequential lookup rather than a whole-collection scan), and
one per source document for its own outgoing references. A per-document
`AsyncKeyedLock` guarantees revision-number atomicity under concurrent
`ReviseAsync` calls, mirroring `SettingsProvider`'s own per-key locking
rationale. Author attribution mirrors `AuditRecorder`'s own pattern
exactly: resolved from `ICurrentPrincipalAccessor`, falling back to an
`"unknown"` sentinel rather than failing the write. See `WP7.1A
Implementation Report.md` for the complete file-by-file account.

## 6. Alternatives Considered

**A new, dedicated storage abstraction purpose-built for revisioned,
linked documents**, rather than building on `IPersistenceStore` —
considered and rejected in `ADR-0053`, since every capability needed
(durable storage, per-key concurrency safety, a loud failure mode)
already exists in `IPersistenceStore`.

**A whole-collection scan for revision history and references**,
mirroring `IAuditQuery`'s own existing pattern — considered and
rejected in favour of a more targeted design (sequential key reads for
revisions; a per-source-document collection for references), avoiding
`TD-12`'s own linear-scan characteristic for this framework's specific
access patterns.

## 7. Why This Solution Was Chosen

It reuses proven, already-tested infrastructure (`IPersistenceStore`,
`AsyncKeyedLock`, `ICurrentPrincipalAccessor`) rather than introducing
anything new, keeping this Work Package's own real net-new surface
limited to the Engineering Data Model's own genuine, novel need: a
document/revision/reference shape no existing service provides.

## 8. Architectural Principles

Applies `FOUNDATION.md`'s existing principles without modification:
one component, one reason to change (Materials, when it exists, will
not reinvent revisioning); state has exactly one owner (a document's
own identity record is never mutated except its `CurrentRevisionNumber`
pointer); every non-obvious decision recorded in writing (`ADR-0053`).
Establishes `docs/engineering/Engineering Principles.md` as a new,
permanent document — six principles, each demonstrated by working code,
not merely asserted.

## 9. Files Added

13 new production files under `src/Tempest.Core/EngineeringData/`; 7 new
sample files under `src/Samples/Tempest.Samples/`; 1 file modified
(`TempestHost.cs`); 6 new test files under `tests/Tempest.Core.Tests/
EngineeringData/`, `Runtime/`, and `Samples/`; 1 test file modified
(`ClockModuleDiscoveryTests.cs`). Full list: `WP7.1A Implementation
Report.md`.

## 10. Trade-offs

`Content` remains an opaque `string` (`TD-17`) — every future consumer
defines its own structure within it, mirroring how `AuditRecorder`/
`SettingsProvider` already serialize their own DTOs into
`IPersistenceStore`'s own opaque string values. `LinkAsync`'s own
high-concurrency behaviour against a single source document is untested
at the depth `ReviseAsync`'s own atomicity is (`TD-18`) — both disclosed
in `WP7.1A Technical Debt Assessment.md`, neither believed to be a
current correctness risk.

## 11. Common Mistakes

A future consumer should **not** assume `Content` enforces any schema —
it does not, by design (Principle 3, "Engineering data is independent
of calculations," and `TD-17`). A future consumer should **not** invent
a parallel exception type for "document not found" — reuse
`EngineeringDocumentNotFoundException` directly, exactly as `WP7.1A
Future Capability Recommendations.md` Recommendation 2 states for the
future Verification framework.

## 12. Future Evolution

Candidates `G` (Materials) and `H` (Verification) depend on this
framework directly and are now unblocked with real, tested behaviour to
build against, not merely an approved contract — see `WP7.1A Engineering
Foundation Impact Assessment.md` for the complete, candidate-by-candidate
account.

## 13. Key Takeaways

1. A Contract Review's proposed signatures are a strong default, not a
   guarantee — this Work Package found and corrected one real
   convention mismatch (`EngineeringDataException`'s class modifier)
   that a five-minute read of existing sibling exception types during
   `WP 7.0C` would have caught.
2. A design decision not specified in the contract (how revision
   history is actually looked up) can still matter architecturally —
   the sequential-key-read design avoids inheriting `TD-12`'s own
   limitation for this framework, a genuine, disclosed improvement
   recorded in `ADR-0053`, not assumed at contract time.
3. A well-scoped Contract Review makes the following implementation
   Work Package's own scope discipline close to automatic — no moment
   during this Work Package created real temptation to add a
   calculation, a unit, or a discipline-specific concept.

## Architectural Debt Assessment

`TD-17` (Content is string-only) and `TD-18` (LinkAsync concurrency
untested at scale) — both newly disclosed, both Open, neither
Release Blocking. Full detail: `WP7.1A Technical Debt Assessment.md`.

## Observations

This is the fourth consecutive Work Package in the Engineering
Foundation phase, and the first to produce code — following three
architecture/planning Work Packages is itself a meaningful transition,
tested directly by this Work Package's own comprehensive validation
(clean Debug/Release builds, 1052/1052 tests, both configurations) map
back to real, working software rather than only documentation.

## Related Documents

`docs/releases/v0.7.0/WP7.1A Implementation Report.md` and its six
companion deliverables; `ADR-0053`; `docs/engineering/Engineering
Principles.md`; `docs/releases/v0.7.0/WP7.0C Engineering Foundation
Contracts.md`.
