# ADR-0051: Export/Import Is Orthogonal to the Internal Persistence Abstraction — Kind Routing, Format/Serialization Abstractions, and Scope Boundaries

## Status

Accepted — `WP 6.7` (Export/Import Framework), 2026-07-29.

## Context

`v0.6.0`'s own architecture package anticipated this decision but
deliberately left it unratified pending `WP 6.7`'s own implementation
phase. `Required ADRs.md` named the core orthogonality question
(Export/Import vs. `IPersistenceStore`) as this Work Package's own
required ADR. Implementation surfaced three further genuine decisions
`Public Interface Catalogue.md`'s own draft (`IExportable`,
`IExportService`, `IImportService`, `ExportImportException`,
`IncompatibleExportSchemaException`) left unresolved:

1. **`IImportService.ImportAsync(Stream source, CancellationToken)`'s own
   approved signature carries no destination parameter at all**, yet
   `Platform Service Contracts.md`'s own Responsibilities dimension
   requires it to "read a previously exported artifact back into the
   owning service(s)" — plural — and its own Testing Requirements name
   "multi-source export/import" explicitly. Nothing in the approved
   shape explains how a single `Stream` argument, with no further input,
   can be routed back to more than one owning service.
2. This Work Package's own brief named "Serialization abstraction" and
   "Format abstraction" as implementation scope, neither of which the
   approved catalogue gave interface members.
3. The approved contract's own Testing Requirements explicitly name
   "Corrupted file tests" as a category — a genuinely different failure
   mode from "artifact well-formed but running an unsupported schema
   version," which is all `IncompatibleExportSchemaException` was
   drafted to cover.

This Work Package was also explicitly tasked with assessing and, where
justified, implementing interactions with six already-completed platform
services (Identity, Settings, Persistence, Audit, Notifications,
Reporting) — the largest such list of any Work Package this release.

## Decision

**`IExportable`, `IExportService`, `IImportService`, `ExportImportException`,
and `IncompatibleExportSchemaException` are implemented exactly as
`Public Interface Catalogue.md` drafted** — zero signature deviation.
`ExportImportException` is a concrete, base-plus-subtype type (not
abstract, despite the catalogue's own pseudo-code shorthand), mirroring
`ReportingException`/`ApiException`/`SettingsException`/`IdentityException`/
`CommandException`'s own established real-codebase convention.

**Registered as ordinary DI-public, container-constructed singletons** in
`TempestHost`'s existing Platform Services Registered block (Phase 6),
immediately after the REST API's own `IApiEndpointRegistry` and before the
Composition Root's `IDiagnosticsProvider` — matching `Service Registration
Matrix.md`'s own recommended registration order. `IExportService`/
`IImportService` depend on nothing but Dependency Injection itself,
confirmed directly, consistent with `Platform Service Implementation
Order.md`'s own observation that Export/Import has no hard
proposed-service dependency — only a practical one (a real `IExportable`
source worth integrating against, satisfied by `WP 6.4`'s own Settings).

**`ImportService` is dual-registered — under both its own concrete type
and `IImportService` — mirroring `ADR-0044`'s own precedent for
`CurrentPrincipalAccessor`.** `IImportService`'s own approved shape has
exactly one method and no registration mechanism of any kind. Solving
problem (1) above without touching that approved shape requires an
explicit registration surface living somewhere the interface does not
reach — exactly the situation `ADR-0044` already resolved once: a
privileged registrant (a module, during its own `InitialiseAsync`)
resolves `ImportService`'s own concrete type to call
`RegisterImportable`, while every ordinary consumer resolves only the
read-only `IImportService` interface — both against the exact same
already-constructed instance. `RegisterImportable` is expected to be
called only during Module Initialisation, mirroring
`IReportingService.RegisterDefinition`'s own established convention; a
duplicate registration under the same `IImportable.Kind` throws the new
`DuplicateImportableKindException`, mirroring
`DuplicateReportDefinitionException`/`DuplicateApiRouteException`'s own
first-registration-wins convention.

**`IExportableKind` and `IImportable` are new, additive interfaces — no
approved interface is changed.** `IExportable` itself gains no `Kind`
member; a source that wants its own artifact section tagged with a
stable identifier (rather than falling back to its own runtime type
name) implements the small, optional `IExportableKind` companion
interface instead. `IImportable` is the read-back counterpart
`IImportService` dispatches to by matching `Kind` — entirely unknown to
`IExportable`/`IExportService`, exactly as `IReportTemplate` is unknown
to `IReportingService` (`ADR-0040`). `ExportService` itself needs no
registration of any kind: every call receives its own sources directly,
exactly as the approved signature already allows.

**`IExportFormat`/`JsonExportFormat` is the "Format abstraction," used
only by `ExportService`/`ImportService`'s own internal orchestration.**
It frames one or more already-serialized, opaque `ExportSection`s (each
a `Kind` + `SchemaVersion` + raw bytes, produced by a source's own
`ExportAsync`) into a single artifact stream, and reads that framing
back. This is the mechanism that makes "combine N independent,
mutually-unaware `IExportable.ExportAsync` outputs into one artifact"
possible at all without any source needing to know about any other.
`JsonExportFormat` writes a JSON array of `{ kind, schemaVersion, payload
}` objects, with each section's own bytes carried as base64 — matching
this codebase's existing `System.Text.Json` convention
(`JsonProjectRepository`).

**`IExportPayloadSerializer`/`JsonExportPayloadSerializer` is the
separate, entirely optional "Serialization abstraction,"** used only by
a specific `IExportable`/`IImportable` pair, if that pair chooses to —
never by `ExportService`/`ImportService`, which have no awareness of it
at all. It converts a simple key/value data set to and from raw bytes.
Distinct from `IExportFormat` by construction: the Format abstraction
never sees more than one section's structure (`Kind`/`SchemaVersion`/
opaque bytes) and has no awareness of what is inside a payload; the
Serialization abstraction never sees more than one source's own data and
has no awareness of sections, kinds, artifact framing, or schema-version
routing.

**`CorruptedExportArtifactException` is a new, concrete, sealed
`ExportImportException` subtype**, thrown by `IExportFormat.ReadAsync`
(and `IExportPayloadSerializer.Deserialize`) when a stream does not
contain a well-formed artifact this format recognises — a malformed or
truncated file, structurally distinct from an incompatible-but-legible
artifact. Overloading `IncompatibleExportSchemaException` for this case
was rejected (see Alternatives Considered) — the approved contract's own
Testing Requirements name "Corrupted file tests" as a category distinct
from "Version compatibility tests," and the two failures need
independently identifiable causes for a caller to act on correctly.

**`IImportService.ImportAsync` validates every section's compatibility
before importing any of them.** A single incompatible section anywhere
in a multi-source artifact throws `IncompatibleExportSchemaException`
before a single `IImportable.ImportAsync` is ever invoked — satisfying
`Platform Service Contracts.md`'s own Failure Behaviour requirement
("never attempts a best-effort partial import of an incompatible
artifact") exactly, including for artifacts with more than one section.
Schema-version compatibility is checked by exact equality
(`section.SchemaVersion != importable.SchemaVersion`), not a
greater-than/less-than range — the approved contract's own Versioning
Policy states an incompatible version must be rejected outright, with no
upgrade or downgrade path; a future release may add one explicitly, as
its own ADR-worthy decision (`Platform Service Contracts.md`'s own Future
Extension Points already names this).

**Cross-service integration is demonstrated at the sample-module layer,
never inside `IExportService`/`IImportService` themselves.**
`ExportImportSampleModule` registers two Settings-backed
`SettingExportImportAdapter` instances (each implementing `IExportable`,
`IExportableKind`, and `IImportable` together, reading and writing
through `ISettingsProvider` only) and two commands whose handlers check
`IPermissionEvaluator.HasPermission` (Identity) before exporting or
importing, record the action through `IAuditRecorder` (Audit), and
publish a completion notice through `INotificationDispatcher`
(Notifications) — none of which `ExportService`/`ImportService`
themselves reference. **Persistence is deliberately not consumed
anywhere** — Export/Import's own approved contract states "Persistence
Requirements: None," and no sample component was built to use it
speculatively, mirroring `ADR-0040`'s own identical disclosure for
Reporting. **Reporting is deliberately not exported either** — a
`ReportResult`'s own bytes are explicitly not guaranteed round-trip-safe
(`ADR-0040`), so wrapping one in an `IExportable` would misrepresent it
as portable, versioned artifact data; Settings alone is sufficient to
demonstrate a genuine, testable multi-source round trip. See this Work
Package's own Platform Integration Demonstration for the complete,
per-service account.

## Consequences

**Positive:**

- Every approved interface is implemented with zero deviation, so any
  future consumer can depend on `IExportable`/`IExportService`/
  `IImportService` with full confidence in their shape.
- The additive `IExportableKind`/`IImportable`/`IExportFormat`/
  `IExportPayloadSerializer` elaboration gives every future
  `IExportable` a ready-to-use, reusable framing and serialization
  mechanism today, without constraining a future source's own design (a
  source may ignore both and write its own bytes directly).
- `ImportService`'s dual-registration pattern is now a second concrete
  precedent (after `CurrentPrincipalAccessor`) for "a privileged
  registrant needs a capability the approved public interface
  deliberately does not expose" — a reusable resolution for any future
  Work Package that hits the same shape of problem.
- The "validate every section before importing any" discipline is
  proven directly by a dedicated test (`ImportAsync_OneOfMultipleSectionsIsIncompatible_NoSectionIsImported`),
  not merely asserted in documentation.
- The cross-service integration pattern (permission check, audit record,
  notification, all at the calling layer; Persistence and Reporting both
  disclosed as deliberately unused) is now a concrete, tested precedent
  any future Export/Import consumer can copy directly.

**Negative:**

- A future `IExportable` source wanting its own artifact section
  identified stably across process restarts or renames must implement
  `IExportableKind` explicitly — a source relying on the runtime-type-name
  fallback will silently break its own round trip if that type is ever
  renamed. Disclosed, not hidden, in `IExportableKind`'s own remarks.
- No compression or encryption of exported artifact content exists yet —
  a real, disclosed limitation matching the approved contract's own
  Future Extension Points, not an oversight.
- Schema-version compatibility is strict equality only — a future
  release wanting a genuine upgrade/migration path (e.g., importing an
  older, still-readable schema version) must design that explicitly, as
  its own ADR-worthy decision, rather than assuming today's reject-only
  behaviour will silently start accepting older versions.

## Alternatives Considered

**Folding Export/Import into Persistence, treating a raw
`IPersistenceStore` dump as the export format.** Rejected per `Required
ADRs.md`'s own anticipated decision — this is exactly `ADR-0051`'s own
named alternative: it would couple a user-facing, versioned, portable
artifact's format to an internal storage implementation detail, breaking
the moment Persistence's own internal representation changes for
unrelated reasons.

**Adding a `Kind` property directly to `IExportable`, and a destinations
parameter directly to `IImportService.ImportAsync`.** Rejected — neither
was ever drafted with these members; per this session's own established
"additive elaboration over approved-interface modification" convention
(`IReportTemplate`, `IRole`/`IIdentityService`, `SettingDefinition`,
`IPlatformNotification`), the correct resolution is new, optional,
additive types, never a change to an already-approved interface's own
shape.

**Resolving `IEnumerable<IImportable>` via ordinary DI constructor
injection**, letting the container aggregate every registered
`IImportable` automatically. Rejected after directly inspecting
`TempestServiceProvider`'s own resolution model
(`Dictionary<Type, ServiceDescriptor>`, one registration per type,
a second registration under the same type silently overwriting the
first) — this container has no multi-registration/collection-resolution
mechanism at all, so this alternative was never actually available, not
merely undesirable.

**Overloading `IncompatibleExportSchemaException` for a corrupted or
malformed artifact**, rather than introducing `CorruptedExportArtifactException`.
Rejected — the approved contract's own Testing Requirements name
"Corrupted file tests" as a category distinct from "Version compatibility
tests"; conflating "this artifact is well-formed but I don't support its
version" with "this artifact isn't a valid artifact at all" would leave a
caller unable to distinguish a legitimately incompatible-but-genuine
export from a truncated download or a bit-flipped file.

**Building a genuine schema-upgrade/migration path now** (silently
accepting an older, still-readable schema version). Rejected — the
approved contract's own Future Extension Points name this explicitly as
a future capability, not a `WP 6.7`-own requirement; building it
speculatively, with no real prior schema version to migrate from, would
be exactly the kind of premature capability this project's own
conventions warn against.

**Exporting `ReportResult` through a `ReportingService`-owned
`IExportable` wrapper**, to demonstrate Reporting integration alongside
Settings. Rejected — `ADR-0040`'s own orthogonality decision states a
`ReportResult` is explicitly not guaranteed round-trip-safe; presenting
one as portable, versioned export data would contradict that ADR and
Export/Import's own approved Versioning Policy simultaneously.

## Related Documents

`docs/releases/v0.6.0/Required ADRs.md` (this decision's own anticipated
form); `Platform Service Contracts.md` (Export/Import's own 15-dimension
contract this ADR implements); `ADR-0009` (Composition Root /
container-constructed singleton pattern, confirmed to scale to a tenth
service); `ADR-0040` (Reporting's own orthogonality decision and
`IReportTemplate` precedent this ADR mirrors); `ADR-0044`
(`CurrentPrincipalAccessor`'s own dual-registration precedent, reused
here for `ImportService`); `ADR-0045` (Audit, whose own attribution
convention this Work Package's sample handlers reuse unchanged);
`ADR-0046` (Notifications, whose own Security Considerations this
ADR's notification design honours); `WP6.7 Implementation Report.md`;
`WP6.7 Engineering Review Report.md`; `WP6.7 Platform Integration
Demonstration.md`; `docs/academy/03 Work
Packages/WP6.7-export-import-implementation.md`.
