# WP 4.0 — Platform Contracts

## 1. Introduction

WP 4.0 is the first implementation work package of v0.4.0, and the first
work package of any kind to touch `Tempest.Core` since the Runtime
Foundation (v0.3.0) was tagged. Its job was narrow and deliberate: define
exactly six platform contracts — `IModule` (re-affirmed, not redefined),
`IHostedService`, `ICriticalBackgroundService`, `ICommand`, `IEvent`, and
`IEventHandler<T>` — and nothing else. No dispatcher, no event bus, no
Host-level wiring. Interfaces only.

## 2. Purpose

To give every later v0.4.0 work package (Event Bus, Background Services,
Command Framework, Navigation, and beyond) one settled vocabulary to build
against, so none of them invents its own conventions along the way — and to
prove, in the smallest possible increment, that a work package can add new
platform surface without disturbing a single byte of already-shipped
runtime behaviour.

## 3. Background

v0.4.0's planning phase (two rounds of review) had already made every
decision this work package needed before a line of code was written:
ADR-0020 (Event Bus is DI-public), ADR-0021 (background service failures
are isolated by default), ADR-0022 (Navigation and Commands are
orthogonal), and a governing philosophy — *only define a contract when
there is enough understanding to make it stable* — that explicitly excluded
`INavigationProvider` and `IDiagnosticsProvider` from this work package's
scope, not even as provisional placeholders. WP 4.0's job was translation
of already-settled decisions into code, in the same sense WP 2.7B's
implementation of the Runtime Host was translation of WP 2.7A's frozen
architecture — not invention.

## 4. The Problem

1. **Where do five brand-new interfaces belong**, given the existing
   codebase's consistent one-namespace-per-capability convention
   (`Tempest.Core.Configuration`, `.Logging`, `.Modules`,
   `.DependencyInjection`, `.Runtime`) and one specific naming trap
   (`Tempest.Core.Hosting` already means something else, per ADR-0016)?
2. **Does `IHostedService`'s name collide with anything**, given
   `Microsoft.Extensions.Hosting.IHostedService` is a well-known name
   elsewhere in .NET?
3. **What is the minimal, stable shape for each of the five new
   contracts**, without guessing at behaviour their owning work packages
   (WP 4.4, 4.5, 4.7) haven't designed yet?
4. **Does introducing these contracts change anything about how Discovery,
   or any other existing service, behaves today?**

## 5. The Design

Five new files, three new namespaces, zero changes to existing production
code:

- `Tempest.Core.BackgroundServices` — `IHostedService` (`StartAsync`/
  `StopAsync`, mirroring `IModuleLifecycle`'s async, cancellable shape) and
  `ICriticalBackgroundService` (an empty marker interface extending it —
  criticality is a declaration, not a configurable value).
- `Tempest.Core.Commands` — `ICommand` (an empty marker interface; a
  concrete command type carries its own parameters as data and is
  dispatched by its own type — deliberately mirroring how `IModule` is a
  plain identity contract separate from `IModuleLifecycle`'s behaviour
  contract, reused here rather than invented fresh).
- `Tempest.Core.Events` — `IEvent` (an empty marker interface) and
  `IEventHandler<TEvent>` (one method, `HandleAsync`, constrained to
  `TEvent : IEvent`).
- `Tempest.Core.Modules` — `IModule`/`IModuleLifecycle` untouched. WP 4.0's
  only claim on them is documentary: they are catalogued as part of the
  same platform-contracts family, not moved or modified.

ADR-0024 records the two packaging decisions (capability-namespace
packaging over a shared `Contracts` namespace; keeping the
`IHostedService` name with a clarifying XML remark rather than renaming
it, since there is no actual compiler collision — TempestOS has no
dependency on `Microsoft.Extensions.Hosting`, ADR-0005 — only a human
familiarity concern).

## 6. Alternatives Considered

**A single `Tempest.Core.Contracts` namespace** holding all five new
interfaces together. Rejected — it would have been the only place in the
codebase organised by "these are contracts" rather than by capability,
breaking a consistent pattern for no benefit; see ADR-0024.

**Renaming `IHostedService`** to sidestep the ASP.NET Core naming
coincidence. Rejected for now — there is no compiler-level collision (no
dependency on that package exists), and the concern is addressed with a
documentation note instead, at far lower cost than a rename with no
current consumer to justify it. ADR-0024 explicitly leaves this open to
revisit with real evidence, not treats it as permanently settled.

**Giving `ICommand` a generic result type (`ICommand<TResult>`) or an
accompanying `ICommandHandler<T>` now.** Considered, since the release's
own planning document said a command "has an expected result." Rejected —
neither was in the six names this release's planning explicitly agreed to,
and inventing the handler/result shape now would be exactly the kind of
speculative design ahead of real understanding this work package exists to
avoid. `WP 4.7` designs that shape when it has actually reasoned about
dispatch.

**Adding `INavigationProvider`/`IDiagnosticsProvider`, even provisionally.**
Considered and rejected outright, per the release's own governing
philosophy — see Background, above. Not included in any form.

## 7. Why This Solution Was Chosen

Every decision in this work package traces back to one of two sources:
either the release's own planning phase had already decided it (ADR-0020
through ADR-0023, and the six-contract scope itself), in which case the job
was implementation, not design — or it was a genuinely new, narrow
packaging question (namespace layout, the `IHostedService` naming
question), in which case it was resolved by direct analogy to the existing
codebase's own established conventions (one namespace per capability;
document naming coincidences rather than rename reflexively, as ADR-0016
already did once) and recorded as ADR-0024.

## 8. Architectural Principles

- **Platform Layering** (ADR-0023) — every one of these five contracts is
  a Platform API, the top of the four-layer stack; none of them is, or
  depends on, a Platform Service, a Module, or the Runtime Host.
- **Reuse Before Invention** — `ICommand`'s design deliberately reuses the
  `IModule`/`IModuleLifecycle` split (identity contract separate from
  behaviour contract) rather than inventing a new shape.
- **Fail Fast / Avoid Speculative Design** — `INavigationProvider` and
  `IDiagnosticsProvider`'s absence is not an oversight; it is this work
  package's single most load-bearing decision, made explicitly and
  defended in Alternatives Considered, above.
- **Atomic Phase Principle** — `IHostedService`'s `StartAsync`/`StopAsync`
  each accept a `CancellationToken`, consistent with every other
  cancellable operation in the platform; no behaviour is wired to them yet,
  but the shape is ready to honour the principle once WP 4.5 does.

## 9. Benefits

- Every later v0.4.0 work package (4.4, 4.5, 4.7) now has a settled
  contract to build against, rather than needing to invent one as a side
  effect of its own implementation.
- Proves, concretely, that new platform surface can be added with zero
  behavioural change to the existing runtime — all 164 pre-existing tests
  pass completely unmodified, and a dedicated regression suite
  (`PlatformContractsCompatibilityTests`) proves Discovery's behaviour is
  unaffected by the new interfaces' mere existence.
- ADR-0024 gives every future capability a namespace-placement precedent to
  follow, rather than each work package re-deciding "where do my interfaces
  live" from scratch.

## 10. Trade-offs

- Five new namespaces now exist holding, for the moment, a single interface
  file each. This is a deliberately accepted shape — the namespace exists
  because the contract is stable enough to define now, not because there
  is much to put in it yet (see `Architecture.md`'s reuse-first mandate).
- `IHostedService`'s naming question is documented, not resolved
  permanently — a real, if currently low-probability, source of future
  confusion for a contributor arriving with ASP.NET Core experience.

## 11. Common Mistakes

The mistake most worth naming here is one this work package's own scope
discipline exists to prevent, not one that happened: including
`INavigationProvider` or `IDiagnosticsProvider` "for completeness," on the
reasoning that a contracts-first work package should define *all* the
contracts a release will eventually need. That reasoning is exactly
backwards — a contract defined before its owning work package has done
real design work is a guess wearing the appearance of a decision. The
release's own planning review caught this before implementation began;
this retrospective preserves the reasoning so a future contracts-adding
work package does not have to rediscover it under time pressure.

## 12. Future Evolution

- **WP 4.4 (Event Bus)** implements `IEventBus` against `IEvent`/
  `IEventHandler<T>`, DI-public per ADR-0020.
- **WP 4.5 (Background Services)** implements the Host-level wiring to
  actually start and stop an `IHostedService`, and the
  `ICriticalBackgroundService` failure-classification check, per ADR-0021.
- **WP 4.7 (Command Framework)** designs the handler contract and
  dispatcher `ICommand` deliberately does not yet have.
- **`WP 4.6A` and `WP 4.8`** define `INavigationProvider` and
  `IDiagnosticsProvider` respectively, from scratch, once each has done its
  own design work — this retrospective is the citable record of why they
  are absent here.
- **The `IHostedService` naming question** should be revisited once `WP 4.5`
  gives it real usage, with actual evidence of confusion rather than a
  hypothetical one, per ADR-0024's own Future Considerations.

## 13. Key Takeaways

1. A contracts-first work package is only as good as its restraint —
   WP 4.0's value is defined as much by what it deliberately left
   undefined (`INavigationProvider`, `IDiagnosticsProvider`, `ICommand`'s
   handler shape) as by what it shipped.
2. Reuse-before-invention applies to contract *shape*, not only to
   architecture — `ICommand`'s identity/behaviour split is not a new idea,
   it is `IModule`/`IModuleLifecycle`'s existing idea, applied a second
   time deliberately.
3. Adding new platform surface and changing zero runtime behaviour are not
   in tension when the new surface is genuinely just contracts — 164
   pre-existing tests passing completely unmodified, plus a dedicated
   regression suite proving Discovery's indifference to the new
   interfaces, is the concrete evidence for that claim, not merely the
   intention.

---

## Architectural Debt Assessment

**No new debt introduced.** Every item on record from the Runtime
Foundation (single-sink logging, the dual logging mechanism, no disposable
platform services yet) remains exactly as described, unaffected by this
work package. One item is worth tracking, not as debt but as an open
question this work package deliberately left open: `IHostedService`'s
naming proximity to `Microsoft.Extensions.Hosting.IHostedService` (ADR-0024,
Future Considerations) — revisit with real usage evidence once `WP 4.5`
lands, not before.

## Observations

- **Files changed**: 5 new interface files (`src/Tempest.Core/
  BackgroundServices/IHostedService.cs`,
  `ICriticalBackgroundService.cs`; `src/Tempest.Core/Commands/ICommand.cs`;
  `src/Tempest.Core/Events/IEvent.cs`, `IEventHandler.cs`); 4 new test files
  (13 new tests); 1 new ADR (ADR-0024); Platform Service Map and Engineering
  Glossary updated; this retrospective. Zero existing production files
  modified.
- **Test results**: 177 of 177 passing (164 pre-existing + 13 new), 0
  failures, verified stable across four consecutive full-suite runs.
- **Build results**: 0 warnings, 0 errors.
- **Risks discovered**: none new beyond the `IHostedService` naming
  question already anticipated and documented in planning (`Architecture.md`,
  `WP 4.0`'s own Risks section) and now resolved via ADR-0024's
  documentation-over-rename decision.
- **Readiness assessment**: WP 4.0 is complete and ready to merge into
  `feature/v0.4.0-platform-services`'s own history. `WP 4.1` (Module SDK)
  and every later work package in this release may now build against a
  settled contract surface.
