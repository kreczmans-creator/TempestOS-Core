# Platform Layering: Designing a Platform Service

## 1. Introduction

By v0.4.0, TempestOS has added several genuinely new kinds of platform
capability — the Event Bus, Plugin infrastructure, Platform Versioning —
alongside the six original platform services. Each of these had to answer
the same question before a line of its own code was written: *where does
this new thing live, relative to everything else?* ADR-0023 named the
answer as one general rule, formalising boundaries several earlier ADRs had
each, independently, already drawn. This document is the teaching version
of that ADR: what the four-layer model is, why it exists, and — most
usefully for a reader about to design the *next* platform capability — how
to use it to answer "is this a Platform API, a Platform Service, or
something the Host itself must own" without re-deriving the reasoning from
scratch.

## 2. Purpose

To give a new engineer one repeatable question to ask before designing any
new TempestOS capability: *which of these four layers does this belong to,
and does every dependency it needs actually point downward?* — and to show,
concretely, how every existing capability in the platform already answers
that question.

## 3. Background

TempestOS's four layers, top to bottom:

```
Modules
   ↓
Platform APIs      (contracts — IModule, IEventBus, ICommand,
                     IHostedService, and so on)
   ↓
Platform Services  (concrete implementations — Configuration,
                     Logging, the Event Bus, Discovery, and so on)
   ↓
Runtime Host       (constructs and orchestrates Platform Services;
                     drives Modules through Lifecycle)
```

This was not invented freely for v0.4.0 — it is a *name* given to a pattern
that was already true, independently, in three separate decisions made
across the Runtime Foundation and early v0.4.0 planning: ADR-0013 already
separated platform-service failure from module failure; ADR-0017 already
kept Discovery, Registration, and Lifecycle out of a module's reach;
ADR-0020 already forbade one module from depending on another directly.
ADR-0023 is what happens when a reader notices these are all the same rule,
stated three times independently, and gives that rule one name.

## 4. The Problem

When a new capability is proposed — an Event Bus, a plugin system, a
background-service host — three questions recur, and getting any one of
them wrong produces a design that looks fine in isolation and causes real
trouble once a second consumer exists:

1. **Is this a contract (Platform API) or an implementation (Platform
   Service)?** These are different things with different lifetimes:
   `IEventBus` (a contract, defined once, WP 4.0) versus `EventBus` (an
   implementation, built once real understanding existed, WP 4.4D).
2. **Does a module reach this directly, or does the Host own it and drive
   it?** Configuration and Logging are Platform Services a module resolves
   via ordinary constructor injection. Discovery, Registration, and
   Lifecycle are Host-owned collaborators a module can *never* reach,
   because reaching them would let a module act as if it were the Host
   itself (ADR-0017).
3. **Does every dependency this new capability needs actually point
   downward** — Modules depending on Platform APIs, Platform Services
   depending on nothing module-specific, the Host depending on nothing
   business-specific?

## 5. The Design

**The test that answers question 2, concretely: does this component
orchestrate the module pipeline, or does it merely carry messages/data
between things that already exist?** ADR-0020's own reasoning for the Event
Bus is the clearest worked example: an event bus does not register,
initialise, start, stop, or dispose anything — it carries messages. That
makes it structurally identical *in kind* to Configuration and Logging (a
service modules *consume*), not to Discovery/Registration/Lifecycle (a
mechanism that *drives* modules). The same test, applied to Plugin Discovery
and Plugin Loading (WP 4.2), gives the opposite answer: they *do* decide
what gets loaded into the process before Module Discovery even runs — an
orchestration decision — so they are Host-owned, exactly like Discovery
itself, never DI-public.

**The test that answers question 1**: a Platform API is stable enough that
defining it doesn't require guessing at behaviour its own implementation
hasn't designed yet. WP 4.0's own governing philosophy — "only define a
contract when there is enough understanding to make it stable" — is this
test applied literally: `INavigationProvider` and `IDiagnosticsProvider`
were *not* defined in WP 4.0, on this exact reasoning, and were correctly
deferred to the work packages that had actually done the design work
(`WP 4.6A`, `WP 4.8`).

**The test that answers question 3**: trace every `using` statement a new
type introduces, and ask, for each one, "does this point toward `Modules`,
or away from it?" `Tempest.Core.Events` (Platform APIs) depends on nothing;
`EventBus` (Platform Service) depends only on `Tempest.Core.Logging`
(optional, diagnostic); nothing in `Tempest.Core` ever depends on
`Tempest.Samples`. WP 4.2D's own Platform Services Architecture Review
performed exactly this trace, directly against production source, for
every service in the platform, and found zero exceptions.

## 6. Alternatives Considered

**Deciding layer placement per work package, ad hoc, each time a new
capability is proposed.** This is what v0.4.0's planning explicitly moved
away from — ADR-0023 exists specifically so a reviewer has one checkable
question ("does this dependency point downward?") instead of re-deriving
ADR-0013/0017/0020's reasoning independently for every new capability.

**Treating "Platform API" and "Platform Service" as one layer**, since in
casual conversation "the Event Bus" often means both the contract and the
implementation together. Rejected — ADR-0023 names them as genuinely
separate layers specifically because they have different lifetimes: a
contract can be, and often should be, defined and stabilised well before
its implementation exists (WP 4.0 defined `IEventBus`; WP 4.4D implemented
it, four work packages later).

## 7. Why This Solution Was Chosen

Every new capability in this platform's history has been correctly
classified by asking the same three questions in Section 4 — not by
analogy to whatever the most recent capability happened to do. The model's
value is that it is a *test*, not a description: a reviewer can apply it to
a capability that doesn't exist yet and get a real, checkable answer before
any code is written.

## 8. Architectural Principles

- **Platform Layering** (ADR-0023) itself — the organising principle this
  entire document exists to teach.
- **Separation of Concerns** — a Platform API's stability is a different
  concern from a Platform Service's implementation correctness; conflating
  them (defining a contract speculatively, ahead of real design experience)
  is exactly what WP 4.0's own governing philosophy exists to prevent.
- **Fail Fast, applied to design review** — the downward-dependency test is
  meant to be applied *before* implementation, catching a layering mistake
  at design time rather than after code exists to unwind.

## 9. Benefits

- Every future work package proposing a new platform capability has one,
  reusable review question, rather than needing to re-derive ADR-0013's,
  ADR-0017's, and ADR-0020's reasoning independently each time.
- The Event Bus, Plugin infrastructure, and Platform Versioning — three
  genuinely different kinds of new capability — were each classified
  correctly, on the first attempt, using exactly this model.

## 10. Trade-offs

- "Platform APIs," as a fourth named layer distinct from "Platform
  Services," is new vocabulary a reader has to learn — a real, if modest,
  cost ADR-0023 itself names explicitly in its own Consequences section.
- Enforcement today is a review discipline, not a compiler-enforced or
  architecture-tested constraint — a future automated dependency-direction
  check remains available if a layering violation is ever found only in
  review rather than earlier (ADR-0023's own Future Considerations).

## 11. Common Mistakes

The mistake this model exists to prevent: assuming a new capability that
"feels similar" to an existing one should be classified the same way,
without applying the actual test. Plugin Discovery *looks*, superficially,
like it might belong alongside the Event Bus (both are "new v0.4.0
infrastructure") — but the orchestration test gives an entirely different
answer for each, and getting this specific classification wrong (making
Plugin Discovery DI-public, say) would have handed a module a path back
into deciding what gets loaded into the process before Discovery even
runs — precisely the kind of boundary violation ADR-0017 exists to prevent.

## 12. Future Evolution

Background Services (`WP 4.5`) has since been classified and implemented —
a fourth, Host-owned category, neither a Platform Service nor a Module
(ADR-0029) — following exactly this discipline. Every remaining future
v0.4.0 capability — Navigation, Command Framework — should be classified
against this same model before its own design begins, exactly as the
Event Bus, Plugin infrastructure, and Background Services already were.
`Runtime Host Architecture.md`'s own Future Extensibility section already
sketches a first-pass answer for each; it should be confirmed, not
assumed, once that work package's own design phase begins.

## 13. Key Takeaways

1. "Where does this new thing live?" has a repeatable, three-question test
   — does it orchestrate or does it carry data; is it stable enough to be a
   contract yet; does every dependency point downward — not a case-by-case
   judgment call made fresh each time.
2. A Platform API and a Platform Service are different layers with
   different lifetimes precisely because a contract can, and often should,
   be stabilised well before its implementation is ready to be built.
3. This model is a consolidation of decisions the platform had already made
   independently, not a new constraint — its value is naming a pattern that
   was already true, so it can be applied deliberately to the *next*
   capability instead of being rediscovered by accident.
