# Working with the TempestOS Host

## 1. Introduction

Everything else in this Academy's Runtime Architecture section describes one
piece of the platform at a time: the module pipeline, the startup sequence,
how to write a module. This document is the missing, unifying one — a
single, guided walk through `TempestHost` itself, for a reader who has never
seen it before and wants to understand it as *one thing*, not as six
separate reference documents (`Runtime Host Architecture.md`, `Host
Lifecycle.md`, `Runtime State Machine.md`, `Shutdown Sequence.md`, `Failure
Behaviour.md`, `Ownership Matrix.md`) that each answer one narrow question
precisely, but none of which is meant to be a first read.

If you are about to touch `TempestHost.cs`, or you just want to understand
what actually happens when TempestOS starts, this is the place to start —
then go to whichever of the six reference documents above has the specific
detail you need.

## 2. Purpose

To answer, in one place, in order: what is the Host; why does it exist; what
happens, concretely, from the moment someone calls `RunAsync` to the moment
the process could safely exit; and what are the two or three ideas that make
the rest of the reference documentation make sense once you have them.

## 3. Background

TempestOS's runtime is built from independent platform services — this is
the whole point of the module pipeline (Discovery, Registration, Lifecycle,
Dependency Injection) and everything built on top of it (Configuration,
Logging, Platform Version, Plugins, the Event Bus). Each service was
designed, implemented, and tested in isolation, deliberately, and none of
them knows the others exist. Something still has to bring them up, in the
right order, hold the whole thing in a running state, and bring it back down
again — that something is `TempestHost`. It is, by design, the *only* place
in the codebase where all of these services are visible simultaneously.

## 4. The Problem

A reader new to TempestOS, looking at `TempestHost.cs` for the first time,
tends to ask the same handful of questions:

1. **Why does the order of construction matter so much?** (Configuration,
   then Logging, then Plugins, then Discovery, then Registration, then DI,
   then Lifecycle — never any other order.)
2. **What is the Host's own "state," and how is it different from a
   module's state?**
3. **What happens if something goes wrong halfway through startup?**
4. **What happens if I ask the Host to stop while it's still starting up?**
5. **Why can't I run the same `TempestHost` twice?**

Each of these has a precise, already-documented answer somewhere in
`docs/architecture/`. This document's job is to give you the *shape* of all
five answers together, so the individual documents read as confirmations of
something you already understand, not as five unrelated puzzles.

## 5. The Design

**Six ideas, in order of how much they explain.**

**Idea 1 — the Host is thin.** It does not implement Configuration,
Logging, Discovery, Registration, Dependency Injection, or Lifecycle — every
one of those already existed, fully built and tested, before the Host did.
`TempestHost` calls each one's existing public contract, in a specific
order, and owns exactly the things none of the six individually owns:
ordering, startup, shutdown, cancellation, and disposal. See *Runtime Host
Architecture.md* for the full responsibility/non-responsibility list.

**Idea 2 — startup is a fixed sequence of phases, not a free-for-all.**
Thirteen (now fifteen, with two decimal-numbered plugin phases inserted)
phases, each with its own entry criteria, exit criteria, and failure
behaviour, described exhaustively in *Host Lifecycle.md*. The order is not
arbitrary — it is *forced* by real dependencies: Discovery cannot run
through the DI container because the container's own registrations are
built *from* Discovery's output (a circularity ADR-0008 resolves); Plugin
Discovery has to run before Module Discovery so a plugin's assembly is
already loaded and visible to Discovery's unmodified scan; Logging has to
exist before almost everything else, because everything else wants to log
what it's doing. Read *The Startup Sequence* (Academy) for how this ordering
was derived, not just what it is.

**Idea 3 — the Host has its own state, separate from any module's state.**
Seven states: `Created → Starting → Running → Stopping → Stopped/Faulted →
Disposed`. This is a genuinely different question from "is `ClockModule`
currently running" — the Host can be `Running` while an individual module
sits in `Failed`, and that is not a contradiction, it is the entire point of
ADR-0013 (below). See *Runtime State Machine.md* for every state and legal
transition.

**Idea 4 — there are exactly two categories of failure, and they behave
completely differently.** A **platform-service failure** (Configuration
threw, Discovery threw, the DI container couldn't resolve something) is
**Host-fatal** — the whole platform transitions to `Faulted`, because there
is no coherent way to run "a partially-working platform." A **module
failure** (one module's `InitialiseAsync` threw) is **isolated** — that one
module is marked `Failed`, logged, and every other module still gets its
chance to start; the Host still reaches `Running`. This single distinction
(ADR-0013) is the most load-bearing idea in the whole Host design — see
*Failure Behaviour.md* for every failure mode classified against it.

**Idea 5 — shutdown is one procedure, entered two different ways.**
Whether you ask a running platform to stop gracefully, or cancellation
arrives while it's still starting up, the Host runs the exact same teardown
— stop every module, dispose every module, dispose every platform service —
never two different code paths for what is, underneath, the same job
(ADR-0018). A startup *failure*, by contrast, does not go through this path
at all — it goes straight to `Faulted`, and disposal is attempted from
there instead. See *Shutdown Sequence.md* for both paths, side by side.

**Idea 6 — a Host runs exactly once.** There is no `Reset()`, no restart.
Once `Stopped` or `Faulted`, the only remaining legal transition is to
`Disposed`. A second run means building a new `TempestHostBuilder` and a new
`TempestHost` (ADR-0015) — not because restart would be impossible to build,
but because nothing in the platform (`RuntimeModuleManager`,
`TempestServiceProvider`, `ModuleLifecycleManager`) was ever designed with a
coherent answer to "what does resetting you even mean."

## 6. Alternatives Considered

See each individual reference document's own "Alternatives Considered"
section for the full reasoning behind each idea above — this document
intentionally does not re-litigate them, only orients you to where they
live. The single most instructive one, if you read only one: *Runtime Host
Architecture.md*'s WP 2.7B clarification, where the Host's own
implementation had to resolve a genuine disagreement between its own brief
(which named the builder as the composition root) and the already-frozen
architecture (which named the Host itself) — resolved in the architecture's
favour, following the same precedent ADR-0011 had already set for an
identical class of tension.

## 7. Why This Solution Was Chosen

Every idea above traces back to the same source: the six platform services
already existed, independently designed, before the Host did. The Host's
design is overwhelmingly *discovered* from what those six services already,
implicitly, required of whatever eventually orchestrated them — not
invented freely. See the WP 2.7 Academy retrospective's own Section 6 for
the clearest statement of this: "WP 2.7's job was substantially to discover
and make explicit a design the prior six work packages had already,
implicitly, constrained."

## 8. Architectural Principles

- **Separation of Concerns** — the Host orchestrates; it never reimplements
  any of the services it calls.
- **Deterministic Systems** — startup and shutdown are strictly sequential,
  never concurrent, so "what has happened so far" is always a single,
  well-defined answer.
- **Fail Fast** — a platform-service failure surfaces immediately, as the
  original exception, never swallowed or downgraded.
- **The Atomic Phase Principle** (Engineering Principle 11) — cancellation
  is observed only between phases (or, within Module Initialisation's own
  batch, only between individual modules), never in the middle of one.

## 9. Benefits

- A new engineer can answer "what happens when TempestOS starts" without
  reading six separate documents cover to cover — this document is the map;
  the six documents are the territory, for whichever part you need in
  detail.
- Every one of the six existing platform services required zero
  modification to support the Host — the clearest possible evidence that
  WP 2.1 through WP 2.6's separation-of-concerns discipline held.

## 10. Trade-offs

- This document is deliberately a simplification. It will occasionally be
  slightly behind the full precision of *Host Lifecycle.md* or *Failure
  Behaviour.md* the moment either changes — that is the correct trade for a
  first-read document to make, provided (per Engineering Governance §6) it
  is kept honestly in sync, not left to silently drift.

## 11. Common Mistakes

The most common misreading, for someone arriving from a different platform
or framework: assuming the Host's own `HostState` and a module's own
`ModuleState` must be related, or that one can be derived from the other.
They are deliberately independent (ADR-0012) — `Running` describes the
platform; `ModuleState` describes one module; a healthy platform with one
broken module is a real, correctly-representable state of the world, not an
edge case to special-case away.

## 12. Future Evolution

Everything the platform has added since the Host's own implementation
(Plugin Discovery/Loading, Platform Version, the Event Bus) has slotted into
this same design without requiring the Host's own state machine, failure
model, or shutdown sequence to change — each new capability is either a new
phase (Plugin Discovery/Loading, decimal-numbered, per ADR-0026's precedent)
or a new DI-public service registered during the existing Platform Services
Registered phase (Platform Version, the Event Bus). Any future capability —
Background Services (`WP 4.5`), Navigation, Command Framework — should be
evaluated against this same question first: does it need a new phase (it
orchestrates something), or does it need a new registration (it's a service
a module consumes)? See *Runtime Host Architecture.md*'s own Future
Extensibility section for the current, specific answer for each named
future capability.

## 13. Key Takeaways

1. The Host is thin by design — it is worth understanding *because* it is
   almost entirely a synthesis of decisions the six services underneath it
   already forced, not a large, independent design surface of its own.
2. Two categories of failure — platform-service (Host-fatal) and module
   (isolated) — explain nearly every specific failure-handling rule in the
   whole platform; learn this distinction first, and most of *Failure
   Behaviour.md*'s specific rules will already make sense before you read
   them.
3. When in doubt about where a new capability belongs in the Host's own
   sequence, ask whether it's a new phase or a new registration — this one
   question has correctly resolved every capability added since the Host
   was first implemented.
