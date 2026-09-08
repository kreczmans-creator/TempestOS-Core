# WP 6.7 — Export/Import — Platform Integration Demonstration

## Purpose

Demonstrate the Export/Import Framework interacting with existing
Platform Services — explicitly required by this Work Package's own
brief as a distinct deliverable, naming Identity, Settings, Persistence,
Audit, Notifications, and Reporting as the six services to assess, the
largest such list of any Work Package this release. For each, this
document records: whether it was used, its purpose, the coupling
rationale, and its plausible future consumers.

## How to Read This Document

**`ExportImportSampleModule` — this Work Package's own reference
module — registers two `SettingExportImportAdapter` instances (each
implementing `IExportable`, `IExportableKind`, and `IImportable`
together) and two commands whose handlers contain only a permission
check, a call into `IExportService`/`IImportService`, an Audit record,
and a Notification publish.** Every platform-service interaction below
happens either inside those command handlers (Identity, Audit,
Notifications) or inside `SettingExportImportAdapter` itself (Settings).
Persistence and Reporting are both **deliberately not consumed
anywhere** in this Work Package's own code — disclosed explicitly below,
not silently omitted.

## The Two Commands This Work Package Ships

`ExportSampleDataCommand` → exports both sample settings
(`sample.exportimport.greeting`, `sample.exportimport.subtitle`) as a
single, two-section artifact, requiring
`ExportImportSampleModule.ExportPermissionKey`
(`"exportimport.export"`). `ImportSampleDataCommand` → re-imports the
most recently exported artifact, requiring
`ExportImportSampleModule.ImportPermissionKey`
(`"exportimport.import"`).

## Identity & Permissions

**Used?** Yes — inside both command handlers, not inside
`Tempest.Core.ExportImport` itself.

**Purpose.** `ExportSampleDataCommandHandler`/`ImportSampleDataCommandHandler`
read the current principal via `ICurrentPrincipalAccessor`, then check
`ExportPermissionKey`/`ImportPermissionKey` respectively via
`IPermissionEvaluator.HasPermission` before calling into
`IExportService`/`IImportService`.

**Coupling rationale.** `Platform Service Contracts.md`'s own Security
Considerations for Export/Import state the enforcement point is the
caller, not the service — mirroring Reporting's (`ADR-0040`) and the
REST API's own established convention. `IExportService`/`IImportService`
themselves have zero dependency on `Tempest.Core.Identity`, confirmed
directly by inspecting every `using` directive in
`src/Tempest.Core/ExportImport/`.

**Future consumers.** Any future module wanting export/import
capability follows this identical pattern — check a permission at the
calling layer, then call the unchanged `IExportService`/`IImportService`.

## Settings

**Used?** Yes — the primary, practical integration point this Work
Package's own brief and `Platform Service Implementation Order.md` both
named.

**Purpose.** `SettingExportImportAdapter` reads a setting's current
value via `ISettingsProvider.GetValueAsync` on export, and writes it
back via `ISettingsProvider.SetValueAsync` on import — using
`IExportPayloadSerializer` internally to turn the value into bytes and
back. Two instances (one per sample setting) are exported together as a
single, multi-source artifact, and both are dispatched back to Settings
correctly on import, proven by a dedicated round-trip integration test.

**Coupling rationale.** `IExportable`/`IImportable` are the approved/
additive contracts through which any service's own data becomes
exportable — `SettingExportImportAdapter` is an ordinary,
externally-owned adapter, not a change to `ISettingsProvider` itself.
`Tempest.Core.ExportImport` itself has zero dependency on
`Tempest.Core.Settings` — confirmed directly.

**Future consumers.** Any future setting, or any future service
exposing its own data through the identical `IExportable`/`IImportable`
pattern, works identically with zero Export/Import-specific
accommodation required.

## Persistence

**Used?** **No — deliberately, matching the approved contract's own
Persistence Requirements: "None."**

**Purpose.** N/A.

**Coupling rationale.** `ADR-0051`'s own orthogonality decision states
explicitly: Export/Import reads *from* whatever service owns the
exported data (here, Settings, via its own public interface), never
from `IPersistenceStore` directly. Building Export/Import on top of a
raw `IPersistenceStore` dump was considered and rejected — it would
couple a user-facing, versioned, portable artifact's format to an
internal storage implementation detail, breaking the moment
Persistence's own internal representation changes for unrelated
reasons. No sample component was built to use it speculatively.

**Future consumers.** None anticipated — this is a permanent
architectural boundary, not a temporary scope limitation.

## Audit

**Used?** Yes — inside both command handlers, independently.

**Purpose.** `ExportSampleDataCommandHandler` records
`ExportImportSampleModule.ExportedActionName`
(`"exportimport.exported"`), with `SourceCount`/`ByteLength` in
`Detail`; `ImportSampleDataCommandHandler` records
`ImportedActionName` (`"exportimport.imported"`), with `ByteLength` in
`Detail` — both through the ordinary, unmodified `IAuditRecorder`,
exactly as every other sample module's own command handlers do.

**Coupling rationale.** Not a core-level dependency of
`Tempest.Core.ExportImport` itself (unlike the REST API's own Identity/
Audit precedent) — Export/Import's own approved contract states no
audit requirement of its own; recording is an ordinary calling-layer
decision, matching Reporting's own established convention.

**Future consumers.** Any future export/import-capable module can
record its own action the same way, with no interface change required.

## Notifications

**Used?** Yes — inside both command handlers, independently.

**Purpose.** Each handler publishes an `IPlatformNotification` under
`ExportImportSampleModule.NotificationCategory` (`"ExportImport"`) at
`NotificationSeverity.Success` on completion — a fixed,
non-identifying message, never the exported/imported artifact's own
content, mirroring `WP 6.0`'s own established Security Considerations
precedent for exactly this scenario.

**Coupling rationale.** `Tempest.Core.ExportImport` itself has zero
dependency on `Tempest.Core.Notifications` — confirmed directly. The
same "the core service never needs to know" pattern as Settings, above.

**Future consumers.** Any future export/import-capable module can
publish its own completion notice identically; a future UI Shell could
subscribe to `"ExportImport"` notifications to show a toast on
completion.

## Reporting

**Used?** **No — deliberately, not merely omitted for lack of time.**

**Purpose.** N/A.

**Coupling rationale.** `ADR-0040`'s own orthogonality decision states a
`ReportResult`'s own `ContentType`/`Content` bytes are explicitly not
guaranteed round-trip-safe — a report is presentation-oriented output,
not portable, versioned artifact data. Wrapping `ReportResult` in an
`IExportable` was considered and rejected (see `ADR-0051`'s own
Alternatives Considered) — doing so would misrepresent lossy,
presentation-oriented output as a round-trip-safe export, directly
contradicting `ADR-0040`.

**Future consumers.** None anticipated for `ReportResult` itself; a
future report *definition*'s own underlying data (distinct from its
rendered output) could plausibly become exportable through its own
dedicated `IExportable` adapter, exactly like `SettingExportImportAdapter`
does for Settings — not built here, since no concrete report definition
currently has data worth exporting independent of its own rendered
form.

## Summary Table

| Service | Used? | Where | Coupling Rationale | Future Consumers |
|---|---|---|---|---|
| Identity & Permissions | Yes | Inside both command handlers, not `Tempest.Core.ExportImport` | Enforcement point is the caller, mirroring Reporting/REST API | Every future export/import-capable module |
| Settings | Yes | Inside `SettingExportImportAdapter` | The primary, practical `IExportable`/`IImportable` integration point this release names | Any future setting or service exposing data through the same pattern |
| Persistence | **No** | N/A | `ADR-0051`'s own orthogonality decision — Export/Import never touches `IPersistenceStore` directly | None anticipated — a permanent boundary |
| Audit | Yes, twice | Inside both command handlers, independently | Ordinary calling-layer recording, mirroring Reporting's own convention | Any future export/import-capable module |
| Notifications | Yes, twice | Inside both command handlers, independently | The core service never needs to know; fixed, non-identifying completion message | Any future module; a future UI Shell notification centre |
| Reporting | **No** | N/A | `ADR-0040`'s own round-trip-safety disclosure — a `ReportResult` is not portable, versioned data | A future report *definition's* own data, not its rendered output |

## Related Documents

`WP6.7 Implementation Report.md`; `WP6.7 Engineering Review Report.md`;
`WP6.7 Platform Impact Assessment.md`; `WP6.7 Lessons Learned.md`;
`WP6.7 Technical Debt Assessment.md`; `WP6.7 Future Capability
Recommendations.md`; `ADR-0040`; `ADR-0051`; `docs/releases/v0.6.0/
Platform Service Contracts.md` (Export/Import's own contract);
`WP6.0 Platform Integration Demonstration.md`, `WP6.3 Platform
Integration Demonstration.md` (the precedents this document's own
format follows).
