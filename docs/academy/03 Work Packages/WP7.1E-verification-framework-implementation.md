# WP 7.1E — Verification Framework — Implementation

## 1. Introduction

`WP 7.1E` is the fifth and final implementation Work Package of the
Engineering Foundation phase (`v0.7.0`), completing the entire
programme following `WP 7.1A` (Engineering Data Model), `WP 7.1B`
(Units & Quantities Framework), `WP 7.1C` (Materials Framework), and
`WP 7.1D` (Engineering Calculation Framework). It implements
`Tempest.Core.Verification` — a cross-cutting mechanism answering "has
this engineering claim been demonstrated?" — exactly as `WP7.0C
Engineering Foundation Contracts.md` proposed, extended with explicit
criteria, structured evidence, and validated links this Work Package's
own controlling instruction required. It is the second consecutive Work
Package to include a dedicated Security Review.

## 2. Purpose

To give every future discipline module a single, canonical way to
record whether an engineering claim has been demonstrated — distinct
from Audit (who did what) and from a Calculation Record (what was
computed) — so verification, a Systems Engineering and Quality
Assurance concept with real regulatory weight, is never conflated with
either.

## 3. Background

`WP 7.0B` identified the Verification & Validation Framework (`FCR-0033`)
as depending on the Data Model directly, deliberately not on a
not-yet-designed Requirements Engine. `WP 7.0C` proposed its public
contract — `IVerificationService`, `IVerificationRecord`,
`VerificationOutcome` — and reserved `ADR-0057` for two open questions:
Audit orthogonality, and whether `method` should become a closed
vocabulary. This Work Package resolves both, narrows scope explicitly
to Verification alone (Validation and Requirements Management are both
named out of scope by this Work Package's own controlling instruction),
and designs the additional structure "engineering evidence, not merely
[an outcome]" demands — the same evidentiary requirement `WP 7.1D`
already established a precedent for resolving.

## 4. The Problem

A verification outcome with no attached criteria or evidence is
indistinguishable from an unstated opinion. No existing framework
provided a way to record explicitly what was checked and why a claim
was judged demonstrated, and no existing mechanism let a verification
reference the calculation or supporting document that justified it
without inventing a new dependency on every framework it might touch.

## 5. The Design

Each verification is its own `EngineeringData.IEngineeringDocument` of
`Kind = "VerificationRecord"`. `RecordAsync` links the subject document
to it (`"verifiedBy"`) via `IEngineeringDocumentStore.LinkAsync` —
reusing the Data Model's own existing reference mechanism rather than
building a new index, so `GetVerificationHistoryAsync` can read it back
directly via `GetReferencesAsync`, filtered and sorted by
`VerifiedAt`. `VerificationContext` lets a caller record explicit
criteria, evidence, linked documents, linked calculation records
(validated, via `LinkAsync`), and referenced materials (open,
unvalidated strings, mirroring Calculation's own identical precedent).
`GetVerificationHistoryAsync` is permission-gated, mirroring
`Audit.IAuditQuery`'s own established pattern exactly. See `WP7.1E
Implementation Report.md` for the complete file-by-file account.

## 6. Alternatives Considered

**Recording verification as `IAuditRecord` `Detail` fields** —
considered and rejected; see `ADR-0057` Decision 1 and
`14-verification-framework.md`.

**A dedicated `subjectDocumentId`-to-verification index via direct
`IPersistenceStore` access**, mirroring `MaterialCatalog`'s own design —
considered and rejected once `LinkAsync`/`GetReferencesAsync` was found
to satisfy the identical need with zero new dependency at all.

**A hard dependency on Calculations to validate linked calculation
record Ids** — considered and rejected; a `CalculationRecord<TResult>.Id`
is simply a `Guid` `LinkAsync` can validate directly.

## 7. Why This Solution Was Chosen

It satisfies every literal requirement this Work Package's own
controlling instruction named while requiring the smallest dependency
footprint of any Engineering Foundation framework — one hard dependency
(the Data Model), zero new exception types, zero direct Persistence
dependency, zero dependency on Calculations, Units & Quantities, or
Materials.

## 8. Architectural Principles

Applies `FOUNDATION.md`'s existing principles without modification: one
component, one reason to change; fail fast (a non-existent subject or
linked document fails immediately, never silently). Extends
`docs/engineering/Engineering Principles.md` with five further
principles (24-28) — completing the entire Engineering Foundation
programme's own contribution to that document. Adds a new Academy
concept guide, `14-verification-framework.md`, distinguishing this
framework from both Audit and Calculation — the required output
`WP7.0C Academy Plan.md` itself named.

## 9. Files Added

9 new production files under `src/Tempest.Core/Verification/` — the
smallest of the five Engineering Foundation frameworks; 5 new sample
files under `src/Samples/Tempest.Samples/`; 1 file modified
(`TempestHost.cs`); 5 new test files under `tests/Tempest.Core.Tests/
Verification/`, `Runtime/`, and `Samples/`; 1 test file modified
(`ClockModuleDiscoveryTests.cs`). Full list: `WP7.1E Implementation
Report.md`.

## 10. Trade-offs

`RecordAsync`'s own multi-link sequence is not transactional (`TD-23`).
`VerificationContext` imposes no bound on recorded data volume, and
`GetVerificationHistoryAsync`'s own cost scales with a subject's total
reference count (`TD-24`) — both disclosed in `WP7.1E Technical Debt
Assessment.md` and `WP7.1E Security Review Report.md`, neither Release
Blocking.

## 11. Common Mistakes

A future consumer should **not** treat `Conditional` as equivalent to
`Fail` — it means the claim was demonstrated subject to a disclosed
qualification, a materially different statement. A future consumer
should **not** re-describe a supporting calculation's own result in
`Evidence` text rather than linking to its real `CalculationRecord.Id`
via `LinkCalculationRecord` — the link, not the description, is what
makes the calculation independently re-checkable.

## 12. Future Evolution

This completes the Engineering Foundation programme — all five
frameworks (`FCR-0029`–`FCR-0033`) are now Implemented. A future
Requirements Engine (`FCR-0027`) is Verification's own most likely first
real consumer. `FCR-0036` (transactional multi-document operations)
would resolve `TD-23`, once a real need demonstrates it. See `WP7.1E
Engineering Foundation Impact Assessment.md` for the complete account.

## 13. Key Takeaways

1. A cross-cutting framework's own best design decision can be building
   nothing new — Verification needed no new storage, no new index, and
   no new dependency beyond the Data Model, because the Data Model's own
   existing linking mechanism already solved "query by subject."
2. Three structurally similar "record what happened" types (Audit,
   Calculation Record, Verification) can coexist for the same real
   event without duplication, provided each is scoped to the one
   question only it answers.
3. Narrowing scope explicitly (Validation and Requirements Management
   both named out of scope) made this Work Package's own boundary
   easier to hold than any ambiguity would have — a lesson consistent
   with every prior Engineering Foundation Work Package's own
   experience of well-scoped instructions producing close-to-automatic
   scope discipline.

## Architectural Debt Assessment

`TD-23` (non-transactional multi-link sequence) and `TD-24` (no bound on
recorded data volume; history read scales with reference count) — both
newly disclosed, neither Release Blocking. Full detail: `WP7.1E
Technical Debt Assessment.md`.

## Observations

This is the fifth and final implementation Work Package of the
Engineering Foundation phase — validated by the same discipline as its
predecessors (clean Debug/Release builds, 1275/1275 tests, both
configurations, clean rebuild), and the smallest of the five frameworks
by production file count, a genuine, disclosed reflection of how little
new infrastructure Verification actually needed once the other four
frameworks already existed to build on.

## Related Documents

`docs/releases/v0.7.0/WP7.1E Implementation Report.md` and its seven
companion deliverables; `ADR-0057`; `docs/engineering/Engineering
Principles.md`; `docs/academy/02 Runtime Architecture/
14-verification-framework.md`; `docs/releases/v0.7.0/WP7.0C Engineering
Foundation Contracts.md`.
