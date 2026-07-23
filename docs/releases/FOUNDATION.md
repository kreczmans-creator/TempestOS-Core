# TempestOS Foundation

## What This Document Is

This document is not a release note. It carries no version number and is not
superseded by the next one. Where `docs/releases/vX.Y.Z.md` records what
changed at a point in time, this document records what must not change simply
because time has passed and the people who wrote it are no longer in the
room.

It exists to answer three questions, permanently, for anyone who joins this
project at any point in its future — a new engineer, a new maintainer, an
agent picking up where a prior one left off:

1. Why was TempestOS built this way?
2. What architectural principles are non-negotiable?
3. What must every future contributor preserve, regardless of what they are
   asked to build?

If a future decision appears to conflict with this document, that is a
signal to slow down and read further — not a licence to treat this document
as advisory.

---

## Why TempestOS Was Built This Way

TempestOS could have been built the faster way: a working prototype first,
architecture inferred backward from whatever the prototype happened to do,
documentation written last, if at all. That approach was available at every
stage of this project's history and was not taken, deliberately, at every
one of those stages.

Instead, six platform services — Configuration, Logging, Discovery,
Registration, Dependency Injection, Lifecycle — were each designed,
implemented, tested, and documented in isolation, with explicit boundaries
between them, before anything attempted to wire them together into a running
platform. When the Runtime Host finally did wire them together, the result
was the clearest evidence this project has produced that the approach was
correct: the six services required no redesign. The Host's architecture
(WP 2.7A) was substantially *discovered* from constraints those six services
had already, independently, established — Discovery's independence from
dependency injection (ADR-0008) forced Discovery and Registration to precede
the container's construction (ADR-0011); permissive module disposal
(ADR-0004) generalised to the Host's own fault-recovery path without
inventing anything new; per-module failure isolation (WP 2.3) forced an
explicit platform-service/module failure boundary (ADR-0013) rather than one
uniform policy. None of this was luck. It was the direct, structural
consequence of building each piece to a boundary precise enough that later
pieces could depend on it without needing to renegotiate it.

This is why: **a system that is expensive to change safely is a system that
will eventually be changed unsafely anyway, under whatever pressure is
greatest at the time.** TempestOS is built the way it is so that the correct
way to extend it stays cheaper than the shortcut, for as long as this
platform exists — not because architecture is more important than working
software, but because on a platform expected to last, architecture *is* how
software keeps working.

---

## What Is Non-Negotiable

The following are not conventions to be weighed against convenience on a
given day. They are the terms on which every existing platform service was
built, and the terms every future one is expected to be built on. A change
that violates one of these is an architectural decision in its own right —
it requires an ADR and Technical Review (Engineering Governance, §5, §9), not
a quiet exception.

1. **Architecture precedes implementation, for anything non-trivial.** A
   non-trivial component is designed — its responsibilities, its explicit
   non-responsibilities, its failure behaviour, its state machine if it has
   one — before its first line of production code is written. This project's
   own history is the argument for this rule, not just its enforcement.

2. **Every component has exactly one reason to change, and its boundary is
   enforced structurally, not by convention alone.** Discovery discovers;
   Registration registers; Lifecycle orchestrates; the Host assembles.
   Where the temptation exists for one component to reach into another's
   responsibility — a module resolving the very services that orchestrate
   it, for instance — the platform closes that path outright (ADR-0017),
   rather than trusting every future contributor to simply not do it.

3. **State has exactly one owner, and is never mutated from outside that
   owner.** `RuntimeModule` is immutable from the instant it is created
   (ADR-0001); lifecycle state lives only inside `ModuleLifecycleManager`,
   never on the module itself (ADR-0002); the Host's own state machine is
   independent of, and never derived from, any module's state (ADR-0012). A
   future component that needs to track its own evolving state should follow
   this same shape — a dedicated owner, immutable snapshots handed out to
   everyone else — rather than granting write access to whatever asks for
   it.

4. **A platform-service failure and a module failure are different
   categories of event, and must never be treated the same way.** The
   former is fatal to the whole runtime; the latter is isolated and does not
   prevent the platform from reaching a running state (ADR-0013). Collapsing
   this distinction — even under pressure to "simplify" the failure model —
   removes the platform's ability to tell the difference between "a plugin
   misbehaved" and "the ground itself gave way."

5. **Cleanup is always guaranteed, never conditional on how far execution
   got.** Disposal is legal from any non-terminal state, whether a module or
   the Host itself never got past its first step (ADR-0004, ADR-0019).
   Nothing in this platform should ever be able to leak a resource because it
   failed "too early to clean up properly" — that phrase describes a design
   defect, not an acceptable edge case.

6. **Interruption is observed only at defined boundaries, never in the
   middle of an operation that has already begun.** An operation either
   completes or fails as a whole; cancellation waits for a boundary rather
   than being permitted to tear an operation in half (the Atomic Phase
   Principle, Engineering Principles §11). This is what keeps the set of
   states this platform can be found in small enough for a person to reason
   about.

7. **Every decision that was not the only reasonable choice is recorded, in
   writing, at the time it is made.** An Architecture Decision Record is not
   retrospective paperwork; it is how the reasoning behind a decision
   survives longer than the person who made it. Nineteen exist at the time
   of this writing. The nineteenth will not be the last, and none of them is
   ever silently reversed — a superseded decision is marked superseded, with
   a new record pointing to it, so the history stays whole.

8. **No tier of authority substitutes for another.** The engineer or agent
   implementing a work package has real authority over internal design
   decisions — exercised and immediately documented, never silently.
   Technical Review can accept or reject that reasoning, but does not
   thereby gain the authority to merge or release. Product Approval decides
   whether reviewed, working software actually ships — and that decision is
   sought explicitly, every time, never assumed from a prior occasion
   (Engineering Governance, §9).

9. **Dependencies flow downward only, through exactly four layers: Modules,
   Platform APIs, Platform Services, Runtime Host.** A module never depends
   on another module directly (ADR-0020); a Platform Service never depends
   on a specific module; the Runtime Host never contains business or
   domain-specific logic. ADR-0023 names this as one general rule, but it
   was never a new constraint — it is what ADR-0013, ADR-0017, and ADR-0020
   already required, independently, before anyone had named the pattern
   connecting them. Every future capability is checked against this one
   question before it is built: *does this dependency point downward?*

---

## What Future Contributors Must Preserve

- **The discipline of writing the "why" down, not only the "what."** Source
  code and tests already show *how* TempestOS works. The Academy and the ADR
  record exist because code structurally cannot explain why it works this
  way instead of one of the other ways it could have. A future contributor
  who changes something the Academy describes, without updating the Academy,
  has not finished the change.

- **The willingness to document a contradiction honestly rather than
  resolve it silently.** This platform's own history contains real, found
  tensions — a logging framework that didn't yet honour its own stated
  failure principle, an architectural principle that appeared to conflict
  with already-shipped code, two frozen documents that turned out to
  disagree with each other about when disposal happens. Every one of these
  was written down, reasoned about in the open, and resolved on its merits
  — never quietly patched over and left for the next person to rediscover
  the hard way. This is not a record of a project that got everything right
  the first time. It is a record of a project that treated finding out it
  hadn't as valuable information, not an embarrassment to hide. Preserve
  that instinct specifically; it is worth more than any individual decision
  it has produced.

- **The Runtime Host as the canonical execution environment.** Every future
  capability — a Project Engine, a Requirements Engine, a plugin, a
  background worker — is a module or a platform service that runs *inside*
  the runtime this foundation established, classified per ADR-0013 like
  everything before it. None of them is a reason to stand up a second,
  parallel execution model alongside it. If a genuine need ever appears to
  require exactly that, it is a decision large enough to warrant revisiting
  this document itself, explicitly — not a precedent to set by accident
  through a single work package's convenience.

- **A boundary that exists on purpose is not an obstacle to route around.**
  Every constraint in this document exists because something specific was
  learned by trying the alternative, or by reasoning carefully enough to see
  why the alternative would fail. A future contributor who finds one of
  these boundaries inconvenient should assume there is a reason for it and
  go read that reason (an ADR, a case study, a work package retrospective)
  before working around it — and if the reason genuinely no longer holds,
  the correct response is to change the record that established it, in the
  open, not to quietly build around it.

---

## Closing

This document should be read again, not just once at onboarding. It should
be revisited whenever a decision feels large enough to test one of the
principles above — and it should itself be changed only through the same
rigour it describes: reasoned, reviewed, and recorded, never edited quietly
because a single week's pressure made one of these rules feel inconvenient.

TempestOS was not built quickly. It was built so that it would still make
sense to whoever reads it next. That is the foundation this document
protects.
