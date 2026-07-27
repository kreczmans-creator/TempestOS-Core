# WP 5.0C — Shell & Composition Framework Architecture

## 1. Introduction

WP 5.0C, like WP 2.7A, WP 4.2, WP 4.4, WP 4.5, and WP 5.0A's own
architecture phases before it, produced no production code. Its job was
to design how `Tempest.App` consumes the platform: the Shell that becomes
`Tempest.App`'s own composition root, presents Navigation and the Event
Bus to a user, and reserves explicit room for Commands and Diagnostics —
resolving, in writing, a question this project's own `ADR-0009` first
named at `WP 2.5` and left open ever since: what does "eventually
`Program.cs`" actually look like?

## 2. Purpose

To answer every question this Work Package's own brief named: what
`Tempest.App` currently does, and what it does not; what belongs in the
Shell versus `Tempest.Core`; how the application's own lifetime relates
to the Shell's; how pages are created and selected; whether dependency
injection participates in that process; whether multiple workspaces are
worth building room for; and how the Shell consumes Navigation, the Event
Bus, Hosted Services, and the not-yet-built Command Framework and
Diagnostics — before a single line of implementation exists.

## 3. Background

By the time `WP 5.0C` began, Navigation was fully designed and
implemented (`WP 5.0A`/`WP 5.0B`), and its own architecture document had
already named the Shell's role without designing it: `Tempest.App`
"maintains its own, private mapping from `NavigationItem.Id` to whatever
it knows how to render." Repository Investigation confirmed this remains
exactly as `WP 5.0A` first found it — `Tempest.App`'s entry point still
does not construct or run `TempestHost` at all, running instead a
bootstrap-era console loop against pre-module-pipeline services. This
Work Package is where that gap finally gets a design, not merely a
disclosure.

## 4. The Problem

1. **What is the Shell, structurally?** A module, a hosted service, or a
   composition root sitting above the Runtime Host entirely — and does
   `ITempestHost`'s own current public surface even allow any of these to
   reach the platform services a Shell would need?
2. **What belongs in `Tempest.App` versus `Tempest.Core`?** Presentation,
   page construction, and user input handling versus orchestration,
   startup, and shutdown — and does drawing this boundary require any
   change to the Runtime Host's own UI-agnostic contract?
3. **How are pages created and selected**, and does dependency injection
   participate in that process the same way it already does for modules?
4. **Is a first-class Shell state machine, paralleling `HostState`,
   actually needed** — or do Application lifetime and Shell lifetime
   simply coincide for `v0.5`?
5. **What does the Shell need from Navigation, the Event Bus, Hosted
   Services, and the not-yet-built Command Framework and Diagnostics —
   and can every one of those integrations be expressed without a
   dependency pointing the wrong direction?**

## 5. The Design

See `docs/architecture/Shell & Composition Framework Architecture.md`,
`ADR-0033`, `ADR-0034`, and `ADR-0035` in full. In summary: the Shell is
a **composition root** — the exact role `ADR-0009` reserved for
`Program.cs` at `WP 2.5` without ever designing it — constructed directly
by `Tempest.App`'s own entry point, which builds a `TempestHostBuilder`,
runs the resulting `ITempestHost` as a background task, and presents its
capabilities to a user. It is neither a module nor a hosted service,
since both are expected to *complete* their own startup lifecycle
methods promptly, which a blocking, interactive Shell structurally
cannot. `ITempestHost` gains exactly one new, additive member —
`Services`, a read-only, nullable `ITempestServiceProvider` — so the
Shell can resolve `INavigationProvider`/`IEventBus` the same way a module
already does internally, without weakening `ADR-0017`'s own protection
of Discovery, Registration, Lifecycle, and Hosted Service orchestration
(none of which is ever registered in the container, regardless of who
can see it). The Shell owns a closed, hand-registered mapping from
`NavigationItem.Id` to a rendering action for its own built-in pages;
dependency injection participates only at the Shell's own one-time
resolution of platform services, never inside page construction itself.
Workspace, Navigation Region, and Content Region are required for `v0.5`;
a Status Bar is reserved but unpopulated; Dialogs, Notifications, and
module/plugin-contributed rendering are explicitly deferred; multiple
workspaces is rejected outright for a console shell.

## 6. Alternatives Considered

Recorded in full, with reasoning, in `ADR-0033`/`ADR-0035`'s own
Consequences sections, and permanently indexed as `RD-0034` (the Shell as
a module — rejected because a blocking presentation loop cannot complete
inside `InitialiseAsync`/`StartAsync`), `RD-0035` (the Shell as a hosted
service — rejected for the identical bounded-completion reason),
`RD-0036` (module/plugin-contributed page rendering via a DI-routed or
reflection-discovered registry — rejected because no downward-compatible
contract exists for a module to depend on without either leaking a
rendering type into `Tempest.Core` or inverting the four-layer model),
and `RD-0037` (multiple concurrent workspaces — rejected outright, not
merely deferred, since a console shell has exactly one input/output
stream and no plausible near-term consumer).

## 7. Why This Solution Was Chosen

Every mechanical decision traces back to the same governing question this
release has now applied consistently across Navigation and the Shell
alike: does an already-proven pattern already answer this, or is this
genuinely new ground? The Shell's own role as composition root was not
invented here — `ADR-0009` named it three releases ago; this Work Package
simply designed the destination that ADR always pointed toward.
`ITempestHost.Services` reuses the exact resolution API a module already
uses internally, exposed one layer higher, rather than inventing a second
mechanism. Page construction's DI boundary was drawn by asking the same
question `ADR-0031` already answered for Navigation itself: does this
concept need to know *how* something is rendered, or only *that* a
destination was chosen? The Shell's own page mapping needs the former, by
definition — which is exactly why it lives in `Tempest.App`, never in
`Tempest.Core`.

## 8. Architectural Principles

- **Reuse Before Invention** — `ITempestHost.Services` reuses
  `ITempestServiceProvider` unchanged; the Shell's subscription to
  `NavigationRequestedEvent` reuses the identical publisher-knows-nothing-
  about-subscribers shape `ClockModule`/`ClockLifecycleObserverModule`
  already proved.
- **Platform Layering** (`ADR-0023`) — the Shell sits above all four
  existing layers, consuming the Runtime Host's own public surface
  exactly as a human operator or test harness already does; no module
  gains any new path toward `Tempest.App`.
- **Minimal Host Complexity** — one new, additive, read-only property on
  `ITempestHost`; zero new Host Lifecycle phase; zero change to `Runtime
  State Machine.md`.
- **Avoid Speculative Design** — Dialogs, Notifications, themes,
  module/plugin-contributed rendering, and a first-class Shell state
  machine were each seriously considered and explicitly deferred or
  rejected, precisely because no real consumer for any of them exists
  yet.

## 9. Benefits

- **`ADR-0009`'s own forward reference, open since `WP 2.5`, now has a
  concrete answer** — "eventually `Program.cs`" is no longer a permanently
  deferred aspiration.
- **Zero new Host Lifecycle phase or Runtime State Machine change is
  required** — confirmed by design: `Services` is purely additive, and
  the Shell's own lifecycle is observed, never orchestrated, by the Host.
- **`ADR-0017`'s own boundary is reaffirmed, not merely left alone** —
  this design is the direct, mechanical proof that exposing the
  container's resolution surface externally cannot, structurally, weaken
  the Host-owned/DI-public distinction that already governs everything
  else in this platform.
- **Plugin-contributed Navigation items render automatically, with zero
  Shell-side special-casing** — the same "the platform holds a registry
  it did not create the meaning of" pattern already proven end to end by
  `WP 5.0B`'s own plugin-compatibility test, now extended one layer
  further, to the very first non-module, non-test consumer of Navigation.

## 10. Trade-offs

- This is documentation only — nothing here is enforced by a compiler,
  test, or running code yet, exactly as every architecture-only Work
  Package in this project's history has noted about itself.
- The Shell must coordinate its own concurrency (running `RunAsync` as a
  background task, awaiting it explicitly at shutdown) — a small,
  disclosed responsibility a module or hosted service would not have
  carried, accepted as the cost of the Shell being the one thing that
  must exist before the Host it presents does.
- Module- or plugin-contributed page rendering has no answer yet — a
  real, disclosed limitation for any future plugin wanting to draw its
  own screen, not an oversight.
- The bootstrap-era `BootstrapService`/`HostingService`/`ProjectService`
  code this Work Package's own Repository Investigation re-confirmed is
  explicitly untouched and unmigrated — named here so it is not silently
  assumed to be in scope for `WP 5.0D`.

## 11. Common Mistakes

The mistake most worth naming here is one avoided, not one that happened:
assuming the Host should be extended to *drive* the Shell (a new
lifecycle phase, a new orchestrated participant), rather than recognising
the Shell as the thing that drives the Host. Every existing Host-owned
mechanism (Discovery, Registration, Lifecycle, Hosted Services) exists
specifically because the Host must retain sole orchestration authority
over the module pipeline — reaching for one of those same mechanisms for
the Shell would have quietly inverted who constructs whom, exactly the
inversion `ADR-0033` exists to rule out explicitly rather than leave
implicit.

## 12. Future Evolution

- **`WP 5.0D` (Shell & Composition Framework Implementation)** should
  build `ITempestHost.Services`, the Shell itself, and its built-in
  Workspace/Navigation Region/Content Region, proving each against the
  real, unmodified `TempestHost` and `INavigationProvider` — mirroring
  `WP 5.0B`'s own implementation-then-proof sequence for Navigation.
- **`WP 5.1` (Command Framework)**, once implemented, becomes a new
  source of Shell input handling, exactly as `ADR-0022` already
  illustrates — no change to the Shell's own composition model is
  anticipated.
- **`WP 5.2` (Diagnostics)** populates the already-reserved Status Bar
  region and may register its own `NavigationItem`, proving the Shell's
  own placeholder-then-real-page path against a second, non-synthetic
  consumer.

## 13. Key Takeaways

1. A composition root does not need to be invented from nothing — this
   project's own `ADR-0009` had already named the Shell's destination
   three releases before this Work Package finally designed it; the
   right move was recognising that forward reference and fulfilling it,
   not treating the question as newly open.
2. "Does this component complete its own startup lifecycle method
   promptly?" is the concrete, structural test that rules a module or a
   hosted service out for a blocking, interactive concern — not a matter
   of preference, but a direct consequence of what
   `ModuleLifecycleManager`/`HostedServiceManager` both require to
   proceed.
3. Exposing a container's resolution surface more broadly does not, by
   itself, weaken an existing ownership boundary — what matters is
   whether anything new becomes *registered*, not whether the container
   object itself becomes *visible*. `ADR-0017`'s protection survives this
   design fully intact for exactly that reason.

---

## Architectural Debt Assessment

**No new debt introduced.** This Work Package produced three ADRs, one
architecture document, and four Rejected Designs entries; no code exists
for it to affect. Four named, disclosed deferrals (module/plugin-
contributed rendering; Dialogs/Notifications as first-class services;
themes; a first-class Shell state machine) and one outright rejection
(multiple workspaces) are accepted design exclusions, not newly
discovered debt. Every other debt item on record from the Foundation
phase and `WP 5.0A`/`WP 5.0B` remains exactly as previously described.

## Observations

- **Files added**: `docs/adr/ADR-0033-shell-is-a-composition-root-
  layered-above-the-runtime-host.md`; `docs/adr/ADR-0034-tempesthost-
  exposes-a-read-only-service-resolution-surface.md`; `docs/adr/ADR-0035-
  shell-owns-page-view-construction-independent-of-the-di-container.md`;
  `docs/architecture/Shell & Composition Framework Architecture.md`; 4 new
  Rejected Designs entries (`RD-0034`–`RD-0037`); this retrospective; a
  new Academy concept guide (`02 Runtime Architecture/10-shell-and-
  application-composition.md`). Zero production code files touched —
  this Work Package's own scope is architecture only.
- **ADRs required**: 3 (`ADR-0033`, `ADR-0034`, `ADR-0035`) — each
  written in full, alongside the architecture document, as this Work
  Package's entire deliverable.
- **Risks discovered**: none new. The gap this Work Package designs
  around (`Tempest.App` never having constructed a real `ITempestHost`)
  was already disclosed once, during `WP 5.0A`'s own investigation; this
  Work Package re-confirms it, unchanged, and is itself the response to
  it, not a newly discovered risk.
- **Readiness assessment**: the design is complete and sound. No
  architectural blocker remains before `WP 5.0D`'s own implementation
  begins. This design's own Ownership, Dependency Direction, and
  Composition Model sections were produced with the same rigour `WP 5.0A`
  established for Navigation, and are ready to be realised without
  deviation.
