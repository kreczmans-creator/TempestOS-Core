# WP 7.1B — Units & Quantities Framework — Engineering Review Report

## Purpose

The independent verification pass this Work Package's own controlling
instruction requires before completion — re-checking the implementation
against the approved `WP7.0C` contracts, this Work Package's own
explicit Design Principles, and the "no Platform Service, no DI" claim
specifically, from real, re-run evidence rather than the Implementation
Report's own claims alone.

## Constraint Checklist

| Constraint (from this Work Package's own controlling instruction) | Result |
|---|---|
| Implement the approved contracts exactly | Satisfied — every shown member implemented unchanged; every addition is additive, not a change |
| Shall not implement engineering calculations | Satisfied — `grep` of `src/Tempest.Core/UnitsAndQuantities/` for calculation/formula logic finds only conversion arithmetic (multiplication/division against a fixed factor), never a discipline formula |
| Shall not implement discipline-specific units unless defined by the approved contracts | Satisfied — the seven starting dimensions are cross-cutting (Length, Mass, Duration, Force, Pressure, Area, Volume), none Mechanical/HVAC/Structural/Electrical-specific |
| Immutable | Satisfied — `Unit<TDimension>`/`Quantity<TDimension>` are `readonly record struct`, every property get-only |
| Thread-safe | Satisfied — no shared mutable state exists anywhere in this framework; trivially thread-safe by construction |
| Allocation-conscious where practical | Satisfied — both core types are structs, not classes; no boxing occurs in any operation exercised by this Work Package's own tests |
| Never perform implicit unit conversions | Satisfied — every arithmetic/comparison operator requires the exact same `Unit`, throwing `IncompatibleUnitsException` otherwise, proven by `QuantityTests.Addition_DifferentUnits_ThrowsIncompatibleUnitsException` and its comparison-operator counterpart |
| Never silently discard precision | Satisfied — non-finite values/factors are rejected at construction; `ToString()` with no format uses `double`'s own full round-trippable representation |
| Fail loudly on incompatible dimensions | Satisfied — compile-time rejection for cross-dimension operations (`CompileTimeDimensionSafetyTests.cs`); `IncompatibleUnitsException` for the residual same-dimension-different-unit runtime case |
| No Platform Services, Singleton state, static mutable caches, Dependency Injection, Logging, Hosted Services | Satisfied — confirmed by direct inspection: zero `using` reference to any Platform Service namespace; `TempestHost.cs` untouched; no `ILogger?` parameter anywhere in this framework |
| Zero build warnings | Satisfied — 0 warnings, both Debug and Release, clean rebuild |
| Preserve all existing automated tests | Satisfied — all 1052 pre-existing tests still pass, unmodified |
| Add comprehensive mathematical test coverage | Satisfied — 67 new tests; see `WP7.1B Mathematical Validation Report.md` |

## Platform Impact Assessment

**Zero.** No existing platform service's own public interface,
behaviour, or test was changed. `TempestHost.cs` was not touched — the
first Engineering Foundation Work Package of which this is true.
`ClockModuleDiscoveryTests.cs`'s module count is unchanged (still 16 —
this Work Package adds no sample module, since a pure mathematical
library with no lifecycle needs no living-reference module in the same
sense a Platform Service does; the demonstration instead lives in
`PlatformIntegrationTests.cs`, a direct unit test).

## Four-Layer / Governance Confirmation (Re-Verified Against Real Code)

**Rule (`ADR-0023`).** Modules depend on Platform Services; Platform
Services depend on DI and, where named, other Platform Services; no
Platform Service depends on a Module.

**Check, against the real, committed source:** `ADR-0023`'s own rule
concerns DI-resolved services specifically — `Tempest.Core.
UnitsAndQuantities` registers nothing with the container and is not
itself a Platform Service in the sense that rule addresses. It is,
structurally, a plain value-type library any layer may reference
directly (a Module could construct a `Quantity<Length>` exactly as
freely as a future Platform Service could) — consistent with `WP7.0C
Engineering Foundation Contracts.md`'s own explicit disclosure that this
is "the only Engineering Foundation framework with zero Platform
Service dependency," now additionally confirmed to have zero Platform
Service *consumer* requirement either, at the framework's own layer.

**No circular dependency.** `Tempest.Core.UnitsAndQuantities` has no
outgoing dependency on `Tempest.Core.EngineeringData`, `Materials`,
`Calculations`, or `Verification` — confirmed by direct `using`
inspection. `PlatformIntegrationTests.cs` demonstrates the *inverse*
direction (Engineering Data Model content holding a serialized
`Quantity<TDimension>`), which is `Tempest.Core.Tests` depending on
both, not either production namespace depending on the other.

## Findings Requiring Disclosure

1. **`System.Text.Json` requires an explicit `[JsonConstructor]`
   attribute for correct deserialization of a value type with a
   non-positional, manually-written constructor.** Discovered directly
   during test execution — the first serialization tests written
   without this attribute deserialized to silently-default (zero/null)
   values rather than throwing, because `System.Text.Json`'s default
   constructor-resolution heuristic selected the struct's own implicit
   parameterless constructor over the explicit, validating one. This is
   a genuine implementation-time finding, not anticipated by `WP7.0C`'s
   own contract text (which showed no serialization mechanism at all —
   "Serialization support where defined" was this Work Package's own
   controlling instruction's addition). Full detail: `WP7.1B Lessons
   Learned.md`.
2. **Temperature's affine-conversion gap, found and resolved by
   deferral, not by compromise.** See `ADR-0054`; tracked as `TD-19`/
   `FCR-0034`. Not release-blocking — no current consumer needs
   Temperature.
3. **No other genuine implementation-phase finding arose.** Every other
   aspect of the approved contract's own shown members was implemented
   exactly as specified.

## Verdict

**Satisfied — no release-blocking finding.** The Units & Quantities
Framework is implemented exactly as approved, extended only additively,
with two disclosed findings (the `[JsonConstructor]` requirement and the
Temperature scope boundary), both recorded here and resolved or deferred
appropriately. Ready to serve as the canonical quantity representation
every future Engineering Framework and Engineering Module consumes.

## Related Documents

`WP7.1B Implementation Report.md`; `ADR-0054`; `docs/releases/v0.7.0/
WP7.0C Governance Confirmation.md`; `docs/releases/v0.7.0/WP7.0C
Cross-Framework Dependency Report.md`.
