# WP 2.7B — Runtime Host Implementation

## 1. Introduction

WP 2.7B implements the Runtime Host architecture WP 2.7A designed and froze:
`TempestHost`, `TempestHostBuilder`, `ITempestHost`, `ITempestHostBuilder`, in
`Tempest.Core.Runtime`, plus the Logger sink-isolation fix WP 2.7A identified
but explicitly did not make (architecture-only work modifies no production
code). This is the first work package since WP 2.1 to touch every one of the
six existing platform services at once — not to change any of them, but to
call every one of them, in order, for the first time from a single place.

## 2. Purpose

To build, exactly, the design WP 2.7A already committed to: the Host's
13-phase lifecycle, its own 7-state machine, the platform-service/module
failure boundary (ADR-0013), the two-signal cancellation model (ADR-0014,
ADR-0018), and permissive disposal reused at the Host level (ADR-0004) — and
to do so without redesigning any of it, per this work package's own explicit
constraint.

## 3. Background

WP 2.7A left the architecture complete and every one of its four open
questions resolved (ADR-0015 through ADR-0018), plus two further
Academy artefacts: Engineering Principle 11 (the Atomic Phase Principle) and
the Engineering Glossary, both produced afterward to settle the
lifecycle-phase/atomic-operation terminology this implementation depends on
(see ADR-0018's Terminology section). Nothing about the design itself was
still open by the time this work package began; the work was translation,
not invention.

## 4. The Problem

1. **Fix the Logger sink-isolation gap** WP 2.7A found but could not fix
   (architecture-only), before the Host becomes the heaviest user of logging
   in the whole platform.
2. **Implement the Host's six construction steps** (Configuration, Logging,
   Discovery, Registration, Dependency Injection, Lifecycle) in the exact
   order ADR-0011 already settled, without reopening that ordering question.
3. **Implement the two-signal cancellation model** (ADR-0014) so that
   cancellation is observed only between atomic operations (Engineering
   Principle 11), never in the middle of one, and so that both signals
   converge on the same controlled-shutdown procedure once observed during
   `Starting` (ADR-0018).
4. **Implement the Host's own state machine** (ADR-0012) with descriptive
   exceptions for illegal transitions and single-use enforcement (ADR-0015).
5. **Resolve, rather than silently pick a side on, two genuine ambiguities**
   this implementation surfaced in the frozen architecture and in this work
   package's own brief — see Architectural Decisions, below — without
   redesigning anything either document had already settled unambiguously.

## 5. The Implementation

**The Logger fix** (`Logger.cs`): `_sink.Write(entry)` is now wrapped in a
`try`/`catch`. A sink failure is reported directly to `Console.Error` —
bypassing the failed sink entirely — and never propagates. This closes the
exact gap WP 2.7A's Failure Behaviour document described and recommended
fixing first.

**`TempestHost`** constructs Configuration, Logging, Discovery, Registration,
and Dependency Injection itself, directly, in `RunAsync`, in the order
ADR-0011 fixed — mirroring, line for line, *Runtime Host Architecture.md*'s
"Relationship to Existing Services" section. Discovery, the
`IRuntimeModuleManager`, and the `IModuleLifecycleManager` are held as
private fields and never registered into the `ServiceCollection` (ADR-0017).
A single linked `CancellationToken` (from the caller's own token and an
internal shutdown-request signal) is observed at every phase boundary and
between every module in a batch — never mid-operation — satisfying the
Atomic Phase Principle exactly as ADR-0018's Terminology section describes
it. A platform-service failure transitions the Host to `Faulted` and
rethrows the original, unwrapped exception; an individual module failure
does neither, per ADR-0013, and the Host still reaches `Running`.

**`TempestHostBuilder`** collects configuration sources and produces exactly
one `TempestHost` per instance — `TempestHost`'s constructor is `internal`,
so the builder is the only component that can construct the runtime.

**Shutdown** (`StopInternalAsync`) is one shared routine, entered identically
whether triggered by a graceful stop from `Running` or an early
cancellation/shutdown request from `Starting` — exactly as ADR-0018 and
*Shutdown Sequence.md* require. `StopAllAsync` is passed an escalation token
(a second `StopAsync()` call while already stopping cancels it, matching
*Shutdown Sequence.md*'s "operator escalating from 'please stop' to 'stop
now'"); `DisposeAllAsync` always receives `CancellationToken.None`, per the
same document's explicit requirement that disposal is never interruptible.

**Disposal** (`DisposeAsync`) is always an explicit, separate call — see
ADR-0019 — and is idempotent, safe to call from any state including an
already-`Disposed` host.

## 6. Alternatives Considered

**Having `TempestHostBuilder` construct Configuration, Logging, Discovery,
Registration, and Dependency Injection itself, handing `TempestHost` an
already-fully-assembled platform** — which is what this work package's own
"COMPOSITION ROOT" summary, read literally, describes. Rejected: this
directly contradicts *Host Lifecycle.md*'s Phase 1 ("Host Created… holding
no platform service references yet") preceding Phase 2 ("Configuration
Built"), and *Runtime Host Architecture.md*'s explicit statement that the
Host itself calls `ConfigurationBuilder.AddSource`/`Build()`,constructs
`ReflectionFrameworkDiscoveryService`, and so on. This is the same class of
tension WP 2.7A's own ADR-0011 already resolved once (an illustrative brief
summary vs. the actual, detailed architecture) — resolved here the same way,
in the detailed architecture's favour, rather than by treating the brief's
ASCII-art as a literal, binding redesign of Phase 1/2's ordering.

**Automatically disposing the Host when `RunAsync` faults**, matching a
literal reading of *Shutdown Sequence.md*'s Post-Fault Teardown diagram.
Rejected in favour of always requiring an explicit `DisposeAsync()` call — see
ADR-0019 for the full reasoning; this was a genuine ambiguity between two
frozen documents, not a settled question this work package was free to
reopen casually.

**Making `DisposeAsync()` throw when called on an already-`Disposed` host**,
a literal transplant of ADR-0004's module-level behaviour. Rejected in favour
of standard `IAsyncDisposable` idempotency — see ADR-0019.

**A third "hard stop" cancellation signal**, distinct from the shutdown
request and its escalation. Not introduced — out of scope (the brief
explicitly forbids new capabilities beyond what's specified), and
*Shutdown Sequence.md*'s existing two-token design (a shutdown token for
`StopAllAsync`, `CancellationToken.None` for `DisposeAllAsync`) is satisfied
by treating a repeated `StopAsync()` call as the escalation signal, without
inventing a new concept.

## 7. Why This Solution Was Chosen

Every implementation decision traces back to one of two sources: either the
frozen architecture already specified it exactly (the six-service
construction order, the state machine, the failure boundary), in which case
the job was transcription, not design — or the frozen architecture and/or
this work package's brief left a genuine gap, in which case the gap is
recorded as a decision (ADR-0019) or an explicitly-flagged reconciliation
(the composition-root ordering, above), never silently resolved as if no
tension existed.

## 8. Architectural Principles

- **Atomic Phase Principle** (Engineering Principle 11) — the governing
  principle for this work package's entire cancellation implementation:
  every `ThrowIfCancellationRequested()` call sits at a phase or
  module-batch boundary, never inside one.
- **Separation of Concerns** / **Single Responsibility** — the Host
  orchestrates six already-implemented services; it does not reimplement,
  bypass, or gain new capability over any of them.
- **Fail Fast** — a platform-service failure is Host-fatal and surfaces
  immediately as the original, unwrapped exception; it is never swallowed or
  downgraded to a warning.
- **Deterministic Systems** — single-threaded, sequential orchestration
  throughout `RunAsync`, exactly as *Runtime Host Architecture.md*'s
  Threading section requires.

## 9. Benefits

- The platform now has a real, working single entry point — `TempestHost` —
  for the first time since the module pipeline began in WP 2.1.
- The Logger sink-isolation gap, flagged as a live risk in WP 2.7A's
  retrospective, is closed before the Host became its heaviest user.
- Every one of WP 2.7A's ADRs (ADR-0011 through ADR-0018) is now backed by
  working, tested code, not only documentation.
- Two genuine ambiguities the frozen architecture itself did not settle
  (composition-root ordering vs. this brief; disposal automaticity and
  idempotency) were found and resolved transparently, with ADR-0019 recording
  the one that rose to the level of a real architectural decision.

## 10. Trade-offs

- `TempestHost.RunAsync` swallows `OperationCanceledException` when the
  trigger was an internal shutdown request (`StopAsync()`) rather than the
  caller's own token, matching the established .NET generic-host convention
  for this exact scenario — but this is itself an implementation-level
  contract decision the frozen architecture didn't specify at the interface
  level (WP 2.7A deliberately left `ITempestHost`'s members unspecified), so
  it is documented in XML docs and here rather than assumed obvious.
- No genuinely new test exists for a Host-level defect occurring *during*
  Module Initialisation after modules already exist (as opposed to an
  individual module failure, which is isolated, not Host-fatal) — this
  failure mode is real per *Failure Behaviour.md* but, as that document
  itself notes, is expected to be effectively unreachable in practice; no
  test artificially forces it.

## 11. Common Mistakes

The mistake most worth preserving from this work package: reading a
sequence diagram's *visual flow* as equivalent to its *state-transition
semantics*. *Shutdown Sequence.md*'s Post-Fault Teardown diagram drawing
`-> Disposed` as the last line of a continuous sequence looks, at a glance,
like "this happens automatically" — but the *Runtime State Machine.md*
transition table, read on its own terms, says otherwise ("Disposal is
invoked"). A diagram's ending is not always a claim about *automaticity*; it
can simply be showing what a step *does*, whenever it is eventually invoked.
When a diagram and a transition table disagree, the transition table —
being the more precise, tabular statement — should generally win, and the
diagram should be read as illustrative. ADR-0019 records this exact
reasoning so a future reader does not have to re-derive it.

## 12. Future Evolution

- **Hosted services, background workers, scheduling, plugins, a Requirements
  Engine, a Project Engine** — still not introduced, per this work package's
  explicit constraint. `Runtime Host Architecture.md`'s Future Extensibility
  section remains the design seam for all of them.
- **Service Disposal remains a no-op** — no platform service implements
  `IDisposable`/`IAsyncDisposable` yet. The first one that does will be the
  first real exercise of `TempestHost`'s Service Disposal step.
- **A genuine Host-level defect during Module Initialisation** (as distinct
  from an isolated module failure) remains an untested, if rare, path — a
  future work package touching this area should consider whether it is worth
  a deliberately-forced test case.

## 13. Key Takeaways

1. An architecture that has already resolved every open question (WP 2.7A)
   makes implementation a translation exercise — the vast majority of this
   work package's decisions were "what does the document already say," not
   "what should this be."
2. A frozen architecture can still contain internal tensions its own authors
   didn't notice — two documents describing the same transition in
   different words (a table vs. a diagram) is exactly the kind of thing that
   only surfaces once someone has to write code that must pick one literal
   behaviour. Finding this is not a failure of WP 2.7A; it is a normal,
   expected function of implementation.
3. "Stop and document, don't invent" does not mean "stop all work" — it means
   resolve narrow, genuinely ambiguous points using the most conservative,
   best-precedented choice available (here: matching standard .NET
   `IAsyncDisposable` convention, and re-applying WP 2.7A's own ADR-0011
   precedent), record the reasoning where a reviewer can find it, and keep
   moving — exactly the discipline this Academy has applied to every prior
   tension it has found (the Logger gap, the Atomic Phase Principle/
   `RunBatchAsync` terminology question, and now this).

---

## Architectural Debt Assessment

**No new debt is introduced by this work package.** Every item carried
forward below was already recorded by WP 2.7A and remains exactly as
described; this work package resolves the first one and leaves the rest
unchanged:

- ~~`Logger.Log()` does not isolate sink failures~~ — **fixed by this work
  package.** See the Logger fix, above, and `LoggerTests`'s new sink-isolation
  cases.
- **`LoggerFactory`/`Logger` support exactly one sink**, not fan-out — still
  debt (WP 2.6); unaffected by this work package.
- **Two logging mechanisms coexist** (`ILogger` vs. the legacy
  `LoggingService`) — still debt (WP 2.6); unaffected.
- **No platform service implements `IDisposable`/`IAsyncDisposable` today** —
  still true; `TempestHost`'s Service Disposal step is implemented and
  correctly ordered, but currently has nothing to release. This is not a
  defect in this work package — it is the same designed-for-a-future-need
  no-op WP 2.7A already described — but it means Service Disposal remains
  untested against any real disposable service.
- **A genuine Host-level defect during Module Initialisation is untested** —
  new observation from this work package (see Future Evolution); not a
  defect, a coverage gap around a path the architecture itself describes as
  rare.

## Observations

- **This work package's own brief and the frozen architecture disagreed on
  who constructs Configuration/Logging/Discovery/Registration/DI** — the
  brief's "COMPOSITION ROOT" summary names the builder; *Host Lifecycle.md*
  and *Runtime Host Architecture.md* name the Host itself. Resolved in the
  frozen architecture's favour (see Alternatives Considered), consistent with
  ADR-0011's own precedent for exactly this situation. Flagged here rather
  than silently decided, per this work package's explicit "stop and document
  contradictions" instruction.
- **Two frozen WP 2.7A documents disagreed on Host disposal's automaticity**
  — resolved and recorded as ADR-0019, with ADR-0004 cross-referenced to
  point to it.
- **164 of 164 tests pass**, including 28 new tests exercising `TempestHost`/
  `TempestHostBuilder` directly and 4 new tests exercising the Logger fix;
  the full suite was run five consecutive times with no flakiness observed
  (the concurrency-sensitive cancellation/shutdown-timing tests are
  deterministic by construction — coordinated via `TaskCompletionSource`
  gates, never fixed `Sleep`/timing windows).
