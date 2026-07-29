# WP 6.3 — REST API — Platform Integration Demonstration

## Purpose

Demonstrate each endpoint consuming Platform Services without
introducing business logic — explicitly required by this Work
Package's own brief as a distinct deliverable. For each platform
service named in this Work Package's own Platform Integration
instruction (Identity, Settings, Audit, Notifications, Reporting), this
document records: whether it was used, its purpose, the coupling
rationale, and its plausible future consumers.

## How to Read This Document

**`ApiSampleModule` — this Work Package's own reference module —
contains exactly one line of logic: a `MapCommand` call.** Every
platform-service interaction below happens either inside
`Tempest.Core.Api` itself (Identity, Audit — genuine, approved,
core-level dependencies) or inside `ReportingSampleModule`'s own
already-existing command handler (Settings, Notifications, Reporting —
consumed by a *different* Work Package's own module, exposed over HTTP
without any REST-specific code touching them at all). This is the
clearest possible demonstration that the REST API introduces no
business logic of its own: the endpoint this document walks through
is, in its entirety, one route mapped to one already-registered
command.

## The One Endpoint This Work Package Ships

`POST /api/v1/sample-report` → `ReportingSampleModule.GenerateSampleReportCommandId`,
requiring `ReportingSampleModule.GenerateReportPermissionKey`
(`"reporting.generate"`).

## Identity & Permissions

**Used?** Yes — inside `Tempest.Core.Api` itself, a genuine, approved,
core-level dependency.

**Purpose.** `ApiRequestHandler` extracts a claimed identity id from
the `X-Identity-Id` request header, resolves an `IPrincipal` via the
pure, non-mutating `IIdentityService.GetPrincipal`, and checks the
matched route's own `RequiredPermission` via
`IPermissionEvaluator.HasPermission` before dispatching anything.

**Coupling rationale.** `Platform Service Contracts.md`'s own
Responsibilities dimension for the REST API states explicitly:
"authorize each request via Identity & Permissions before dispatch."
This is not optional integration — it is the approved contract's own
core responsibility, hence a genuine constructor dependency of
`ApiRequestHandler` itself, unlike Reporting's own precedent of keeping
every cross-service dependency at the calling layer.

**Future consumers.** Any future REST route this platform ships
inherits this identical enforcement automatically — every route
registered via `MapCommand` requires a permission, checked the same
way, with no per-route special-casing possible or needed.

## Settings

**Used?** Yes — but entirely inside `ReportingSampleModule`'s own
renderer (`WP 6.0`), not inside `Tempest.Core.Api` or `ApiSampleModule`
at all.

**Purpose.** `SampleSummaryReportRenderer` (`WP 6.0`) reads a
Settings-provided greeting to customise its own rendered report
content. The REST API's own route simply invokes the command that
happens to call this renderer — it has no awareness that Settings is
involved anywhere in the chain.

**Coupling rationale.** `Tempest.Core.Api` itself has zero dependency
on `Tempest.Core.Settings` — confirmed directly by inspecting every
`using` directive in `src/Tempest.Core/Api/`. The REST API's own design
principle ("expose Platform Services... no business logic in
controllers/endpoints") is satisfied precisely because it never needs
to know what a command's own handler depends on.

**Future consumers.** Any future REST-exposed command that itself
depends on Settings will work identically, with zero REST-specific
accommodation required.

## Audit

**Used?** Yes, twice, independently — once inside `Tempest.Core.Api`
itself, once inside `ReportingSampleModule`'s own command handler
(`WP 6.0`).

**Purpose.** `ApiRequestHandler` records every authorized request
(`ApiRequestHandler.RequestAuditAction`, `"api.request"`) through
`IAuditRecorder`, carrying the resolved caller identity explicitly in
`Detail[CallerIdentityId]` — see `ADR-0052` for why this is not
ambient-principal auto-attribution. Independently,
`GenerateSampleReportCommandHandler` (`WP 6.0`) records its own
`"report.generated"` action the same way it always has, regardless of
whether it was invoked via REST, a test, or any other caller.

**Coupling rationale.** `Platform Service Contracts.md`'s own Logging
Requirements for the REST API state explicitly: "this log is also the
natural first input to Audit... though the REST API should call
`IAuditRecorder` explicitly for that." This is a second genuine,
approved, core-level dependency of `ApiRequestHandler` itself, alongside
Identity.

**Future consumers.** Any future REST route automatically gains an
`"api.request"` audit trail, with the real caller identity preserved
correctly in `Detail`, regardless of whether the invoked command's own
handler also performs its own, independent audit recording.

## Notifications

**Used?** Yes — but entirely inside `GenerateSampleReportCommandHandler`
(`WP 6.0`), not inside `Tempest.Core.Api` or `ApiSampleModule` at all.

**Purpose.** The invoked command publishes a completion notice through
`INotificationDispatcher` exactly as it always has — the REST API's own
route mapping has no awareness this happens.

**Coupling rationale.** `Tempest.Core.Api` itself has zero dependency
on `Tempest.Core.Notifications` — confirmed directly. This is the same
"the REST layer never needs to know" pattern as Settings, above.

**Future consumers.** Any future REST-exposed command that publishes a
notification will work identically; `Platform Service Contracts.md`'s
own Future Extension Points for the REST API name "webhook/callback
support (a plausible Notifications integration)" as a genuinely
different, not-yet-built future capability — see `WP6.3 Future
Capability Recommendations.md`.

## Reporting

**Used?** Yes — the entire point of this Work Package's own one
shipped endpoint.

**Purpose.** `POST /api/v1/sample-report` invokes
`ReportingSampleModule.GenerateSampleReportCommandId`, which itself
calls `IReportingService.GenerateAsync`.

**Coupling rationale.** `Tempest.Core.Api` itself has zero dependency
on `Tempest.Core.Reporting` — the coupling exists entirely as "this
route's own configured `commandId` happens to invoke a command that
uses Reporting," precisely the same arm's-length relationship the REST
API has with every platform service except Identity and Audit.

**Future consumers.** Any future report definition, once registered
with its own command, can be exposed over HTTP with a single
`MapCommand` call and no other code.

## Summary Table

| Service | Used? | Where | Coupling Rationale | Future Consumers |
|---|---|---|---|---|
| Identity & Permissions | Yes | Inside `Tempest.Core.Api` itself | Approved contract's own core responsibility ("authorize each request... before dispatch") | Every future REST route, automatically |
| Settings | Yes | Inside `ReportingSampleModule`'s own renderer, not the REST API | The REST API has no awareness Settings is involved | Any future REST-exposed command depending on Settings |
| Audit | Yes, twice | Inside `Tempest.Core.Api` itself, and independently inside `ReportingSampleModule`'s own handler | Approved contract's own Logging Requirement ("the REST API should call `IAuditRecorder` explicitly") | Every future REST route (its own `api.request` entry) plus whatever the invoked command itself records |
| Notifications | Yes | Inside `ReportingSampleModule`'s own handler, not the REST API | The REST API has no awareness Notifications is involved | Any future REST-exposed command that publishes a notification; a future webhook/callback capability |
| Reporting | Yes | The entire point of this Work Package's own shipped endpoint | The REST API's own route configuration names a `commandId`; no direct dependency on `Tempest.Core.Reporting` | Any future report definition exposed over HTTP |

## Related Documents

`WP6.3 Implementation Report.md`; `WP6.3 Engineering Review Report.md`;
`WP6.3 Platform Impact Assessment.md`; `WP6.3 Lessons Learned.md`;
`WP6.3 Technical Debt Assessment.md`; `WP6.3 Future Capability
Recommendations.md`; `ADR-0047`; `ADR-0048`; `ADR-0052`;
`docs/releases/v0.6.0/Platform Service Contracts.md` (the REST API's
own contract); `WP6.0 Platform Integration Demonstration.md` (the
precedent this document's own format follows).
