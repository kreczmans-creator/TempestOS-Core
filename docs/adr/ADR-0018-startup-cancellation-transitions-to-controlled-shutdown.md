# ADR-0018: Startup Cancellation Transitions to Controlled Shutdown

## Status

Accepted — resolves WP 2.7's one surviving open question (a shutdown request
arriving during `Starting`), 2026-07-22. Architecture only; no code changes
accompany this decision.

## Context

*Runtime State Machine.md* originally modelled cancellation during `Starting`
as a direct edge straight to `Stopped`: "Startup cancellation token fires →
attempt teardown of partial startup → `Stopped`," entirely separate from the
`Running → Stopping → Stopped` path a graceful, post-`Running` shutdown
follows. This left WP 2.7's one surviving open question unanswered: what
happens if the *other* signal (ADR-0014's running-time shutdown request, not
the startup cancellation token) arrives while the Host is still `Starting`?
No path was drawn for it at all.

Having two separate, independently-specified "tear down whatever exists"
procedures — one bespoke path for a cancelled startup, one for a graceful
post-`Running` shutdown — was also, on inspection, an unforced complication.
Both need to do exactly the same thing: dispose whatever modules exist
(Module Disposal), then whatever platform services exist (Service Disposal),
tolerating however much or little was actually built. There was no good
reason for two descriptions of that one procedure to exist.

## Decision

Both signals — startup cancellation and an early shutdown request — are
handled identically when they arrive during `Starting`: the Host transitions
`Starting → Stopping`, exactly as `Running → Stopping` already does, and
Module Disposal + Service Disposal run against whatever was actually built so
far. The `Starting → Stopped` direct edge is removed entirely — every path to
`Stopped` now passes through `Stopping`.

This applies only to cancellation and shutdown-request signals, neither of
which is a failure. A genuine platform-service failure during `Starting`
still transitions directly to `Faulted`, per ADR-0013, entirely unaffected by
this decision — `Faulted`'s own path to `Disposed` (ADR-0004's Host-level
reuse) remains its own, separate edge, since a fault and a deliberate stop
signal are different categories of event, not two names for the same thing.

## Consequences

**Positive:**

- **Startup remains deterministic.** Nothing about the phases themselves
  changes — Configuration Built through Module Initialisation still happen in
  the same fixed order. Only where an interrupted sequence transitions to
  next is affected.
- **Disposal guarantees remain valid.** `Stopping`'s controlled shutdown
  already guarantees, via ADR-0004's Host-level reuse, that disposal is
  attempted regardless of what exists. Routing early-interrupted startups
  through the same state means they inherit that guarantee automatically,
  rather than needing a second, separately-verified guarantee for a bespoke
  path.
- **Cleanup logic is shared, not duplicated.** Module Disposal and Service
  Disposal are specified once, in `Stopping`, and reused for every path that
  reaches it — a fully-started platform shutting down gracefully, and a
  startup interrupted after only two of six phases completed, are torn down
  by the exact same procedure.
- **Partial startup cannot leak resources by omission.** A bespoke,
  separately-specified "partial teardown" procedure risks being less
  complete than the main shutdown procedure (missing a step the main one has
  and the bespoke one doesn't). Sharing one specification removes that risk
  structurally, not by discipline alone.
- **The state machine remains minimal**, and this directly resolves WP 2.7's
  one surviving open question as a side effect, rather than requiring a
  separate design for it: an early shutdown request now has an obvious
  answer — the same one cancellation already has, because both now lead to
  the same place.

**Negative:**

- A reader encountering `Stopping` for the first time needs to know it can be
  entered from two different states (`Running` or `Starting`), not just one —
  mitigated by this ADR and by *Runtime State Machine.md*'s diagram showing
  both edges explicitly.
- The `Stopping`-phase disposal procedure must be specified generally enough
  to handle "almost nothing was built yet" as comfortably as "everything was
  built and running" — a marginally more general requirement than a
  post-`Running`-only path would need. Judged a small, worthwhile cost for
  the guarantees above.

## Future Considerations

If a future hosted-service or background-work capability introduces its own
startup-time resources needing disposal, it should register with this same
`Stopping`-phase disposal procedure — not add a third, separate teardown path.
Any future work package tempted to special-case "cleanup during startup"
differently from "cleanup during shutdown" should revisit this ADR's
reasoning first.
