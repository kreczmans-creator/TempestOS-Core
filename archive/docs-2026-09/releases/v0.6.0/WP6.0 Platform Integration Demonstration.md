# WP 6.0 — Reporting Framework — Platform Integration Demonstration

## Purpose

A dedicated record of how the Reporting Framework consumes previously
completed platform services — explicitly required by this Work
Package's own brief as a distinct deliverable from its Implementation
Report, Engineering Review Report, and Platform Impact Assessment. For
each platform service named in this Work Package's own Platform
Integration instruction (Identity, Settings, Persistence, Audit,
Notifications), this document records: whether it was used, its
purpose, the coupling rationale, and its plausible future consumers. A
platform service intentionally not consumed is explained, not silently
omitted.

## How to Read This Document

**No integration lives inside `Tempest.Core.Reporting` itself.**
`IReportingService`/`ReportingService` depend on nothing but
Dependency Injection — confirmed directly by inspecting every `using`
directive in `src/Tempest.Core/Reporting/`. Every platform-service
interaction below is demonstrated at `ReportingSampleModule`'s own
calling layer (`Tempest.Samples`) — its command handler
(`GenerateSampleReportCommandHandler`) and its renderer
(`SampleSummaryReportRenderer`) — exactly matching the approved
contract's own Security Considerations ("the enforcement point is the
caller, not the service").

## Identity & Permissions

**Used?** Yes.

**Purpose.** `GenerateSampleReportCommandHandler` reads the current
principal via `ICurrentPrincipalAccessor` and checks
`ReportingSampleModule.GenerateReportPermissionKey`
(`"reporting.generate"`) via `IPermissionEvaluator.HasPermission` before
calling `IReportingService.GenerateAsync` — a denied check is reported
as an ordinary `CommandResult.Failure`, mirroring
`CheckSamplePermissionCommandHandler`'s own established convention, not
an unhandled `PermissionDeniedException`.

**Coupling rationale.** `Platform Service Contracts.md`'s own Security
Considerations for Reporting state explicitly: "`GenerateAsync` does
not itself check permissions... the enforcement point is the caller,
not the service." This is the concrete realisation of that design —
Reporting itself never references `Tempest.Core.Identity` at all; the
coupling exists entirely in the sample module's own command handler,
which is free to check whatever authorization its own use case
requires.

**Future consumers.** Any future Reporting consumer (an engineering
module, a `WP 6.3` REST API endpoint) that wants permission-gated
report generation should follow this identical pattern — check the
permission in its own calling code, not by modifying
`IReportingService` itself.

## Settings

**Used?** Yes.

**Purpose.** `SampleSummaryReportRenderer` reads a Settings-registered
value (`SampleSummaryReportRenderer.GreetingSettingKey`,
`"sample.reporting.greeting"`) via `ISettingsProvider.GetValueAsync` to
customise its own rendered report content — a renderer-owned
configuration value, not a platform-wide one.

**Coupling rationale.** `Platform Service Contracts.md`'s own
Configuration Requirements for Reporting state explicitly: "None beyond
what a specific renderer implementation may itself require (e.g., a
PDF renderer's own font/template path) — that configuration belongs to
the renderer, not to `IReportingService` itself." This is the concrete
realisation of that design — `ReportingService` itself never references
`Tempest.Core.Settings`; the coupling exists entirely in one specific
renderer's own constructor, as an ordinary peer dependency.

**Future consumers.** Any future renderer implementation needing its
own runtime-configurable value (a template path, a formatting
preference) should depend on `ISettingsProvider` directly, exactly as
`SampleSummaryReportRenderer` does — never by adding a configuration
parameter to `IReportingService` or `IReportRenderer<TDefinition>`
themselves.

## Persistence

**Used?** No — deliberately.

**Purpose.** None. No component in this Work Package's own scope reads
from or writes to `IPersistenceStore`.

**Coupling rationale.** `Platform Service Contracts.md`'s own
Persistence Requirements for Reporting state explicitly: "None.
Reporting is stateless beyond its in-memory registration table; a
generated report is handed back to the caller and is not retained by
the service itself." Building a Persistence dependency into Reporting
or its sample module now would directly contradict this approved,
explicit non-requirement — it would be exactly the kind of speculative
coupling this Work Package's own instructions warn against ("do not
introduce unnecessary coupling; every dependency shall be justified").
This is the first platform service since Persistence was introduced
(`WP 6.4`) with a genuine, approved reason not to depend on it,
demonstrating that Persistence's own reuse (by Settings, `WP 6.4`; by
Audit, `WP 6.5`) is driven by actual need, not applied reflexively to
every new service regardless of whether it stores anything durable.

**Future consumers.** If a future Work Package builds a durable report
history or inbox (`AT-09`'s own revisit trigger), that capability
should be built as a consuming layer over `IPlatformNotification`-style
subscription or a dedicated history service depending on
`IPersistenceStore` directly — not by adding a persistence dependency
to `IReportingService` itself.

## Audit

**Used?** Yes.

**Purpose.** After a successful `GenerateAsync` call,
`GenerateSampleReportCommandHandler` records the action through
`IAuditRecorder.RecordAsync`, using
`ReportingSampleModule.ReportGeneratedActionName`
(`"report.generated"`) with the report's own content type carried in
`Detail`.

**Coupling rationale.** `Platform Service Contracts.md`'s own Audit
Framework entry names Reporting explicitly as "a plausible future
consumer... none yet implemented" — this Work Package makes that
plausibility concrete. Audit's own approved contract requires no
interface change to support this (correlation-style detail is already
carried in `Detail`, per `ADR-0045`'s own precedent); the coupling
exists entirely in the sample module's own command handler, as an
ordinary peer dependency alongside `IReportingService` itself.

**Future consumers.** Any future Reporting consumer that considers
"who generated which report, and when" a compliance-relevant fact
should record it through `IAuditRecorder` at its own calling layer,
exactly as `GenerateSampleReportCommandHandler` does — Reporting itself
will never record this automatically.

## Notifications

**Used?** Yes.

**Purpose.** After recording the audit entry,
`GenerateSampleReportCommandHandler` publishes an `IPlatformNotification`
(`Category = ReportingSampleModule.ReportGeneratedNotificationCategory`,
`"Reporting"`; `Severity = NotificationSeverity.Success`) through
`INotificationDispatcher`, carrying a fixed, non-identifying success
message only.

**Coupling rationale.** `Platform Service Contracts.md`'s own
Notification Framework Security Considerations name this exact
scenario explicitly: "a 'user X's report is ready' notification should
not leak report content to an unauthorized subscriber; the notification
payload should carry only what's safe for any subscriber of that type
to see." This Work Package's own published notification honours that
requirement directly — the full result (content type, byte length) is
returned only to the command's own caller, via `CommandResult`, never
broadcast. `Tempest.Core.Reporting` itself never references
`Tempest.Core.Notifications` — the coupling exists entirely in the
sample module's own command handler.

**Future consumers.** Any future Reporting consumer wanting a
"report ready" notice should follow this identical pattern — publish a
notification carrying only safe, non-identifying content at its own
calling layer, never inside `IReportingService` itself, and never
including report content in the notification payload.

## Summary Table

| Service | Used? | Purpose | Coupling Rationale | Future Consumers |
|---|---|---|---|---|
| Identity & Permissions | Yes | Permission-gate report generation | Contract states the caller is the enforcement point, not the service | Any permission-gated future consumer |
| Settings | Yes | Customise a specific renderer's own output | Contract states renderer-owned configuration belongs to the renderer | Any renderer needing runtime-configurable values |
| Persistence | No | — | Contract states "None"; Reporting is stateless beyond its in-memory registration table | A future history/inbox capability, built as a separate consuming layer |
| Audit | Yes | Record who generated which report, and when | Contract names Reporting as a plausible future Audit consumer | Any consumer treating generation as a compliance-relevant fact |
| Notifications | Yes | Publish a "report generated" completion notice | Contract's own Security Considerations name this exact scenario; payload deliberately carries no report content | Any consumer wanting a "report ready" notice |

## Related Documents

`WP6.0 Implementation Report.md`; `WP6.0 Engineering Review Report.md`;
`WP6.0 Platform Impact Assessment.md`; `WP6.0 Lessons Learned.md`;
`WP6.0 Technical Debt Assessment.md`; `WP6.0 Future Capability
Recommendations.md`; `ADR-0040`; `docs/releases/v0.6.0/Platform Service
Contracts.md` (Reporting's and Notifications' own contracts this
document cross-references).
