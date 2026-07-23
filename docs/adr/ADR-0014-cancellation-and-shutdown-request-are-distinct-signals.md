# ADR-0014: Cancellation and Shutdown-Request Are Distinct Signals

## Status

Accepted — WP 2.7 (Runtime Host Architecture), 2026-07-22. Architecture only;
no code changes accompany this decision.

**Update, WP 2.7 architectural review:** this ADR's Negative consequence —
"what happens if a shutdown request arrives during `Starting`, before
`Running` is reached" — was the one open question that survived this work
package's initial review. It is now resolved by ADR-0018, *Startup
Cancellation Transitions to Controlled Shutdown*: both signals, when they
arrive during `Starting`, transition the Host to `Stopping` identically. The
two signals remain conceptually distinct, exactly as this ADR decided — only
their *handling once either fires during `Starting`* was left open, and is
what ADR-0018 settles.

## Context

Every lifecycle operation in the platform already accepts a
`CancellationToken` (WP 2.3), and cancellation already has an established
meaning at that level: "stop processing further modules in this batch
immediately; do not treat this as a module failure." The Host needs a way for
something external (an operator, a process manager, a signal handler) to ask
it to stop — the question is whether that request should be modelled as the
*same* signal as WP 2.3's existing cancellation, or as something distinct.

Two designs were considered:

**Option A — One signal.** A single `CancellationToken`, supplied once when
the Host starts, that means both "abort startup if it hasn't finished" and
"stop the running platform" depending on when it fires. Simple: one token,
one meaning, interpreted contextually by when it's observed.

**Option B — Two signals.** The startup-time `CancellationToken` (aborts
startup, exactly as WP 2.3 already defines) is kept separate from a
distinct "shutdown requested" signal that only has meaning once the Host has
reached `Running` — triggering a *graceful* `Stopping` sequence (Stop, then
Dispose, in the established reverse order) rather than an abrupt abort.

## Decision

Option B. Cancellation and shutdown-request are modelled as two distinct
signals with two distinct meanings:

- A **startup cancellation token**, observed only during `Starting`, means
  exactly what it already means throughout WP 2.3: abort immediately, without
  treating it as a fault, and attempt to tear down whatever was already
  brought up (see *Runtime State Machine.md*'s `Starting → Stopped` path).
- A **shutdown request**, observed only once `Running`, initiates the Host's
  own graceful shutdown sequence — `Running → Stopping → Stopped` — calling
  `StopAllAsync` then `DisposeAllAsync` in the established order.

## Consequences

**Positive:**

- The two signals' very different consequences (abandon startup vs. begin an
  orderly, multi-step shutdown of an already-running platform) are kept
  conceptually separate, matching how differently they need to be handled.
- A future implementation is free to satisfy both from the same underlying
  OS-level trigger (for example, both wired to `Ctrl+C`/`SIGTERM`) without
  that operational convenience forcing the *architectural* concepts to merge
  — the Host's internal model stays clear even if, in practice, one external
  event happens to raise both.
- This mirrors the platform's existing convention of treating cancellation as
  categorically different from failure (WP 2.3, reaffirmed by WP 2.6's
  `Logger`/sink design) — extending, rather than contradicting, an established
  pattern.

**Negative:**

- Two signals are conceptually more to reason about than one — a future
  implementer needs to be clear about which one governs which phase, and what
  happens if a shutdown request arrives *during* `Starting` (before `Running`
  is reached at all) — see the Open Questions section of the WP 2.7 Academy
  review for this specific gap, left open rather than resolved speculatively
  here.

## Future Considerations

If hosted services or background workers are introduced (see the Platform
Service Map's planned extensibility points), each will need its own
cancellation story, and should be designed to observe the *shutdown request*
signal (to participate in graceful shutdown), not the startup cancellation
token (which will already have served its purpose and completed by the time
any hosted service is running).
