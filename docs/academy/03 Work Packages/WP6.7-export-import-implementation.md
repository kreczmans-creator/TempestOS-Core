# WP 6.7 — Export/Import Framework Implementation

## 1. Introduction

WP 6.7 delivers the Export/Import Framework — the seventh Work Package
of the Platform Services phase (`v0.6.0`) to ship real code, and the
sixth of those seven to be sequenced ahead of its own nominal numeric
position, per `Platform Service Implementation Order.md`'s own explicit
recommendation. Implemented in a single pass, directly against the
already-approved architecture and Contract Review packages — no
separate architecture phase, mirroring every one of its six
predecessors. This Work Package completes the Reporting/Export/Import
orthogonality question `ADR-0040` first raised, and resolves a genuine
gap the approved interface catalogue itself left open: how a
single-`Stream`, no-destination-parameter `IImportService.ImportAsync`
can plausibly "read... back into the owning service(s)" — plural.

## 2. Purpose

To build `Tempest.Core.ExportImport` exactly as the approved
architecture specified — `IExportable`, `IExportService`,
`IImportService`, `ExportImportException`,
`IncompatibleExportSchemaException` — as a thin, user-facing,
`Stream`-based, portable-artifact I/O layer, explicitly distinct from
the internal `IPersistenceStore` abstraction; to resolve `Required
ADRs.md`'s own `ADR-0051` orthogonality question; and to design a
routing mechanism that lets a single artifact carry more than one
source's own data back to more than one owning service, without
changing any approved interface's own shape.

## 3. Background

`WP 6.1` (Permissions & Identity), `WP 6.4` (Settings Framework), `WP
6.5` (Audit Framework), `WP 6.2` (Notification Framework), `WP 6.0`
(Reporting Framework), and `WP 6.3` (REST API) were all already
implemented. `Platform Service Implementation Order.md`'s own Export/
Import entry named Settings (and, optionally, Reporting) as the
practical integration point worth having before building something
"testable rather than speculative" — both existed. `ADR-0040`'s own
Reporting-orthogonality decision had already settled, in principle,
that Export/Import must never be built as a duplicate of Reporting's
own output mechanism; this Work Package is where that principle gets
tested against a real, working implementation for the first time.

## 4. The Problem

Three things needed resolving, one of which the approved contract
itself left genuinely open:

1. **A versioned, round-trip-safe artifact format for arbitrary
   platform data** — nothing in this codebase writes a portable,
   schema-versioned export today; `IPersistenceStore` is internal,
   format-coupled state, explicitly the wrong tool per `ADR-0051`.
2. **A way for one artifact to carry more than one source's own data,
   and for import to route each section back to the right owning
   service** — the approved `IImportService.ImportAsync(Stream source,
   CancellationToken)` signature carries no destination parameter at
   all, yet the contract's own Responsibilities and Testing
   Requirements both name multi-source, multi-destination round trips
   as required behaviour.
3. **"Serialization abstraction" and "Format abstraction"** — named in
   this Work Package's own brief as implementation scope, present in
   neither `IExportable` nor `IExportService`/`IImportService`'s own
   approved shape.

## 5. The Design

**Implemented exactly as drafted, zero signature deviation:**
`IExportable.SchemaVersion`/`ExportAsync`,
`IExportService.ExportAsync(Stream, IReadOnlyList<IExportable>, ...)`,
`IImportService.ImportAsync(Stream, ...)`. `ExportImportException` is a
concrete, base-plus-subtype type (not abstract, mirroring
`ReportingException`'s/`ApiException`'s own established convention),
with `IncompatibleExportSchemaException` as its one approved subtype.

**Kind-based routing, solving problem 2 additively (`ADR-0051`):**
`IExportableKind` is a new, optional companion to `IExportable` — a
source implementing it tags its own artifact section with a stable
`Kind`; a source that does not falls back to its own runtime type name.
`IImportable` is the new read-back counterpart, registered ahead of
time (typically during Module Initialisation) via a concrete-type-only
`ImportService.RegisterImportable` method — not part of `IImportService`
itself. `ImportService` is dual-registered in `TempestHost` under both
its own concrete type and `IImportService`, the exact same
already-constructed instance under two service-type keys, mirroring
`ADR-0044`'s own `CurrentPrincipalAccessor` precedent precisely: a
module needing `RegisterImportable` resolves the concrete type, while
every ordinary consumer resolves only the read-only interface.
`ImportAsync` validates every section's `Kind` and `SchemaVersion`
*before* importing any of them — an incompatible section anywhere in a
multi-source artifact aborts the whole call before a single
`IImportable.ImportAsync` runs, satisfying the approved contract's own
"never a best-effort partial import" requirement exactly, proven
directly by a dedicated test.

**Format and Serialization abstractions, solving problem 3 additively:**
`IExportFormat`/`JsonExportFormat` frames one or more already-written,
opaque `ExportSection`s (`Kind` + `SchemaVersion` + raw bytes) into a
single JSON-array artifact — used only by `ExportService`/
`ImportService`'s own internal orchestration, with zero awareness of
what is inside any one section's own payload. `IExportPayloadSerializer`/
`JsonExportPayloadSerializer` is a separate, entirely optional
abstraction a specific `IExportable`/`IImportable` pair may use
internally to turn its own key/value data into bytes — unknown to
`ExportService`/`ImportService`, mirroring `IReportTemplate<T>`'s own
optional-collaborator precedent from `WP 6.0` exactly. A malformed or
truncated artifact is rejected as the new, additive
`CorruptedExportArtifactException`, kept structurally distinct from
`IncompatibleExportSchemaException` — a well-formed-but-unsupported
artifact and a corrupted file are different failure modes, and the
approved contract names "Corrupted file tests" as its own testing
category.

**Cross-service integration, entirely at the sample-module layer:**
`ExportImportSampleModule` registers two Settings-backed
`SettingExportImportAdapter` instances — one class implementing
`IExportable`, `IExportableKind`, and `IImportable` together — and two
commands whose handlers check a permission (Identity), export or
import (Export/Import), record the action (Audit), and publish a
completion notice (Notifications). Persistence is deliberately not
consumed anywhere (the approved contract states "Persistence
Requirements: None"); Reporting is deliberately not exported either,
since a `ReportResult`'s own bytes are explicitly not guaranteed
round-trip-safe (`ADR-0040`).

## 6. Alternatives Considered

See `ADR-0051` for the complete reasoning. In summary: building
Export/Import directly on `IPersistenceStore` was rejected per
`Required ADRs.md`'s own anticipated decision (it would couple a
portable artifact's format to an internal storage detail); adding a
`Kind` property directly to `IExportable` and a destinations parameter
directly to `IImportService.ImportAsync` was rejected as an
unauthorised change to already-approved interfaces; resolving
`IEnumerable<IImportable>` via ordinary DI constructor injection was
rejected after directly inspecting `TempestServiceProvider`'s own
resolution model and confirming it supports exactly one registration
per type, with no collection-resolution mechanism at all; overloading
`IncompatibleExportSchemaException` for a corrupted artifact was
rejected as conflating two genuinely different failure modes the
approved contract's own Testing Requirements treat as distinct
categories; and exporting `ReportResult` through a `ReportingService`-owned
`IExportable` wrapper was rejected as directly contradicting
`ADR-0040`'s own round-trip-safety disclosure.

## 7. Why This Solution Was Chosen

Every approved interface is implemented with zero deviation, so any
future consumer can depend on `IExportable`/`IExportService`/
`IImportService` with full confidence in their shape. The Kind-routing
mechanism resolves a genuine gap in the approved contract using a
pattern (concrete-type dual registration) this codebase had already
proven once, rather than inventing a new mechanism or quietly changing
an approved interface. Keeping Format and Serialization as two
separate, optional abstractions — rather than one, or none — gives a
future `IExportable` author a ready-to-use envelope mechanism and a
ready-to-use payload mechanism independently, without forcing either on
a source that would rather write its own bytes directly.

## 8. Architectural Principles

- **A Gap in an Approved Interface Is Filled by a New Type, Never a
  Change to the Approved One** — `IExportableKind`/`IImportable`/
  `IExportFormat`/`IExportPayloadSerializer` all exist because
  `IExportable`/`IExportService`/`IImportService` themselves stayed
  exactly as drafted.
- **Reuse a Proven Resolution Pattern Before Inventing a New One** —
  `ImportService`'s dual registration is `ADR-0044`'s own
  `CurrentPrincipalAccessor` pattern, reapplied to a structurally
  identical problem (a privileged registrant needs a capability the
  approved public interface deliberately does not expose).
- **Validate Everything Before Committing Anything** — a multi-section
  import either fully succeeds or fully fails; no section is ever
  applied speculatively ahead of the others' own compatibility check.
- **Two Distinct Failure Modes Deserve Two Distinct Exception Types** —
  "this artifact's version isn't supported" and "this artifact isn't
  well-formed at all" are different facts a caller needs to tell apart.

## 9. Files Added

`src/Tempest.Core/ExportImport/IExportable.cs`;
`src/Tempest.Core/ExportImport/IExportService.cs`;
`src/Tempest.Core/ExportImport/IImportService.cs`;
`src/Tempest.Core/ExportImport/ExportImportException.cs`;
`src/Tempest.Core/ExportImport/IncompatibleExportSchemaException.cs`;
`src/Tempest.Core/ExportImport/IExportableKind.cs`;
`src/Tempest.Core/ExportImport/IImportable.cs`;
`src/Tempest.Core/ExportImport/ExportSection.cs`;
`src/Tempest.Core/ExportImport/IExportFormat.cs`;
`src/Tempest.Core/ExportImport/JsonExportFormat.cs`;
`src/Tempest.Core/ExportImport/IExportPayloadSerializer.cs`;
`src/Tempest.Core/ExportImport/JsonExportPayloadSerializer.cs`;
`src/Tempest.Core/ExportImport/ExportService.cs`;
`src/Tempest.Core/ExportImport/ImportService.cs`;
`src/Tempest.Core/ExportImport/CorruptedExportArtifactException.cs`;
`src/Tempest.Core/ExportImport/DuplicateImportableKindException.cs`;
`src/Samples/Tempest.Samples/ExportImportSampleModule.cs`;
`src/Samples/Tempest.Samples/SettingExportImportAdapter.cs`;
`src/Samples/Tempest.Samples/SampleExportArtifactStore.cs`;
`src/Samples/Tempest.Samples/ExportSampleDataCommand.cs`;
`src/Samples/Tempest.Samples/ExportSampleDataCommandHandler.cs`;
`src/Samples/Tempest.Samples/ImportSampleDataCommand.cs`;
`src/Samples/Tempest.Samples/ImportSampleDataCommandHandler.cs`;
`tests/Tempest.Core.Tests/ExportImport/ExportImportFixtures.cs`;
`tests/Tempest.Core.Tests/ExportImport/RecordingLevelLogger.cs`;
`tests/Tempest.Core.Tests/ExportImport/ExportServiceTests.cs`;
`tests/Tempest.Core.Tests/ExportImport/ImportServiceTests.cs`;
`tests/Tempest.Core.Tests/ExportImport/JsonExportFormatTests.cs`;
`tests/Tempest.Core.Tests/ExportImport/JsonExportPayloadSerializerTests.cs`;
`tests/Tempest.Core.Tests/ExportImport/ExceptionTests.cs`;
`tests/Tempest.Core.Tests/Samples/ExportImportSampleModuleIntegrationTests.cs`;
`docs/adr/ADR-0051-export-import-is-orthogonal-to-the-internal-persistence-abstraction.md`;
this retrospective. **Modified:**
`src/Tempest.Core/Runtime/TempestHost.cs` (registration only);
`tests/Tempest.Core.Tests/Samples/ClockModuleDiscoveryTests.cs`
(sample-module count, 13 → 14).

## 10. Trade-offs

- **No compression or encryption of exported artifact content**
  (`AT-11`) — matching the approved contract's own Future Extension
  Points; an `IExportable` implementation is individually responsible
  for redacting or refusing to export sensitive content, mirroring how
  Persistence imposes no content-level policy on Settings/Audit.
- **No schema-upgrade/migration path** (`AT-12`) — an incompatible
  schema version is always rejected outright, never upgraded or
  downgraded, matching the approved contract's own Versioning Policy
  exactly; a future release may add one explicitly, as its own
  ADR-worthy decision.

## 11. Common Mistakes

- **Assuming `IExportable.Kind` exists** — it does not; `IExportable`'s
  own approved shape carries only `SchemaVersion`. Kind tagging is
  entirely optional, via the separate, additive `IExportableKind`.
- **Assuming `IImportService` maintains its own registry directly** —
  it does not; `IImportService` itself is a two-method-free, purely
  read-only interface. Registration lives on `ImportService`'s own
  concrete type, resolved separately from the interface.
- **Assuming a schema version "close enough" (e.g., one version lower)
  will import successfully** — compatibility is exact equality only;
  there is no partial-compatibility or upgrade behaviour in this
  release.

## 12. Future Evolution

A genuine schema-upgrade/migration path once a real schema version bump
ships with a concrete backward-compatibility requirement; compression
and/or encryption of exported artifact content once a concrete
deployment scenario names either as a requirement; a Licensing (`WP
6.6`) or future engineering module consumer, exercising Export/Import
against data neither Settings nor this Work Package's own sample module
anticipated — all named explicitly as future, separately-scoped
responsibilities, not designed now.

## 13. Key Takeaways

1. When an approved interface's own signature genuinely cannot support
   a requirement the same contract names elsewhere (here: multi-
   destination import through a single-`Stream`, no-destination
   method), the resolution is an additive registration mechanism on the
   concrete type, not a change to the approved interface — and if a
   prior Work Package already solved a structurally identical problem
   (`ADR-0044`'s dual registration), reuse that pattern rather than
   inventing a new one.
2. Before assuming a container can resolve "every registered X" as a
   collection, check what the container actually supports — this
   codebase's own custom DI container has exactly one registration per
   type, confirmed by direct inspection of `TempestServiceProvider`,
   not assumed from familiarity with other, more featureful containers.
3. Two failure modes that feel similar in prose ("this artifact isn't
   supported") can still deserve two different exception types when a
   caller genuinely needs to react differently — a version mismatch is
   recoverable information (try a different platform version); a
   corrupted file is not (the artifact itself needs to be re-obtained).

## Architectural Debt Assessment

`docs/governance/Quality/Technical Debt Register.md` gained two new
trade-offs (`AT-11`, `AT-12`); no existing Technical Debt item required
annotation — Export/Import introduces no instance of any previously-
tracked gap.

## Observations

This Work Package's own repository review, re-deriving every touched
register directly, found three further genuine, pre-existing
governance-documentation drifts, none related to its own scope:
`docs/architecture/Platform Service Map.md`'s own Audit and
Notifications "Consumers" entries had read "none yet implemented" since
before `WP 6.0` first shipped a real consumer of each — corrected here.
More substantially, `Interface Register.md`, `Dependency Injection
Register.md`, and `Module Register.md` had each gone stale since `WP
5.2`, missing every public interface, DI registration, and sample
module `WP 6.1` through `WP 6.3` added (23 interfaces, 10 registration
call sites, and 6 modules, respectively) — each register's own Coverage
Status is corrected from "Complete" to "Partial," disclosing the exact
gap rather than silently continuing to claim completeness, with only
this Work Package's own new entries added and the larger, six-Work-
Package backfill explicitly left for `WP 6.8` (Platform Services
Integration Review) — the Work Package whose own stated purpose is
exactly this kind of accumulated-drift audit.

## Related Documents

`docs/releases/v0.6.0/Release Architecture.md` and companions (the
architecture package this Work Package implemented); `Platform Service
Contracts.md` and companions (the Contract Review package); `ADR-0040`
(the Reporting orthogonality decision this Work Package completes);
`ADR-0044` (the dual-registration precedent `ImportService` reuses);
`ADR-0051`; `docs/architecture/Platform Service Map.md` (Export/Import
entry, and the Audit/Notifications consumer corrections);
`docs/governance/Engineering/Interface Register.md`,
`Dependency Injection Register.md`, `Module Register.md` (each
disclosed as Partial); `docs/governance/Quality/Technical Debt
Register.md` (`AT-11`, `AT-12`); `docs/academy/03 Work
Packages/WP6.0-reporting-framework-implementation.md`,
`WP6.3-rest-api-implementation.md` (the precedents this Work Package's
own single-pass implementation approach and dual-registration reuse
follow).
