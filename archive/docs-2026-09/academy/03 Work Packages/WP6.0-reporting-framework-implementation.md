# WP 6.0 — Reporting Framework Implementation

## 1. Introduction

WP 6.0 delivers the Reporting Framework — the fifth Work Package of the
Platform Services phase (`v0.6.0`) to ship real code, following `WP
6.1`/`WP 6.4`/`WP 6.5`/`WP 6.2`'s own precedent of implementing
directly against the already-approved architecture and Contract Review
packages, no separate architecture phase. Unlike its four predecessors,
`WP 6.0` is the first to match its own nominal numeric position in
`WorkPackages.md` — `Platform Service Implementation Order.md` names it
tied for third priority, with no hard proposed-service dependency
blocking it from proceeding whenever bandwidth allows.

## 2. Purpose

To build `Tempest.Core.Reporting` exactly as the approved architecture
specified — `IReportDefinition`, `IReportRenderer<TDefinition>`,
`IReportingService` — as the single reporting engine every future
TempestOS module can depend on; to resolve the "Template abstraction"
gap the brief named but the architecture package never drafted as an
interface member, without touching any approved interface; and to
assess and, where genuinely justified, implement this Work Package's
own interactions with four already-completed platform services
(Identity, Settings, Persistence, Audit) plus Notifications.

## 3. Background

`WP 6.1` (Permissions & Identity), `WP 6.4` (Settings Framework), `WP
6.5` (Audit Framework), and `WP 6.2` (Notification Framework) were all
already implemented, each ahead of its own nominal order. Reporting has
no hard dependency on any of the five platform services that preceded
it — `Platform Service Implementation Order.md` names it explicitly as
having "no hard proposed-service dependency; can proceed once the team
has bandwidth, independent of `6.1`/`6.4`/`6.6`." `Required ADRs.md`
had already anticipated this Work Package's own core decision in
outline: Reporting is registered as an ordinary DI-public singleton,
and a report is explicitly not guaranteed round-trip-safe or
re-importable, distinguishing it from Export/Import's own future,
versioned contract.

## 4. The Problem

Four things needed to exist:

1. **A single, domain-neutral reporting engine** — nothing in this
   codebase today lets a module register a report definition and its
   own renderer, then generate it by Id, without inventing its own
   ad hoc mechanism.
2. **Orthogonality with Export/Import, not duplication** — Reporting
   and the not-yet-started `WP 6.7` (Export/Import) both "get data out"
   of the platform; without a clear boundary, a future implementer
   could easily blur the two.
3. **A genuine gap between the brief and the approved draft** — the
   brief named "Template abstraction" and "Export abstraction" as
   implementation scope, but `Public Interface Catalogue.md`'s own
   draft gave neither an interface member.
4. **Real, demonstrated cross-service integration** — the brief
   explicitly required assessing and, where justified, implementing
   interactions with five already-completed or same-phase platform
   services, not merely asserting Reporting's own independence from
   each.

## 5. The Design

**Implemented exactly as drafted, zero signature deviation:**
`IReportDefinition` (`Id`, `Name`), `IReportRenderer<TDefinition>.RenderAsync`,
`IReportingService` (`RegisterDefinition`/`GenerateAsync`/`RegisteredDefinitions`),
`ReportRequest`, `ReportResult`. `ReportingException` is a concrete,
base-plus-subtype type (not abstract, despite the catalogue's own
pseudo-code shorthand) — mirroring `SettingsException`/
`IdentityException`/`CommandException`'s own established convention —
with two real subtypes: `DuplicateReportDefinitionException` and
`ReportDefinitionNotFoundException`.

**Registration and dispatch (`ADR-0040`):** an ordinary DI-public,
container-constructed singleton, registered in `TempestHost`'s existing
Platform Services Registered block (Phase 6), immediately after the
Event Bus and before Notifications, matching `Service Registration
Matrix.md`'s own recommended order. `ReportingService` holds
registrations in a single, lock-guarded dictionary keyed by
`IReportDefinition.Id`; `GenerateAsync` looks up the registered renderer
under the lock, then invokes it outside the lock, so two concurrent
generations never block on each other. A renderer's own exception
propagates unmodified — logged at `Warning`, never wrapped — mirroring
the Command Framework's own dispatch failure model (`ADR-0038`), not
the Event Bus's or Notification Dispatcher's own per-subscriber
isolation.

**No internal permission-gating:** `GenerateAsync` does not itself
check permissions — `Platform Service Contracts.md`'s own Security
Considerations state explicitly that the enforcement point is the
caller, mirroring how Navigation and the Command Framework themselves
impose no authorization internally.

**Template abstraction (`ADR-0040`):** `IReportTemplate<TDefinition>` is
a new, additive interface — not a modification to `IReportDefinition`/
`IReportRenderer<TDefinition>`/`IReportingService`. Entirely optional: a
renderer may apply a template internally (an ordinary constructor-
injected collaborator) or render its own output directly.
`PlainTextReportTemplate<TDefinition>` is a concrete, genuinely reusable
general-purpose template shipped alongside the abstraction.

**Export abstraction: deliberately not built.** `ADR-0040`'s own
orthogonality decision means a dedicated export interface inside
`Tempest.Core.Reporting` would duplicate `WP 6.7`'s own future scope.
`ReportResult`'s own `ContentType`/`Content` shape is already
Reporting's own output mechanism — explicitly not guaranteed
round-trip-safe or re-importable.

**Cross-service integration, demonstrated at the calling layer only:**
`ReportingSampleModule` (`Tempest.Samples`, the twelfth production
sample module) registers `SampleSummaryReportDefinition` and its own
renderer (`SampleSummaryReportRenderer`, which reads a Settings-provided
greeting as an ordinary peer dependency and delegates layout to
`PlainTextReportTemplate<SampleSummaryReportDefinition>`), then
registers a command (`GenerateSampleReportCommand`) whose handler
checks a permission via `IPermissionEvaluator` (Identity), generates the
report through `IReportingService`, records the action through
`IAuditRecorder` (Audit), and publishes a completion notice through
`INotificationDispatcher` (Notifications) — carrying only a fixed,
non-identifying success message, per Notifications' own Security
Considerations for exactly this "report is ready" scenario. Persistence
is deliberately not consumed anywhere — the approved contract's own
Persistence Requirements state "None." See this Work Package's own
Platform Integration Demonstration for the complete, per-service
account.

## 6. Alternatives Considered

See `ADR-0040` for the complete reasoning. In summary: folding Reporting
into Export/Import as one combined "data output" service was rejected
per `Required ADRs.md`'s own anticipated decision; building a dedicated
export interface inside Reporting now was rejected as scope encroachment
on `WP 6.7`'s own future Work Package; modifying the three approved
interfaces to add template awareness directly was rejected since the
additive `IReportTemplate<TDefinition>` already satisfies the need
without touching them; having `GenerateAsync` check permissions
internally was rejected since the approved contract names the caller as
the enforcement point; and including report content or a summary in the
completion notification was rejected per Notifications' own Security
Considerations.

## 7. Why This Solution Was Chosen

Every approved interface is implemented with zero deviation, so any
future consumer (an engineering module, the REST API) can depend on
`IReportDefinition`/`IReportRenderer<TDefinition>`/`IReportingService`
with full confidence in their shape. Declining to build an export
abstraction keeps Reporting's own scope boundary exactly where
`Required ADRs.md` anticipated it. The cross-service integration
pattern — permission check, audit record, notification, all at the
calling layer — is now a concrete, tested precedent any future
Reporting consumer can copy directly, rather than each future consumer
inventing its own approach independently.

## 8. Architectural Principles

- **Reuse Before Invention** — the dispatch failure model (`ADR-0038`),
  the permission-enforcement pattern (`ADR-0044`), and the Composition
  Root registration pattern (`ADR-0009`) were all reused directly;
  nothing new was invented where an existing, proven mechanism already
  served.
- **Additive Elaboration Over Approved-Interface Modification** — the
  fifth consecutive Work Package this release to fill a brief-named gap
  (a template abstraction) with a new type rather than reshape an
  already-approved interface.
- **Respect a Sibling Work Package's Own Future Scope** — declining to
  build "Export abstraction" despite the brief naming it, because doing
  so would duplicate `WP 6.7`'s own not-yet-started scope, is a direct
  application of this release's own orthogonality discipline.
- **Demonstrate Integration at the Calling Layer, Not Inside the
  Service** — Reporting's own approved contract already states the
  enforcement point is the caller; this Work Package's own sample
  module proves that pattern concretely rather than leaving it
  theoretical.

## 9. Files Added

`src/Tempest.Core/Reporting/IReportDefinition.cs`;
`src/Tempest.Core/Reporting/IReportRenderer.cs`;
`src/Tempest.Core/Reporting/IReportingService.cs`;
`src/Tempest.Core/Reporting/ReportRequest.cs`;
`src/Tempest.Core/Reporting/ReportResult.cs`;
`src/Tempest.Core/Reporting/ReportingException.cs`;
`src/Tempest.Core/Reporting/DuplicateReportDefinitionException.cs`;
`src/Tempest.Core/Reporting/ReportDefinitionNotFoundException.cs`;
`src/Tempest.Core/Reporting/ReportingService.cs`;
`src/Tempest.Core/Reporting/IReportTemplate.cs`;
`src/Tempest.Core/Reporting/PlainTextReportTemplate.cs`;
`src/Samples/Tempest.Samples/ReportingSampleModule.cs`;
`src/Samples/Tempest.Samples/SampleSummaryReportDefinition.cs`;
`src/Samples/Tempest.Samples/SampleSummaryReportRenderer.cs`;
`src/Samples/Tempest.Samples/GenerateSampleReportCommand.cs`;
`src/Samples/Tempest.Samples/GenerateSampleReportCommandHandler.cs`;
`tests/Tempest.Core.Tests/Reporting/ReportingServiceFixtures.cs`;
`tests/Tempest.Core.Tests/Reporting/RecordingLevelLogger.cs`;
`tests/Tempest.Core.Tests/Reporting/ReportingServiceTests.cs`;
`tests/Tempest.Core.Tests/Reporting/ExceptionTests.cs`;
`tests/Tempest.Core.Tests/Reporting/PlainTextReportTemplateTests.cs`;
`tests/Tempest.Core.Tests/Runtime/ReportingHostRegistrationTests.cs`;
`tests/Tempest.Core.Tests/Samples/ReportingSampleModuleIntegrationTests.cs`;
`docs/adr/ADR-0040-reporting-is-di-public-and-orthogonal-to-export-import.md`;
this retrospective. **Modified:**
`src/Tempest.Core/Runtime/TempestHost.cs` (registration only);
`tests/Tempest.Core.Tests/Samples/ClockModuleDiscoveryTests.cs`
(sample-module count, 11 → 12).

## 10. Trade-offs

- **No delivery-channel abstraction or durable report history exists
  this release** (`AT-09`) — a disclosed, deliberate limitation matching
  the approved contract's own Future Extension Points, not an
  oversight.
- **`ReportingService` never checks permissions itself** — every future
  consumer must remember to enforce authorization at its own calling
  layer; Reporting will never do this automatically, by design.
- **`ReportingSampleModule` deliberately does not use Persistence** —
  disclosed explicitly as matching the approved contract's own
  "Persistence Requirements: None," not an incomplete integration.

## 11. Common Mistakes

- **Assuming `IReportingService.GenerateAsync` enforces its own
  authorization** — it does not; a caller invoking it directly (not
  through a permission-checking command handler) bypasses any
  authorization entirely, by design.
- **Assuming "Export abstraction" belongs inside `Tempest.Core.Reporting`**
  because the brief named it as scope — `ADR-0040`'s own orthogonality
  decision and `Platform Service Contracts.md`'s own Event Publication
  Rules make clear this is `WP 6.7`'s own future responsibility, not
  Reporting's.
- **Assuming a completion notification may safely summarise report
  content** — `Platform Service Contracts.md`'s own Notification
  Framework Security Considerations name this exact leak risk
  explicitly; a notification payload must carry only what's safe for
  any subscriber of that type to see.

## 12. Future Evolution

Report generation progress/streaming for a long-running renderer;
scheduled/recurring report generation; delivery-channel abstractions
(email, webhook, push) as first-party handler implementations; a
durable notification history/inbox that could also serve as a report
delivery record, if `AT-08`'s own revisit trigger fires; a future `WP
6.3` (REST API) webhook/callback integration; `WP 6.7` (Export/Import)
treating a generated `ReportResult` as one plausible export source —
all named explicitly as future, separately-scoped responsibilities, not
designed now.

## 13. Key Takeaways

1. Not every brief-named deliverable maps onto an interface the
   architecture package actually drafted — "Report builder" and "Report
   metadata" map directly onto `RegisterDefinition` and
   `IReportDefinition.Id`/`Name` respectively, requiring no new type at
   all, while "Template abstraction" required a genuinely new, additive
   interface. Recognising which is which, rather than mechanically
   building something for every brief-named noun, is itself part of the
   implementation work.
2. A release-wide orthogonality decision (Reporting vs. Export/Import)
   protects a not-yet-started sibling Work Package's own future design
   space just as much as it protects the implementing Work Package's
   own scope — declining to build "Export abstraction" here is as much
   about respecting `WP 6.7`'s own future freedom to design its own
   versioned contract as it is about Reporting's own restraint.
3. Demonstrating cross-service integration at the calling layer, with a
   real command handler wiring together Identity, Reporting, Audit, and
   Notifications, produces a concrete, copyable precedent — a future
   Work Package building its own Reporting consumer has a working
   example to follow, not just a paragraph of contract prose describing
   what integration "should" look like.

## Architectural Debt Assessment

`docs/governance/Quality/Technical Debt Register.md` gained one new,
disclosed trade-off, `AT-09`, recording the deliberate absence of a
delivery-channel abstraction and durable report history this release —
matching the approved contract's own Future Extension Points exactly.
No tracked Technical Debt item (`TD-01` through `TD-12`) or existing
trade-off (`AT-01` through `AT-08`) required annotation — Reporting
introduces no new instance of any previously-disclosed gap.

## Observations

This Work Package's own cross-service integration tests
(`ReportingSampleModuleIntegrationTests`) passed on first attempt,
unlike `WP 6.2`'s own experience with `NotificationDispatcher`'s
exact-static-type-dispatch trap — Reporting dispatches by string Id,
not generic type, and `ReportingSampleModule`'s own command handler
calls each of Identity/Reporting/Audit/Notifications directly, with no
intervening generic-dispatch layer of its own that could introduce a
similar type-mismatch class of defect. This Work Package's own
repository review, re-deriving every touched register directly, found
no further stale figures beyond what `WP 6.2`'s own review had already
corrected — a sign the disclosed governance-documentation drift found
across `WP 6.1`–`WP 6.2` has not recurred since being fixed.

## Related Documents

`docs/releases/v0.6.0/Release Architecture.md` and companions (the
architecture package this Work Package implemented); `Platform Service
Contracts.md` and companions (the Contract Review package); `ADR-0038`;
`ADR-0040`; `ADR-0044`; `ADR-0046`; `docs/architecture/Platform Service
Map.md` (Reporting entry); `docs/governance/Quality/Technical Debt
Register.md` (`AT-09`); `docs/academy/03 Work Packages/WP6.1-permissions-
and-identity-implementation.md`, `WP6.4-settings-framework-
implementation.md`, `WP6.5-audit-framework-implementation.md`,
`WP6.2-notification-framework-implementation.md` (the precedents this
Work Package's own single-pass implementation approach follows).
