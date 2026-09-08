# WP 7.1C — Materials Framework — Engineering Review Report

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
| Implement the approved contracts exactly | Satisfied — one changed member (`Properties`' own value type), fully authorised by `ADR-0055`'s own reserved question; every other shown member unchanged |
| Shall not perform engineering calculations | Satisfied — `grep` of `src/Tempest.Core/Materials/` for calculation/formula logic finds only property value encode/decode, never a computation |
| Shall not implement material selection algorithms | Satisfied — no selection, ranking, or comparison logic exists anywhere in this namespace |
| Shall not implement design allowables beyond the approved contracts | Satisfied — no safety factor, margin, or design-methodology-specific value exists anywhere |
| Immutable where practical | Satisfied — `MaterialProperty`/`MaterialPropertyProvenance` are `sealed record`s; `MaterialSpecification`'s own properties are all get-only |
| Support revision history | Satisfied — `ReviseAsync` proven to increment revision number and preserve prior revisions, readable directly through `IEngineeringDocumentStore` |
| Support provenance | Satisfied — every `MaterialProperty` carries a mandatory `MaterialPropertyProvenance`; constructor throws if omitted |
| Support traceability | Satisfied — `UnderlyingDocumentId` proven directly retrievable and linkable through `IEngineeringDocumentStore` |
| Remain discipline-neutral | Satisfied — `Category` is an open string, no fixed taxonomy; no Mechanical/HVAC/Structural/Electrical concept appears anywhere |
| Consume the Engineering Data Model | Satisfied — every material specification is an `IEngineeringDocument` of `Kind = "MaterialSpecification"` |
| Consume the Units & Quantities Framework | Satisfied — every property value is a boxed `Quantity<TDimension>` |
| No design-code-specific assumptions, country-specific standards, material databases, material selection logic, safety factors, calculation behaviour | Satisfied — confirmed by direct inspection; no such concept exists anywhere in this namespace |
| Zero build warnings | Satisfied — 0 warnings, both Debug and Release, clean rebuild |
| Preserve all existing automated tests | Satisfied — all 1119 pre-existing tests still pass, unmodified in behaviour (one, `ClockModuleDiscoveryTests`, updated for an expected, disclosed module-count change, not a regression) |
| Add comprehensive automated test coverage | Satisfied — 55 new tests across unit, serialization, revision, provenance, equality, immutability, traceability, failure, and regression categories |

## Platform Impact Assessment

No existing platform service's own public interface, behaviour, or
test was changed. `TempestHost.cs` gained one new registration line and
one new `using` statement — the smallest possible change satisfying DI
registration, mirroring every prior Work Package's own minimal-diff
registration precedent. `ClockModuleDiscoveryTests.cs`'s module-count
assertion changed from 16 to 17, an expected, disclosed consequence of
adding a seventeenth real sample module.

## Four-Layer / Governance Confirmation (Re-Verified Against Real Code)

**Rule (`ADR-0023`).** Modules depend on Platform Services; Platform
Services depend on DI and, where named, other Platform Services; no
Platform Service depends on a Module.

**Check, against the real, committed source:**

- `MaterialCatalog` depends on `IEngineeringDocumentStore` and
  `IPersistenceStore` (both Platform Services) and `ILogger?` (optional,
  DI) — confirmed by direct inspection of its constructor. No
  dependency on any Module.
- `MaterialsSampleModule` (a Module) depends on `IMaterialCatalog`,
  `ICommandDispatcher`, `ICommandRegistry` — all Platform Services, the
  correct direction.
- **Finding: Satisfied.** `Tempest.Core.Materials` is classified, in
  practice, as a Platform Service-layer namespace, per `ADR-0055`'s own
  confirmation of `ADR-0013`'s default.

**No circular dependency.** `Tempest.Core.Materials` depends on
`Tempest.Core.EngineeringData` and `Tempest.Core.UnitsAndQuantities`;
neither depends back on Materials — confirmed by direct `using`
inspection of both. `Tempest.Core.Materials` has no outgoing dependency
on `Calculations` or `Verification`, neither of which exists yet.

## Findings Requiring Disclosure

1. **A direct `IPersistenceStore` dependency, not anticipated by the
   approved contract's own "indirectly, through
   `IEngineeringDocumentStore`" framing.** `IEngineeringDocumentStore`
   provides no lookup-by-arbitrary-string or enumerate-by-`Kind`
   capability; `MaterialCatalog` needed its own small index to provide
   `FindAsync`/`ListAsync`/duplicate-registration checking. Resolved in
   `ADR-0055`, tracked as a disclosed design decision, not a defect.
2. **`ReviseAsync`'s own concurrency behaviour relies entirely on
   `EngineeringDocumentStore`'s own already-proven revision-number
   atomicity** — `MaterialCatalog.ReviseAsync` itself adds no additional
   locking beyond the registration-time `AsyncKeyedLock`, since two
   concurrent revisions of the same material are full-content replacements,
   not partial merges, and therefore need no additional coordination
   beyond what the underlying document store already guarantees. Verified
   by direct reasoning, not by a dedicated stress test — disclosed
   explicitly, not assumed silently.
3. **No other genuine implementation-phase finding arose.** Every other
   aspect of the approved contract implemented exactly as specified or
   extended purely additively.

## Verdict

**Satisfied — no release-blocking finding.** The Materials Framework is
implemented exactly as approved (one member's own value type changed,
fully authorised by its own reserved ADR), with two disclosed findings,
both recorded here and in `ADR-0055`. Ready to serve as the canonical
representation every future Materials-adjacent capability builds on.

## Related Documents

`WP7.1C Implementation Report.md`; `ADR-0055`; `docs/releases/v0.7.0/
WP7.0C Governance Confirmation.md`; `docs/releases/v0.7.0/WP7.0C
Cross-Framework Dependency Report.md`.
