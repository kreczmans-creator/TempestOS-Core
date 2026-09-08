# WP 6.7 — Export/Import — Implementation Report

## Status

**Complete.** Implemented on `feature/v0.6.0-platform-services`, directly
against the already-approved `v0.6.0` architecture package and Contract
Review package — neither package was revised during implementation.
The sixth of `v0.6.0`'s seven implemented Work Packages to be sequenced
ahead of its own nominal numeric position in `WorkPackages.md`, per
`Platform Service Implementation Order.md`'s own explicit
recommendation. Per this Work Package's own closing instruction,
implementation stops here, pending engineering approval.

## Scope Delivered

| Deliverable | Status |
|---|---|
| Export service | Delivered — `IExportService`/`ExportService`, exactly as approved |
| Import service | Delivered — `IImportService`/`ImportService`, exactly as approved, plus an additive `RegisterImportable` method on the concrete type |
| Import/export pipeline | Delivered — `ExportService` writes each source's own opaque bytes as an `ExportSection`, framed by `IExportFormat`; `ImportService` reads the framing back, validates every section before importing any of them, and dispatches by `Kind` |
| Serialization abstraction | Delivered, additive — `IExportPayloadSerializer`/`JsonExportPayloadSerializer`, optional, used only by a specific `IExportable`/`IImportable` pair |
| Format abstraction | Delivered, additive — `IExportFormat`/`JsonExportFormat`, used only by `ExportService`/`ImportService`'s own internal orchestration |
| Version compatibility support | Delivered — `IExportable.SchemaVersion`/`IImportable.SchemaVersion`, checked for exact equality; `IncompatibleExportSchemaException` on mismatch or unknown kind |
| Validation | Delivered — every section validated before any is imported; a malformed/truncated artifact rejected as `CorruptedExportArtifactException` |
| Dependency Injection registration | Delivered — `IExportService` as an ordinary Phase 6 singleton; `ImportService` dual-registered under its own concrete type and `IImportService` |
| Host integration | Delivered — registered in `TempestHost`'s existing Platform Services Registered block (Phase 6), immediately after the REST API's own `IApiEndpointRegistry` |
| Logging | Delivered — information-level on successful export/import (source/section count, `Kind`, schema version), warning-level on a schema-incompatibility rejection, matching the approved contract's own Logging Requirements |
| Diagnostics | **Not delivered as an `IDiagnosticsProvider` interface change** — see "Diagnostics," below, mirroring every prior Work Package's own identical scope decision |

## Suitability for Future Consumers

`IExportable`, `IExportService`, and `IImportService` are implemented
with zero deviation from `Public Interface Catalogue.md`, so any future
service (Licensing, an engineering module) can depend on them with full
confidence in their shape. A future service wanting to be exportable
needs only implement `IExportable` (and, optionally, `IExportableKind`
for a stable section identifier); a future service wanting to consume a
specific artifact needs only implement `IImportable` and register it
with `ImportService`'s own concrete type during its own Module
Initialisation — no change to any approved interface required for
either.

## Diagnostics: What Was and Was Not Done

Mirroring every prior Work Package's own identical finding: extending
the approved, shipped `IDiagnosticsProvider` (`WP 5.2`, `ADR-0039`)
would be a change to an approved public interface, requiring
documentation, an ADR, and genuine necessity per this Work Package's own
instructions. No such necessity exists — Export/Import's own
observability need is fully satisfiable through ordinary logging
(delivered) and the sample module's own demonstrable behaviour
(delivered).

## The Kind-Routing Mechanism: A Genuine Gap the Approved Contract Left Open

`IImportService.ImportAsync`'s own approved signature —
`Task ImportAsync(Stream source, CancellationToken cancellationToken = default)`
— carries no destination parameter, yet the approved contract's own
Responsibilities and Testing Requirements both require reading a
multi-source artifact back into more than one owning service. Resolved
additively: `IExportableKind` tags a source's own artifact section with
a stable `Kind` (falling back to the source's own runtime type name if
not implemented); `IImportable` is the read-back counterpart, registered
ahead of time via `ImportService.RegisterImportable` — a method on the
concrete type, not part of `IImportService` itself. `ImportService` is
dual-registered in `TempestHost` under both its own concrete type and
`IImportService`, mirroring `ADR-0044`'s own `CurrentPrincipalAccessor`
precedent exactly: a module needing to register resolves the concrete
type; every ordinary consumer resolves only the interface. See
`ADR-0051` for the complete account, including why a DI-resolved
`IEnumerable<IImportable>` was considered and rejected after directly
inspecting `TempestServiceProvider`'s own one-registration-per-type
resolution model.

## Production Code

16 files under `src/Tempest.Core/ExportImport/`; 7 files under
`src/Samples/Tempest.Samples/`; 1 file modified
(`src/Tempest.Core/Runtime/TempestHost.cs`, registration only). See the
retrospective's own "Files Added" section for the complete list.

## Testing

58 new tests (972 total, up from the `WP 6.3` baseline of 914), across
every category the implementation brief named:

| Category | Delivered |
|---|---|
| Unit tests | `ExportServiceTests`, `ImportServiceTests`, `JsonExportFormatTests`, `JsonExportPayloadSerializerTests`, `ExceptionTests` |
| Integration tests | `ExportImportSampleModuleIntegrationTests` — a full export-then-overwrite-then-import round trip through the real `ISettingsProvider`, driven entirely by the real, unmodified module pipeline |
| Import validation tests | `ImportAsync_RegisteredKind_*`, `ImportAsync_MultipleSections_*`, `ImportAsync_ExportThenImportRoundTrip_*` |
| Export validation tests | `ExportAsync_SingleSource_*`, `ExportAsync_MultipleSources_*`, `ExportAsync_SourceWithoutIExportableKind_FallsBackToItsOwnRuntimeTypeName` |
| Version compatibility tests | `ImportAsync_SchemaVersionMismatch_ThrowsIncompatibleExportSchemaException`, `ImportAsync_NoImportableRegisteredForKind_ThrowsIncompatibleExportSchemaException`, `ImportAsync_OneOfMultipleSectionsIsIncompatible_NoSectionIsImported` |
| Failure injection tests | `ExportAsync_SourceThrows_*`, `ExportAsync_DestinationStreamThrows_*`, `ImportAsync_ImportableThrows_*`, `ImportAsync_SourceStreamThrows_*` |
| Corrupted file tests | `ImportAsync_CorruptedArtifact_*`, `ImportAsync_TruncatedArtifact_*`, `JsonExportFormatTests.ReadAsync_NotJson_*`, `ReadAsync_PayloadIsNotValidBase64_*` |
| Regression tests | `ClockModuleDiscoveryTests` updated for the fourteenth sample module |
| Concurrency tests | `ImportAsync_ConcurrentCallsForDistinctArtifacts_BothCompleteCorrectly` |

## Validation Performed

- **Clean build.** `dotnet build tests/Tempest.Core.Tests/Tempest.Core.Tests.csproj`
  from a fully removed `bin`/`obj` tree, both Debug and Release
  configurations: 0 warnings, 0 errors, both times.
- **Complete automated test suite.** `dotnet test` in both Debug and
  Release configurations: 972/972 passing, both times; Debug re-run a
  second consecutive time to confirm stability, with no instance of the
  previously-disclosed `Console.Out`-capture flake observed.
- **Static analysis.** 0 compiler warnings (`Nullable` enabled
  project-wide) in both configurations.
- **Documentation validation.** Every code example in `Public Interface
  Catalogue.md` referenced by this Work Package's own implementation was
  cross-checked against the real, compiled signatures — no drift found.
- **Dependency validation.** Confirmed directly: `Tempest.Core.ExportImport`
  depends only on `Tempest.Core.Logging` (all existing Platform
  Services/DI), plus `System.Text.Json` (BCL) — no dependency on any
  Module, no circular reference (`grep -rl "Tempest.Core.ExportImport"
  src/Tempest.Core` finds only `TempestHost.cs` outside the namespace's
  own folder). No dependency on Identity, Settings, Audit, or
  Notifications directly — all four are consumed only at the
  sample-module calling layer.
- **Engineering self-review.** See `WP6.7 Engineering Review Report.md`.

## A Genuine Design Question, Resolved by Direct Inspection Rather Than Assumption

This Work Package's own implementation phase considered resolving the
Kind-routing mechanism via ordinary DI constructor injection of
`IEnumerable<IImportable>` — a pattern familiar from more featureful DI
containers. Before committing to it, `TempestServiceProvider`'s own
source (`src/Tempest.Core/DependencyInjection/TempestServiceProvider.cs`)
was inspected directly: registrations are held in a single
`Dictionary<Type, ServiceDescriptor>`, one entry per service type, with
a second registration under the same type silently overwriting the
first. No collection-resolution mechanism exists. This confirmed the
alternative was never actually available, not merely undesirable — see
`ADR-0051`'s own Alternatives Considered.

## Related Documents

`docs/academy/03 Work Packages/WP6.7-export-import-
implementation.md` (the full retrospective); `ADR-0051`; `WP6.7
Engineering Review Report.md`; `WP6.7 Platform Integration
Demonstration.md`; `WP6.7 Platform Impact Assessment.md`; `WP6.7 Lessons
Learned.md`; `WP6.7 Technical Debt Assessment.md`; `WP6.7 Future
Capability Recommendations.md`.
