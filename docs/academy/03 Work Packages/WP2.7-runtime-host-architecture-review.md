# WP 2.7 — Runtime Host Architecture Review

## 1. Introduction

WP 2.7 is different in kind from every work package before it: it produced no
production code at all. Its job was to design the Runtime Host — the single
entry point that will one day orchestrate Configuration, Logging, Dependency
Injection, Discovery, Registration, and Lifecycle into one running platform —
before a single line of it is implemented. This retrospective covers six new
architecture documents, four new ADRs, one ADR update, and — not originally
in scope, but too significant to leave undocumented — a real gap discovered
between WP 2.6's stated architectural principle and its shipped code.

## 2. Purpose

To answer, in writing, before implementation begins: what phases does the
Host pass through; what does it own and explicitly not own; what does startup
and shutdown actually look like, end to end; what state machine governs the
Host itself; and what happens when each of the platform services it
orchestrates fails.

## 3. Background

By the time WP 2.7 began, six platform services existed, each independently
documented, tested, and reviewed (WP 2.1–2.6), and the Platform Service Map
had just made explicit what every one of the previous six retrospectives had
been circling: every implemented service's "consumers" column eventually
points at something that doesn't exist yet. "Host" was the Platform Service
Map's own name for that gap. WP 2.7 is the design work that gap requires
before it can be closed.

## 4. The Problem

1. **What order do six independently-designed services actually need to come
   up in**, given that their real dependencies (not the brief's own
   illustrative phase list) determine the only correct answer?
2. **What does the Host own, and what must it explicitly never touch**,
   given six work packages' worth of established separation-of-concerns
   discipline this new component must not undo?
3. **What does the Host's own "state" mean**, given a module-level state
   machine (`ModuleState`) already exists and it would be tempting, but wrong,
   to simply reuse or derive from it?
4. **What happens when something fails**, at every one of the failure points
   the six existing services can produce, and does the Host apply WP 2.3's
   established per-module isolation uniformly, or does something at the
   platform-service level need to behave differently?
5. **How does shutdown actually work**, including the case where shutdown is
   really a post-fault teardown of a startup that never finished?
6. **Where does everything else the platform will eventually need — hosted
   services, a Requirements Engine, a Project Engine, background workers,
   scheduling, plugins — actually plug in**, without requiring the Host itself
   to be redesigned for each one?

## 5. The Design

Six documents, each answering one of the questions above in detail: *Runtime
Host Architecture.md* (responsibilities, non-responsibilities, threading,
future extensibility), *Host Lifecycle.md* (all thirteen phases, each with
purpose/entry/exit/failure criteria), *Startup Sequence.md* (a complete
sequence diagram and failure-path table), *Shutdown Sequence.md* (graceful and
post-fault teardown diagrams, plus the cancellation-token discipline that
governs them), *Runtime State Machine.md* (the Host's own seven-state machine,
with an explicit illegal-transitions list), and *Failure Behaviour.md* (every
named failure mode, resolved against ADR-0013's governing principle).

The central design finding, stated plainly: **the six existing services
already determine almost the entire design.** Discovery's independence from DI
(ADR-0008) forces Discovery and Registration to precede DI container
construction, not follow it (ADR-0011). WP 2.3's per-module isolation forces
the Host to distinguish "platform-service failure" from "module failure"
explicitly (ADR-0013), rather than applying one uniform policy. ADR-0004's
permissive-disposal philosophy generalises cleanly to the Host's own
`Faulted → Disposed` transition, needing no new invention, only recognition
that the same reasoning applies one level up. WP 2.7's job was substantially
to *discover and make explicit* a design the prior six work packages had
already, implicitly, constrained — not to invent one freely.

## 6. Alternatives Considered

**Deriving Host state from aggregated module state**, rather than giving the
Host its own state machine. Rejected — see ADR-0012: a Host can be `Running`
with modules `Failed`, which is not representable as a derived aggregate
without losing information either question needs answered independently.

**One cancellation signal for both startup and shutdown.** Considered and
rejected — see ADR-0014: startup cancellation ("abandon what hasn't finished")
and a shutdown request ("gracefully stop what has") have different
consequences and are kept conceptually distinct, even though a future
implementation may wire both to the same external trigger (`Ctrl+C`) in
practice.

**Applying WP 2.3's per-module isolation uniformly to platform services too**
(a Configuration failure just gets logged and the Host limps on without
configuration). Rejected outright — see ADR-0013: platform services are
infrastructure every module depends on; there is no coherent "partial
platform" the way there is a coherent "partially healthy module set."

**Treating the brief's illustrative phase order as literal and binding.**
Considered, since the brief is the direct source of the phase names. Rejected
in favour of reconciling the phase list with the actual, already-established
dependency graph (ADR-0011) — a literal reading would have required either
violating ADR-0008 (giving Discovery a DI dependency it was deliberately
designed without) or inventing a "rebuild the container after Discovery"
capability the DI container has never had and was not asked for here.

**Silently fixing the `Logger` sink-isolation gap discovered during this
work.** Rejected — WP 2.7 is architecture-only and modifies no production
code, per its own explicit constraint. The gap is documented in full (this
retrospective's Architectural Debt Assessment, and *Failure Behaviour.md*'s
"Logging failure" section) rather than quietly patched, consistent with the
Engineering Governance principle of documenting unrelated issues and fixing
only blockers — this is not a blocker to WP 2.7's own deliverables, all of
which are documentation.

## 7. Why This Solution Was Chosen

Every non-obvious decision in this work package traces back to the same
governing question: does six work packages' worth of established
architecture already answer this, or is this genuinely new ground? Where the
answer was "already answered" (Discovery's DI-independence, per-module
isolation, permissive disposal), the Host's design simply had to recognise and
apply that answer correctly, one level up. Where the answer was genuinely new
(the Host's own state machine, the platform-service/module failure boundary,
the two-signal cancellation model), each was resolved with its own ADR, on its
own explicit reasoning, rather than by analogy alone.

## 8. Architectural Principles

- **Separation of Concerns** — the Host orchestrates; it does not implement
  any of the six services it calls, and does not gain any new capability none
  of them already had.
- **Single Responsibility** — the Host's non-responsibilities list
  (business logic, configuration parsing, module implementation, logging
  implementation) is as important a design artefact as its responsibilities
  list.
- **State Machines** — the Host gets a properly modelled state machine
  (`Runtime State Machine.md`), with explicit terminal states and an explicit
  illegal-transitions list, following exactly the discipline WP 2.2/2.3
  established for `ModuleState`.
- **Fail Fast** / **Deterministic Systems** — the failure model
  (*Failure Behaviour.md*) makes every failure path's outcome an explicit,
  documented fact rather than something an implementer would need to decide
  ad hoc, on the day, under time pressure.

## 9. Benefits

- Implementation, when it happens, has a complete design to build against —
  every phase, every transition, every failure path, and every open question
  is already named, rather than being discovered mid-implementation.
- The reconciliation in ADR-0011 means the eventual implementation will not
  need to retrofit Discovery's DI-independence after the fact — the correct
  ordering is established before any code exists to get it wrong.
- The platform-service/module failure boundary (ADR-0013) gives the future
  implementation a single, clear rule to apply at every one of the five
  platform-service call sites, rather than five ad hoc decisions.
- A real, previously undiscovered gap between WP 2.6's stated principle and
  its shipped code was found and documented as a direct consequence of
  designing the Host's failure model carefully enough to need to state what
  "a logging failure" should do.

## 10. Trade-offs

- This is documentation only — none of it is enforced by a compiler, a test,
  or running code yet. Every rule in *Failure Behaviour.md* and every
  transition in *Runtime State Machine.md* is a design commitment the
  implementation work package must actually honour; nothing here guarantees
  that on its own.
- Several genuine open questions were identified but deliberately left open
  rather than resolved speculatively (see Open Questions below) — this is a
  trade-off in itself: resolving them now, without an implementation to test
  the answer against, risked guessing wrong in a way that would be more
  expensive to unwind later than deferring the decision.

## 11. Common Mistakes

The mistake most worth preserving from this work package is a near miss avoided,
not one that happened: treating the brief's illustrative Host phase order as
a literal specification rather than a starting point to reconcile against
reality. Every one of WP 2.1 through WP 2.6's briefs contained similarly
illustrative language ("expected phases include," "suggested states") that
turned out to need adaptation once checked against the actual dependency
graph of what already existed. The lesson, restated once more because it keeps
recurring: a brief's phase or state *names* are usually right; a brief's
*implied ordering or exact shape*, when it conflicts with an already-
established ADR, needs to lose that conflict, explicitly and in writing (as
ADR-0011 does here), not be followed literally out of deference to the
document that suggested it.

## 12. Future Evolution

- **Implementation.** The obvious next step: a work package that actually
  builds the Host against this design, resolving the open questions below as
  it goes (or explicitly deferring them further, with reasoning, rather than
  silently).
- **The `Logger` sink-isolation fix** (see Architectural Debt Assessment)
  should land before, or as part of, the Host's own implementation — the Host
  will be the heaviest user of logging during startup and shutdown, and is
  exactly the caller this fix protects.
- **Hosted services, Requirements Engine, Project Engine, background workers,
  scheduling, plugins** — all named in *Runtime Host Architecture.md*'s Future
  Extensibility section, each with a first-pass note on where it would plug
  in; none designed in any further detail here, deliberately.

## 13. Key Takeaways

1. An architecture-only work package's value is proportional to how much of
   its design is *discovered* from what already exists, rather than invented
   fresh — WP 2.7's central finding was that six prior work packages had
   already constrained almost the entire answer; the job was recognising that,
   not designing from a blank page.
2. A brief's illustrative ordering is a starting hypothesis, not a
   specification — checking it against existing ADRs (here, ADR-0008) is what
   turned a plausible-looking phase list into a correct one.
3. Two independent state machines answering two different questions
   (platform state vs. individual module state) is the right design when, and
   only when, the two questions really can have different answers at the same
   moment — ADR-0012 exists because that's demonstrably true here (`Running`
   with a `Failed` module is a real, expected state of the world).
4. Documentation work can surface real implementation bugs without writing or
   changing a single line of code — designing a failure model rigorously
   enough to state "logging failures must never terminate the runtime" is
   what exposed that this is not currently true, months after the code that
   violates it shipped and passed all of its own tests (which never exercised
   a failing sink).

---

## Architectural Debt Assessment

- **`Logger.Log()` does not isolate sink failures.** `_sink.Write(entry)` is
  called with no exception handling, directly contradicting WP 2.6's own
  stated principle ("logging failures must never terminate the runtime"). Not
  fixed here (WP 2.7 modifies no production code); recommended as a small,
  scoped fix before or alongside Host implementation. See *Failure
  Behaviour.md*, "Logging failure."
- **`LoggerFactory`/`Logger` support exactly one sink**, not fan-out —
  already noted as debt in the WP 2.6 retrospective; the Host's design does
  not need multi-sink support to proceed, but a future sink addition should
  resolve this via a composite `ILogSink`, not by changing the Host.
- **Two logging mechanisms coexist** (`ILogger` vs. the legacy
  `LoggingService`) — already noted as debt in WP 2.6; unaffected by, and not
  resolved by, this work package.
- **No platform service implements `IDisposable`/`IAsyncDisposable` today** —
  Service Disposal (*Host Lifecycle.md*, phase 12) is currently a designed
  no-op. This is not a defect — the Host's design accounts for disposal
  correctly whether or not there is anything to dispose — but it means the
  Host's disposal-ordering guarantee is, for now, untested against any real
  disposable service. The first platform service to implement
  `IDisposable`/`IAsyncDisposable` should be treated as the first real test of
  this part of the design.
- **`Tempest.Core.Hosting` (the pre-existing `HostingService`) and the new
  Runtime Host risk namespace/naming confusion** — flagged in *Runtime Host
  Architecture.md* as an open question for the implementation work package,
  not resolved here.

## Open Questions

**Resolved since this retrospective was first written**, under architectural
review (see *Runtime Host Architecture.md*, *Runtime State Machine.md*, and
the Ownership Matrix for where each resolution now lives):

- ~~Should a `Stopped` or `Faulted` Host ever support being restarted?~~
  **Resolved: no — ADR-0015**, *Runtime Hosts Are Not Restartable*. A
  `TempestHost` is single-use; a second run means a new
  `TempestHostBuilder`/`TempestHost` pair, never a transition back to
  `Starting`. Reasoning: restart is not cheap to support — it would require
  inventing reset semantics for at least three components
  (`RuntimeModuleManager`, `TempestServiceProvider`, `ModuleLifecycleManager`)
  that were never designed for it, none of which currently has a coherent
  answer to "what does reset even mean here."
- ~~Where, precisely, should the new Runtime Host live?~~ **Resolved —
  ADR-0016**, *The Host Lives in Tempest.Core.Runtime, Distinct From
  Tempest.Core.Hosting*. Names: `TempestHost`, `TempestHostBuilder`,
  `ITempestHost`, `ITempestHostBuilder`. Governing rule: Runtime = platform,
  Hosting = environment/deployment adapters.
- ~~Should Discovery, Registration, and Lifecycle become resolvable via the DI
  container?~~ **Resolved: no — ADR-0017**, *Discovery, Registration, and
  Lifecycle Remain Host-Owned Collaborators, Not Public DI Services*.
  Registering them would let a module reach back into the machinery
  orchestrating it (registering new modules mid-startup, stopping other
  modules, retriggering discovery), directly undermining the deterministic
  startup model this whole work package exists to establish.

- ~~What happens if a shutdown request arrives during `Starting`, before
  `Running` is ever reached?~~ **Resolved — ADR-0018**, *Startup Cancellation
  Transitions to Controlled Shutdown*. Both the startup cancellation token and
  an early shutdown request, whichever fires first, transition the Host
  `Starting → Stopping` — the same controlled-shutdown procedure a graceful,
  post-`Running` shutdown already uses, not a bespoke "partial teardown" path.
  This was the one open question that survived the first round of
  architectural review; none remain outstanding.

**No open questions remain from WP 2.7's original four.** All were resolved
under architectural review (ADR-0015 through ADR-0018) rather than deferred to
implementation.

## Risks

- **The `Logger` sink-isolation gap** (Architectural Debt Assessment) is a
  real, live risk to any code — including the future Host itself — that logs
  during a failure path today: a failing sink can currently mask or corrupt
  the very failure being logged, by throwing before the original failure's
  own handling completes.
- **This design is unimplemented and unverified.** Every sequence diagram and
  state transition here is a design intent, not a tested behaviour — the risk
  standard to any architecture-only work package: the design could still
  prove wrong or incomplete once real implementation and testing begin.
- ~~Namespace collision risk between the new Host and
  `HostingService`/`Tempest.Core.Hosting`~~ — **resolved by ADR-0016**; no
  longer a risk.

## Recommendations

1. Fix the `Logger` sink-isolation gap before or alongside Host
   implementation — it is small, scoped, and directly protects the Host's own
   heaviest use case (logging throughout startup and shutdown, including
   failure paths).
2. Implement the controlled-shutdown procedure (Module Disposal + Service
   Disposal) as a single, shared routine from the very first line of code,
   invoked identically whether `Stopping` was entered from `Running` or from a
   cancelled/interrupted `Starting` (ADR-0018) — do not let two separate
   implementations of "tear down whatever exists" emerge during
   implementation, even informally.
3. Implement `TempestHost`/`TempestHostBuilder` in `Tempest.Core.Runtime`
   exactly as ADR-0016 names them, and keep Discovery/Registration/Lifecycle
   Host-held per ADR-0017 from the first line of code — these are decided, not
   suggestions to reconsider mid-implementation.
4. Use the Ownership Matrix as the first reference when any "who should do
   this?" question comes up during implementation — it is designed to answer
   most of them without needing to re-derive the reasoning from scratch.
