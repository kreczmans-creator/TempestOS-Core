# Navigation Architecture

## 1. Introduction

`Tempest.Core.Navigation` (designed `WP 5.0A`, implemented `WP 5.0B`,
`ADR-0031`/`ADR-0032`) is TempestOS's answer to a question every
application eventually faces: how do built-in pages, future modules, and
future plugins all contribute to one coherent way of getting around,
without the platform needing to know what any of them look like? This
document teaches the reasoning behind that design — not its exact method
signatures, which belong to `Navigation Framework Architecture.md` and
now exist in code exactly as designed, with zero deviation, in
`src/Tempest.Core/Navigation/`.

## 2. Purpose

To explain why a concept that sounds inherently visual — "navigation" —
can still have a genuinely platform-owned, UI-agnostic model underneath
it, and to name the recurring mistake this design exists to prevent: letting a
rendering concern leak into a layer that has no business knowing about
it.

## 3. Background

Every capability TempestOS has built through `v0.4.0` — Configuration,
Logging, the module pipeline, the Event Bus, Background Services — is
provably UI-agnostic: none of it renders anything, none of it references
a UI framework. `Tempest.App` itself, meanwhile, is a bare console loop
with no navigation concept of any kind. Introducing Navigation therefore
raises a question none of TempestOS's prior work packages had to answer:
is this the first genuinely *application-layer* concept, that belongs
outside `Tempest.Core` entirely, or is there a UI-agnostic model hiding
inside it, the way there already was inside "a command" and "an event"?

## 4. The Problem

1. **Where is the line?** Between "what pages exist, how are they
   organised" (a question about data) and "what does picking one
   actually put on screen" (a question about rendering) — and does
   TempestOS's existing four-layer model (`ADR-0023`) already have room
   for the first without needing the second?
2. **Who decides what a module contributes?** Does a module declare its
   navigation items passively, or register them actively — and does the
   answer depend on *when*, in the Host's own lifecycle, that
   registration needs to happen?
3. **How does "go here" actually reach whatever renders**, without
   inventing a second messaging system alongside the one that already
   exists?

## 5. The Design

**The model lives in `Tempest.Core`; rendering lives in `Tempest.App`.**
A `NavigationItem` is pure data — identity, title, an optional *symbolic*
icon key (never a rendered image), ordering, grouping, an optional parent
reference for hierarchy, and an optional visibility predicate. It carries
no view, no delegate, no UI framework reference of any kind.
`INavigationProvider`/`NavigationService` holds a registry of these and
exposes one behaviour beyond registration: `Navigate(id)`, which
publishes a `NavigationRequestedEvent` through the already-existing
`IEventBus` — the identical mechanism a module already uses to publish
any other event. Whatever renders (`Tempest.App`, today; potentially a
different shell entirely, tomorrow) subscribes to that event and decides,
using its own private mapping from `Id` to whatever it knows how to
draw, what actually happens next. `Tempest.Core.Navigation` never sees,
and never needs, that mapping.

**Contribution is imperative, not declarative.** A module or
plugin-loaded module constructor-injects `INavigationProvider` and calls
`Register(...)` from its own `InitialiseAsync`, exactly as it would call
`IEventBus.Subscribe<T>`. See "Platform/Application Separation," below,
and the Reflection-Based Discovery concept guide, for why this is the
correct choice here even though reflection-based discovery has served
three other platform capabilities well.

## 6. Alternatives Considered

Recorded in full in `ADR-0031`/`ADR-0032` and `RD-0030`–`RD-0033`: putting
Navigation entirely in `Tempest.App` (rejected — the model genuinely does
not need to know anything about rendering, so keeping it out of
`Tempest.Core` would separate a platform-shaped registry from every
other platform-shaped registry, for no reason); declarative,
attribute-based contribution (rejected — the instantiation-avoidance
problem that justifies `ModuleMetadataAttribute` does not exist here);
Navigation as a Host-owned collaborator (rejected — it carries no
orchestration authority over anything); a dedicated Navigation
publish/subscribe channel (rejected — the Event Bus already does exactly
this); and a first-class permission model (rejected — nothing in this
platform yet knows what a permission is).

## 7. Why This Solution Was Chosen

Every piece of this design reuses a pattern this platform had already
proven at least once before choosing it. The one genuinely new judgment
call — whether a UI-adjacent concept could still be architecturally
UI-agnostic — was resolved by testing it against the same standard
`ICommand`/`IEvent` already met: does the contract need to know *how*
anything happens, or only *that* something was requested? Navigation's
answer is identical to theirs.

## 8. Architectural Principles

- **Platform Layering** (`ADR-0023`) — the model is a Platform API/
  Service pair; rendering is an application-layer concern depending
  downward on it, never the reverse.
- **Reuse Before Invention** — DI-public ownership, imperative
  registration, and Event Bus reuse are each borrowed directly from an
  already-proven precedent.
- **Avoid Speculative Design** — permissions, current-location tracking,
  and declarative contribution were each seriously considered and
  explicitly deferred, not silently omitted.

## 9. Benefits

- A future, entirely different rendering technology could consume the
  exact same `Tempest.Core.Navigation` registry without one line of it
  changing — the strongest available proof the boundary is real.
- Zero new Host Lifecycle phase, zero new discovery mechanism, zero new
  publish/subscribe channel — the design costs the platform almost
  nothing structurally to add.

## 10. Trade-offs

- Whatever renders must maintain its own private `Id`-to-rendering
  mapping — `Tempest.Core.Navigation` will never do this for it, by
  design, not by oversight.
- No permission model exists yet; `IsVisible` is the seam a future one
  would use, not a permission system in disguise today.

## 11. Common Architectural Mistakes

**Assuming "sounds like UI" means "belongs in the application."** The
mistake worth naming most directly: treating Navigation as
self-evidently an application-layer concept purely because its *name*
suggests screens and menus, without asking what data it actually needs
to hold. The correct question is never "does this concept relate to
something a human sees" — nearly everything eventually does — but
"does representing this concept require knowing *how* it is rendered."
`ICommand` represents a user's requested action without knowing what
button triggered it; `NavigationItem` represents a destination without
knowing what it looks like. Both pass the same test.

**Letting a rendering reference leak in "just this once."** A future
contributor extending `NavigationItem` with, say, a `RenderAction`
delegate "because it's convenient for this one case" would quietly
reopen the exact boundary `ADR-0031` exists to hold shut. If a
consumer's own convenience seems to require this, the correct response
is to solve it in `Tempest.App`'s own mapping table, not in
`Tempest.Core.Navigation`.

**Coupling Navigation to Commands because they feel related.** `ADR-0022`
already settled this for the platform as a whole: Navigation and Command
Framework are orthogonal, and application logic — never either
service — is what wires a command's outcome to a navigation request.
Navigation's own design does not reopen this question; it inherits it.

## 12. Future Evolution

- `WP 5.0B` implemented this design exactly as approved, against three
  real, discovered sample modules (`NavigationSampleModule` and two
  companions) each contributing a real navigation item — mirroring the
  Event Bus's own implementation-then-proof sequence, and proving the
  duplicate-ID failure is isolated by the existing, unmodified
  `ModuleLifecycleManager` with no new Host failure policy required, and a
  plugin-loaded module contributes navigation through the identical path
  an ordinarily-discovered module uses, with zero plugin-specific
  mechanism.
- `WP 5.1` (Command Framework) is expected to call
  `NavigationService.Navigate(...)` from application logic without
  either service changing.
- A future permission system plugs into `IsVisible` without
  `NavigationService` itself needing to change.

## 13. Key Takeaways

1. A concept's *name* is not evidence of which architectural layer it
   belongs in — its *data requirements* are.
2. Reusing an already-proven pattern for a new capability is itself a
   quiet, cumulative form of evidence that the original pattern was
   well-designed, not merely convenient the first time.
3. A boundary someone has to actively resist "just this once" leaking
   across is still a real boundary — naming the temptation explicitly, as
   this guide does, is part of what keeps it held.

## Related Documents

`docs/architecture/Navigation Framework Architecture.md`; `ADR-0022`;
`ADR-0031`; `ADR-0032`; `docs/architecture/Rejected Designs.md`
(`RD-0030`–`RD-0033`); `docs/academy/02 Runtime Architecture/
06-platform-layering.md`; `docs/academy/04 Design Patterns/
04-reflection-based-discovery.md`; `docs/academy/02 Runtime
Architecture/04-building-an-event-driven-module.md`.
