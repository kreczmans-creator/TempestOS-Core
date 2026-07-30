# WP 7.1B — Units & Quantities Framework — Implementation Report

## Status

Complete. The second implementation Work Package of the Engineering
Foundation phase (`v0.7.0`) — production code, tests, and one ADR were
produced, following `WP 7.0C`'s own approved contracts.

## Scope Delivered

`Tempest.Core.UnitsAndQuantities` implemented exactly as `WP7.0C
Engineering Foundation Contracts.md` proposed, extended (not changed)
with members the contract's own illustrative code block did not show
but this Work Package's own controlling instruction required:

- `IDimension` — the empty marker interface, unchanged from the
  approved proposal.
- `Unit<TDimension>` — `Symbol`, `ToBaseUnitFactor`, exactly as
  proposed, with constructor validation added (non-null/non-whitespace
  symbol; positive, finite factor).
- `Quantity<TDimension>` — `Value`, `Unit`, `ConvertTo`, exactly as
  proposed, extended with: arithmetic operators (`+`, `-`, scalar `*`,
  scalar `/`), comparison (`IComparable<Quantity<TDimension>>`, `<`,
  `>`, `<=`, `>=`), formatting (`ToString`, `IFormattable`), and parsing
  (`Parse`/`TryParse`) — all named explicitly in this Work Package's own
  Implementation Scope, none present in `WP7.0C`'s own illustrative code
  block, none changing a member the contract did show.
- `IUnitConverter`/`UnitConverter` — implemented, resolving `ADR-0054`'s
  own reserved "is it worth building" question in favour of building it.
- `IncompatibleUnitsException` — implemented exactly as proposed
  (`sealed class`, extends `Exception` directly — this framework's
  contract itself showed no abstract base, unlike every other
  Engineering Foundation framework).
- Seven dimension marker types (`Length`, `Mass`, `Duration`, `Force`,
  `Pressure`, `Area`, `Volume`) and seven matching static unit
  catalogues — a starting set, not present in `WP7.0C`'s own contract
  text at all, since choosing concrete dimensions and units was this
  Work Package's own implementation-scope discretion.
- **No DI registration.** `TempestHost.cs` is untouched by this Work
  Package — confirmed by this Work Package's own diff.

**Not implemented, per this Work Package's own explicit scope
boundary:** engineering calculations, discipline-specific units beyond
this starting catalogue, and Temperature (an affine dimension — see
Deviations, below).

## Deviations From the Approved Contract

**None to the contract's own shown members.** Every member
`WP7.0C Engineering Foundation Contracts.md`'s own code block showed —
`Unit<TDimension>.Symbol`/`ToBaseUnitFactor`, `Quantity<TDimension>.
Value`/`Unit`/`ConvertTo`, `IUnitConverter.Convert`,
`IncompatibleUnitsException` — is implemented exactly as shown. Every
addition (arithmetic, comparison, formatting, parsing, the seven
dimensions and catalogues) is exactly that — an addition, not a change
to anything the contract specified.

**One disclosed scope boundary, not a defect:** Temperature is absent
from the starting catalogue. `Unit<TDimension>.ToBaseUnitFactor`
supports only a single multiplicative factor; Celsius↔Fahrenheit
requires an affine (scale-and-offset) conversion this shape cannot
express without a change to every existing dimension's own conversion
arithmetic. Resolved in `ADR-0054`'s own "Temperature Deliberately
Deferred" section; tracked as `TD-19`/`FCR-0034`.

## Platform Integration

**No Platform Service is consumed, and none depends on this framework.**
This is the one Engineering Foundation framework `WP7.0C Platform
Integration Matrix.md` itself predicted would have zero Platform
Service dependency — confirmed by implementation: no `using` reference
anywhere in `src/Tempest.Core/UnitsAndQuantities/` to `Tempest.Core.
Identity`, `Tempest.Core.Persistence`, `Tempest.Core.Logging`, or any
other Platform Service namespace. The one integration this Work
Package's own controlling instruction required demonstrating —
`Quantity<TDimension>` as `IEngineeringDocumentStore` content — is
proven by `PlatformIntegrationTests.cs`: a `Quantity<Mass>` is
JSON-serialized into a document's `Content`, revised, and read back
unchanged. No Platform Service integration beyond this is required,
since nothing in this framework needs Identity, Persistence, Audit, or
any other Platform Service to function.

## Production Code

| File | Purpose |
|---|---|
| `IDimension.cs` | The compile-time dimension marker interface |
| `Unit.cs` | `Unit<TDimension>` — a named unit, with validated construction |
| `Quantity.cs` | `Quantity<TDimension>` — value + unit, conversion, arithmetic, comparison, formatting, parsing |
| `IncompatibleUnitsException.cs` | The one exception type this framework defines |
| `IUnitConverter.cs`, `UnitConverter.cs` | The untyped-caller convenience wrapper (`ADR-0054`) |
| `Length.cs`/`LengthUnits.cs`, `Mass.cs`/`MassUnits.cs`, `Duration.cs`/`DurationUnits.cs`, `Force.cs`/`ForceUnits.cs`, `Pressure.cs`/`PressureUnits.cs`, `Area.cs`/`AreaUnits.cs`, `Volume.cs`/`VolumeUnits.cs` | Seven dimension markers and their starting unit catalogues |

20 new production files; 0 modified.

## Testing

67 new tests, across:

- **Unit** — `UnitTests.cs` (construction/validation), `QuantityTests.cs`
  (construction, conversion, arithmetic, comparison, equality,
  formatting).
- **Conversion/Dimensional Analysis** — `DimensionCatalogueTests.cs`
  (every catalogued unit round-trips through its own base unit, for all
  seven dimensions).
- **Compile-Time Rejection** — `CompileTimeDimensionSafetyTests.cs`
  (documents the exact `CS1503`/`CS0019` errors reproduced by attempting
  cross-dimension conversion/arithmetic; verified by direct inspection,
  disclosed as `AT-14`).
- **Parsing** — `QuantityParsingTests.cs` (`TryParse`/`Parse`,
  recognised and unrecognised input).
- **Serialization** — `QuantitySerializationTests.cs` (`System.Text.Json`
  round-trip for both `Quantity<TDimension>` and `Unit<TDimension>`,
  requiring a `[JsonConstructor]` attribute discovered necessary during
  implementation — see Lessons Learned).
- **Failure/Edge Case** — non-finite values and factors, zero, negative
  values, division by zero, extremely small (`1e-9`) and extremely large
  (`1e12`) values, all covered in `QuantityTests.cs`'s own `[Theory]`
  cases.
- **Platform Integration** — `PlatformIntegrationTests.cs`.
- **Exception** — `ExceptionTests.cs`.
- **UnitConverter** — `UnitConverterTests.cs`.

**1119/1119 tests passing** (1052 baseline + 67 new), 0 failures, both
Debug and Release, from a fully clean (`bin`/`obj` removed) rebuild.

## Validation Performed

- Clean Debug build: 0 warnings, 0 errors.
- Clean Release build: 0 warnings, 0 errors.
- Full automated test suite: 1119/1119, both configurations.
- Dependency validation: `Tempest.Core.UnitsAndQuantities` depends on
  nothing beyond the .NET base class library (`System`, `System.
  Globalization`, `System.Text.Json.Serialization`) — no circular
  dependency, no dependency on any Platform Service or Module.
- No layering violation: a pure value-type library requires no
  four-layer classification of its own — `ADR-0023`'s own rule concerns
  DI-resolved services, and this framework registers none.
- Mathematical consistency review: every catalogued unit's own
  conversion factor independently checked against its own published SI/
  Imperial definition (see `WP7.1B Mathematical Validation Report.md`).

## Related Documents

`docs/releases/v0.7.0/WP7.0C Engineering Foundation Contracts.md`;
`ADR-0054`; `docs/engineering/Engineering Principles.md`; `WP7.1B
Engineering Review Report.md` and its five companion deliverables.
