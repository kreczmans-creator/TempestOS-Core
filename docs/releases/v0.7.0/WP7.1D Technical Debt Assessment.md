# WP 7.1D — Engineering Calculation Framework — Technical Debt Assessment

## Purpose

Discloses every new debt item or trade-off this Work Package's own
implementation and Security Review introduce, and confirms which
existing debt items (if any) it touches — mirroring `WP7.1A`/`WP7.1B`/
`WP7.1C Technical Debt Assessment.md`'s own format.

## Existing Debt: What Actually Happened

**No existing Technical Debt Register item (`TD-01` through `TD-20`) is
touched by this Work Package.** `Tempest.Core.Calculations` depends only
on `IEngineeringDocumentStore` and `ICurrentPrincipalAccessor`, neither
of which this Work Package modifies. `TD-19`/`FCR-0034` (affine unit
conversion) is referenced, not touched — Calculation's own by-convention
use of `Quantity<TDimension>` simply inherits the same seven-dimension
boundary Units & Quantities already established. `AT-15` (Materials' own
no-permission-gating trade-off) is structurally similar to, but
independent of, this Work Package's own `AT-16` — neither references the
other's own implementation.

## New Debt Disclosed by This Work Package

### TD-21 — No Cancellation Reaches Into `Calculate` Once Execution Has Started

**What.** `ICalculationDefinition.Calculate` carries no
`CancellationToken` — matching the approved contract's own signature,
which had none either.

**Why this is debt, not merely a limitation.** A long-running or
blocking calculation definition cannot be cancelled by
`ExecuteAsync`'s own caller once dispatch has begun.

**Revisit trigger.** A real, demonstrated need for cancelling an
in-flight calculation.

### TD-22 — `CalculationContext` Imposes No Bound on Recorded Data Volume or Type Fidelity

**What.** A definition may record an unbounded number of intermediate
results, constraint checks, or material references in one execution;
separately, `CalculationIntermediateResult.Value` is not guaranteed to
deserialize back to its exact original CLR type if read from durable
storage later.

**Why this is debt, not merely a limitation.** No current consumer
exercises either capability, but a future definition author could
record unbounded data or expect durable round-trip fidelity that is not
actually guaranteed.

**Revisit trigger.** A real, demonstrated need for either bounded
recording or guaranteed durable type fidelity.

## New Accepted Trade-off Disclosed by This Work Package

### AT-16 — No Dependency on Materials for Material-Reference Validation

**What.** `CalculationContext.ReferenceMaterial` accepts any string,
unverified against `Tempest.Core.Materials`.

**Why this is a trade-off, not debt.** The approved contract does not
require a hard Materials dependency for Calculation; an open,
unvalidated reference mirrors `EngineeringData.DocumentReference.
RelationshipKind`'s own established precedent.

**Revisit trigger.** A real, demonstrated need for framework-internal
reference validation.

## Summary Table

| # | Item | Status | Revisit Trigger |
|---|---|---|---|
| TD-21 | No cancellation reaches into `Calculate` | New, Open | A real, demonstrated need for in-flight cancellation |
| TD-22 | No bound on `CalculationContext`-recorded data volume or type fidelity | New, Open | A real, demonstrated need for either capability |
| AT-16 | No dependency on Materials for material-reference validation | New, Accepted Trade-off | A real, demonstrated need for framework-internal validation |

**Total: 2 new debt items disclosed, 1 new accepted trade-off disclosed,
0 existing items worsened.**

## Related Documents

`docs/governance/Quality/Technical Debt Register.md` (updated with
`TD-21`/`TD-22`/`AT-16` in this same Work Package); `ADR-0056`; `WP7.1D
Implementation Report.md`; `WP7.1D Security Review Report.md`.
