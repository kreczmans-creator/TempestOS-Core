# ADR-0089: "Execute" and "Record Result" Are One Command (`RecordVerificationResultCommand`) Over `IVerificationService.RecordAsync` — No Adapter Is Needed

## Status

Accepted — `v0.9.0` "Mechanical Foundation", `WP 9.3A` (Verification Management Workspace), 2026-08-07.

## Context

`WP 9.3A`'s own controlling instruction names both "Execute" and "Record Result" as distinct Verification Management capabilities, alongside "Attach Evidence" as a third. `WP 9.2A`'s own `ADR-0086` faced a structurally similar-looking naming choice for Calculations and answered it with `CalculationTemplateRegistry` — a genuine, new Workspace-layer adapter, because `ICalculationEngine.ExecuteAsync<TInput,TResult>` is generic per Template and needed a type-erasure bridge to reach one non-generic Workspace command.

`IVerificationService` (`Tempest.Core.Verification`, `WP 7.1E`, confirmed by direct read) has no equivalent problem: it declares exactly one mutating method, `RecordAsync(subjectDocumentId, outcome, method, VerificationContext, ...)` — a caller-driven assertion (a human or an external process states an outcome, having already gathered criteria and evidence), never a framework-computed dispatch. There is no generic `TInput`/`TResult` per Template to erase, and no second action to bridge to a first. `VerificationContext` itself already accumulates criteria, evidence, and links before `RecordAsync` is called — there is no "attach evidence afterward" capability anywhere in the Framework to wrap either.

## Decision

**"Execute," "Record Result," and "Attach Evidence" are realised as one command, `RecordVerificationResultCommand`/`RecordVerificationResultCommandHandler`, wrapping `IVerificationService.RecordAsync` directly — never a second, separate "Execute" mechanism, and never a `CalculationTemplateRegistry`-equivalent adapter.** The command accepts `Outcome`/`Method` plus `IReadOnlyList<VerificationCriterion>`/`IReadOnlyList<VerificationEvidenceEntry>`/linked-document/linked-calculation-record/referenced-material lists — the exact shape `VerificationContext` itself exposes, reused directly (`VerificationCriterion`/`VerificationEvidenceEntry` are already public `Tempest.Core.Verification` records; the command never redeclares them). The handler populates a fresh `VerificationContext` from these lists and calls `RecordAsync` once.

`Command Palette` discoverability for the three named verbs is satisfied by one `CommandDescriptor` (`verification.record-result`), whose own `description` states plainly that it realises all three — never three separate, misleadingly-independent-looking descriptors over the one real action.

## Consequences

**Positive:**

- Zero new Workspace-layer adapter type — the second consecutive real-discipline Work Package (after `WP 9.2A`'s own Calculations, which needed exactly one adapter) to introduce zero or minimal connecting machinery, here needing none at all.
- `VerificationContext`'s own existing validation (`ArgumentException`/`ArgumentNullException` on malformed input) is reused unmodified — the command never duplicates or reimplements it.
- A caller with pre-gathered evidence records a complete, evidentiary result in one dispatch, exactly matching how a real verification report is actually produced (criteria and evidence exist before the outcome is asserted, not after).

**Negative:**

- A caller cannot "execute" a Verification Activity without simultaneously supplying an outcome — there is no intermediate "in progress, no result yet" command distinct from the Activity's own `LifecycleState.InReview` transition (`SetVerificationActivityStatusCommand`, `ADR-0090`). Judged acceptable: `InReview` already represents "activity started, no result yet" honestly; a caller wanting to signal "work has begun" uses that transition, and calls `RecordVerificationResultCommand` only once a real result exists — mirrors how a real verification activity is actually tracked (started, then concluded), never conflating the two.
- The Command Palette's own single "Record Verification Result" entry does not literally say "Execute" anywhere a user browsing by that exact word would find it by name alone — mitigated by the descriptor's own `description` field naming all three verbs explicitly.

## Alternatives Considered

**A `CalculationTemplateRegistry`-equivalent `VerificationMethodRegistry`, mapping each named Method (Inspection/Analysis/Test/Demonstration) to its own Workspace-layer adapter.** Considered and rejected; `IVerificationService.RecordAsync`'s own `method` parameter is already a plain, open string (never a compile-time generic type per method), so there is no type-erasure problem to solve — a registry here would be a structure imitating `ADR-0086`'s own shape without its own underlying justification (a real generic-dispatch problem).

**Two separate commands, `ExecuteVerificationActivityCommand` (a status-only transition) and a distinct `RecordVerificationResultCommand`.** Considered; the first would be entirely redundant with `SetVerificationActivityStatusCommand`'s own existing `InReview` transition — inventing a second mechanism for the identical effect was judged to be exactly the kind of duplicate machinery this Work Package's own "no duplicate framework" instruction forbids.

## Related Documents

`ADR-0086`; `ADR-0090`; `WP9.3A Implementation Report.md`; `WP9.3A Technical Debt Assessment.md`; `src/Tempest.App/Workspace/Verification/RecordVerificationResultCommand.cs`; `src/Tempest.App/Workspace/Calculations/CalculationTemplateRegistry.cs`.
