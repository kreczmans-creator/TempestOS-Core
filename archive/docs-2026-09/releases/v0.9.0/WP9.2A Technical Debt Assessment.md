# WP 9.2A — Engineering Calculations Workspace — Technical Debt Assessment

## Purpose

Reviews the Technical Debt Register for items this Work Package's own
implementation created, extended, or should have created and did not.

## New Items

### `TD-29` — Recalculate Cannot Resume From a Previously-Executed Input

**What.** `CalculationRecord<TResult>`/`CalculationRecordDto<TResult>`
(`Tempest.Core.Calculations`, `WP 7.1D`, unmodified by this Work Package)
retain `Result`/`Assumptions`/`IntermediateResults`/`Validation`/
`ReferencedMaterialIds`/`ExecutedAt`/`ExecutedByPrincipalId` — never the
`TInput` that produced them. `RecalculateCalculationCommand` therefore
still requires a fresh `InputJson` from the caller, identical in shape to
`ExecuteCalculationCommand`; it cannot offer a parameterless "run it
again with the same numbers" gesture.

**How it was found.** Reasoned through directly while designing
`CalculationTemplateRegistry`'s own JSON-marshalled adapter — confirmed
by inspecting `CalculationRecordDto<TResult>`'s own field list, not found
via a failing test.

**Disposition — disclosed, not fixed.** `RecalculateCalculationCommand`'s
own XML documentation states this limitation directly. No data-
correctness issue — every execution, first or repeated, is a fresh,
correctly-recorded, fully evidentiary `CalculationRecord`; only the
convenience of not re-supplying input is unavailable.

**Why this is debt, not merely a limitation.** "Recalculate" as a
concept, and as this Work Package's own controlling instruction names
it, most naturally reads as "run the same calculation again," which a
caller would reasonably expect to need no new input. The underlying
Framework's own stored shape does not yet support that reading.

**Revisit trigger.** A real UI consumer of this Workspace surface (once
one exists — today, every command is invoked directly, in tests, or
would be through a future presentation layer this Work Package's own
scope does not build) surfacing a genuine need to re-run with the last
input unchanged, or a future Work Package extending
`CalculationRecordDto`/`CalculationRecord` (a `Tempest.Core.Calculations`
change, out of this Work Package's own "reuse, do not redesign execution"
scope) to retain it.

**Disposition.** Open.

### `TD-30` — `ICalculationResult`/`IVerificationResult`/`IApprovalGate` Family: Declared Domain Contracts With No Concrete Implementation Anywhere

**What.** Four Domain-level contracts — `ICalculationResult`,
`IVerificationResult` (`Contracts/Calculations.cs`/`Contracts/RequirementsVerification.cs`,
`WP 8.2B`) and `IApprovalGate`, `IApproval`, `IReview`, `IReviewGate`
(`Contracts/Lifecycle.cs`, `WP 8.2B`) — have zero concrete
implementations anywhere in this platform, confirmed by a direct,
whole-repository search performed as part of this Work Package's own
implementation. Two direct consequences, both worked around, neither
fixed: `EvidenceComposer`/`ITraceable.GetEvidenceAsync` honestly resolves
`CalculationResults`/`VerificationResults` empty for every object, always
(not merely for Calculations); and no governed Approval/Review record
(who approved what, when, against what evidence) can exist — "Approval
State" is necessarily read from `IHasLifecycle.Status` alone.

**How it was found.** `WP 9.1A`'s own `RequirementsPropertyFacetProvider`
already worked around half of this gap (`GetEvidenceAsync`'s own
permission-gating, a *different* problem it happened to also carry) by
reading `GetRelationshipsAsync` directly, but never formally registered
the deeper "the Evidence composition itself is structurally empty"
finding in the Technical Debt Register — it is disclosed only in inline
XML documentation (`EvidenceComposer`'s own remarks) and this Work
Package's own controlling instruction to reuse Evidence surfaced it
directly. The `IApprovalGate` family's own total absence was found while
designing this Work Package's own "Calculation Approval State" scope
item and confirming no existing mechanism could supply it.

**Disposition — disclosed, not fixed, and now formally registered.**
Every Calculations Workspace read that would naturally reach for
`GetEvidenceAsync` reads `GetRelationshipsAsync`/`GetIncomingAsync`
directly instead (`CalculationsPropertyFacetProvider`,
`CalculationRecordReader`) — real, correct, evidentiary data, just not
through the Domain's own composed `IEvidence` shape. "Approval State" is
a `LifecycleState` reading (`Approved`/`Released` → "Yes"), not a
governed sign-off.

**Why this is debt, not merely a limitation.** Both contract families
were designed and frozen at the architecture stage (`WP 8.2B`) with a
clear intended shape; two releases and three Engineering-discipline
Workspace integrations later, neither has ever been given a concrete
realisation anywhere. This is a real, growing gap between what the
Domain *declares* and what it *does*, not a single Work Package's own
narrow scope boundary.

**Revisit trigger.** A future Work Package that needs governed,
queryable Approval/Review records (rather than a bare status), or that
needs `IEvidence.CalculationResults`/`VerificationResults` to actually
resolve non-empty for a real cross-discipline traceability view. See
`FCR-0051`/`FCR-0052`.

**Disposition.** Open.

## Existing Items Reviewed for Extension or Change

- **`TD-22`/`TD-24`/`WP 9.0A`'s, `WP 9.0B`'s, and `WP 9.1A`'s own
  equivalent findings** (`ListAllAsync`/list-and-filter reads scale with
  total object count) — the same pattern recurs in
  `CalculationsNodeProvider`/`CalculationRecordReader`/`CalculationsKpiCards`.
  Not separately re-registered; see `WP9.2A Security Review Report.md`.
- **`TD-26`** (Runtime Host module-initialisation timing) — unaffected by
  this Work Package; the same test-level `HasRegistered` wait continues
  to be sufficient, confirmed by four consecutive full clean runs with
  zero flakes on that dimension, including this Work Package's own
  further cross-module dependency edges
  (`EngineeringCalculationsWorkspaceSampleModule` →
  `MechanicalProductStructureSampleModule`/`RequirementsWorkspaceSampleModule`).
- **`TD-27`** (unspecified `ConcurrentDictionary`/`IPersistenceStore`
  iteration order) — this Work Package's own new node-provider ordering
  (`CalculationsNodeProvider`, Templates/Sets/Calculations all sorted by
  Title/Name via explicit `OrderBy`) was written with `TD-27`'s own
  lesson already in mind — no reliance on iteration order anywhere,
  confirmed by four consecutive full clean runs with zero flakes. No
  recurrence.

## Items Considered and Not Raised

- **No Calculation Set "add member"/"remove member" command** — not
  Technical Debt: `ICalculationSet.MemberCalculationIds` is frozen at
  construction by Domain design (`WP 8.2C`), identical to Mechanical's
  own `Configuration.MemberRevisions` (`WP 9.0B`), which also received no
  mutator; a scope decision, not an oversight — see `WP9.2A Engineering
  Review Report.md`.
- **`CalculationTemplateRegistry` rebuilt fresh on every process start,
  never persisted** — not raised: Templates are, by the Calculation
  Framework's own original design (`WP 7.1D`), registered at
  module-initialisation time every run, exactly like `ICommandRegistry`'s
  own descriptors; persisting a registration catalogue would contradict
  that design, not extend it.
- **`CalculationRecordReader`'s JSON parsing is duck-typed against
  `CalculationRecordDto<TResult>`'s own current property names, with no
  compile-time link to it** — considered directly: `CalculationRecordDto<TResult>`
  is `internal` to `Tempest.Core.Calculations` by design (an
  intentionally private serialization shape, `ADR-0056`), so no public
  contract could be referenced instead; a rename of any of its own five
  read properties (`CalculationId`/`ExecutedAt`/`ExecutedByPrincipalId`/
  `Validation`/`Result`/`IntermediateResults`/`ReferencedMaterialIds`)
  would silently break `CalculationRecordReader` without a compiler
  error. **Recorded as a disclosed, accepted coupling, not registered as
  a separate Technical Debt item** — the coupling is to a same-repository,
  same-release internal type whose own shape is exercised directly by
  this Work Package's own test suite (`CalculationsNodeProviderAndFacetsTests`),
  which would itself fail immediately on any such rename, functioning as
  the regression guard a formal contract would otherwise provide.

## Verdict

**Two new items formally registered (`TD-29`, `TD-30`)**, both disclosed
limitations rather than correctness defects — no data-correctness issue
exists anywhere in the shipped implementation. No existing item's own
disposition worsened; `TD-27`'s own lesson was applied proactively, with
zero recurrence.

## Related Documents

`docs/governance/Quality/Technical Debt Register.md`; `WP9.0A Technical
Debt Assessment.md` (`TD-26`); `WP9.0B Technical Debt Assessment.md`
(`TD-27`); `WP9.1A Technical Debt Assessment.md` (`TD-28`); `ADR-0086`;
`ADR-0087`; `WP9.2A Future Capability Assessment.md`.
