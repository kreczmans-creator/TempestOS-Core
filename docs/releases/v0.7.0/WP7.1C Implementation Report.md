# WP 7.1C — Materials Framework — Implementation Report

## Status

Complete. The third implementation Work Package of the Engineering
Foundation phase (`v0.7.0`) — production code, tests, and one ADR were
produced, following `WP 7.0C`'s own approved contracts.

## Scope Delivered

`Tempest.Core.Materials` implemented exactly as `WP7.0C Engineering
Foundation Contracts.md` proposed, extended (not changed) with a
structured, provenance-carrying property type resolving `ADR-0055`'s
own reserved property-typing question:

- `IMaterialCatalog` — `RegisterAsync`, `FindAsync`, `ListAsync`, all
  three implemented exactly as proposed (with `category` added as an
  optional, appended parameter — purely additive), plus `ReviseAsync`
  (new — see Additions, below).
- `IMaterialSpecification` — `MaterialId`, `Name`, `Properties`,
  `UnderlyingDocumentId`, all implemented exactly as proposed, plus
  `Category` and `RevisionNumber` (new).
- `MaterialProperty(object Value, MaterialPropertyProvenance Provenance)`
  — replaces the approved contract's own bare `object` property value,
  resolving `ADR-0055`'s reserved question in favour of "design a
  stronger alternative."
- `MaterialPropertyProvenance` — `SourceReference`, `SourceRevision`,
  `ValidationStatus`, `ConfidenceLevel`, `ApplicableConditions`, `Notes`
  — every field this Work Package's own controlling instruction named.
- `MaterialCatalog` — the concrete implementation, resolving `ADR-0055`
  (thin index over `IEngineeringDocumentStore`, plus a direct
  `IPersistenceStore` dependency for its own `materialId` index).
- `MaterialsException`, `DuplicateMaterialException`,
  `MaterialNotFoundException` — all implemented exactly as proposed
  (`MaterialsException` non-abstract, matching this codebase's own
  universal convention, the same disclosed deviation `WP 7.1A` already
  established).
- DI registration: `IMaterialCatalog` registered as an ordinary Phase 6
  singleton in `TempestHost.cs`, immediately after Engineering Data.
- `MaterialsSampleModule` — the living reference module, registering
  and revising a fictional material during its own initialisation, and
  exposing `RegisterSampleMaterialCommand`/`ReviseSampleMaterialCommand`
  for manual invocation.

**Not implemented, per this Work Package's own explicit scope
boundary:** engineering calculations, material selection algorithms,
design allowables, design-code-specific assumptions, country-specific
standards, material databases, safety factors. Every value used in
tests and the sample module is fictional, explicitly disclosed as such
in its own `SourceReference`/`Notes`, never presented as a real material
standard's own published value.

## Additions Beyond the Approved Contract

**`IMaterialCatalog.ReviseAsync`** — the approved contract showed only
three methods; this Work Package's own Implementation Scope names
"Material revision support" explicitly. `ReviseAsync` records a new
revision of an existing material's own properties, giving
`MaterialNotFoundException` (declared but, per `WP7.0C`'s own Error
Handling section, previously without a concrete throwing use) its first
real use.

**`Category` on `IMaterialSpecification`/`RegisterAsync`** — an open,
caller-assigned classification string, appended as an optional
parameter (default `null`) so the approved contract's own three
required parameters (`materialId`, `name`, `properties`) are unchanged
in position or type.

## Deviations From the Approved Contract

**One change to a shown member, fully authorised by its own reserved
ADR.** `IMaterialSpecification.Properties`'s own value type changed
from the contract's proposed bare `object` to `MaterialProperty` (value
+ mandatory provenance) — this is exactly the "design a stronger
alternative" option `WP7.0C Required ADR Catalogue.md` itself named as
an acceptable resolution to `ADR-0055`'s own reserved question, not an
unauthorised deviation. See `ADR-0055` for the full reasoning.

**One disclosed, minor deviation, same as `WP 7.1A`/`WP 7.1B`'s own
precedent:** `MaterialsException` implemented as `public class`, not
`public abstract class` as the contract's own literal text showed —
matching this codebase's universal exception-hierarchy convention.

No other deviation exists.

## Platform Integration

Confirmed exactly as `WP7.0C Platform Integration Matrix.md` predicted:
Engineering Data Model (`IEngineeringDocumentStore`, every material
specification is a document of `Kind = "MaterialSpecification"`) and
Units & Quantities (`Quantity<TDimension>`, every property value) are
both real, exercised dependencies — proven directly, not merely
asserted, by `UnderlyingDocumentId`-based traceability tests and the
property-value codec's own round-trip tests. Persistence is consumed
directly, not only indirectly as the approved contract's own framing
suggested — a genuine implementation finding, see `ADR-0055` Decision 3.
Identity & Permissions is **not** consumed directly — authorization is
expected at the calling layer, mirroring Reporting/Navigation's own
precedent (disclosed as `AT-15`). No other Platform Service integration
is required, since nothing else in this framework needs one.

## Production Code

| File | Purpose |
|---|---|
| `IMaterialCatalog.cs`, `IMaterialSpecification.cs` | The public service and entity contracts |
| `MaterialProperty.cs`, `MaterialPropertyProvenance.cs` | The provenance-carrying property value type |
| `MaterialPropertyValidationStatus.cs`, `MaterialPropertyConfidenceLevel.cs` | Provenance enums |
| `MaterialsException.cs`, `DuplicateMaterialException.cs`, `MaterialNotFoundException.cs` | Exception hierarchy |
| `MaterialSpecification.cs` | Concrete, internal entity implementation |
| `MaterialSpecificationDto.cs`, `MaterialPropertyDto.cs` | Internal, JSON-serializable persistence shapes |
| `MaterialPropertyValueCodec.cs` | Encodes/decodes a boxed `Quantity<TDimension>` to/from its plain, serializable parts |
| `MaterialCatalog.cs` | The concrete service implementation (`ADR-0055`) |
| `TempestHost.cs` (modified) | Phase 6 DI registration |
| `MaterialsSampleModule.cs`, `RegisterSampleMaterialCommand(Handler).cs`, `ReviseSampleMaterialCommand(Handler).cs` | The living reference module and its two commands |

14 new production files; 1 modified (`TempestHost.cs`).

## Testing

55 new tests, across:

- **Unit** — `MaterialCatalogTests.cs` (register/find/list/revise
  round-trip, constructor validation).
- **Serialization/Codec** — `MaterialPropertyValueCodecTests.cs` (all
  seven dimensions round-trip; unsupported-type/unrecognised-dimension
  failure).
- **Revision** — revision-number increments on `ReviseAsync`; name/
  category preserved across revision.
- **Provenance** — every one of the six provenance fields round-trips
  exactly through registration and lookup.
- **Equality/Immutability** — `MaterialPropertyProvenance`/
  `MaterialProperty` structural equality; `with`-expression producing a
  new instance without mutating the original.
- **Traceability** — `UnderlyingDocumentId` directly retrievable and
  linkable through `IEngineeringDocumentStore`, proving "Material
  references" without duplicating the Data Model's own capability.
- **Failure Injection** — `DuplicateMaterialException`,
  `MaterialNotFoundException`, `PersistenceStoreUnavailableException`
  propagating unmodified.
- **Concurrency/Regression** — 20 concurrent registrations of different
  materials all succeed; 10 concurrent registrations of the *same*
  `materialId` produce exactly one success.
- **Registration** — `MaterialsHostRegistrationTests.cs` (three tests).
- **Integration** — `MaterialsSampleModuleIntegrationTests.cs` (seven
  tests).
- `ClockModuleDiscoveryTests.cs` updated: module count 16 → 17.

**1174/1174 tests passing** (1119 baseline + 55 new), 0 failures, both
Debug and Release, from a fully clean (`bin`/`obj` removed) rebuild.

## Validation Performed

- Clean Debug build: 0 warnings, 0 errors.
- Clean Release build: 0 warnings, 0 errors.
- Full automated test suite: 1174/1174, both configurations.
- Dependency validation: no circular dependency; `MaterialCatalog`
  depends only on `IEngineeringDocumentStore` and `IPersistenceStore`,
  both pre-existing Platform Services; neither depends back on
  Materials.
- No layering violation: `Tempest.Core.Materials` is an ordinary
  Platform Service-layer namespace, depending only on other Platform
  Services and the DI container — no dependency on any Module.

## Related Documents

`docs/releases/v0.7.0/WP7.0C Engineering Foundation Contracts.md`;
`ADR-0055`; `docs/engineering/Engineering Principles.md`; `WP7.1C
Engineering Review Report.md` and its five companion deliverables.
