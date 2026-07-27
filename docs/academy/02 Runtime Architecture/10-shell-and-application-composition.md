# Shell & Application Composition

## 1. Introduction

The Shell (designed `WP 5.0C`, implemented `WP 5.0D`,
`ADR-0033`/`ADR-0034`/`ADR-0035`) is TempestOS's answer to a question the
platform has been able to avoid until now: once every platform capability
exists — Discovery, Dependency Injection, the Event Bus, Navigation —
*something* has to actually assemble a running instance and put it in
front of a person. This document teaches the reasoning behind that
something's design — not its exact method signatures, which belong to
`Shell & Composition Framework Architecture.md` and now exist in code,
as `TempestShell` (`Tempest.App.Shell`), exactly as designed.

## 2. Purpose

To explain why "the thing that runs the application" is not the same
component as "the thing the application runs," and to name the recurring
mistake this design exists to prevent: reaching for an existing
orchestration mechanism (a module, a hosted service) for a job that
structurally cannot fit inside it, merely because that mechanism is
already there.

## 3. Background

Every platform capability TempestOS has built — Discovery, Dependency
Injection, the Event Bus, Navigation — is something a *module* consumes.
Nothing until now has asked: who assembles the platform in the first
place, and presents it to a human? `Tempest.App`, TempestOS's own
executable, has never actually answered this — its entry point runs a
bootstrap-era console loop that predates the module pipeline entirely and
does not construct or run `TempestHost` at all. `ADR-0009`, written three
releases earlier, already anticipated that `Program.cs` would eventually
become "the composition root" — without ever designing what that meant
concretely. The Shell is that design.

## 4. The Problem

1. **What kind of thing is "the thing that runs the application"?** A
   participant the Host drives (a module, a hosted service), or something
   that drives the Host?
2. **Where is the line** between "orchestrating the platform" (already
   the Runtime Host's job) and "presenting the platform to a user" (not
   yet anyone's job)?
3. **How does whatever presents the platform actually reach the services
   it needs to present** — Navigation, the Event Bus — given that today,
   nothing outside the module pipeline can resolve either?
4. **Who constructs a page**, and does that construction need the same
   machinery a module's own construction already uses?

## 5. The Design

**The Shell sits above the Runtime Host, not inside it.** It is a
*composition root* — `Tempest.App`'s own entry point constructs a
`TempestHostBuilder`, builds an `ITempestHost`, and hands it to the
Shell, which runs it (as a background task, since running blocks until
shutdown), presents its capabilities, and asks it to stop when the user
is done. This is the exact role `ADR-0009` reserved for `Program.cs`
without ever building it.

**The Shell is not a module or a hosted service**, because neither can
structurally accommodate a blocking, interactive presentation loop: a
module's `InitialiseAsync`/`StartAsync` and a hosted service's
`StartAsync` are each expected to *complete*, so the platform can proceed
toward `Running`. Something that blocks on user input forever cannot
live inside either.

**`ITempestHost` gains one new, read-only door: `Services`.** Once
Dependency Injection is built, this property hands out the same
`ITempestServiceProvider` a module already resolves its own dependencies
through — the Shell uses it to reach `INavigationProvider` and
`IEventBus`, exactly as a module would, just from one layer higher up.
Nothing that was Host-owned before (Discovery, Registration, Lifecycle,
Hosted Service orchestration) becomes reachable through this door,
because none of them was ever placed in the container to begin with.

**The Shell owns its own pages, entirely separately from the platform's
own DI container.** A `NavigationItem`'s `Id` is just data; turning it
into something on screen is the Shell's own, hand-maintained mapping —
built by the Shell, for the Shell, using whatever platform services the
Shell already resolved once at startup.

## 6. Alternatives Considered

Recorded in full in `ADR-0033`/`ADR-0035` and `RD-0034`–`RD-0037`: the
Shell as a module (rejected — a blocking presentation loop cannot
complete inside a lifecycle method expected to return promptly); the
Shell as a hosted service (rejected — the identical bounded-completion
problem); routing page construction through the platform's own DI
container so modules and plugins could contribute their own views
(rejected for now — no downward-compatible contract exists for a module
to depend on without inverting the platform's own layering); and multiple
concurrent workspaces (rejected outright — a console shell has exactly
one input and output stream).

## 7. Why This Solution Was Chosen

The one genuinely new judgment call this design makes — is the Shell a
participant the Host drives, or the thing that drives the Host — was
resolved by asking a concrete, structural question: does this component
need to exist *before* the thing it is presenting even starts running?
For the Shell, the answer is unambiguously yes — it is the thing that
calls `Build()` in the first place. Every other design decision here
follows from taking that answer seriously rather than reaching for the
nearest existing mechanism (module, hosted service) out of familiarity.

## 8. Architectural Principles

- **Platform Layering** (`ADR-0023`) — the Shell is the layer *above*
  Modules, Platform APIs, Platform Services, and the Runtime Host,
  consuming the topmost of those four, never contained by it.
- **Reuse Before Invention** — `ITempestHost.Services` reuses the
  existing `ITempestServiceProvider` unchanged; the Shell's own
  subscription to `NavigationRequestedEvent` reuses the exact
  publisher/subscriber shape the Event Bus already proved.
- **Avoid Speculative Design** — Dialogs, Notifications, themes, and
  module/plugin-contributed rendering were each seriously considered and
  explicitly deferred, since no real consumer for any of them exists yet.

## 9. Benefits

- A question this project's own foundational ADR (`ADR-0009`) raised
  three releases ago, and left open ever since, now has a concrete,
  written answer.
- The Runtime Host needed no new lifecycle phase, no new state, and no
  awareness that a Shell exists at all — its own UI-agnostic contract is
  unaffected, not merely preserved by convention.
- A plugin-contributed navigation item renders automatically, with zero
  Shell-side special-casing, the moment the Shell exists — the same
  "the platform holds a registry it did not create the meaning of"
  pattern already proven for ordinarily-discovered modules, now reaching
  its first genuinely external consumer.

## 10. Trade-offs

- The Shell must manage its own concurrency — running the Host in the
  background while presenting on its own thread — rather than the
  platform handling this for it.
- No module or plugin can yet contribute its own page; it can only
  contribute a `NavigationItem`, which the Shell will render as a generic
  placeholder until a future Work Package designs a real answer.
- No first-class Dialog, Notification, or theming concept exists yet —
  today's console shell has no real need for any of them.

## 11. Common Architectural Mistakes

**Assuming "the thing that runs the app" must be driven by the Host,
because everything else so far has been.** Every prior platform
capability — modules, hosted services — is something the Host discovers
or constructs and then drives through a lifecycle *it* owns. The Shell
breaks that pattern deliberately: it is the thing that constructs the
Host, not a thing the Host constructs. Missing this distinction would
lead naturally toward trying to wedge a blocking, interactive loop into a
module's `StartAsync` — exactly the mistake `RD-0034`/`RD-0035` exist to
head off.

**Assuming exposing the DI container more broadly must weaken an
existing boundary.** `ITempestHost.Services` does not touch, weaken, or
bypass `ADR-0017`'s protection of Discovery, Registration, or Lifecycle
— those remain unreachable for the simple reason that none of them was
ever registered in the container at all. Visibility of the container
object and registration of a specific service are two separate
questions; this design only changes the first.

**Assuming referencing a type's `const` field loads its declaring
assembly.** A genuine implementation-time finding (`WP 5.0D`): the
Shell's own page mapping keys itself off
`NavigationSampleModule.NavigationItemId` — a compile-time
`const string`. The C# compiler inlines a `const`'s value directly into
the *referencing* assembly's own IL; nothing about reading it requires
the CLR to load the *declaring* assembly at runtime. Discovery only sees
what `AppDomain.CurrentDomain.GetAssemblies()` already contains — so
without an explicit `typeof(NavigationSampleModule).Assembly` access
forcing the load first, `Tempest.Samples`'s own modules would never
appear, and Discovery would silently find zero of them. The general
lesson: a `const` reference and a `typeof`/instance reference look
similar in source but carry a materially different runtime guarantee —
worth knowing before assuming "I referenced the type, so its assembly
must be loaded."

## 12. Future Evolution

- `WP 5.0D` implemented this design exactly as approved, against the real
  `TempestHost`, `NavigationSampleModule`, and
  `SecondaryNavigationSampleModule` — mirroring the Event Bus's and
  Navigation's own implementation-then-proof sequence, and surfacing one
  genuine, non-obvious finding along the way: see "Common Architectural
  Mistakes," below, on `const` fields and assembly loading.
- `WP 5.1` (Command Framework) becomes a new source of Shell input
  handling, wired exactly as `ADR-0022` already illustrates.
- `WP 5.2` (Diagnostics) populates the Shell's already-reserved Status
  Bar region.

## 13. Key Takeaways

1. "Does this need to exist before the thing it presents starts running"
   is the concrete test that separates a composition root from a
   participant the runtime drives — worth asking explicitly, not
   assumed from whichever mechanism happens to already exist.
2. Making a resource more *visible* to an external caller is not the same
   change as making more of that resource *resolvable* — a boundary
   enforced by what is registered survives a boundary enforced by who can
   see the container perfectly intact.
3. A forward reference left open in an early architectural decision
   (`ADR-0009`'s own "eventually `Program.cs`") is worth revisiting
   deliberately once the platform is ready for it, rather than treating
   it as settled simply because time has passed since it was written.
4. A `const` field is resolved entirely at compile time, in the
   *referencing* assembly — reading one is not evidence the *declaring*
   assembly has been, or ever will be, loaded at runtime. Where an
   assembly's mere presence matters (as it does for reflection-based
   discovery), force the load explicitly and say why, rather than relying
   on an incidental-looking reference to do it implicitly.

## Related Documents

`docs/architecture/Shell & Composition Framework Architecture.md`;
`ADR-0009`; `ADR-0017`; `ADR-0022`; `ADR-0023`; `ADR-0031`; `ADR-0033`;
`ADR-0034`; `ADR-0035`; `docs/architecture/Rejected Designs.md`
(`RD-0034`–`RD-0037`); `docs/academy/02 Runtime Architecture/
09-navigation-architecture.md`; `docs/academy/02 Runtime Architecture/
05-the-runtime-host.md`.
