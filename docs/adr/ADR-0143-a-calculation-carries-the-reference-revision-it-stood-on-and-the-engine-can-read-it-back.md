# ADR-0143: A Calculation Carries the Reference Revision It Stood On, and the Engine Can Read It Back

## Status

Accepted — First Calculation phase, 2026-09-07.

Amended by `WP 17.9.3` (2026-09-08): verifying a reference record requires the `reference.verify` permission and releasing it requires `reference.release`, checked through `IPermissionEvaluator` (ADR-0044) inside `ReferenceReviewService`; both are held by the session principal's roles by default (`ApplicationPermissions.LocalSession`). The design-freeze review of 2026-09-08 found that any signed-in principal could release any record (hazard H8). The reviewer and the date still come from the session and the clock, never from a caller.

## Context

The platform arrived at its first real numerical calculation with a
complete calculation framework already in place: `ICalculationDefinition`,
`ICalculationEngine`, `CalculationRecord`, `CalculationContext`, and five
worked calculation definitions, every one of them properly dimensioned on
`Quantity<TDimension>`.

None of it was the problem. Three narrower things were.

**1. No calculation could say where its material properties came from.**
Every existing definition took a bare `Quantity<Pressure>` for an allowable
stress. `MaterialSelectionMarginCalculationDefinition` came closest, taking
a `string MaterialId` — which names a record but not a revision, and a
record's properties change. `CalculationContext.ReferenceMaterial` has the
same limit. So a stored calculation could name the material it used and
still not let anybody reproduce it, because the record it named might have
been corrected since.

**2. The engine was write-only.** `ExecuteAsync` recorded every execution
durably and there was no way to read one back as the typed result it was.
A display reader exists at the application layer, but it flattens the
result to a string — enough to show somebody, not enough to reproduce
anything, and in particular not enough to recover a pinned revision.

**3. Nothing stood between the catalogue and the calculation.**
`Calculate` must be pure — no I/O, no lookups — so resolving a material was
necessarily the caller's job, and every caller was free to do it
differently, or not at all.

Separately, the integration phase had left an open finding: reaching
`Released` required writing a reviewer's name, date and verification status
into a record's provenance, all three of them plain fields any caller could
set. A review was a string.

## Decision

**A calculation result carries a `ReferencePin`, not a record id.**
`BracketSectionCheckResult` holds the exact library, record and revision
its material properties were read from. The pin travels *inside* the
result, so it is serialised with the numbers and recovered with them; it is
not recorded alongside them and lost on the way to storage.

**`ICalculationEngine` gains `FindRecordAsync<TResult>`.** The engine
becomes read/write. A stored record can be recovered as the typed result it
was, which is what makes a pinned revision reachable after a restart.

**A stored record records which result type produced it.**
`CalculationRecordDto` gains a nullable `ResultTypeName`, checked on read.
Without it, reading a record as the wrong result type silently succeeds —
`System.Text.Json` ignores unrecognised members and defaults the rest, so a
bracket check read as a bolt shear result comes back well-formed and full
of zeroes. Nullable, so records written before the field existed still
read; they simply cannot be checked.

**One governed service per calculation stands between the catalogue and
the pure definition.** `GovernedBracketCheckService` resolves the material,
refuses a Draft one, refuses a missing or wrongly-dimensioned property, and
builds the pin. It is deliberately specific: one service, one calculation,
with the property names written into it.

**The review act is bound to the signed-in principal.**
`ReferenceReviewService` takes the reviewer from
`ICurrentPrincipalAccessor` and the date from an injected `TimeProvider`.
Neither is a parameter. With nobody signed in, the act is refused rather
than attributed to an unknown principal.

## Consequences

A calculation performed today remains reproducible after the material it
used has been corrected: the pinned revision is recorded, persists, and is
still retrievable from the catalogue. This is demonstrated across three
host restarts and a supersession.

A review can no longer be forged by naming somebody else. It can still be
performed carelessly — the platform cannot tell whether a person actually
opened the document — which is why `ReferenceReviewStatement.SourceConsulted`
is required and stored verbatim, so a later reader can judge the claim.

`Tempest.Core.Calculations` now depends on `Tempest.Core.Materials` and
`Tempest.Core.ReferenceData`. One direction only, and the same dependency
`EngineeringIntelligence` already takes on `Materials`.

### What was deliberately not done

**No generic resolution layer.** A "resolve any property for any
calculation" abstraction would have to decide, for all calculations at
once, what dimension each property must carry, what to do when one is
missing, and which lifecycle states are tolerable. Those have different
right answers per calculation. When a second calculation needs the same
shape, it can be extracted from two real examples rather than one imagined
one.

**No `CalculationBase` hierarchy, no orchestration engine, no second
persistence, no new reporting framework.** The bracket's calculation pack
and verification artefact already had fields for inputs, outputs,
assumptions, execution record ids and a verification result; they had
simply never been given any. `BracketEngineeringRecordService` fills them
in and adds no new structure.

**No automatic approval.** `BracketCheckOutcome` has `MeetsCriteria` and
`DoesNotMeetCriteria` and will not gain an `Approved`. Approving a design
is a governed act by a person; the enumeration reports what the arithmetic
found.
