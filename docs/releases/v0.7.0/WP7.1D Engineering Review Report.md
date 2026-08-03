# WP 7.1D — Engineering Calculation Framework — Engineering Review Report

## Purpose

The independent verification pass this Work Package's own controlling
instruction requires before completion — re-checking the implementation
against the approved `WP7.0C` contracts, this Work Package's own
explicit Design Principles, and the four-layer dependency rule, from
real, re-run evidence rather than the Implementation Report's own claims
alone.

## Constraint Checklist

| Constraint (from this Work Package's own controlling instruction) | Result |
|---|---|
| Implement the approved contracts exactly | Satisfied — one changed member (`Calculate`'s own signature), fully authorised by `ADR-0056`'s own reserved question; every other shown member unchanged |
| Provide calculation infrastructure only | Satisfied — `grep` of `src/Tempest.Core/Calculations/` for Mechanical/Structural/HVAC/Electrical/Manufacturing logic finds none; the only calculation defined anywhere in this Work Package's own scope (`DoubleLengthCalculationDefinition`) is deliberately trivial and non-domain-specific |
| Remain deterministic | Satisfied — `ExecuteAsync_SameInputMultipleTimes_AlwaysProducesTheSameResult` (5 repetitions, identical result every time) |
| Remain reproducible | Satisfied — same evidence; every execution's own inputs/outputs/assumptions are fixed and durably recorded |
| Support traceability | Satisfied — `CalculationRecord.Id` proven directly usable with `IEngineeringDocumentStore` |
| Support provenance | Satisfied — `CalculationId`/`ExecutedAt`/`ExecutedByPrincipalId`/`Assumptions`/`ReferencedMaterialIds` together constitute complete, self-contained provenance |
| Support explicit assumptions | Satisfied — `CalculationMetadata.Assumptions` copied into every record; proven to survive unchanged |
| Support explicit constraints | Satisfied — `CalculationConstraint`/`CalculationConstraintCheck`/`CalculationValidationResult` |
| Separate definition from execution | Satisfied — `ICalculationDefinition` (fixed, registered once) vs. `CalculationContext`/`CalculationRecord` (fresh, per execution) |
| Separate inputs from outputs | Satisfied — `TInput` is never stored in `CalculationRecord`; only `TResult` and what the definition explicitly chose to record via `CalculationContext` |
| Separate engineering logic from presentation | Satisfied — no formatting, UI, or report-rendering logic exists anywhere in this namespace |
| No Mechanical/Structural/HVAC/Electrical/Manufacturing mathematics, design-code logic, safety-factor policy, UI concerns, report formatting | Satisfied — confirmed by direct inspection |
| Zero build warnings | Satisfied — 0 warnings, both Debug and Release, clean rebuild |
| Preserve all existing automated tests | Satisfied — all 1174 pre-existing tests still pass, unmodified in behaviour (one, `ClockModuleDiscoveryTests`, updated for an expected, disclosed module-count change) |
| Add comprehensive automated test coverage | Satisfied — 52 new tests across unit, execution, validation, serialization, traceability, assumption, reproducibility, equality, failure, concurrency categories |
| Complete a documented Security Review | Satisfied — see `WP7.1D Security Review Report.md` |

## Platform Impact Assessment

No existing platform service's own public interface, behaviour, or
test was changed. `TempestHost.cs` gained one new registration line and
one new `using` statement. `ClockModuleDiscoveryTests.cs`'s module-count
assertion changed from 17 to 18, an expected, disclosed consequence of
adding an eighteenth real sample module.

## Four-Layer / Governance Confirmation (Re-Verified Against Real Code)

**Rule (`ADR-0023`).** Modules depend on Platform Services; Platform
Services depend on DI and, where named, other Platform Services; no
Platform Service depends on a Module.

**Check, against the real, committed source:**

- `CalculationEngine` depends on `IEngineeringDocumentStore` and
  `ICurrentPrincipalAccessor` (both Platform Services) and `ILogger?`
  (optional, DI) — confirmed by direct inspection of its constructor.
  No dependency on any Module.
- `CalculationSampleModule` (a Module) depends on `ICalculationEngine`,
  `ICommandDispatcher`, `ICommandRegistry` — all Platform Services, the
  correct direction.
- **Finding: Satisfied.** `Tempest.Core.Calculations` is classified, in
  practice, as a Platform Service-layer namespace, consistent with
  `WP7.0C Governance Confirmation.md`'s own "as proposed" default.

**No circular dependency.** `Tempest.Core.Calculations` depends on
`Tempest.Core.EngineeringData` and `Tempest.Core.Identity`; neither
depends back on Calculations. `Tempest.Core.Calculations` has no
outgoing dependency on `Tempest.Core.Materials` (`ADR-0056` Decision 6)
or `Tempest.Core.UnitsAndQuantities` (a by-convention relationship only,
confirmed by direct inspection — no `using` reference to
`Tempest.Core.UnitsAndQuantities` exists anywhere in
`src/Tempest.Core/Calculations/`). `Verification`, the one remaining
Engineering Foundation framework, does not exist yet, so no forward
reference to it exists either.

## Findings Requiring Disclosure

1. **`Calculate`'s own signature required a genuine, disclosed change**
   to accommodate `CalculationContext` — resolved and documented in
   `ADR-0056`, not merely absorbed silently.
2. **Two Security Review findings not anticipated by prior planning**
   (`TD-21`, `TD-22`) — see `WP7.1D Security Review Report.md` for the
   complete account; both proportionate, neither Release Blocking.
3. **No other genuine implementation-phase finding arose.** Every other
   aspect of the approved contract's own shown members was implemented
   exactly as specified.

## Verdict

**Satisfied — no release-blocking finding.** The Engineering Calculation
Framework is implemented exactly as approved (one member's own
signature changed, fully authorised by its own reserved ADR), with a
dedicated Security Review producing two disclosed, proportionate
findings, both recorded here, in `ADR-0056`, and in the Technical Debt
Register. Ready to serve as the canonical calculation abstraction every
future Engineering Module builds on.

## Related Documents

`WP7.1D Implementation Report.md`; `WP7.1D Security Review Report.md`;
`ADR-0056`; `docs/releases/v0.7.0/WP7.0C Governance Confirmation.md`;
`docs/releases/v0.7.0/WP7.0C Cross-Framework Dependency Report.md`.
