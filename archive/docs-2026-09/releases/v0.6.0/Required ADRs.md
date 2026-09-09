# TempestOS v0.6.0 — Required ADR List

## Status

**A catalogue of anticipated ADRs, not finished ADR documents.** None of
the twelve entries below is written as an `Accepted`-status file under
`docs/adr/` — each is deferred to its own owning Work Package's dedicated
architecture phase, exactly as `ADR-0036`–`ADR-0038` were authored
during `WP 5.1A`'s own dedicated phase rather than during `v0.5.0`'s
original release planning. Numbering begins at `ADR-0040` — the highest
existing ADR is `ADR-0039-diagnostics-is-di-public-lazy-projection.md`,
confirmed directly against `docs/adr/` immediately before this document
was written.

This is a release-wide architectural review's own judgment about *which*
decisions will need an ADR and *roughly what* each is likely to decide —
sufficient for `WP 6.8` (Platform Services Integration Review) to verify
none was skipped, and for each owning Work Package to know, before its
own architecture phase starts, what question it is expected to answer.
Each entry names the alternative genuinely considered and rejected by
this review, per Engineering Governance §5's own criterion for when an
ADR is warranted at all.

## The List

### ADR-0040 — Reporting Is DI-Public and Orthogonal to Export/Import

**Originating WP.** `WP 6.0`.
**Context.** Reporting produces presentation-oriented output from
platform/module data. It is easily confused with Export/Import, which
also "gets data out" of the platform.
**Anticipated decision.** Reporting is registered as an ordinary
DI-public, container-constructed singleton (mirroring Navigation/Command
Framework); a report is explicitly not guaranteed round-trip-safe or
re-importable, distinguishing it from Export/Import's own versioned
contract.
**Alternative considered and rejected.** Folding Reporting into
Export/Import as one combined "data output" service — rejected because
a report's own presentation concerns (formatting, layout, possible
lossiness) are irrelevant to Export/Import's round-trip guarantee, and
conflating the two would force one service to satisfy two incompatible
contracts.

### ADR-0041 — A Shared Persistence Abstraction Serves Settings and Audit

**Originating WP.** `WP 6.4` (established as part of its own scope,
reused explicitly by `WP 6.5`).
**Context.** Neither Settings nor Audit has anywhere to durably store
its own state; nothing in the current platform persists anything since
the bootstrap-era `JsonProjectRepository` went dead.
**Anticipated decision.** `WP 6.4` establishes a minimal
`Tempest.Core.Persistence`/`IPersistenceStore` abstraction as part of
its own scope; `WP 6.5` depends on it explicitly rather than building a
second, incompatible storage mechanism.
**Alternative considered and rejected.** Each of `WP 6.4` and `WP 6.5`
building its own independent storage mechanism — rejected because it
would produce two incompatible, ad hoc persistence layers for what is
architecturally the same underlying need, discovered only after both
had already shipped, mirroring exactly the kind of avoidable
architectural debt this project's own governance process exists to
catch before implementation.

### ADR-0042 — Settings Is DI-Public and Distinct From Configuration

**Originating WP.** `WP 6.4`.
**Context.** Settings and Configuration (`WP 2.5`) sound similar enough
that a future reader could reasonably ask why both exist.
**Anticipated decision.** Configuration remains read-only, immutable,
loaded once at startup (`ADR-0009`, Case Study 05) — unchanged. Settings
is read-write, at runtime, backed by the new Persistence abstraction,
and raises `ISettingsChangedEvent` through the existing Event Bus on
every change.
**Alternative considered and rejected.** Extending `IConfigurationProvider`
itself to permit runtime writes — rejected because it would break every
existing consumer's own assumption of immutability (Case Study 05's own
stated reasoning), a far larger blast radius than introducing one new,
narrowly-scoped service.

### ADR-0043 — Identity Model Scope: Local-Only, Extensible

**Originating WP.** `WP 6.1`.
**Context.** `Threat Model.md` assumptions 4 and 5 already bound this
platform's current threat model to a single local user/process; `WP 6.1`
is the first Work Package to need an actual identity concept, and could
plausibly over-scope toward external identity-provider federation
(OAuth/OIDC/SAML) that nothing in this platform currently needs.
**Anticipated decision.** `IIdentity`/`IPrincipal` model scoped to
local-only principals in this release, with an explicitly extensible
shape (no closed enum of identity kinds, no hard-coded single-user
assumption) so a future release can add federation without a breaking
change.
**Alternative considered and rejected.** Building external
identity-provider federation now, "since Identity will need it
eventually" — rejected as speculative scope beyond what any named
`v0.6.0` Work Package (including the REST API) actually requires; this
project's own engineering discipline explicitly rejects building for
hypothetical future requirements.

### ADR-0044 — Authorization Enforcement Point

**Originating WP.** `WP 6.1`.
**Context.** `TD-09` (plugin isolation), `TD-10` (Navigation ownership),
and `TD-11` (Command/Navigation registration-order squatting) have all
remained open since their respective disclosures, each explicitly
triggered on "the first Work Package with a genuine reason to build an
authorization concept." `WP 6.1` is that Work Package.
**Anticipated decision.** `IPermissionEvaluator.RequirePermission` is
the single, uniform enforcement point every other service (REST API,
Audit, and any future consumer) calls — rather than each service
inventing its own ad hoc check — closing `TD-09`/`TD-10`/`TD-11`
together by giving the platform, for the first time, one authoritative
place those three debts' own underlying gaps can actually be closed.
**Alternative considered and rejected.** Leaving authorization checks
distributed, ad hoc, per-service — rejected because it is the exact
condition that allowed `TD-09`/`TD-10`/`TD-11` to accumulate
unaddressed in the first place; a single enforcement point is the only
option that plausibly resolves all three at once.

### ADR-0045 — Audit Is a Durable, Queryable Record, Distinct From Logging and Diagnostics

**Originating WP.** `WP 6.5`.
**Context.** Logging, Diagnostics, and Audit each describe "what
happened," and this platform has already had to draw this exact kind of
boundary twice before (Navigation/Commands, `ADR-0022`; Commands/Event
Bus, `ADR-0037`/`RD-0039`) — a third, structurally similar confusion is
foreseeable without an explicit ADR.
**Anticipated decision.** Logging remains developer-facing and
not-guaranteed-durable; Diagnostics remains a live snapshot of *current*
state; Audit is the durable, queryable *history* of attributable
actions, depending on Persistence (storage) and Identity & Permissions
(attribution) — a genuinely new capability, not a rename of either
existing one.
**Alternative considered and rejected.** Extending Diagnostics to
retain historical snapshots instead of introducing Audit — rejected
because Diagnostics' entire design (`ADR-0039`) is built around
lazy, `Func<T>`-projected *current* state with no persistence layer at
all; retrofitting durability into it would contradict its own founding
ADR.

### ADR-0046 — Notifications Are Derived From Events, Not a Replacement Pub/Sub

**Originating WP.** `WP 6.2`.
**Context.** The Event Bus already provides publish/subscribe; a
poorly-scoped Notification Framework could easily become a second,
redundant dispatch mechanism.
**Anticipated decision.** `INotification` is derived from (or raised
alongside) an `IEvent`; `INotificationDispatcher` is built on top of the
existing `IEventBus`, never a parallel implementation of subscription/
dispatch machinery. `INotificationHandler<T>` is subscribed
imperatively at runtime, mirroring `IEventHandler<T>`'s own proven
shape — never resolved generically through the container (`RD-0040`).
**Alternative considered and rejected.** A fully independent
Notification dispatch pipeline with its own subscription model —
rejected as unjustified duplication of machinery the Event Bus already
provides and has already proven (`ADR-0028`).

### ADR-0047 — REST API Is a Background Hosted Service

**Originating WP.** `WP 6.3`.
**Context.** The REST API is a long-running process that must start
after modules initialise and stop before they dispose — exactly the
shape `IHostedService` (`WP 4.5`) was built for, and the first Work
Package with a genuine reason to use it as a real, non-infrastructure
consumer (`AT-07`).
**Anticipated decision.** The REST API's hosting scaffold implements
`IHostedService`, discovered and orchestrated identically to any other
hosted service — start Phase 8.1, stop Phase 10.1 (`ADR-0030`). No new
Host Lifecycle phase.
**Alternative considered and rejected.** A bespoke Host-level phase
dedicated to "network services" — rejected because `IHostedService`
already fully describes the start-after-Initialisation/
stop-before-Disposal lifecycle the REST API needs; inventing a parallel
mechanism would duplicate `WP 4.5` for no architectural benefit.

### ADR-0048 — REST Endpoints Dispatch Through the Existing Command Framework

**Originating WP.** `WP 6.3`.
**Context.** Without an explicit decision, a REST endpoint implementation
could easily grow its own request-handling logic directly, duplicating
what the Command Framework already does.
**Anticipated decision.** Every REST route registered via
`IApiEndpointRegistry.MapCommand` dispatches through the existing,
unmodified `ICommandRegistry.InvokeAsync` — realising the Command
Framework's own original design intent (`Command Framework
Architecture.md`: "...or a future automation/AI service") rather than
introducing a second invocation mechanism.
**Alternative considered and rejected.** REST endpoints calling
application/domain logic directly, bypassing the Command Framework —
rejected because it would create two parallel, divergent invocation
paths (menu/toolbar/keyboard-shortcut-originated commands vs.
REST-originated calls) for what should be the same underlying
operation, undermining the very uniformity `ADR-0036`/`ADR-0037`
established.

### ADR-0049 — Adopting ASP.NET Core/Kestrel for the REST API

**Originating WP.** `WP 6.3`.
**Context.** `ADR-0005` committed this platform to a custom, minimal
dependency-injection container specifically to avoid a large third-party
framework dependency. The REST API is the first Work Package with a
plausible, well-justified reason to reconsider that stance for a
narrower purpose: HTTP hosting.
**Anticipated decision.** Adopt ASP.NET Core/Kestrel — part of the .NET
SDK's own shared framework, not a third-party NuGet package in the sense
`ADR-0005` targeted — for HTTP listening, routing, and TLS only. This
platform's own DI container, Command Framework, and every other
platform service remain entirely unchanged and unreplaced; ASP.NET
Core is confined to the REST API's own hosting boundary.
**Alternative considered and rejected.** Hand-rolling an HTTP/1.1
listener directly over raw sockets — rejected as a disproportionate
undertaking (TLS, chunked transfer encoding, header parsing, routing)
compared to reusing a component already bundled with the .NET SDK the
project already targets, for a benefit (avoiding one framework
dependency) `ADR-0005`'s own reasoning was never actually about — that
ADR was about *dependency injection*, not HTTP hosting.

### ADR-0050 — License Validation Is a Host-Startup, Host-Fatal Gate

**Originating WP.** `WP 6.6`.
**Context.** `WP 5.2`/`ADR-0039` had to solve a real Composition-Root/
container-timing chicken-and-egg problem for Diagnostics using `Func<T>`
accessors. Licensing has a superficially similar timing constraint and
risks reaching for the same solution unnecessarily.
**Anticipated decision.** `ILicenseValidator` runs at Host startup,
before the DI container exists, reading its own license-file source
directly with no constructor dependencies — deliberately a leaf, mirroring
Platform Version (`ADR-0023`) — rather than depending on any
container-constructed service. An invalid license aborts startup,
Host-fatal, per `ADR-0013`'s existing platform-service-failure
classification.
**Alternative considered and rejected.** Reusing `WP 5.2`'s own
`Func<T>` lazy-accessor pattern to let Licensing depend on a
container-constructed `Persistence` singleton — rejected because it
would recreate the exact timing dependency that pattern exists to work
around, when the simpler option (Licensing reads its own file directly,
no container dependency at all) avoids the problem entirely rather than
solving it after the fact.

### ADR-0051 — Export/Import Is Orthogonal to the Internal Persistence Abstraction

**Originating WP.** `WP 6.7`.
**Context.** "Persistence" and "Export/Import" both sound like "get data
in and out of the platform," inviting the same kind of surface-level
confusion `ADR-0022` and `ADR-0037`/`RD-0039` already had to resolve for
other service pairs.
**Anticipated decision.** `IPersistenceStore` is internal, platform-owned
key-value/document state, never directly exposed to a user. Export/
Import is user-facing, `Stream`-based, portable-artifact I/O with its
own versioned, round-trip-safe contract (`IExportable.SchemaVersion`);
it reads *from* whatever service owns the data being exported (Settings,
a Reporting definition), never from `IPersistenceStore` directly.
**Alternative considered and rejected.** Building Export/Import directly
on top of `IPersistenceStore`, treating a raw dump of persisted
key-value pairs as the "export format" — rejected because it would
couple a user-facing portable artifact's format to an internal storage
implementation detail, breaking the moment Persistence's own internal
representation changes for unrelated reasons.

## Summary Table

| ADR | Title | Originating WP |
|---|---|---|
| ADR-0040 | Reporting Is DI-Public and Orthogonal to Export/Import | `WP 6.0` |
| ADR-0041 | A Shared Persistence Abstraction Serves Settings and Audit | `WP 6.4` |
| ADR-0042 | Settings Is DI-Public and Distinct From Configuration | `WP 6.4` |
| ADR-0043 | Identity Model Scope: Local-Only, Extensible | `WP 6.1` |
| ADR-0044 | Authorization Enforcement Point | `WP 6.1` |
| ADR-0045 | Audit Is a Durable, Queryable Record, Distinct From Logging and Diagnostics | `WP 6.5` |
| ADR-0046 | Notifications Are Derived From Events, Not a Replacement Pub/Sub | `WP 6.2` |
| ADR-0047 | REST API Is a Background Hosted Service | `WP 6.3` |
| ADR-0048 | REST Endpoints Dispatch Through the Existing Command Framework | `WP 6.3` |
| ADR-0049 | Adopting ASP.NET Core/Kestrel for the REST API | `WP 6.3` |
| ADR-0050 | License Validation Is a Host-Startup, Host-Fatal Gate | `WP 6.6` |
| ADR-0051 | Export/Import Is Orthogonal to the Internal Persistence Abstraction | `WP 6.7` |

## Related Documents

`Release Architecture.md`; `Platform Services Overview.md`; `Public
Interface Catalogue.md`; `Service Lifecycle.md`; `docs/adr/` (where each
of these will be formally authored, during its own owning Work
Package's dedicated architecture phase); `docs/academy/06 Engineering
Standards/Engineering Governance.md` §5 (ADR criteria).
