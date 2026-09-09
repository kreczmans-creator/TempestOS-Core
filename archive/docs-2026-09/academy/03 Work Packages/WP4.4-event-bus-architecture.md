# WP 4.4 — Event Bus Architecture

## 1. Introduction

WP 4.4, like WP 2.7A and WP 4.2 before it, produced no production code.
Its job was to design the Event Bus completely — dispatch, subscription,
ordering, failure isolation, re-entrancy, and registration — after a prior
task (`WP 4.4C`) discovered, while investigating the repository rather
than assuming its own brief, that no `IEventBus` had ever actually been
built.

## 2. Purpose

To answer, in writing, every question `WorkPackages.md`'s own `WP 4.4`
Scope named as needing an explicit decision — dispatch ordering,
per-subscriber failure isolation, re-entrancy policy — plus the further
questions this architecture phase's own brief asked: ownership, dependency
direction, registration mechanics, synchronous-versus-asynchronous
dispatch, subscriber lifetime, diagnostics, and interaction with plugins
and future Background Services.

## 3. Background

`WP 4.4A` and `WP 4.4B` resolved a real, but different, problem: a
discovered module could not receive any constructor-injected, DI-public
service at all. With that fixed, a task assuming the Event Bus itself
already existed (`WP 4.4C`) was assigned to extend `ClockModule` to
publish through it. Investigating the repository directly — not assuming
the brief's own premise — found no `IEventBus` anywhere: not a file, not
an interface, not an implementation. `WP 4.4A`/`4.4B` had been named
correctly all along as *prerequisites for* `WP 4.4`, not `WP 4.4` itself.
Rather than inventing a minimal Event Bus under time pressure inside a
task titled "Integration," the discrepancy was reported, and this proper,
dedicated architecture phase was authorised instead — the same discipline
this release has now applied three times running (`WP 2.7A`/`WP 4.2`/
this one): find a real gap, stop, design it properly, then build it.

## 4. The Problem

1. **Is the Event Bus a Platform Service?** Already decided — ADR-0020 —
   not reopened.
2. **How does a module actually subscribe** — imperative, or
   auto-discovered by the DI container?
3. **How is an event dispatched** — synchronously in structure, or with
   genuine concurrency; in what order; with what cancellation boundary?
4. **What happens when a subscriber throws** — isolated, or is there a
   critical opt-in mirroring `ICriticalBackgroundService`?
5. **What happens when a handler publishes from within its own handler**
   — the re-entrancy question `WP 4.4`'s own Scope named explicitly?
6. **Does registering the bus require any new DI capability**, or does
   `ServiceCollection`'s existing surface already suffice?
7. **How does this interact with plugin-loaded modules and a future
   Background Services capability**, neither of which should need
   special-casing?

## 5. The Design

See `docs/adr/ADR-0028-event-bus-dispatch-subscription-and-failure-model.md`
and `docs/architecture/Event Bus Architecture.md` in full. In summary:
`IEventBus` exposes `Subscribe`/`Unsubscribe`/`PublishAsync`, imperative,
not DI-discovered. `PublishAsync` dispatches sequentially, in subscription
order, over an independent snapshot taken at the start of each call —
which is what makes re-entrant publishing safe without any deferred-queue
machinery. A subscriber's exception is caught, logged at `Error`, and
never rethrown to the publisher or the Host — unconditionally, with no
critical-subscriber escalation. `EventBus` is registered as an ordinary
container-constructed singleton (`services.Singleton<IEventBus,
EventBus>()`), during the existing Platform Services Registered phase —
no Composition Root treatment, no new DI capability, no new Host phase.

## 6. Alternatives Considered

Recorded in full, with reasoning, in ADR-0028's own "Decision" and
"Alternatives Considered" sections, and permanently indexed as RD-0019
(DI-auto-discovered handlers — rejected as requiring a genuine new
multi-registration-resolution capability `TempestServiceProvider` has
never needed), RD-0020 (deferred/queued re-entrant publishing — rejected
as solving a problem the snapshot-based design does not actually have),
RD-0021 (polymorphic event dispatch — rejected/deferred, no current event
hierarchy exists to design against), and RD-0022 (a per-subscriber
critical opt-in mirroring ADR-0021 — rejected, echoing RD-0011's own
reasoning for the analogous plugin question).

## 7. Why This Solution Was Chosen

Every non-obvious decision traces back to preferring the simplest design
that is still genuinely correct, over a more elaborate one solving a
problem this release does not yet have. Snapshot-based dispatch makes
re-entrancy safe without a queue; imperative subscription avoids a real
container change; a single, unconditional isolation rule is simpler than
mirroring ADR-0021's own three-category shape for a component that is not,
in fact, shaped like a background service. Where a genuine, real precedent
already existed (`ModuleLifecycleManager.RunBatchAsync`'s own sequential,
cancellation-aware batch shape), this design reused it directly rather
than inventing a new dispatch model from scratch.

## 8. Architectural Principles

- **Reuse Before Invention** — dispatch ordering and the cancellation
  boundary mirror `RunBatchAsync`'s own established shape; registration
  reuses `ServiceCollection.Singleton<TService, TImplementation>()`,
  already built since `WP 2.4`.
- **Minimal Host Complexity** — one new line in `TempestHost.cs`'s
  existing Platform Services Registered block, when implemented; no new
  phase, state, or transition.
- **Avoid Speculative Design** — polymorphic dispatch and a critical
  opt-in were both seriously considered and both deferred, precisely
  because no current consumer needs either.
- **One Responsibility Per Service** — `IEventBus` carries messages; it
  registers, initialises, starts, stops, and disposes nothing, exactly as
  ADR-0020 already established.
- **Constructor Injection Through Normal DI Patterns** — the entire reason
  this design exists is to give a module ordinary constructor-injected
  access to a DI-public service, consistent with everything `WP 4.4A`/
  `4.4B` already proved possible.

## 9. Benefits

- **Every dispatch/failure/re-entrancy question `WP 4.4`'s own Scope named
  now has a decided, written answer** — nothing is left to be discovered
  as a bug during implementation.
- **Zero new Dependency Injection capability required** — confirmed by
  design, not merely hoped for: `EventBus`'s own constructor needs nothing
  `AddInstance` provides, and multi-registration resolution (rejected,
  RD-0019) was never actually necessary.
- **The Event Bus/Command Framework distinction (`Risks.md` R3) is
  documented explicitly**, ahead of `WP 4.7`, directly satisfying that
  risk's own named mitigation.
- **A genuine, real gap was found and corrected before it could cause
  harm** — a task that assumed a nonexistent Event Bus was stopped before
  it either built one under the wrong name and pressure, or silently
  failed to deliver its own stated objective.

## 10. Trade-offs

- This is documentation only — nothing here is enforced by a compiler,
  test, or running code yet, exactly as every architecture-only work
  package in this release has noted about itself.
- No automatic unsubscription on module stop/dispose — a deliberate,
  disclosed gap (ADR-0028), not a defect.
- Subscriber references are held strongly for the bus's whole lifetime
  unless explicitly unsubscribed — acceptable today, since every
  anticipated subscriber already lives exactly that long as a DI
  singleton regardless.

## 11. Common Mistakes

The mistake most worth naming here is one avoided at the very start of
this work package's own history: proceeding to extend `ClockModule`
against an Event Bus assumed, but never verified, to exist. Investigating
the repository directly — the same discipline every prior architecture
phase in this release has applied — caught the gap before any code was
written against a premise that was not actually true.

## 12. Future Evolution

- **`WP 4.4`'s own implementation** should build `IEventBus`/`EventBus`
  exactly as designed here, proven against dedicated test modules first —
  mirroring `WP 4.4B`'s own precedent — before extending `ClockModule`.
- **`ClockModule`'s own extension** (the original `WP 4.4C` objective) is
  now fully unblocked and should follow immediately after the bus itself
  is implemented and proven.
- **A companion module**, subscribing to whatever `ClockModule` publishes,
  remains `WP 4.4`'s own Deliverable to add "if it does not already
  exist" — not designed or built here.
- **Automatic unsubscription and a critical-subscriber opt-in** both
  remain available, purely additively, per RD-0022 and ADR-0028's own
  Future Considerations, if a real need for either ever emerges.

## 13. Key Takeaways

1. A work package's own stated premise is worth verifying against the
   actual repository before building anything against it — this release
   has now found a false premise this way twice (the parameterless-
   constructor collision in `WP 4.3`; a nonexistent Event Bus here), both
   times before any code was written on top of it.
2. The simplest design that is still correct is preferable to a more
   elaborate one solving a problem that does not yet exist — snapshot-
   based re-entrancy safety, chosen over a deferred queue, is this
   design's clearest example.
3. Reusing an already-established pattern (`RunBatchAsync`'s own
   sequential, cancellation-aware batch shape) for a new component's own
   dispatch loop is not a lack of originality — it is exactly the "Reuse
   Before Invention" discipline this release has applied consistently
   since `WP 4.0`.

---

## Architectural Debt Assessment

**No new debt introduced.** This work package produced one ADR, one
architecture document, and four Rejected Designs entries; no code exists
for it to affect. Every debt item on record from the Runtime Foundation,
WP 4.0–4.4B, and WP 4.2D remains exactly as previously described. Two
named, accepted gaps (no automatic unsubscription; no critical-subscriber
opt-in) are disclosed as part of this design, not newly discovered debt.

## Observations

- **Files changed**: 1 new ADR (`ADR-0028-event-bus-dispatch-subscription-and-failure-model.md`);
  1 new architecture document (`Event Bus Architecture.md`); 4 new
  Rejected Designs entries (RD-0019–RD-0022); `Platform Service Map.md`
  and Engineering Glossary's Event Bus entries updated; `WorkPackages.md`,
  `CHANGELOG.md`, and `Risks.md` updated; this retrospective. Zero
  production code files touched — none exist for this work package to
  touch, and `ClockModule` remains completely untouched.
- **ADRs required**: 1 (ADR-0028) — written in full, as this work
  package's entire deliverable.
- **Risks discovered**: none new. The Event Bus/Command Framework
  distinction (`Risks.md` R3) is now documented explicitly, ahead of
  `WP 4.7`, reducing rather than adding to that risk.
- **Readiness assessment**: the design is complete and sound. No
  architectural blocker remains before `WP 4.4`'s own implementation
  begins. `ClockModule`'s own extension to publish through the now-designed
  bus should follow immediately after, mirroring exactly the sequence
  `WP 4.2`'s own architecture-then-implementation phases already
  established twice in this release.
