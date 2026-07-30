# ADR-0049: Adopting ASP.NET Core/Kestrel for the REST API

## Status

Accepted — `WP 6.3` (REST API), 2026-07-29.

## Context

`v0.6.0`'s own architecture package anticipated this decision but
deliberately left it unratified pending `WP 6.3`'s own implementation
phase — `Public Interface Catalogue.md` itself declined to draft the
hosted-service scaffold type "pending `ADR-0049`'s ratification."
`Required ADRs.md` named this Work Package's own required ADR:
`ADR-0005` committed this platform to a custom, minimal dependency-
injection container specifically to avoid a large third-party framework
dependency; the REST API is the first Work Package with a plausible,
well-justified reason to reconsider that stance for a narrower purpose —
HTTP hosting. `Risk Register.md`'s own `R3` named this as this release's
first substantial dependency on a pre-built framework component, with
no direct precedent in this codebase, and required the integration
boundary to be prototyped explicitly before committing to it.

## Decision

**Adopted ASP.NET Core/Kestrel, via `<FrameworkReference Include="Microsoft.AspNetCore.App" />`
added to `Tempest.Core.csproj`** — part of the .NET SDK's own shared
framework, already installed alongside the targeted `net10.0` runtime,
not a third-party NuGet package in the sense `ADR-0005` targeted. This
required no change to the project's own SDK type (`Microsoft.NET.Sdk`,
not `Microsoft.NET.Sdk.Web`) and no new external package reference.

**`RestApiHostedService` uses `WebApplication.CreateSlimBuilder()`,
confined entirely to its own one type.** This platform's own DI
container, Command Framework, and every other platform service remain
entirely unchanged and unreplaced, exactly as anticipated — verified
directly: no `using Tempest.Core.*` type is ever resolved via
`HttpContext.RequestServices` anywhere in this codebase. Every mapped
route delegate closes over the exact `ApiRequestHandler` instance
`RestApiHostedService` itself received via ordinary constructor
injection from TempestOS's own container.

**A genuine, disclosed nuance found during implementation:**
`WebApplication` necessarily builds its own, internal
`IServiceProvider` as an unavoidable implementation detail of ASP.NET
Core's own hosting model — a second, minimal DI container does exist at
runtime, purely as Kestrel/routing plumbing (`IServer`,
`IServerAddressesFeature`, and so on). `ADR-0049`'s own anticipated
language ("this platform's own DI container... remain entirely
unchanged and unreplaced") is satisfied in the sense that matters: that
second container is never asked to resolve a single TempestOS-specific
service, and TempestOS's own `TempestServiceProvider` remains the sole
source of truth for every `Tempest.Core` dependency. This distinction —
"a second container exists as an implementation detail" versus "a
second container competes with TempestOS's own" — is named explicitly
here rather than left for a future reader to discover by inspecting the
generated IL.

**Binds to the loopback address only by default** (`127.0.0.1`,
configurable port via `Api:Port`), a disclosed mitigation for the
absence of real authentication (`ADR-0052`) rather than a TLS or
network-boundary control this release actually implements. **No TLS is
configured this release** — see this Work Package's own Technical Debt
Assessment.

## Consequences

**Positive:**

- HTTP/1.1 listening, routing, TLS capability (available, not yet
  configured), and connection management are all provided by a
  component already bundled with the targeted SDK — no hand-rolled
  socket/protocol-parsing code, and no genuinely new external
  dependency to track, patch, or audit.
- The integration boundary is real and proven, not merely designed:
  every request in this Work Package's own test suite is a genuine
  HTTP round trip through real Kestrel, against the real, unmodified
  `TempestHost` — not an in-process simulation.
- `Risk Register.md`'s own `R3` mitigation ("prototype the integration
  boundary explicitly before committing to it in implementation") is
  satisfied by this Work Package's own single-pass implementation,
  which found no hosting-model conflict with the existing Composition
  Root.

**Negative:**

- A second, ASP.NET-Core-internal `IServiceProvider` exists at runtime,
  even though it is never used for a TempestOS service — a future
  reader inspecting a memory dump or debugging session could be
  confused by its presence if this ADR's own disclosure is not read
  first.
- No TLS is configured this release, and the default bind address
  (loopback-only) is a mitigation, not a fix, for the absence of real
  authentication — see `ADR-0052`.

## Alternatives Considered

**Hand-rolling an HTTP/1.1 listener directly over raw sockets.**
Rejected per `Required ADRs.md`'s own anticipated decision — a
disproportionate undertaking (TLS, chunked transfer encoding, header
parsing, routing) compared to reusing a component already bundled with
the .NET SDK the project already targets, for a benefit (avoiding one
framework dependency) `ADR-0005`'s own reasoning was never actually
about — that ADR was about *dependency injection*, not HTTP hosting.

**Using the full `Microsoft.NET.Sdk.Web` project SDK** (a separate
ASP.NET Core project) instead of a `FrameworkReference` inside the
existing `Tempest.Core` class library. Rejected — `Tempest.Core` is
this platform's own single class library housing every platform
service; introducing a second project purely to host Kestrel would
fragment where platform services live, for no benefit the
`FrameworkReference` approach does not already provide within the
existing project structure.

**Implementing `IHttpApplication<TContext>` directly against the raw
`KestrelServer`/`IServer` API, bypassing `WebApplication` entirely** —
considered first, for the tightest possible confinement of ASP.NET
Core's own machinery. Rejected as materially more complex (manual
`IHttpContextFactory` wiring, manual feature-collection bookkeeping) for
no behavioural difference once `WebApplication`'s own internal
container was confirmed never to compete with TempestOS's own — the
simpler `WebApplication.CreateSlimBuilder()` path achieves the identical
confinement guarantee with far less custom hosting-internals code to
maintain and trust.

## Related Documents

`docs/releases/v0.6.0/Required ADRs.md` (this decision's own anticipated
form); `Platform Service Contracts.md` (the REST API's own contract);
`Risk Register.md` (`R2`, `R3`); `ADR-0005` (the custom DI container
decision this ADR deliberately does not reopen); `ADR-0047` (the hosted-
service scaffold this ADR's own Kestrel integration lives inside);
`ADR-0052` (identity resolution and the loopback-only default this
ADR's own bind address mitigates for); `docs/governance/Quality/
Technical Debt Register.md`; `docs/academy/03 Work
Packages/WP6.3-rest-api-implementation.md`.
