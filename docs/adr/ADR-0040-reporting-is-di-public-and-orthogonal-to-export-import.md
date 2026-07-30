# ADR-0040: Reporting Is DI-Public and Orthogonal to Export/Import — Template Abstraction, Cross-Service Integration, and Scope Boundaries

## Status

Accepted — `WP 6.0` (Reporting Framework), 2026-07-29.

## Context

`v0.6.0`'s own architecture package anticipated this decision but
deliberately left it unratified pending `WP 6.0`'s own implementation
phase. `Required ADRs.md` named the core orthogonality question
(Reporting vs. Export/Import) as this Work Package's own required ADR.
Implementation surfaced two further genuine decisions: this Work
Package's own brief named "Template abstraction" and "Export
abstraction" as implementation scope, neither of which `Public
Interface Catalogue.md`'s own draft (`IReportDefinition`,
`IReportRenderer<TDefinition>`, `IReportingService`) gave interface
members; and this Work Package was also explicitly tasked with
assessing and, where justified, implementing interactions with four
already-completed platform services (Identity, Settings, Persistence,
Audit) plus one implemented in the same release phase (Notifications).

## Decision

**Reporting is implemented exactly as `Public Interface Catalogue.md`
drafted** — `IReportDefinition`, `IReportRenderer<TDefinition>`,
`IReportingService`, `ReportRequest`, `ReportResult` — with zero
signature deviation. `ReportingException` is a concrete, base-plus-
subtype type (not abstract, despite the catalogue's own pseudo-code
shorthand), mirroring `SettingsException`/`IdentityException`/
`CommandException`'s own established real-codebase convention, with two
concrete subtypes actually thrown: `DuplicateReportDefinitionException`
and `ReportDefinitionNotFoundException`.

**Registered as an ordinary DI-public, container-constructed singleton**
in `TempestHost`'s existing Platform Services Registered block (Phase
6), immediately after the Event Bus and before Notifications — matching
`Service Registration Matrix.md`'s own recommended registration order.
`IReportingService` depends on nothing but Dependency Injection itself —
confirmed directly, and consistent with `Platform Service Implementation
Order.md`'s own observation that "Reporting has no hard proposed-service
dependency."

**`GenerateAsync` does not itself check permissions.** Per `Platform
Service Contracts.md`'s own Security Considerations, "the enforcement
point is the caller, not the service" — mirroring how Navigation and the
Command Framework themselves impose no authorization internally
(`ADR-0032`, `ADR-0037`). A caller invoking report generation through
the Command Framework is responsible for its own authorization check.

**`IReportTemplate<TDefinition>` is a new, additive interface — no
approved interface is changed.** `Public Interface Catalogue.md`'s own
draft named only the three types above; "Template abstraction" and this
Work Package's own Template Strategy instructions ("Separate: Report
data, Report layout, Rendering pipeline") were named in the brief but
never drafted as interface members. Mirroring `WP 6.1`'s own
`IRole`/`IIdentityService`, `WP 6.4`'s own `SettingDefinition`, and `WP
6.2`'s own `IPlatformNotification` precedent, this gap is filled with a
new, entirely optional type: a renderer implementation may apply a
template internally (an ordinary constructor-injected collaborator) or
render its own output directly — `IReportingService` has no awareness
of templates at all. `PlainTextReportTemplate<TDefinition>` is a
concrete, genuinely reusable general-purpose template shipped alongside
the abstraction, usable by any current or future report definition
without that definition's own renderer needing to write layout logic
itself.

**"Export abstraction" is explicitly out of this Work Package's own
scope.** This release's own anticipated decision — Reporting is
orthogonal to Export/Import (`WP 6.7`, not yet started) — means
building a dedicated export interface inside `Tempest.Core.Reporting`
would directly duplicate `WP 6.7`'s own future scope and contradict this
very ADR's own orthogonality decision. `ReportResult`'s own
`ContentType`/`Content` shape already is Reporting's own "output"
mechanism — a report is handed back to its caller as bytes with a MIME
type, explicitly **not** guaranteed round-trip-safe or re-importable,
distinguishing it from Export/Import's own versioned contract. Getting
those bytes to a file, a downstream system, or a delivery channel is
each specific caller's or a future `WP 6.7`'s own responsibility, not
Reporting's.

**Cross-service integration is demonstrated at the sample-module layer,
never inside `IReportingService` itself.** `ReportingSampleModule`'s own
`GenerateSampleReportCommandHandler` checks
`IPermissionEvaluator.HasPermission` (Identity) before generating,
records the action through `IAuditRecorder` (Audit), and publishes a
completion notice through `INotificationDispatcher` (Notifications) —
none of which `ReportingService` itself references. `SampleSummaryReportRenderer`
reads a Settings-provided greeting (Settings) as an ordinary,
renderer-owned collaborator, exactly as `Platform Service Contracts.md`'s
own Configuration Requirements anticipated ("that configuration belongs
to the renderer, not to `IReportingService` itself"). Persistence is
deliberately **not** consumed anywhere — Reporting's own approved
contract states "Persistence Requirements: None," and no sample
component was built to use it speculatively. See this Work Package's own
Platform Integration Demonstration for the complete, per-service
account.

**The completion notification carries no report content.** `Platform
Service Contracts.md`'s own Notification Framework Security
Considerations name this exact scenario explicitly: "a 'user X's report
is ready' notification should not leak report content to an
unauthorized subscriber." `GenerateSampleReportCommandHandler`'s own
published notification carries a fixed, non-identifying success message
only — the full result (content type, byte length) is returned solely
to the command's own caller, via `CommandResult`, never broadcast.

## Consequences

**Positive:**

- Every approved interface is implemented with zero deviation, so any
  future consumer (an engineering module, the REST API) can depend on
  `IReportDefinition`/`IReportRenderer<TDefinition>`/`IReportingService`
  with full confidence in their shape.
- The additive `IReportTemplate<TDefinition>`/`PlainTextReportTemplate<TDefinition>`
  elaboration gives every future report definition a ready-to-use,
  reusable layout today, without constraining a future renderer's own
  design (HTML, PDF, or any other format may implement the same
  interface independently).
- Declining to build an export abstraction keeps Reporting's own scope
  boundary exactly where `Required ADRs.md` anticipated it, leaving `WP
  6.7` free to design Export/Import's own versioned contract without
  Reporting having pre-empted or duplicated any part of it.
- The cross-service integration pattern (permission check, audit
  record, notification, all at the calling layer) is now a concrete,
  tested precedent any future Reporting consumer can copy directly.

**Negative:**

- A future consumer wanting Reporting to enforce its own permissions
  internally would need to add that itself, at the calling layer, every
  time — `IReportingService` will never do this automatically, by
  design.
- No delivery mechanism (email, webhook, a durable report history)
  exists yet — a real, disclosed limitation matching the approved
  contract's own Future Extension Points, not an oversight.

## Alternatives Considered

**Folding Reporting into Export/Import as one combined "data output"
service.** Rejected per `Required ADRs.md`'s own anticipated decision —
a report's own presentation concerns (formatting, layout, possible
lossiness) are irrelevant to Export/Import's round-trip guarantee;
conflating the two would force one service to satisfy two incompatible
contracts.

**Building a dedicated "Export abstraction" inside `Tempest.Core.Reporting`
now**, since the brief named it as scope. Rejected — the approved
contract's own orthogonality decision (this ADR) and Event Publication
Rules ("Reporting itself stays uninvolved... mirroring the deliberate
orthogonality `ADR-0040` establishes against Export/Import") make this
exactly the kind of scope encroachment on `WP 6.7`'s own future
Work Package this release's own governance rules out.

**Modifying `IReportDefinition`/`IReportRenderer<TDefinition>`/`IReportingService`
directly to add template awareness.** Rejected — none of the three was
ever drafted with a template parameter; adding one now would be an
unnecessary, unapproved change to already-approved interfaces for a need
the additive `IReportTemplate<TDefinition>` already satisfies without
touching them.

**Having `IReportingService.GenerateAsync` check permissions
internally.** Rejected — `Platform Service Contracts.md`'s own Security
Considerations state explicitly that Reporting does not itself check
permissions; the enforcement point is the caller, mirroring Navigation
and the Command Framework's own precedent.

**Including report content or a content summary in the completion
notification.** Rejected — `Platform Service Contracts.md`'s own
Notification Framework Security Considerations name this exact leak
risk explicitly; a notification payload must carry only what's safe for
any subscriber of that type to see.

## Related Documents

`docs/releases/v0.6.0/Required ADRs.md` (this decision's own anticipated
form); `Platform Service Contracts.md` (Reporting's own 15-dimension
contract this ADR implements, and Notifications' own Security
Considerations this ADR's notification design honours); `ADR-0028`
(Event Bus, referenced for the Notifications integration);
`ADR-0038` (Command dispatch failure model, mirrored by
`IReportingService.GenerateAsync`'s own failure behaviour); `ADR-0044`
(the enforcement point Reporting's own sample consumer reuses);
`ADR-0046` (Notifications, whose own Security Considerations this ADR's
notification design honours); `docs/academy/03 Work
Packages/WP6.0-reporting-framework-implementation.md`.
