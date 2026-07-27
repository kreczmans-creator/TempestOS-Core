# WP 4.5 — Background Services Architecture

## 1. Introduction

WP 4.5, like WP 2.7A, WP 4.2, and WP 4.4 before it, produced no production
code. Its job was to design the Background Services subsystem completely —
discovery, ownership, construction, startup/shutdown ordering, failure
classification, threading, cancellation, diagnostics, and Host Lifecycle
placement — realising the extensibility seam `Runtime Host Architecture.md`
had named, in prose, since `WP 2.7A`, and the failure model `ADR-0021`
already decided during original v0.4.0 planning, without either being
implemented until now.

## 2. Purpose

To answer, in writing, every question this work package's own brief named:
whether background services are Platform Services, Modules, or their own
kind of Host-owned runtime component; who owns, creates, starts, stops, and
monitors them; how they interact with Dependency Injection, the Event Bus,
Plugins, cancellation, and logging; whether they execute sequentially,
concurrently, or independently; and precisely where they sit in the Host's
own phase table — before a single line of implementation exists.

## 3. Background

By the time WP 4.5 began, `ADR-0021` had already decided background
service *failure classification* — isolated by default, Host-fatal only if
a service declares itself `ICriticalBackgroundService` — during original
v0.4.0 release planning, well before this work package's own brief was
issued. `WP 4.0` had already defined both contracts (`IHostedService`,
`ICriticalBackgroundService`). What remained undecided was everything
*mechanical*: how a background service is found, who constructs it, when
it starts and stops relative to the module pipeline and to its siblings,
and exactly where its own Host Lifecycle phases sit. `Risks.md` (R1) had
already anticipated this work package as "the single riskiest touch-point"
in the release, and explicitly recommended following `ADR-0026`'s own
decimal sub-numbering precedent rather than re-deriving how to insert a
phase from scratch.

## 4. The Problem

1. **Classification** — is a background service a Platform Service, a
   Module, or something else entirely, given it fits none of the existing
   categories cleanly?
2. **Discovery** — can an existing mechanism (Module Discovery, or the DI
   container itself) be reused, or does this need something new — and if
   something new, how does it avoid duplicating Module Discovery's own,
   already-frozen responsibility?
3. **Ownership and orchestration** — who constructs a hosted service, who
   decides when it starts and stops, and is that orchestrator Host-owned
   or DI-public?
4. **Ordering and concurrency** — do multiple hosted services start
   sequentially, concurrently, or independently, and does "independent"
   actually mean something different from "concurrent" here?
5. **Failure mechanics** — exactly how does `ADR-0021`'s already-decided
   isolated/critical distinction get realised in code, including during
   shutdown, without contradicting the Host's own existing cleanup
   guarantees?
6. **Host Lifecycle placement** — precisely which new phase(s), numbered
   how, sitting in which `HostState`, with what entry/exit/failure
   criteria?

## 5. The Design

See `docs/adr/ADR-0029-background-service-discovery-ownership-and-orchestration.md`,
`docs/adr/ADR-0030-background-service-host-lifecycle-placement.md`, and
`docs/architecture/Background Services Architecture.md` in full. In
summary: a background service is a **fourth, Host-owned category** —
neither a Platform Service (a module never resolves a *specific* hosted
service via constructor injection) nor a Module (no `IModule`, no `Id`, not
driven by `ModuleLifecycleManager`) — discovered by a new, dedicated
`IHostedServiceDiscoveryService` that mirrors Module/Plugin Discovery's own
reflection-based pattern but, critically, **never instantiates a
candidate** (a hosted service carries no metadata to read, so the
parameterless-constructor constraint `ADR-0027` solved for modules never
arises here at all). Discovered types are registered as ordinary,
self-referential singletons during the *existing* Platform Services
Registered phase — no new DI capability, no new registration phase. A new,
Host-owned `IHostedServiceManager` starts and stops every discovered
service sequentially, in deterministic order (ascending by type
`FullName`, reversed for stop), mirroring `ModuleLifecycleManager.RunBatchAsync`'s
own established batch shape exactly, with one addition: a critical
service's exception is never isolated — it propagates immediately,
producing `Starting → Faulted` or `Stopping → Faulted`, the identical
transitions the Host's own failure model already defines for a
platform-service failure and a shutdown-time Host-level defect,
respectively. Two new, decimal-numbered Host Lifecycle phases — `8.1`
(Hosted Services Started, within `Starting`) and `10.1` (Hosted Services
Stopped, within `Stopping`) — realise exactly the placement `Runtime Host
Architecture.md` named in prose since `WP 2.7A`.

## 6. Alternatives Considered

Recorded in full, with reasoning, in ADR-0029's own Decision and
Alternatives Considered sections, and permanently indexed as RD-0023
(DI multi-registration resolution — rejected as an unnecessary new
container capability, echoing RD-0019's identical finding for the Event
Bus), RD-0024 (a dedicated `HostedServiceDescriptor` type — rejected since
there is no metadata for one to carry), RD-0025 (extending
`ReflectionFrameworkDiscoveryService` itself — rejected as blurring Module
Discovery's own single responsibility, echoing why Plugin Discovery got
its own service instead), RD-0026 (active Host-level monitoring of ongoing
background work — rejected as speculative given `IHostedService`'s own
contract exposes no surface to monitor), RD-0027 (a new, dedicated
discovery/registration phase — rejected in favour of folding into the
existing Platform Services Registered phase, echoing `WP 4.4D`'s own
identical choice for the Event Bus), RD-0028 (concurrent start of
independent services — rejected in favour of sequential, deterministic
starting, echoing `ModuleLifecycleManager`'s own established shape), and
RD-0029 (automatic restart/backoff for isolated failures — rejected as
premature, with no real hosted service yet built to design a policy
against, echoing ADR-0021's own already-standing Future Considerations).

## 7. Why This Solution Was Chosen

Every non-obvious decision traces back to the same governing question this
release has applied consistently since `WP 4.0`: does an already-proven
pattern already answer this, or is this genuinely new ground? Discovery,
ordering, and failure isolation each reuse an already-proven pattern
directly (reflection-based discovery; `RunBatchAsync`'s sequential batch
shape; `ADR-0013`'s isolated/Host-fatal boundary, already extended once by
`ADR-0021`). The two genuinely new elements — that a hosted service never
needs a metadata-avoidance mechanism at all, and exactly where a critical
failure's exception should surface relative to the Host's own existing
`Faulted`/cleanup guarantees — were each reasoned through explicitly, not
assumed by analogy.

## 8. Architectural Principles

- **Reuse Before Invention** — reflection-based discovery, sequential
  batch orchestration with per-item isolation, and the Event Bus as the
  one cross-component communication channel are all reused directly; only
  two genuinely new types (`IHostedServiceDiscoveryService`,
  `IHostedServiceManager`) are introduced.
- **Minimal Host Complexity** — one new registration line in the existing
  Platform Services Registered phase; two new, decimal-numbered phases,
  following `ADR-0026`'s own precedent exactly; no new `HostState`, no new
  transition.
- **Platform Layering** (ADR-0023) — a hosted service instance depends
  downward on Platform APIs/Services; the orchestrator that starts and
  stops it is Host-owned, never depended upon by anything above it.
- **Avoid Speculative Design** — active monitoring and automatic restart
  were both seriously considered and both deferred, precisely because
  neither has a real, demonstrated consumer yet.

## 9. Benefits

- Every question this work package's own brief named now has a decided,
  written answer, before any implementation — nothing is left to be
  discovered as a bug mid-implementation.
- **A hosted service is constructor-injectable from its very first
  implementation** — confirmed by design, not merely hoped for: because
  `IHostedService` carries no metadata, its own discovery step never
  instantiates a candidate, so the `ADR-0027`-shaped prerequisite modules
  once needed simply does not arise here.
- **Zero new Dependency Injection capability required** — multi-
  registration resolution (rejected, RD-0023) was never actually
  necessary, the second time this exact finding has been confirmed
  independently (the first being RD-0019, for the Event Bus).
- Demonstrates decimal sub-numbering composes correctly across two,
  independent phase-table insertions (Plugin Discovery/Loading at
  `3.1`/`3.2`; Hosted Services at `8.1`/`10.1`) in the same table.

## 10. Trade-offs

- This is documentation only — nothing here is enforced by a compiler,
  test, or running code yet, exactly as every architecture-only work
  package in this release has noted about itself.
- No monitoring exists for a hosted service's own work after `StartAsync`
  returns — a disclosed, deliberate gap (RD-0026), not an oversight; a
  service wanting to surface a later failure should do so via `IEventBus`.
- No automatic restart/backoff for an isolated failure — explicitly
  deferred (RD-0029), echoing `ADR-0021`'s own already-standing Future
  Considerations.
- `Host Lifecycle.md`'s table now has fifteen numbered phases where it
  once had thirteen — a real, if well-precedented, growth in what a new
  reader must take in.

## 11. Common Mistakes

The mistake most worth naming here is one avoided, not one that happened:
assuming a critical hosted service's failure during *shutdown*
(`StopAsync`) should be treated more leniently than one during *startup*
(`StartAsync`), on the reasoning that "the platform is already stopping
anyway." Examined directly against `ADR-0004`/`ADR-0019`'s own existing
guarantees, the correct answer is that both are Host-fatal, symmetrically
— but neither prevents the platform's disposal guarantees from still
holding, since `Faulted → Disposed` remains always legal regardless of
which phase produced the fault. Treating a critical service's stop
failure as merely "isolated, because we're shutting down anyway" would
have quietly weakened the opt-in a service author deliberately chose,
for no principled reason connected to what that opt-in actually means.

## 12. Future Evolution

- **`WP 4.5`'s own implementation** should build
  `IHostedServiceDiscoveryService`/`ReflectionHostedServiceDiscoveryService`
  and `IHostedServiceManager`/`HostedServiceManager` and prove both against
  dedicated test fixtures first — mirroring `WP 4.4B`'s own precedent
  exactly — before extending the sample module set with a background
  service demonstrating both the isolated-failure default and the critical
  opt-in, per `WorkPackages.md`'s own already-approved Deliverables.
- **Active monitoring** of a hosted service's own later failures, and
  **automatic restart/backoff** for an isolated failure, both remain
  available, purely additively, per RD-0026/RD-0029 and ADR-0021's own
  Future Considerations, if a real need for either ever emerges.

## 13. Key Takeaways

1. A component with no identity metadata (`IHostedService`, unlike
   `IModule`) can be simpler to make constructor-injectable than one with
   metadata — because there is nothing to read before construction, there
   is no reason to ever construct a candidate during discovery at all,
   which sidesteps `ADR-0027`'s own prerequisite entirely rather than
   needing an equivalent solution.
2. "Isolated by default, critical by opt-in" (ADR-0021) is a decision about
   *what* happens on failure; realising it precisely still requires a
   separate, explicit decision about exactly *when*, in the Host's own
   state machine, that failure surfaces — the two are related but not the
   same question, and both needed answering.
3. A phase-table extension pattern (decimal sub-numbering, ADR-0026),
   proven once, composes cleanly a second time, in a completely different
   part of the same table, without the second insertion needing to account
   for the first — real evidence the original pattern was well-chosen, not
   merely convenient for its first use.

---

## Architectural Debt Assessment

**No new debt introduced.** This work package produced two ADRs, one
architecture document, and seven Rejected Designs entries; no code exists
for it to affect. Two named, accepted gaps (no active monitoring of a
hosted service's own later failures; no automatic restart/backoff) are
disclosed as part of this design, not newly discovered debt. Every other
debt item on record from the Runtime Foundation, WP 4.0–4.4F remains
exactly as previously described.

## Observations

- **Files changed**: 2 new ADRs (`ADR-0029-background-service-discovery-
  ownership-and-orchestration.md`, `ADR-0030-background-service-host-
  lifecycle-placement.md`); 1 new architecture document (`Background
  Services Architecture.md`); 7 new Rejected Designs entries
  (RD-0023–RD-0029); `Host Lifecycle.md`, `Runtime State Machine.md`,
  `Failure Behaviour.md`, `Ownership Matrix.md`, `Platform Service Map.md`,
  `Engineering Glossary.md`, and `Runtime Host Architecture.md` all
  updated; `Academy Index.md` and `Academy Masterclass Roadmap.md`
  updated; `WorkPackages.md` and `CHANGELOG.md` updated; this
  retrospective. Zero production code files touched — none exist for this
  work package to touch.
- **ADRs required**: 2 (ADR-0029, ADR-0030) — both written in full, as this
  work package's entire deliverable.
- **Risks discovered**: none new. `Risks.md` R1's own, already-named risk
  (this being the work package most likely to tempt a change to
  `TempestHost`'s core sequencing) is mitigated exactly as that risk
  itself recommended — decimal sub-numbering, no renumbering, the same
  rigour every existing phase already has.
- **Readiness assessment**: the design is complete and sound. No
  architectural blocker remains before `WP 4.5`'s own implementation
  begins. This design's own Component Design, Ownership table, and Public
  Surface listing were all produced with the same rigour `WP 4.4`'s own
  Event Bus design phase established, and are ready to be realised without
  deviation.
