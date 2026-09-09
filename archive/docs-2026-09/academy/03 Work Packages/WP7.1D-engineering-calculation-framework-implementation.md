# WP 7.1D — Engineering Calculation Framework — Implementation

## 1. Introduction

`WP 7.1D` is the fourth implementation Work Package of the Engineering
Foundation phase (`v0.7.0`), following `WP 7.1A` (Engineering Data
Model), `WP 7.1B` (Units & Quantities Framework), and `WP 7.1C`
(Materials Framework). It implements `Tempest.Core.Calculations` — a
shared calculation dispatch mechanism, mirroring the Command
Framework's own "one dispatch mechanism, not reinvented per consumer"
precedent — exactly as `WP7.0C Engineering Foundation Contracts.md`
proposed, substantially extended with metadata, assumptions,
constraints, a validation model, an execution context, and material
references this Work Package's own controlling instruction required.
It is also the first Work Package in this phase to include a dedicated
Security Review.

## 2. Purpose

To give every future Mechanical, Structural, Electrical, and Building
Services/HVAC capability a single, canonical calculation abstraction —
so a calculation execution becomes durable engineering evidence, not
merely a transient numerical answer, and no future discipline module
reinvents dispatch, recording, or evidentiary structure for itself.

## 3. Background

`WP 7.0B` identified the Calculation Framework (`FCR-0032`) as
depending, by convention, on Units & Quantities. `WP 7.0C` proposed its
public contract — `ICalculationDefinition<TInput, TResult>`,
`ICalculationEngine`, a four-field `CalculationRecord<TResult>` — and
reserved `ADR-0056` for two open questions: purity enforcement, and
whether `CalculationRecord<TResult>` should integrate with the
Engineering Data Model. This Work Package resolves both, and designs
the additional structure a calculation "representing engineering
evidence, not merely a numerical answer" demands.

## 4. The Problem

A calculation's own final number is only as trustworthy as the
assumptions, constraints, and intermediate steps that produced it — a
bare `TResult` with no attached context is indistinguishable from a
guess. No existing framework provided a way to attach "what was
assumed, what was checked, what steps were taken, what materials were
consulted" to a calculation's own result, and the approved contract's
own illustrative `Calculate(TInput)` signature had no side channel to
record any of it.

## 5. The Design

`ICalculationDefinition<TInput, TResult>` carries fixed
`CalculationMetadata` (Name, Description, Category, Assumptions,
Constraints); `Calculate(TInput, CalculationContext)` receives a fresh,
non-shared recorder to declare intermediate results, constraint checks,
and referenced materials while computing its own result.
`CalculationEngine` — a type-erased registry mirroring the Command
Framework's own dispatch pattern — durably records every execution as
an `EngineeringData.IEngineeringDocument` of `Kind = "CalculationRecord"`,
giving `CalculationRecord<TResult>.Id` stable identity and genuine
revision capability inherited directly from the Data Model, with no new
storage mechanism. See `WP7.1D Implementation Report.md` for the
complete file-by-file account.

## 6. Alternatives Considered

**Leaving `Calculate(TInput)` unchanged, recording assumptions/
constraints/intermediate results through a separate, ambient side
channel** — considered and rejected. An ambient channel would
compromise the purity guarantee this framework's whole concurrency
argument rests on; a fresh, non-shared, explicitly-passed
`CalculationContext` preserves it.

**A hard dependency on `Tempest.Core.Materials` for validated material
references** — considered and rejected in `ADR-0056`; the approved
contract does not require it, and an open string reference (mirroring
`DocumentReference.RelationshipKind`'s own precedent) avoids coupling
every calculation consumer to Materials even when it never touches one.

**A dedicated Roslyn analyzer enforcing `Calculate`'s own purity** —
considered and rejected, exactly as `WP7.0C Required ADR Catalogue.md`
itself anticipated: no demonstrated, real problem with convention-only
enforcement exists yet to justify the infrastructure cost.

## 7. Why This Solution Was Chosen

It satisfies every literal requirement this Work Package's own
controlling instruction named (metadata, assumptions, constraints, a
validation model, intermediate results, material references, revision-
capable identity) through additive extension of the approved contract's
own shown members, reusing proven infrastructure (the Engineering Data
Model, the Command Framework's own type-erased dispatch shape) rather
than inventing new mechanisms for problems already solved elsewhere in
this platform.

## 8. Architectural Principles

Applies `FOUNDATION.md`'s existing principles without modification: one
component, one reason to change; determinism proven by test, not merely
documented. Extends `docs/engineering/Engineering Principles.md` with
seven further principles (17-23) and adds a new Academy concept guide,
`13-calculation-framework.md`, distinguishing this framework from the
Command Framework — the required output `WP7.0C Academy Plan.md`
itself named.

## 9. Files Added

17 new production files under `src/Tempest.Core/Calculations/`; 5 new
sample files under `src/Samples/Tempest.Samples/`; 1 file modified
(`TempestHost.cs`); 8 new test files under `tests/Tempest.Core.Tests/
Calculations/`, `Runtime/`, and `Samples/`; 1 test file modified
(`ClockModuleDiscoveryTests.cs`). Full list: `WP7.1D Implementation
Report.md`.

## 10. Trade-offs

`Calculate` carries no `CancellationToken` (`TD-21`) — a long-running
definition cannot be cancelled once dispatched, accepted since
calculation definitions remain trusted, first-party, in-process code.
`CalculationContext` imposes no bound on recorded data volume, and
recorded intermediate values are not guaranteed to survive a durable
round-trip back to their original CLR type (`TD-22`) — both disclosed
in `WP7.1D Technical Debt Assessment.md` and `WP7.1D Security Review
Report.md`, neither Release Blocking.

## 11. Common Mistakes

A future consumer should **not** perform I/O or mutate shared state
inside `Calculate` "because it only runs once" — nothing prevents this
at compile time, and doing so silently reintroduces the exact
concurrent-execution risk purity exists to avoid. A future consumer
should **not** treat `CalculationInputInvalidException` and a
`Conditional` validation outcome as interchangeable — the former means
no record is created at all; the latter means a real result was
returned with an unmet advisory constraint disclosed alongside it.

## 12. Future Evolution

Candidate `H` (Verification & Validation) remains the one Engineering
Foundation framework not yet implemented, sequenced behind Candidate
`I` (Requirements Engine), unchanged. `FCR-0035` (execution cancellation)
is the one new capability this Work Package's own Security Review
identified. See `WP7.1D Engineering Foundation Impact Assessment.md`
for the complete account.

## 13. Key Takeaways

1. A framework's own evidentiary requirement ("engineering evidence, not
   merely a numerical answer") can justify extending an approved
   contract's own illustrative shape substantially — every extension
   here is additive to what `WP7.0C` showed, never a silent change to
   a member it specified.
2. Reusing a sibling framework's own proven dispatch pattern (the
   Command Framework's type-erased registry) for a structurally similar
   but semantically distinct problem (calculations vs. commands) works
   cleanly, provided the one deciding property — purity — is kept
   explicit and tested, not merely assumed to transfer.
3. A dedicated Security Review, performed for the first time in this
   phase, surfaced two genuine, proportionate findings (`TD-21`,
   `TD-22`) neither the Engineering Review nor the Contract Review had
   named — worth continuing for future Engineering Foundation Work
   Packages, not a one-off exercise.

## Architectural Debt Assessment

`TD-21` (no cancellation reaches into `Calculate`) and `TD-22` (no bound
on `CalculationContext`-recorded data volume or type fidelity) — both
newly disclosed, neither Release Blocking. Full detail: `WP7.1D
Technical Debt Assessment.md`; `WP7.1D Security Review Report.md`.

## Observations

This is the fourth consecutive implementation Work Package of the
Engineering Foundation phase, and the first to depend on all three of
its predecessors simultaneously (Data Model for storage, Units &
Quantities by convention for dimensioned inputs/outputs, an open
reference to Materials) — validated by the same discipline as its
predecessors (clean Debug/Release builds, 1226/1226 tests, both
configurations, clean rebuild), plus a new, dedicated Security Review
this phase had not previously required.

## Related Documents

`docs/releases/v0.7.0/WP7.1D Implementation Report.md` and its seven
companion deliverables; `ADR-0056`; `docs/engineering/Engineering
Principles.md`; `docs/academy/02 Runtime Architecture/
13-calculation-framework.md`; `docs/releases/v0.7.0/WP7.0C Engineering
Foundation Contracts.md`.
