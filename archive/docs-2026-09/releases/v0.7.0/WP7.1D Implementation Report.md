# WP 7.1D — Engineering Calculation Framework — Implementation Report

## Status

Complete. The fourth implementation Work Package of the Engineering
Foundation phase (`v0.7.0`) — production code, tests, one ADR, and a
dedicated Security Review were produced, following `WP 7.0C`'s own
approved contracts.

## Scope Delivered

`Tempest.Core.Calculations` implemented exactly as `WP7.0C Engineering
Foundation Contracts.md` proposed, extended (not changed) with the
metadata/context/validation structure this Work Package's own
controlling instruction required:

- `ICalculationDefinition<TInput, TResult>` — `CalculationId`,
  `Calculate`, implemented exactly as proposed in name and purpose,
  plus `Metadata` (new) and `Calculate`'s own extended signature (new
  `CalculationContext` parameter — see Additions, below).
- `ICalculationEngine` — `RegisterDefinition`, `ExecuteAsync`, both
  implemented exactly as proposed, including the exact exception
  contract (`DuplicateCalculationException`,
  `CalculationDefinitionNotFoundException`,
  `CalculationInputInvalidException`).
- `CalculationRecord<TResult>` — `CalculationId`, `Result`, `ExecutedAt`,
  `ExecutedByPrincipalId`, all implemented exactly as proposed, plus
  `Id`, `Assumptions`, `IntermediateResults`, `Validation`,
  `ReferencedMaterialIds`, `RevisionNumber` (all new — see Additions).
- `CalculationEngine` — the concrete implementation, resolving
  `ADR-0056` (mandatory Engineering Data Model integration; no direct
  Persistence dependency needed, unlike Materials).
- `CalculationException`, `DuplicateCalculationException`,
  `CalculationDefinitionNotFoundException`,
  `CalculationInputInvalidException` — all implemented exactly as
  proposed (`CalculationException` non-abstract, matching this
  codebase's universal convention, the same disclosed deviation
  `WP 7.1A`/`WP 7.1C` already established).
- DI registration: `ICalculationEngine` registered as an ordinary Phase
  6 singleton in `TempestHost.cs`, immediately after Materials.
- `CalculationSampleModule` — the living reference module, registering
  and executing `DoubleLengthCalculationDefinition` (a deliberately
  trivial, non-domain-specific calculation) during its own
  initialisation, exposing `ExecuteSampleCalculationCommand` for manual
  invocation.

**Not implemented, per this Work Package's own explicit scope
boundary:** any Mechanical, Structural, HVAC, Electrical, or
Manufacturing formula; design-code logic; safety-factor policy; UI
concerns; report formatting.

## Additions Beyond the Approved Contract

**`CalculationMetadata`, `CalculationAssumption`, `CalculationConstraint`**
— fixed, per-definition declarative data (Name, Description, Category,
Assumptions, Constraints), satisfying "Calculation metadata,"
"Assumptions," and "Constraints" from this Work Package's own
Implementation Scope, none shown in the approved contract's own
illustrative code.

**`CalculationContext`, `CalculationIntermediateResult`,
`CalculationConstraintCheck`** — a fresh, non-shared, per-execution
recorder `Calculate` uses to declare intermediate values, per-execution
constraint outcomes, and referenced materials, satisfying "Intermediate
result model," "Validation model," "Calculation context," and "Material
references."

**`CalculationValidationOutcome`, `CalculationValidationResult`** — the
validation model, derived automatically from recorded constraint
checks, never asserted directly by the engine.

**`CalculationRecord<TResult>`'s own expanded shape** — `Id` (stable
identity, also the underlying `IEngineeringDocument`'s own Id),
`Assumptions` (copied from `Metadata` at execution time),
`IntermediateResults`, `Validation`, `ReferencedMaterialIds`, and
`RevisionNumber`, satisfying "Calculation identity" and "Calculation
revision support."

## Deviations From the Approved Contract

**One change to a shown member, fully authorised by its own reserved
ADR.** `Calculate`'s own signature changed from `Calculate(TInput
input)` to `Calculate(TInput input, CalculationContext context)` —
`ADR-0056`'s own Decision 3 resolves this as the necessary consequence
of this Work Package's own "Calculation context" requirement, which the
approved contract's own illustrative shape could not otherwise satisfy.
Every other shown member (`CalculationId`, `RegisterDefinition`,
`ExecuteAsync`'s own signature, all three exception types) is
implemented unchanged.

**One disclosed, minor deviation, same as `WP 7.1A`/`WP 7.1B`/
`WP 7.1C`'s own precedent:** `CalculationException` implemented as
`public class`, not `public abstract class` as the contract's own
literal text showed.

No other deviation exists.

## Platform Integration

Confirmed exactly as `WP7.0C Platform Integration Matrix.md` predicted,
extended by this Work Package's own findings: Engineering Data Model
(`IEngineeringDocumentStore`, every execution is a document of
`Kind = "CalculationRecord"`) and Identity & Permissions
(`ICurrentPrincipalAccessor`, for `ExecutedByPrincipalId`) are both
real, exercised dependencies. Units & Quantities is consumed by
convention only — `DoubleLengthCalculationDefinition`'s own `TInput`/
`TResult` are both `Quantity<Length>`, but nothing in `ICalculationEngine`
itself constrains this. Materials is **not** a dependency — material
references are open, unvalidated strings (`ADR-0056` Decision 6). Audit
is **not** consumed — a plausible, not mandatory, future integration,
unchanged from the approved contract's own framing. No direct
`IPersistenceStore` dependency exists, unlike Materials — each
execution always creates a fresh document, never looked up later by a
caller-chosen key.

## Production Code

| File | Purpose |
|---|---|
| `ICalculationDefinition.cs`, `ICalculationEngine.cs` | The public definition and engine contracts |
| `CalculationMetadata.cs`, `CalculationAssumption.cs`, `CalculationConstraint.cs` | Fixed, per-definition declarative metadata |
| `CalculationContext.cs`, `CalculationIntermediateResult.cs`, `CalculationConstraintCheck.cs` | The per-execution recorder and its own recorded shapes |
| `CalculationValidationOutcome.cs`, `CalculationValidationResult.cs` | The validation model |
| `CalculationRecord.cs` | The immutable execution record — engineering evidence |
| `CalculationException.cs`, `DuplicateCalculationException.cs`, `CalculationDefinitionNotFoundException.cs`, `CalculationInputInvalidException.cs` | Exception hierarchy |
| `CalculationRecordDto.cs` | Internal, JSON-serializable persistence shape |
| `CalculationEngine.cs` | The concrete engine implementation (`ADR-0056`) |
| `TempestHost.cs` (modified) | Phase 6 DI registration |
| `DoubleLengthCalculationDefinition.cs`, `CalculationSampleModule.cs`, `ExecuteSampleCalculationCommand(Handler).cs` | The living reference calculation, module, and command |

17 new production files; 1 modified (`TempestHost.cs`).

## Testing

52 new tests, across:

- **Unit** — `CalculationEngineTests.cs` (register/execute round-trip,
  constructor validation), `CalculationContextTests.cs` (recording
  methods, validation).
- **Execution** — dispatch to the correct registered definition;
  mismatched-signature rejection.
- **Validation** — all-constraints-satisfied → `Valid`; a soft,
  unsatisfied constraint → `Conditional`, result still returned.
- **Serialization** — the underlying document's own JSON content
  contains the expected `CalculationId`/`Result` fields, verified via
  `JsonDocument.Parse`.
- **Traceability** — `CalculationRecord.Id` directly retrievable through
  `IEngineeringDocumentStore`.
- **Assumptions** — a definition's own declared assumptions survive
  into the resulting record unchanged.
- **Reproducibility** — the same input, executed five times, always
  produces the identical result.
- **Equality/Immutability** — the small record types
  (`CalculationAssumption`, `CalculationIntermediateResult`,
  `CalculationConstraintCheck`) structural-equality and
  `with`-expression tests.
- **Failure Injection** — `CalculationInputInvalidException`,
  `CalculationDefinitionNotFoundException`,
  `PersistenceStoreUnavailableException`, all propagating unmodified.
- **Concurrency** — 30 concurrent executions of the same pure
  calculation, different inputs, all producing correct, non-interfering
  results.
- **Registration** — `CalculationHostRegistrationTests.cs` (three
  tests).
- **Integration** — `CalculationSampleModuleIntegrationTests.cs` (five
  tests).
- `ClockModuleDiscoveryTests.cs` updated: module count 17 → 18.

**1226/1226 tests passing** (1174 baseline + 52 new), 0 failures, both
Debug and Release, from a fully clean (`bin`/`obj` removed) rebuild.

## Validation Performed

- Clean Debug build: 0 warnings, 0 errors.
- Clean Release build: 0 warnings, 0 errors.
- Full automated test suite: 1226/1226, both configurations.
- Dependency validation: no circular dependency; `CalculationEngine`
  depends only on `IEngineeringDocumentStore` and
  `ICurrentPrincipalAccessor`, both pre-existing Platform Services;
  neither depends back on Calculations.
- No layering violation: `Tempest.Core.Calculations` is an ordinary
  Platform Service-layer namespace, depending only on other Platform
  Services and the DI container.
- Dedicated Security Review performed — see `WP7.1D Security Review
  Report.md`.

## Related Documents

`docs/releases/v0.7.0/WP7.0C Engineering Foundation Contracts.md`;
`ADR-0056`; `docs/engineering/Engineering Principles.md`; `WP7.1D
Engineering Review Report.md`, `WP7.1D Security Review Report.md`, and
their five other companion deliverables.
