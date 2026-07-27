# ADR-0033: The Shell Is a Composition Root Layered Above the Runtime Host, Not a Module or a Hosted Service

## Status

Accepted — `v0.5.0` "Developer Experience" release, `WP 5.0C` (Shell &
Composition Framework Architecture), 2026-07-27. Resolves the question
`WP 5.0C`'s own brief names first: how does `Tempest.App` consume the
platform?

## Context

`Tempest.App`'s current entry point (`Program.cs`) does not construct or
run `ITempestHost`/`TempestHostBuilder` at all — confirmed directly,
during this Work Package's own Repository Investigation. It runs a
pre-module-pipeline console loop against bootstrap-era services
(`BootstrapService`, `HostingService`, `ProjectService`), a fact already
disclosed once, during `WP 5.0A`'s own investigation, and re-confirmed,
unchanged, here. Something must become the code that actually assembles a
*running* platform and presents it to a user — `ADR-0009` named this
destination in `WP 2.5`, calling it "the composition root... eventually
`Program.cs`," without ever designing it. Three structurally different
shapes were available for that "something": a **module**, participating
in the existing module pipeline; a **hosted service**, participating in
the existing Background Services orchestration; or a **composition root**,
sitting above the Runtime Host entirely, the way `ADR-0009` already
anticipated.

## Decision

**The Shell is a composition root, layered above `ITempestHost`. It is
neither a module nor a hosted service.** `Tempest.App`'s entry point
constructs a `TempestHostBuilder`, builds an `ITempestHost`, and hands it
to the Shell; the Shell runs the Host (via a background task, since
`RunAsync` blocks until shutdown), presents the platform's capabilities
to a user, and requests shutdown when the user is done. This is the exact
role `ADR-0009` already reserved for `Program.cs` — this decision
fulfils it, rather than introducing a competing one.

**Why not a module.** A module's `InitialiseAsync`/`StartAsync` are
expected to *complete*, so `ModuleLifecycleManager`'s batch-per-phase
orchestration can proceed to the next module, and so the Host can
eventually reach `Running`. A Shell whose own presentation loop blocks on
console input has no natural completion point inside either lifecycle
method — it would either hang Host startup indefinitely, or be forced to
spawn its own background thread from within a module anyway, at which
point nothing about the module pipeline was actually being used for the
Shell's own purpose. See `RD-0034`.

**Why not a hosted service.** `IHostedService.StartAsync`'s own contract
states it is "invoked once, between Module Initialisation and Runtime
Running" — the same bounded-completion expectation, for the same reason:
`HostedServiceManager.StartAllAsync` must finish before the Host can
reach `Running`. A blocking, interactive Shell cannot satisfy this any
more than it could a module's `StartAsync`. See `RD-0035`.

**Why a composition root.** The Shell needs to exist, and begin doing its
own work, *before* the Host it presents even begins running — it is the
thing that calls `Build()` in the first place. A component the Host
itself constructs and drives (a module, a hosted service) cannot also be
the thing that constructs and drives the Host. Only a layer sitting
above the Host, exactly where a human operator or a test harness already
sits today, can coherently be both.

## Consequences

**Positive:**

- Fulfils `ADR-0009`'s own forward reference directly — `Program.cs`
  finally becomes the composition root that ADR already named as this
  project's eventual destination, rather than leaving it a permanently
  deferred aspiration.
- The Runtime Host requires no new lifecycle phase, no new state, and no
  awareness of the Shell's existence — `Runtime Host Architecture.md`'s
  own "UI-agnostic" requirement is unaffected, confirmed rather than
  merely asserted.
- Application lifetime and Shell lifetime can coincide without
  contradiction, since both now describe the same outer layer
  (`Program.Main`), not two competing execution models.

**Negative:**

- The Shell must run `host.RunAsync(...)` as a background task rather than
  awaiting it directly, and must coordinate its own shutdown
  (`StopAsync`/await/`DisposeAsync`) explicitly — a small, disclosed
  amount of concurrency-coordination responsibility the Shell now owns
  that a module or hosted service would not have needed to.
- A future contributor accustomed to frameworks where "the application"
  and "the thing the host manages" are the same object may find "the
  Shell constructs the Host, not the other way around" counter-intuitive
  at first — the Academy article accompanying this ADR exists specifically
  to make this ordering, and the reasoning behind it, discoverable.

## Future Considerations

If a future, fundamentally different shell (a GUI, a web front end) is
ever built, it adopts this same composition-root relationship to
`ITempestHost` — construct, run in the background, present, request
shutdown — rather than re-opening the module-vs-hosted-service-vs-
composition-root question this ADR already settles. If a genuine need
ever arises for the Shell's own lifetime to diverge from the process's
own lifetime (outliving it, or being a strict subset of it), that is a
new architectural question warranting its own ADR, not a silent extension
of this one.
