# ADR-0052: The REST API Resolves Identity Per-Request Without Touching the Ambient Current Principal — Empirically Verified

## Status

Accepted — `WP 6.3` (REST API), 2026-07-29.

## Context

This ADR was not named in `Required ADRs.md`'s own catalogue — it is a
genuine, implementation-driven decision this Work Package's own brief
authorises ("if deviation is required: explain why, minimise the
change, produce the appropriate ADR"). `docs/releases/v0.6.0/Risk
Register.md`'s own `R1` named the exact tension this ADR resolves, as
an explicit residual mitigation from `WP 6.1`: "`CurrentPrincipalAccessor`'s
ambient (not `AsyncLocal<T>`) design will need real reconsideration once
`WP 6.3` introduces genuine request concurrency... must decide whether
`CurrentPrincipalAccessor` needs to become request-scoped."
`CurrentPrincipalAccessor`'s own remarks (`WP 6.1`) named the identical
question as its own explicit revisit trigger. `Service Lifecycle.md`'s
own REST API description names `ICurrentPrincipalAccessor` as one of
three things "every inbound request... resolves," without itself
resolving how that is made safe under genuine concurrency — this Work
Package is exactly the point `Required ADRs.md` and `Risk Register.md`
both named for answering that question.

## Decision

**`CurrentPrincipalAccessor` remains unchanged — still a single,
`lock`-protected mutable field, not `AsyncLocal<T>`-backed.** This was
not a judgment call made from reasoning alone: an `AsyncLocal<T>`-backed
implementation was built and tested directly against this codebase's
full 862-test baseline (pre-`WP 6.3`), and regressed 17 pre-existing
tests. The failure mode: `IdentitySampleModule`/`AuditSampleModule`/
other sample modules establish a principal once, during Module
Initialisation, and later, separately-awaited test code (and, in the
real `TempestHost`, any later caller reached via `ModuleLifecycleManager.InitialiseAllAsync`'s
own internal task-composition) expects that principal to remain visible
to a later, sibling call chain — behaviour `AsyncLocal<T>` does not
provide, since it flows forward only within a single logical call
chain's own continuations, never sideways to an unrelated later caller
whose own call chain forked from a shared ancestor via `Task.WhenAll` or
similar composition. This confirms `CurrentPrincipalAccessor`'s own `WP
6.1` remarks were correct in substance, now confirmed empirically rather
than by reasoning alone.

**The REST API's own request pipeline (`ApiRequestHandler`) never
calls `IIdentityService.EstablishCurrentPrincipal` or
`CurrentPrincipalAccessor.SetCurrent`.** Instead, it resolves a
per-request `IPrincipal` via the pure, non-mutating
`IIdentityService.GetPrincipal(identityId)` — already part of the
approved `IIdentityService` shape (`WP 6.1`) — and passes that principal
explicitly to `IPermissionEvaluator.HasPermission`, which itself takes
the principal as an ordinary parameter, never reading ambient state.
This is safe for concurrent requests by construction: each request
holds its own local `IPrincipal` reference; no shared mutable state is
ever written by the REST pipeline. Proven directly by a dedicated test
sending ten concurrent, differently-permissioned requests and confirming
each is authorized independently and correctly.

**Identity carried over HTTP is a bare header value, trusted outright —
no cryptographic verification of any kind.** `ApiRequestHandler.IdentityHeaderName`
(`X-Identity-Id`) extends this release's own local-only identity model
(`ADR-0043`: "a caller supplies an identity id it already trusts...
there is no authentication") over HTTP, mechanically, without adding a
credential-verification layer this release's own approved scope never
asked for. This is the platform's first network-facing attack surface
(`Platform Service Contracts.md`'s own Security Considerations); the
absence of real authentication is disclosed loudly, not silently built
to look more secure than it is. Mitigated, not fixed, by binding to the
loopback address only by default (`ADR-0049`).

**Audit attribution for REST requests uses an explicit `Detail` entry,
not ambient-principal auto-attribution.** Because the REST pipeline
never establishes the ambient current principal, `IAuditRecorder.RecordAsync`'s
own automatic attribution (`ADR-0045`) would record every REST request
as `AuditRecorder.UnknownActorId` if left to its own default. Rather
than accept a systematically wrong "unknown" attribution for the one
service most likely to need audit accuracy (a network-facing surface),
`ApiRequestHandler` carries the resolved caller identity explicitly, in
`Detail[ApiRequestHandler.CallerIdentityDetailKey]` — mirroring `WP
6.5`'s own established convention of carrying an attribute `IAuditRecord`
has no dedicated property for, in `Detail`, under a documented,
well-known key, requiring no interface change.

## Consequences

**Positive:**

- This platform's first genuinely concurrent, per-request scenario is
  handled correctly, proven directly by a concurrent-request test —
  not merely assumed safe because no prior Work Package needed to
  think about it.
- `CurrentPrincipalAccessor`'s own `WP 6.1` design and every existing
  test that depends on it remain completely unaffected — zero
  regression risk from this Work Package's own implementation, verified
  directly, not merely argued.
- The `Detail`-carried caller-identity convention requires no interface
  change to `IAuditRecord`, immediately usable, and directly consistent
  with `WP 6.5`'s own established pattern for exactly this class of
  need.

**Negative:**

- Any future command handler that itself calls `IAuditRecorder.RecordAsync`
  (relying on ambient-principal auto-attribution) will record `"unknown"`
  as its own actor id when invoked via the REST API — a real,
  disclosed limitation a future Work Package must design around
  explicitly (pass the caller identity through some other channel) if
  precise per-command attribution under REST invocation is required.
  Named explicitly in this Work Package's own Technical Debt Assessment
  and Future Capability Recommendations, not left for a future
  maintainer to discover by accident.
- Identity resolved via a REST header is trusted outright, with no
  cryptographic verification — a genuine, disclosed security limitation
  of this release's own local-only identity model, now exposed over a
  network boundary for the first time.

## Alternatives Considered

**Migrating `CurrentPrincipalAccessor` to an `AsyncLocal<T>`-backed
implementation.** Built and empirically tested — rejected because it
regressed 17 pre-existing tests, confirming `WP 6.1`'s own original
reasoning (that this release's actual need is one ambient principal,
visible to every subsequent caller for the life of the process, not a
per-async-flow-isolated one) remains correct. This is the one
alternative in this ADR actually implemented and measured, not merely
reasoned about, before being rejected.

**Having the REST pipeline call `EstablishCurrentPrincipal` per
request anyway, accepting the race.** Rejected — this would make audit
attribution and any future ambient-principal-dependent command handler
genuinely, silently wrong under concurrent load: two simultaneous
requests from different callers could see each other's principal,
a correctness defect worse than the disclosed "unknown" attribution
this ADR's own chosen design produces instead.

**Introducing a REST-specific, request-scoped `ICurrentPrincipalAccessor`
implementation, used only by the REST pipeline.** Considered — rejected
as unnecessary complexity: `IPermissionEvaluator`'s own existing,
explicit-parameter shape already provides everything the REST pipeline
needs (a principal to check), without introducing a second
`ICurrentPrincipalAccessor` implementation with its own scoping rules to
document and maintain.

**Adding a dedicated `ActorId` parameter to `IAuditRecorder.RecordAsync`.**
Rejected — this would be a change to an already-approved, already-
shipped `WP 6.5` interface for a need `Detail`'s own documented
extensibility already covers, exactly mirroring `ADR-0045`'s own
rejection of a dedicated `CorrelationId` property for the identical
reason.

## Related Documents

`docs/releases/v0.6.0/Risk Register.md` (`R1`, whose own residual
mitigation this ADR resolves); `ADR-0043` (local-only identity model,
extended over HTTP here); `ADR-0044` (`CurrentPrincipalAccessor`'s own
original ambient design and revisit trigger); `ADR-0045` (the `Detail`-
carried-attribute convention this ADR reuses); `ADR-0049` (the loopback-
only default this ADR's own disclosed authentication limitation is
mitigated by); `Service Lifecycle.md` (the REST API's own five-point
description this ADR clarifies); `WP6.3 Technical Debt Assessment.md`;
`WP6.3 Future Capability Recommendations.md`; `docs/academy/03 Work
Packages/WP6.3-rest-api-implementation.md`.
