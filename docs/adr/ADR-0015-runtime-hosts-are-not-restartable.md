# ADR-0015: Runtime Hosts Are Not Restartable

## Status

Accepted — resolves WP 2.7's Open Question 2, 2026-07-22. Architecture only;
no code changes accompany this decision.

## Context

*Runtime State Machine.md* (WP 2.7) left open whether a `Stopped` or `Faulted`
`TempestHost` could transition back to `Running` — an in-process restart —
rather than requiring a new instance for another run. This was flagged as a
product question, not resolvable from architecture alone, and deferred.

On reflection, it is answerable from the architecture alone, because
restart is not actually available cheaply — it would have to be invented,
component by component, across services that were never designed to support
it:

- `RuntimeModuleManager` has no deregistration API — there is no way to
  return it to an empty, pre-registration state short of discarding it.
- `TempestServiceProvider`'s singleton cache has no invalidation
  mechanism — resolved singletons stay resolved for the provider's whole
  life (WP 2.4).
- `ModuleLifecycleManager`'s own state machine treats `Disposed` as terminal
  for every individual module (ADR-0004) — a module cannot legally return to
  `Registered` after being disposed, by design.
- Every state machine already in the platform — `ModuleState`, and now the
  Host's own (ADR-0012) — was designed around "build once, tear down once."
  None of them were designed with a "reset to construct-time state" operation
  in mind, and retrofitting one onto any single component would be a real,
  separate design exercise with its own questions (what does "reset" mean for
  an already-resolved singleton? for a module discovered from an assembly
  that's since been unloaded?).

Restart is therefore not "flip one flag" — it is "design reset semantics for
at least three components that have never needed them," each a decision in
its own right.

## Decision

A `TempestHost` instance is single-use: `Created → Running → Stopped →
Disposed` (or `→ Faulted → Disposed`). `Stopped` and `Faulted` never
transition back to `Starting` or `Running` — this was already documented as
the illegal-transitions list in *Runtime State Machine.md*; this ADR makes it
a decided design commitment rather than an open question. A second run is
always a new `TempestHostBuilder` producing a new `TempestHost`, backed by
entirely fresh instances of every collaborator (a new `RuntimeModuleManager`,
a new `TempestServiceProvider`, and so on) — nothing is reused across runs.

## Consequences

**Positive:**

- Dramatically simpler lifecycle reasoning: no component anywhere in the
  platform needs a reset/reinitialisation code path, ever.
- Consistent with every state machine already in the platform, all of which
  already assume "constructed once, disposed once" as their entire lifetime
  model — this decision doesn't introduce a new constraint, it recognises one
  that was already implicitly true everywhere else.
- Removes an entire category of future bug: partially-reset state left over
  from a previous run silently corrupting the next one.

**Negative:**

- A hypothetical future need for in-process restart (a long-lived supervisor
  process wanting to restart the platform without restarting the OS process)
  is met, under this decision, by discarding and fully rebuilding everything —
  more expensive than a hypothetical "soft reset" would be. Accepted: no such
  requirement exists today, and designing reset semantics speculatively,
  ahead of a real need, is exactly the kind of premature complexity this
  Academy's own principles (see Fail Fast, Deterministic Systems) argue
  against.

## Future Considerations

If in-process restart ever becomes a genuine requirement, it should be built
as a new capability *layered above* `TempestHost` — a supervisor that
constructs, runs, disposes, and reconstructs `TempestHost` instances in a
loop — rather than by adding reset semantics to `TempestHost` or any of its
collaborators directly. This preserves every existing state machine's
terminal-disposal guarantee rather than weakening it for the sake of restart
support.
