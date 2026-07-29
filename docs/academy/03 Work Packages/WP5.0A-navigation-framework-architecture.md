# WP 5.0A — Navigation Framework Architecture

## 1. Introduction

WP 5.0A, like WP 2.7A, WP 4.2, WP 4.4, and WP 4.5's own architecture
phases before it, produced no production code. Its job was to design the
Navigation Framework completely — representation, registration,
ownership, dependency direction, the platform/application rendering
boundary, lifecycle placement, and testing strategy — resolving the one
genuinely open architectural question the `v0.4.0` release carried
forward under this Work Package's prior name, `WP 4.6A`: does Navigation
belong in `Tempest.Core` at all?

## 2. Purpose

To answer, in writing, every question this Work Package's own brief
named: how navigation is represented; how items are registered; whether
modules and plugins contribute navigation, and how; whether registration
is declarative or imperative; whether hierarchy, ordering, grouping,
icons, and visibility are needed for `v0.5`; and precisely where the
boundary between `Tempest.Core` and `Tempest.App` sits — before a single
line of implementation exists.

## 3. Background

By the time WP 5.0A began, `ADR-0022` (Navigation and Command Framework
are orthogonal platform services) had already been decided, during
original `v0.4.0` release planning, resolving the harder of the two
questions `Architecture.md` once named for this Work Package. What
remained undecided was everything else: is Navigation even a
`Tempest.Core` concept, given the platform built so far is entirely
UI-agnostic and `Tempest.App` is a bare console loop with no rendering
abstraction of any kind; and, if so, what does it look like mechanically
— ownership, registration, and how a requested navigation is
communicated to whatever renders.

`v0.4.0` shipped as "Platform Foundation" with Navigation rescoped out
entirely (`docs/releases/v0.4.0/ReleasePlan.md`'s "Scope" section); this
Work Package begins the `v0.5.0` "Developer Experience" release under its
renumbered name.

## 4. The Problem

1. **Platform/application boundary** — is "navigation" inherently a UI
   concept that belongs in `Tempest.App`, or can a *model* of navigation
   be genuinely UI-agnostic, the way `ICommand`/`IEvent` already are?
2. **Ownership** — does `NavigationService` carry any orchestration
   authority over the module pipeline (which would make it Host-owned,
   `ADR-0017`), or is it a peer, DI-public service (`ADR-0020`'s Event
   Bus precedent)?
3. **Registration model** — declarative (an attribute, mirroring
   `ModuleMetadataAttribute`) or imperative (a runtime call, mirroring
   Event Bus subscription) — and does the instantiation-avoidance
   problem `ModuleMetadataAttribute` solves even apply here?
4. **Notification mechanism** — when a navigation is requested, how does
   whatever renders find out — a new, Navigation-specific publish/
   subscribe channel, or reuse of the already-implemented `IEventBus`?
5. **Data shape** — does `v0.5` genuinely need hierarchy, ordering,
   grouping, icons, and visibility, and is a permission model
   appropriate now or premature?

## 5. The Design

See `docs/architecture/Navigation Framework Architecture.md`,
`ADR-0031`, and `ADR-0032` in full. In summary: Navigation's *model* —
`NavigationItem` (data only: identity, title, an optional symbolic icon
key, ordering, grouping, hierarchy via a parent reference, an optional
visibility predicate) and `INavigationProvider`/`NavigationService` (an
imperative registry plus a `Navigate` method) — lives in `Tempest.Core`,
in a new `Tempest.Core.Navigation` namespace, following `ADR-0024`'s
established capability-packaging pattern. Rendering — resolving a
registered item's `Id` to an actual screen or console menu case — is,
and remains, entirely `Tempest.App`'s own responsibility, with zero
rendering type anywhere in `Tempest.Core.Navigation`. `NavigationService`
is DI-public (mirroring the Event Bus, not Discovery), registered
imperatively (mirroring Event Bus subscription, not
`ModuleMetadataAttribute`), and reuses `IEventBus` itself to publish a
`NavigationRequestedEvent` when a navigation is requested — introducing
zero new publish/subscribe machinery. No new Host Lifecycle phase, and
no change to `Runtime State Machine.md`, is required.

## 6. Alternatives Considered

Recorded in full, with reasoning, in `ADR-0031`/`ADR-0032`'s own
Consequences sections, and permanently indexed as `RD-0030` (declarative,
attribute-based navigation contribution — rejected because the
instantiation-avoidance problem `ModuleMetadataAttribute` solves does not
exist for Navigation, which registers *after* the DI container and
modules already exist), `RD-0031` (a dedicated Navigation publish/
subscribe mechanism, separate from the Event Bus — rejected as pure
duplication of already-proven, already-tested machinery), `RD-0032`
(Navigation as a Host-owned collaborator — rejected because it carries no
orchestration authority over the module pipeline, the same non-authority
the Event Bus already has), and `RD-0033` (a first-class permission/role
model in Navigation — rejected as speculative design ahead of any real
authentication/authorization concept existing anywhere in this platform,
echoing `RD-0002`'s own identical reasoning for this same release).

## 7. Why This Solution Was Chosen

Every non-obvious decision traces back to a single governing question
this release has now applied consistently five times: does an
already-proven pattern already answer this, or is this genuinely new
ground? Ownership, registration, and notification each reuse an
already-proven pattern directly (the Event Bus's own DI-public,
imperative, publish/subscribe shape). The one genuinely new question —
whether a UI-adjacent concept like "navigation" can still be
architecturally UI-agnostic — was reasoned through explicitly, not
assumed by analogy: the same split already proven for Commands and
Events (a contract that represents *intent* or *occurrence* without
knowing what happens as a result) applies identically to a contract that
represents *destinations* without knowing how any of them are drawn.

## 8. Architectural Principles

- **Reuse Before Invention** — DI-public ownership, imperative
  registration, and Event Bus reuse for notification are all directly
  reused from the Event Bus's own already-proven shape; zero new
  mechanism is introduced.
- **Platform Layering** (`ADR-0023`) — Navigation's model sits in
  `Tempest.Core` as an ordinary Platform API/Service pair; rendering
  sits in `Tempest.App`, a layer above, depending downward on Navigation,
  never the reverse.
- **Minimal Host Complexity** — zero new Host Lifecycle phase, zero
  change to `Runtime State Machine.md`; `NavigationService` is registered
  during the existing Platform Services Registered phase, exactly like
  the Event Bus.
- **Avoid Speculative Design** — a first-class permission model was
  seriously considered and explicitly deferred, precisely because no
  real authentication/authorization concept, and no real
  navigation-contributing module, yet exists to design one against.

## 9. Benefits

- The one architectural open question this release inherited from
  `v0.4.0`'s own planning (`Architecture.md`'s "Does Navigation belong in
  `Tempest.Core` at all?") now has a decided, written answer, before any
  implementation exists — nothing is left to be discovered as ambiguous
  mid-implementation.
- **Zero new Host Lifecycle phase or Runtime State Machine change is
  required** — confirmed by design, not merely hoped for: Navigation's
  DI-public ownership places it in the exact same registration step the
  Event Bus already uses.
- **A module's own registration failure needs no new Host-level failure
  policy** — because registration happens inside a module's own
  lifecycle method, `ModuleLifecycleManager`'s existing, unmodified
  per-module isolation (`ADR-0013`) already covers it completely.
- Demonstrates that not every new platform capability needs its own
  reflection-based discovery mechanism — the third time this exact
  question was asked (after Modules and Plugins both genuinely needed
  one), and the second time the answer was "no" (after Hosted Services).

## 10. Trade-offs

- This is documentation only — nothing here is enforced by a compiler,
  test, or running code yet, exactly as every architecture-only Work
  Package in this project's history has noted about itself.
- `Tempest.App` (or any future UI shell) must maintain its own, entirely
  separate mapping from `NavigationItem.Id` to whatever it actually
  renders — a disclosed, deliberate consequence of keeping
  `Tempest.Core.Navigation` genuinely rendering-free, not an oversight.
- `NavigationService` now carries a mandatory dependency on `IEventBus`
  — a real, if precedented (`LoggerFactory` → `IConfigurationProvider`),
  platform-service-to-platform-service coupling.
- No permission model, no current-location tracking, and no declarative
  contribution mechanism exist yet — all disclosed, deliberate
  deferrals, not gaps discovered by accident.

## 11. Common Mistakes

The mistake most worth naming here is one avoided, not one that
happened: assuming that because "navigation" *sounds* like a UI concept,
it must therefore live in `Tempest.App`, without examining what a
navigation *model* — as opposed to navigation *rendering* — actually
requires. Examined directly against the precedent `ICommand`/`IEvent`
already established (a contract representing something a human
eventually interacts with, while remaining completely ignorant of how),
the correct answer was that a navigation model is exactly as
UI-agnostic as those two contracts already are — the boundary is not
"Navigation vs. no Navigation in `Tempest.Core`," it is "the model in
`Tempest.Core`, the rendering in `Tempest.App`," and conflating the two
would have either wrongly kept a genuinely platform-shaped registry out
of the platform, or wrongly let a rendering reference leak into it.

## 12. Future Evolution

- **`WP 5.0B` (Navigation Implementation)** should build
  `NavigationItem`/`NavigationService` and prove both against a real,
  discovered module contributing a real navigation item — mirroring
  `WP 4.4D`'s own precedent (implement the Event Bus, prove it directly,
  touch no consumer) — before any consumer module is extended to use it
  for real.
- **`WP 5.1` (Command Framework)**, once implemented, can call
  `NavigationService.Navigate(...)` directly from application logic,
  exactly as `ADR-0022` already illustrates — no change to either
  service is anticipated.
- **A future permission system**, whenever a real one is designed, plugs
  into `NavigationItem.IsVisible` without `NavigationService` itself
  needing to change — the seam already exists, deliberately, per
  `RD-0033`'s own deferral.

## 13. Key Takeaways

1. A concept that sounds inherently UI-shaped can still have a genuinely
   UI-agnostic *model* underneath it — the question worth asking is not
   "does this sound like UI," but "does the data this concept needs
   require knowing how anything is drawn."
2. Not every new platform capability needs its own reflection-based
   discovery mechanism — recognising *why* a prior capability needed one
   (an instantiation-avoidance problem, specific to Discovery's own
   timing) is what correctly rules it out for a case that does not share
   that problem, rather than copying the pattern by surface resemblance
   alone.
3. Reusing an already-proven mechanism (the Event Bus) for a
   structurally similar new need (Navigation's own notification) is
   itself evidence the original mechanism was well-designed — a second,
   independent capability finding no reason to need its own version is a
   stronger endorsement than the mechanism's own first use ever was.

---

## Architectural Debt Assessment

**No new debt introduced.** This Work Package produced two ADRs, one
architecture document, and four Rejected Designs entries; no code exists
for it to affect. Three named, disclosed deferrals (a permission model;
current-location/history tracking; declarative contribution) are
accepted design exclusions, not newly discovered debt. Every other debt
item on record from the Foundation phase remains exactly as previously
described.

## Observations

- **Files added**: `docs/adr/ADR-0031-navigation-contracts-belong-in-
  tempest-core.md`; `docs/adr/ADR-0032-navigation-is-di-public-with-
  imperative-registration.md`; `docs/architecture/Navigation Framework
  Architecture.md`; 4 new Rejected Designs entries (`RD-0030`–`RD-0033`);
  this retrospective; a new Academy concept guide (`02 Runtime
  Architecture/09-navigation-architecture.md`); `docs/releases/v0.5.0/`
  (new release scaffold, renumbering the Developer Experience phase).
  Zero production code files touched — none exist for this Work Package
  to touch.
- **ADRs required**: 2 (`ADR-0031`, `ADR-0032`) — both written in full, as
  this Work Package's entire deliverable alongside the architecture
  document.
- **Risks discovered**: none new. The one risk this release inherited
  concerning Navigation (`Risks.md` R2, deferred out of `v0.4.0`) is
  addressed by this design exactly as R2's own mitigation anticipated —
  the architecture-then-implementation split, mirroring `WP 2.7A`'s own
  approach to the Runtime Host.
- **Readiness assessment**: the design is complete and sound. No
  architectural blocker remains before `WP 5.0B`'s own implementation
  begins. This design's own Public Surface, Ownership, and Dependency
  Direction sections were produced with the same rigour `WP 4.5`'s own
  Background Services design phase established, and are ready to be
  realised without deviation.
