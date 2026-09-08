# WP 6.3 — REST API Implementation

## 1. Introduction

WP 6.3 delivers the REST API — the sixth Work Package of the Platform
Services phase (`v0.6.0`) to ship real code, and the second (after `WP
6.0`) to actually match its own nominal numeric position in
`WorkPackages.md`. Implemented in a single pass, directly against the
already-approved architecture and Contract Review packages — no
separate architecture phase, mirroring every one of its five
predecessors. Unlike any of them, this Work Package required a genuine
first for this platform: adopting a substantial pre-built framework
component (ASP.NET Core/Kestrel) and resolving this codebase's first
genuinely concurrent, per-request scenario.

## 2. Purpose

To build `Tempest.Core.Api` exactly as the approved architecture
specified — `IApiEndpointRegistry`, `ApiRouteDescriptor` — as a thin
transport layer over the existing Command Framework, with zero business
logic of its own; to resolve the three questions `Required ADRs.md`
named as this Work Package's own required ADRs (hosted-service
placement, Command Framework dispatch, ASP.NET Core/Kestrel adoption);
and to resolve `Risk Register.md`'s own `R1` residual mitigation —
whether `CurrentPrincipalAccessor` needs to become request-scoped now
that genuine concurrent requests are a real, current-release scenario.

## 3. Background

`WP 6.1` (Permissions & Identity), `WP 6.4` (Settings Framework), `WP
6.5` (Audit Framework), `WP 6.2` (Notification Framework), and `WP 6.0`
(Reporting Framework) were all already implemented. `Risk Register.md`'s
own `R2` required `WP 6.1` to be genuinely landed (not merely
architected) before this Work Package began — confirmed true. `R3`
named this release's first substantial dependency on a pre-built
framework component as a real integration risk, requiring the boundary
to be prototyped explicitly before committing to it in implementation —
this Work Package's own single-pass implementation *is* that prototype,
proven directly by a full, real-HTTP test suite rather than designed
and left unverified.

## 4. The Problem

Four things needed to exist, plus one question this Work Package was
specifically positioned to answer:

1. **A way for an external HTTP client to invoke platform capability**
   — nothing in this codebase today exposes any command, service, or
   module capability outside the running process.
2. **A hosting mechanism** — this platform's own custom DI container
   (`ADR-0005`) was never meant to include HTTP listening, and no
   precedent exists for adopting an external framework component of any
   real size.
3. **A dispatch mechanism that does not duplicate the Command
   Framework** — without an explicit decision, a REST endpoint could
   easily grow its own request-handling logic, undermining
   `ADR-0036`–`ADR-0038`'s own uniformity.
4. **Genuine concurrency** — every prior platform service in this
   codebase has operated under a single-ambient-principal, effectively
   single-caller-at-a-time model; the REST API is the first to make
   concurrent, independently-identified callers a first-class, expected
   condition.

## 5. The Design

**Implemented exactly as drafted, zero signature deviation:**
`IApiEndpointRegistry.MapCommand`/`Routes`, `ApiRouteDescriptor`.
`ApiException` is a concrete, base-plus-subtype type (not abstract,
mirroring `ReportingException`'s own established convention), with one
real subtype, `DuplicateApiRouteException`.

**Hosted-service placement (`ADR-0047`):** `RestApiHostedService`
implements `IHostedService`, discovered and orchestrated identically to
any other hosted service — started Phase 8.1, stopped Phase 10.1, no
new Host Lifecycle phase. Isolated by default, not critical (`ADR-0021`)
— a start failure (a port already in use) is caught and logged, proven
directly by occupying the configured port before starting the Host and
confirming it still reaches `Running`.

**Command Framework dispatch (`ADR-0048`):** every registered route
dispatches through the existing, unmodified `ICommandRegistry.InvokeAsync`.
`ApiSampleModule`, this Work Package's own reference module, contains
zero business logic of its own whatsoever — it maps one route directly
to `ReportingSampleModule`'s own already-registered command, a
deliberate departure from every prior sample module's own
"independently usable" convention, chosen because it is the single
clearest possible proof that the REST layer itself introduces no
business logic.

**ASP.NET Core/Kestrel adoption (`ADR-0049`):** adopted via a
`FrameworkReference` to the already-installed shared framework — no new
external package. `RestApiHostedService` uses `WebApplication.CreateSlimBuilder()`,
confined entirely to its own one type; every mapped route delegate
closes over the exact `ApiRequestHandler` instance received via
ordinary constructor injection from TempestOS's own container, never
resolving a `Tempest.Core` service through ASP.NET Core's own internal
`IServiceProvider`.

**Identity resolution and Audit attribution (`ADR-0052`), empirically
resolved, not merely reasoned about:** `Risk Register.md`'s own `R1`
named this Work Package as the point where `CurrentPrincipalAccessor`'s
ambient design "will need real reconsideration." An `AsyncLocal<T>`-backed
implementation was built and tested directly against the full,
pre-existing 862-test suite — and regressed 17 tests, because
`IdentitySampleModule`/`AuditSampleModule`/other sample modules
establish a principal once during Module Initialisation and expect it
to remain visible to a later, separately-composed call chain, a
guarantee `AsyncLocal<T>` does not provide. `CurrentPrincipalAccessor`
therefore remains entirely unchanged. `ApiRequestHandler` instead
resolves a per-request `IPrincipal` via the pure, non-mutating
`IIdentityService.GetPrincipal`, passed explicitly to
`IPermissionEvaluator.HasPermission` — safe for concurrent requests by
construction, since no shared mutable state is ever written. Because
the ambient principal is never touched, `IAuditRecorder.RecordAsync`'s
own automatic attribution would record every REST request as
`"unknown"` — `ApiRequestHandler` instead carries the real caller
identity explicitly in `Detail[CallerIdentityId]`, mirroring `WP 6.5`'s
own established convention.

**Identity over HTTP carries no real authentication:** a bare
`X-Identity-Id` header, trusted outright — the exact same local-only
trust model `ADR-0043` already established, now extended over a network
boundary for the first time, disclosed loudly rather than built to look
more secure than it is. Mitigated by binding to the loopback address
only by default.

## 6. Alternatives Considered

See `ADR-0047`/`ADR-0048`/`ADR-0049`/`ADR-0052` for the complete
reasoning. In summary: a bespoke Host-level phase for "network
services" was rejected as duplicating `IHostedService`; REST endpoints
calling application logic directly was rejected as creating a second,
divergent invocation path; hand-rolling an HTTP listener over raw
sockets was rejected as disproportionate; and — the one alternative in
this Work Package actually built and measured, not merely reasoned
about — migrating `CurrentPrincipalAccessor` to `AsyncLocal<T>` was
rejected after empirical regression testing proved it unsafe for this
codebase's own existing usage pattern.

## 7. Why This Solution Was Chosen

Every approved interface is implemented with zero deviation, so any
future consumer (an engineering module) can depend on
`IApiEndpointRegistry` with full confidence in its shape. Confining
ASP.NET Core to one type keeps `Risk Register.md`'s own `R3` concern
(hosting-model conflict with the existing Composition Root) fully
contained and verified, not merely asserted. The empirically-tested
identity-resolution design means this platform's first concurrent,
per-request scenario is handled correctly, proven by a dedicated
concurrent-request test, while leaving `WP 6.1`'s own already-shipped,
already-tested design completely untouched.

## 8. Architectural Principles

- **Verify, Don't Merely Reason, When a Real Test Is Available** — the
  `CurrentPrincipalAccessor` decision was resolved by building and
  measuring the rejected alternative directly, not by argument alone.
- **Reuse Before Invention** — the Command Framework's own dispatch
  model, the Composition Root registration pattern, and
  `IIdentityService.GetPrincipal`'s own already-approved, non-mutating
  shape were all reused directly; nothing new was invented where an
  existing, proven mechanism already served.
- **A Sharp Boundary Around a New Dependency** — ASP.NET Core is
  confined to exactly one type, with its own internal container never
  competing with TempestOS's own, disclosed explicitly rather than left
  for a future reader to discover by accident.
- **Disclose a Security Limitation Loudly, Never Build It to Look More
  Secure Than It Is** — the absence of real authentication is named
  directly, in the ADR, the retrospective, and the Technical Debt
  Register, not softened.

## 9. Files Added

`src/Tempest.Core/Api/IApiEndpointRegistry.cs`;
`src/Tempest.Core/Api/ApiRouteDescriptor.cs`;
`src/Tempest.Core/Api/ApiException.cs`;
`src/Tempest.Core/Api/DuplicateApiRouteException.cs`;
`src/Tempest.Core/Api/ApiEndpointRegistry.cs`;
`src/Tempest.Core/Api/ApiResponse.cs`;
`src/Tempest.Core/Api/ApiRequestHandler.cs`;
`src/Tempest.Core/Api/RestApiHostedService.cs`;
`src/Tempest.Core/Api/OpenApiDocumentGenerator.cs`;
`src/Samples/Tempest.Samples/ApiSampleModule.cs`;
`tests/Tempest.Core.Tests/Api/ApiRequestHandlerFixtures.cs`;
`tests/Tempest.Core.Tests/Api/FakeAuditRecorder.cs`;
`tests/Tempest.Core.Tests/Api/ApiRequestHandlerTests.cs`;
`tests/Tempest.Core.Tests/Api/ApiEndpointRegistryTests.cs`;
`tests/Tempest.Core.Tests/Api/ExceptionTests.cs`;
`tests/Tempest.Core.Tests/Api/OpenApiDocumentGeneratorTests.cs`;
`tests/Tempest.Core.Tests/Api/RestApiHostedServiceTests.cs`;
`tests/Tempest.Core.Tests/Runtime/ApiHostRegistrationTests.cs`;
`tests/Tempest.Core.Tests/Samples/ApiSampleModuleIntegrationTests.cs`;
`docs/adr/ADR-0047-rest-api-is-a-background-hosted-service.md`;
`docs/adr/ADR-0048-rest-endpoints-dispatch-through-the-command-framework.md`;
`docs/adr/ADR-0049-adopting-aspnetcore-kestrel-for-the-rest-api.md`;
`docs/adr/ADR-0052-rest-api-identity-resolution-never-touches-the-ambient-current-principal.md`;
this retrospective. **Modified:**
`src/Tempest.Core/Tempest.Core.csproj` (`FrameworkReference` to
`Microsoft.AspNetCore.App`); `src/Tempest.Core/Runtime/TempestHost.cs`
(registration only); `tests/Tempest.Core.Tests/Samples/ClockModuleDiscoveryTests.cs`
(sample-module count, 12 → 13). `src/Tempest.Core/Identity/CurrentPrincipalAccessor.cs`
was modified experimentally (to `AsyncLocal<T>`), tested, found to
regress 17 tests, and reverted to its original, unchanged form — no net
change shipped.

## 10. Trade-offs

- **No real authentication exists this release** (`TD-13`) — a
  disclosed, deliberate limitation; identity is a bare, unverified
  header value, mitigated only by a loopback-only default bind address.
- **No TLS is configured** (`TD-14`) — matching the approved contract's
  own "beyond local development" framing, not a current requirement.
- **A command handler relying on ambient-principal Audit attribution
  will record `"unknown"` when invoked via REST** (`TD-15`) — the real
  caller identity is preserved correctly, but only in the REST API's
  own `api.request` audit entry's `Detail`, not automatically visible
  to a different command's own, separate `RecordAsync` call.
- **No request-parameter binding exists** (`AT-10`) — every REST-exposed
  command dispatches only its own parameterless default instance; an
  inbound request's own body or query string is never threaded into it,
  matching the approved contract's own `MapCommand` signature exactly.

## 11. Common Mistakes

- **Assuming `IApiEndpointRegistry`/`ApiRequestHandler` check
  permissions the same way `IAuditQuery` does (throwing, internally)** —
  they do not; permission enforcement and identity resolution happen
  entirely within `ApiRequestHandler`'s own pipeline, mapped directly to
  HTTP status codes, never as a thrown exception crossing its own
  public `HandleAsync` boundary.
- **Assuming a REST request automatically produces a correctly-attributed
  Audit record for whatever command it invokes** — only the REST API's
  own `api.request` entry carries the correct caller identity, in
  `Detail`; a different command handler's own, separate audit call
  still uses the (untouched) ambient principal.
- **A genuine, found-not-invented lesson**: assuming
  `CurrentPrincipalAccessor` could simply be switched to
  `AsyncLocal<T>` because "that's the textbook fix for ambient state
  under concurrency" — textbook correctness does not automatically
  transfer to a codebase with an established, different usage pattern;
  the only way to know for certain was to build it and run the existing
  suite against it.

## 12. Future Evolution

A genuine authentication mechanism (API keys, OAuth/OIDC, mutual TLS)
once this platform is exposed beyond a trusted local/loopback boundary;
TLS configuration for a concrete deployment scenario; request-parameter
binding for a REST-exposed command that needs caller-supplied values;
rate limiting and request/response compression (`Platform Service
Contracts.md`'s own Future Extension Points); webhook/callback support
as a plausible Notifications integration — all named explicitly as
future, separately-scoped responsibilities, not designed now.

## 13. Key Takeaways

1. When a prior Work Package's own ADR names an explicit revisit
   trigger ("once genuine request concurrency exists"), the Work
   Package that arrives at that trigger should test the originally-
   deferred alternative directly, not merely re-argue the original
   reasoning — the empirical answer here (regression, not success)
   would not have been discovered by reasoning alone.
2. Confining a new external dependency to exactly one type, with an
   explicit, written boundary about what it may never be used for
   (resolving a platform-specific service), is what keeps "adopt a
   framework component" from becoming "adopt a second architecture" —
   the boundary has to be verified by direct inspection, not merely
   declared.
3. Safety under concurrency is often best achieved by *avoiding* shared
   mutable state entirely, not by making the shared mutable state
   thread-safe — `ApiRequestHandler`'s own per-call-local `IPrincipal`
   sidesteps the entire class of problem `AsyncLocal<T>` was being
   reached for to solve.

## Architectural Debt Assessment

`docs/governance/Quality/Technical Debt Register.md` gained three new
tracked debt items (`TD-13`, `TD-14`, `TD-15`) and one new trade-off
(`AT-10`). `AT-07` ("Zero real hosted services exist beyond the
infrastructure") is updated to Retired — this is the Work Package its
own revisit trigger explicitly named in advance. `TD-04` (`IHostedService`
naming proximity) is annotated: real usage evidence for the concern it
names has now arrived, since this codebase genuinely depends on the
ASP.NET Core ecosystem for the first time, though no actual confusion
has been reported and the item remains Open, not Resolved.

## Observations

This Work Package's own repository review, re-deriving every touched
register directly, found one further genuine, pre-existing governance-
documentation drift: `docs/governance/Engineering/Hosted Services
Register.md` had never been updated when `WP 6.2` shipped
`NotificationSampleHostedService` — this codebase's first real,
non-infrastructure hosted service — so its own "zero production hosted
services exist" Coverage Note survived, stale, through an entire Work
Package that directly contradicted it. Corrected here, populated with
both entries, at the same time `RestApiHostedService` becomes its
second real entry.

## Related Documents

`docs/releases/v0.6.0/Release Architecture.md` and companions (the
architecture package this Work Package implemented); `Platform Service
Contracts.md` and companions (the Contract Review package); `ADR-0021`;
`ADR-0036`–`ADR-0038`; `ADR-0043`/`ADR-0044` (the identity model and
enforcement point this Work Package's own identity resolution reuses);
`ADR-0045` (the `Detail`-carried-attribute convention `TD-15`'s own
resolution mirrors); `ADR-0047`; `ADR-0048`; `ADR-0049`; `ADR-0052`;
`docs/architecture/Platform Service Map.md` (REST API entry);
`docs/governance/Engineering/Hosted Services Register.md`;
`docs/governance/Quality/Technical Debt Register.md` (`AT-07`, `AT-10`,
`TD-04`, `TD-13`, `TD-14`, `TD-15`); `docs/releases/v0.6.0/Risk
Register.md` (`R1`, `R2`, `R3`); `docs/academy/03 Work
Packages/WP6.1-permissions-and-identity-implementation.md`,
`WP6.0-reporting-framework-implementation.md` (the precedents this
Work Package's own single-pass implementation approach follows).
