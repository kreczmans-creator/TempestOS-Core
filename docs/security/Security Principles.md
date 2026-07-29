# TempestOS Security Principles

## Purpose

The standing security principles TempestOS is designed against, as
observed and confirmed by `WP 5.0S`'s Platform Security Baseline Audit.
Some of these principles were already implicit in existing ADRs before
this Work Package (this document names them explicitly for the first
time); none are new architectural decisions — this Work Package changed
no approved architecture. Every future Work Package's Definition of Done
should be checked against this document, alongside `Platform Security
Review v0.5.0.md`'s own baseline.

## 1. Least Privilege for Orchestration Machinery

The machinery that discovers, registers, and drives the lifecycle of a
module must never be reachable *by* a module. `IRuntimeModuleManager`,
`IModuleLifecycleManager`, and `IFrameworkDiscoveryService` are
constructed and held only as private `TempestHost` fields — never
registered into the dependency injection container (ADR-0017) — so no
amount of DI resolution, reflection, or service-locator behaviour from
inside a module can reach them. `ITempestHost.Services` (ADR-0034)
exposes only the DI-public surface, and this exclusion was directly
regression-tested at `WP 5.0D`.

**Why it matters:** a module that could reach its own orchestrator could
re-register itself, deregister a sibling, or otherwise manipulate the
very system meant to supervise it. This principle is what makes "a
module cannot escalate its own privilege by asking nicely" true today.

## 2. Isolate Failure, Not Trust

TempestOS's failure-isolation conventions (ADR-0021, ADR-0025, ADR-0028,
ADR-0029) isolate *malformed or failing* modules, plugins, and event
subscribers from taking down the rest of the platform. This is a
**reliability** guarantee, not a **trust** boundary — a plugin whose
assembly loads without error is not isolated from anything once it is
running. Every audit finding in `Platform Security Review v0.5.0.md`
concerning plugins exists specifically because this distinction is easy
to conflate: "isolated from crashing the Host" and "isolated from the
rest of the process" are different properties, and TempestOS today only
has the first one.

## 3. Validate Untrusted Input at the Boundary It Crosses

Every place external, structurally-untrusted data enters the platform —
a configuration source, a plugin manifest's JSON, a module's declared
metadata — validates that data before constructing a domain object from
it (`ConfigurationBuilder.ValidateEntry`, `PluginManifestDiscoveryService.
ParseAndValidate`, `ReflectionFrameworkDiscoveryService.ValidateMetadata`).
None of these use a raw, unvalidated DTO past the point where it is
parsed. This Work Package extended the principle to one place it had not
yet reached: a plugin manifest's `AssemblyFileName` is now validated to
resolve *within* the folder that declared it (Finding PL-1), not only to
be a syntactically valid path.

## 4. Fail Isolated, Not Silent — and Never Fail Open on a Critical Path

Every isolation convention in TempestOS logs the failure it isolates
(`PluginFailureLogging`, `EventBus.PublishAsync`'s per-subscriber catch,
`HostedServiceManager`'s per-service catch) — a failure is never simply
swallowed. And where a failure is *not* safe to isolate — a critical
hosted service failing to start or stop (ADR-0021/ADR-0029) — the
convention is to fail the whole Host (`HostState.Faulted`) rather than
continue in a state nobody asked for. Security-relevant failures should
follow the same shape: isolate and log what can be safely isolated; fail
loudly, not silently, when something cannot be.

## 5. Configuration and Logging Are Infrastructure, Not Policy

`ILogger` and `IConfigurationProvider` are pure abstractions — no
component that logs or reads configuration knows or decides *where* logs
go or *how* configuration is sourced. This matters for security
specifically because it means a future secrets-handling or log-redaction
policy can be introduced at exactly one place (a new `ILogSink`, a new
`IConfigurationSource`) without touching the hundreds of call sites that
already log or read configuration today. No such policy exists yet
(see `Platform Security Review v0.5.0.md`, Logging section) — this
principle is what will make adding one, later, tractable rather than a
rewrite.

## 6. A Trust Boundary Deserves an ADR, Not a Comment

Where TempestOS has drawn a real trust boundary, it is documented as an
architectural decision with a rejected alternative (ADR-0017's exclusion
of Discovery/Registration/Lifecycle from DI; ADR-0025/ADR-0026's
plugin-failure classification), not left as an implicit convention a
future contributor could accidentally erode. This audit found no place
where a trust boundary existed in practice but not in an ADR. Where this
audit recommends a *future* trust boundary be drawn (plugin isolation,
navigation ownership — see `Security Roadmap.md`), it recommends an ADR
be written for it, not a quiet code change.

## 7. Do Not Invent Security Theatre Ahead of a Real Need

TempestOS's engineering discipline already distinguishes **debt**
(something that should eventually be fixed) from a **disclosed, accepted
trade-off** (a deliberate exclusion, not expected to need fixing absent a
real, demonstrated need) — see `Technical Debt Register.md`. This audit
applies the same discipline to security: it does not recommend building
authentication, encryption, or a plugin sandbox today, because none of
assumptions 4–9 in `Threat Model.md` are live yet, and speculative
security machinery with no real threat to defend against is itself a
maintenance and complexity cost. It does recommend that when a real need
arrives, the relevant `Security Roadmap.md` item is picked up *before*
the capability ships, not after.

## Related Documents

`Threat Model.md`; `Platform Security Review v0.5.0.md`; `Security
Roadmap.md`; ADR-0017, ADR-0021, ADR-0025, ADR-0026, ADR-0028, ADR-0029,
ADR-0034; `docs/academy/06 Engineering Standards/Engineering Governance.md`.
