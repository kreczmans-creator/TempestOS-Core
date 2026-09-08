# WP 7.1A — Engineering Data Model — Implementation Report

## Status

Complete. The first implementation Work Package of the Engineering
Foundation phase (`v0.7.0`) — production code, tests, and one ADR were
produced, following `WP 7.0C`'s own approved contracts.

## Scope Delivered

`Tempest.Core.EngineeringData` implemented exactly as `WP7.0C
Engineering Foundation Contracts.md` proposed, with one disclosed,
minor deviation (see Deviations, below):

- `IEngineeringDocument`, `IDocumentRevision`, `DocumentReference` —
  the three public entity contracts, unchanged from the approved
  proposal.
- `IEngineeringDocumentStore` — `CreateAsync`, `FindAsync`,
  `ReviseAsync`, `GetRevisionHistoryAsync`, `LinkAsync`,
  `GetReferencesAsync`, all six methods implemented exactly as proposed.
- `EngineeringDocumentStore` — the concrete implementation, resolving
  `ADR-0053` (built on `IPersistenceStore`, no new storage abstraction).
- `EngineeringDataException`, `EngineeringDocumentNotFoundException` —
  both implemented; see Deviations for the base exception's class
  modifier.
- DI registration: `IEngineeringDocumentStore` registered as an
  ordinary Phase 6 singleton in `TempestHost.cs`, immediately after
  Export/Import.
- `EngineeringDataSampleModule` — the living reference module, creating,
  revising, and linking a document during its own initialisation, and
  exposing `CreateSampleDocumentCommand`/`ReviseSampleDocumentCommand`
  for manual invocation.

**Not implemented, per this Work Package's own explicit scope
boundary:** engineering calculations, engineering standards, and any
Mechanical/HVAC/Structural/Electrical concept. `Content` remains an
opaque `string` throughout.

## Deviations From the Approved Contract

**One, minor, disclosed.** `WP7.0C Engineering Foundation Contracts.md`
proposed `EngineeringDataException` as `public abstract class`. The
real, existing codebase convention — `PersistenceException`,
`SettingsException`, `AuditException`, all in namespaces `WP7.0C` itself
cites as precedent — is uniformly `public class` (non-abstract), with
no concrete instance ever thrown directly, by convention rather than by
compiler enforcement. `WP7.0C`'s own proposal was written without
directly reading these existing files; this Work Package's own
implementation follows the real, established convention rather than
the contract's literal text, since matching a universal existing
pattern is not itself a new architectural decision requiring an ADR —
it is applying one that already governs every other exception hierarchy
in this codebase. Disclosed here per this Work Package's own governing
instruction ("If a deviation is required: document the issue, minimise
the change... explain why the approved contract could not be
implemented as specified").

No other deviation exists — every method signature, every exception
type, every DI registration shape matches `WP7.0C Engineering
Foundation Contracts.md` exactly.

## Platform Integration

Confirmed exactly as `WP7.0C Platform Integration Matrix.md` predicted:
Identity & Permissions (author attribution via
`ICurrentPrincipalAccessor`, mirroring `IAuditRecorder`'s own pattern
exactly) and Persistence (`IPersistenceStore`, per `ADR-0053`) are both
real, exercised dependencies. Settings, Licensing, Diagnostics, Audit,
Reporting, Notifications, REST API, and Export/Import are not consumed
by this Work Package's own implementation — each remains a plausible,
not yet realised, future integration, exactly as that matrix disclosed.
See `WP7.1A Platform Consumption Assessment.md` for the full account.

## Production Code

| File | Purpose |
|---|---|
| `IEngineeringDocument.cs`, `IDocumentRevision.cs`, `DocumentReference.cs` | Public entity contracts |
| `IEngineeringDocumentStore.cs` | The public service contract |
| `EngineeringDataException.cs`, `EngineeringDocumentNotFoundException.cs` | Exception hierarchy |
| `EngineeringDocument.cs`, `DocumentRevision.cs` | Concrete, internal entity implementations |
| `EngineeringDocumentDto.cs`, `DocumentRevisionDto.cs`, `DocumentReferenceDto.cs` | Internal, JSON-serializable persistence shapes |
| `EngineeringDocumentStore.cs` | The concrete service implementation (`ADR-0053`) |
| `TempestHost.cs` (modified) | Phase 6 DI registration |
| `EngineeringDataSampleModule.cs`, `CreateSampleDocumentCommand(Handler).cs`, `ReviseSampleDocumentCommand(Handler).cs` | The living reference module and its two commands |

13 new production files; 1 modified (`TempestHost.cs`).

## Testing

36 new tests, across:

- **Unit** — `EngineeringDocumentStoreTests.cs` (create/find/revise/
  history/link/references round-trip, constructor validation).
- **Concurrency/Regression** — revision-number atomicity under 20
  concurrent `ReviseAsync` calls against the same document.
- **Failure Injection** — `PersistenceStoreUnavailableException`
  propagates unmodified from `CreateAsync`.
- **Exception** — `ExceptionTests.cs`.
- **Registration** — `EngineeringDataHostRegistrationTests.cs` (four
  tests: resolvable, singleton semantics, shared `IPersistenceStore`
  instance with Audit/Settings, real round-trip through the real Host).
- **Integration** — `EngineeringDataSampleModuleIntegrationTests.cs`
  (seven tests: constructor injection, initialise-time lifecycle,
  command registration, both command paths, cross-pipeline durability,
  full real-Host execution).
- `ClockModuleDiscoveryTests.cs` updated: module count 15 → 16.

**1052/1052 tests passing** (1016 baseline + 36 new), 0 failures, both
Debug and Release, from a fully clean (`bin`/`obj` removed) rebuild.

## Validation Performed

- Clean Debug build: 0 warnings, 0 errors.
- Clean Release build: 0 warnings, 0 errors.
- Full automated test suite: 1052/1052, both configurations.
- Dependency validation: no circular dependency; `EngineeringDocumentStore`
  depends only on `IPersistenceStore` and `ICurrentPrincipalAccessor`,
  both pre-existing, both peers or below in the four-layer model.
- No layering violation: `Tempest.Core.EngineeringData` is an ordinary
  Platform Service-layer namespace, depending only on Persistence and
  Identity (both Platform Services) and the DI container — no
  dependency on any Module.

## Related Documents

`docs/releases/v0.7.0/WP7.0C Engineering Foundation Contracts.md`;
`ADR-0053`; `docs/engineering/Engineering Principles.md`; `WP7.1A
Engineering Review Report.md` and its five companion deliverables.
