# WP 7.1F — Engineering Core Consumption Matrix

## Purpose

For each of the five Engineering Core frameworks, this matrix records:
who consumes it, the concrete evidence that consumption is real and
verified (not merely claimed), and its certification status — mirroring
`WP6.8 Platform Consumption Matrix.md`'s own role and format for the
`v0.6.0` Platform Services. "Verified consumer" means a real, compiling,
test-exercised piece of code — a sample module, a sibling Engineering
Core framework, or a dedicated test — never a hypothetical future
consumer.

## Matrix

| Framework | Consumers | Verification Evidence | Status |
|---|---|---|---|
| **Engineering Data Model** (`Tempest.Core.EngineeringData`) | `EngineeringDataSampleModule` (real contributor); `Tempest.Core.Materials` (every material *is* a document); `Tempest.Core.Calculations` (every execution recorded as a document); `Tempest.Core.Verification` (every subject and every verification record *is* a document) — the only Engineering Core framework consumed by all four siblings | `EngineeringDocumentStoreTests.cs` (23 tests); `EngineeringDataHostRegistrationTests.cs`; `EngineeringDataSampleModuleIntegrationTests.cs`; direct `using Tempest.Core.EngineeringData;` confirmed in `Materials/`, `Calculations/`, `Verification/` source | **Verified — the Engineering Core's own foundation, with the broadest consumption of any of the five** |
| **Units & Quantities** (`Tempest.Core.UnitsAndQuantities`) | No sample module of its own (deliberate — zero DI registration, nothing to demonstrate through a module); `Tempest.Core.Materials` (every dimensioned property); expected, by convention, by `Tempest.Core.Calculations` (no compile-time dependency, confirmed) | `QuantityTests.cs`, `UnitTests.cs`, `DimensionCatalogueTests.cs`, `CompileTimeDimensionSafetyTests.cs`, `QuantitySerializationTests.cs` (47 test attributes total); `PlatformIntegrationTests.cs` proves a `Quantity<Mass>` round-trips as `IEngineeringDocumentStore` content, the one integration this framework's own contract names; direct `using Tempest.Core.UnitsAndQuantities;` confirmed in `Materials/` source | **Verified** (one real sibling-framework consumer, Materials; the only Engineering Core framework with zero Platform Service dependency and no DI registration of any kind, exactly as designed) |
| **Materials** (`Tempest.Core.Materials`) | `MaterialsSampleModule` (real contributor); named, by both `Tempest.Core.Calculations` and `Tempest.Core.Verification`, as an open `string` material reference (`CalculationContext.ReferenceMaterial`, `VerificationContext.ReferenceMaterial`) — deliberately unvalidated, not a compile-time dependency (`AT-16`, `AT-17`) | `MaterialCatalogTests.cs` (45 test attributes); `MaterialsHostRegistrationTests.cs`; `MaterialsSampleModuleIntegrationTests.cs` | **Verified** (one real contributing module; referenced, not depended upon, by two sibling frameworks — a deliberate design boundary, not a gap) |
| **Calculations** (`Tempest.Core.Calculations`) | `CalculationSampleModule` (real contributor); named as a plausible future consumer for any discipline-specific Engineering Module, none yet exists | `CalculationEngineTests.cs` (44 test attributes, including a 30-concurrent-execution purity/concurrency test); `CalculationHostRegistrationTests.cs`; `CalculationSampleModuleIntegrationTests.cs` | **Verified** (single real consumer this programme; deliberately the first Engineering Foundation framework with a dedicated Security Review) |
| **Verification** (`Tempest.Core.Verification`) | `VerificationSampleModule` (real contributor); named as `FCR-0027`'s (Requirements Engine) own most natural future consumer, none yet exists | `VerificationServiceTests.cs` (39 test attributes, including a 15-concurrent-`RecordAsync` test); `VerificationContextTests.cs`; `VerificationHostRegistrationTests.cs`; `VerificationSampleModuleIntegrationTests.cs` | **Verified** (single real consumer this programme; the simplest dependency shape of the five — `EngineeringData` only, no direct `Persistence`) |

## Cross-Framework Consumption Confirmed Directly

Re-derived from real `using` statements, not from any Work Package's own
retrospective claim (`grep -rhoP "^using Tempest\.Core\.[A-Za-z]+;"` per
namespace):

- `Materials` imports `EngineeringData` and `UnitsAndQuantities` — both
  confirmed real, compiled dependencies, not merely documented ones.
- `Calculations` imports `EngineeringData` only — its own relationship to
  `UnitsAndQuantities` is convention, not a compile-time dependency,
  confirmed by the absence of any `using Tempest.Core.UnitsAndQuantities;`
  anywhere in `src/Tempest.Core/Calculations/`.
- `Verification` imports `EngineeringData` only — no dependency on
  `Materials`, `Calculations`, or a future Requirements Engine, confirmed
  the same way.

## Observations

**Engineering Data Model is the only Engineering Core framework consumed
by all four of its siblings** — the strongest possible confirmation that
`ADR-0053`'s own foundational role is real, not aspirational. Every other
Engineering Foundation framework either builds on it directly
(`Materials`, `Calculations`, `Verification`) or was designed
deliberately not to need it (`UnitsAndQuantities`, a pure value-type
library with no storage concern of its own).

**Every framework has at least one real, tested sample-module consumer
except Units & Quantities**, which deliberately has none — the same
"designed boundary, not a gap" pattern `WP6.8 Platform Consumption
Matrix.md` itself found for Reporting and Export/Import (`ADR-0040`,
`ADR-0051`), now confirmed for a sixth time across this project's history.

**No Engineering Core framework yet has two independent real consumers**
— every one of the five has either exactly one sample-module contributor
or (Units & Quantities) exactly one sibling-framework consumer. This
differs from `v0.6.0`'s own Consumption Matrix, where the REST API
reached two independent consumers within its own release. This is
expected, not a gap: the Engineering Foundation program­me exists to be
consumed by a future, real, discipline-specific Engineering Module — none
exists yet, and none was promised by any of the five Work Packages'
own approved scope. `WP7.1F Engineering Core Certification Report.md`
does not treat single-consumer status as a certification blocker for
this reason, mirroring `WP6.8`'s own identical reasoning for Reporting
and Export/Import.

**Materials is the only Engineering Core framework consumed by two
siblings without either taking a compile-time dependency on it** — both
`Calculations` and `Verification` reference a material only as an open,
unvalidated string (`AT-16`, `AT-17`). This is a deliberately weaker
coupling than `Materials`' own dependency on `EngineeringData`/
`UnitsAndQuantities`, disclosed and justified identically in both cases:
neither framework has a hard dependency on Materials, so validating a
material reference would cost adding one purely for validation.

## Related Documents

`WP7.1F Engineering Core Certification Report.md`; `WP7.1F Engineering
Core Architecture Conformance Report.md`; `docs/governance/Engineering/
Module Register.md`, `Dependency Injection Register.md` (both fully
backfilled by this Work Package); `WP7.0C Cross-Framework Dependency
Report.md`.
