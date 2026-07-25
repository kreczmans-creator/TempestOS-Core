# ADR-0030: Background Service Host Lifecycle Placement

## Status

Accepted — v0.4.0, WP 4.5 (design phase), 2026-07-25. Resolves the second
of the two decisions ADR-0021 left open for background services: not *how*
they are discovered, owned, and orchestrated (ADR-0029), but precisely
*where*, in `Host Lifecycle.md`'s phase table, that orchestration occurs.
Mirrors exactly the precedent ADR-0026 established for Plugin Discovery/
Loading — a phase-table extension decided by its own dedicated ADR, before
implementation, following the same decimal sub-numbering discipline.

## Context

`Runtime Host Architecture.md`'s own Future Extensibility section named the
intended placement, in prose, since WP 2.7A: background services "would
slot in between Module Initialisation and Runtime Running at startup, and
at the front of Shutdown — started after modules are initialised, stopped
before modules are." `Risks.md` (R1) already anticipated this work package
would need to extend the Host's own phase table a second time (the first
being ADR-0026's Plugin Discovery/Loading insertion) and explicitly
recorded the expectation that it "should follow ADR-0026's decimal
sub-numbering precedent rather than re-deriving how to insert a phase from
scratch." This ADR is that decision, made explicitly rather than assumed.

### What already exists, confirmed directly

- `Host Lifecycle.md`'s current table: `1` (Host Created) through `13`
  (Host Disposed), with `3.1`/`3.2` (Plugin Discovery/Loading) already
  inserted between `3` (Logging Built) and `4` (Module Discovery) —
  proof that decimal sub-numbering, once used, composes cleanly with a
  second, later insertion elsewhere in the same table.
- `8` (Module Initialisation) and `9` (Runtime Running) are adjacent,
  with nothing currently between them.
- `10` (Shutdown Requested) and `11` (Module Disposal) are adjacent, with
  nothing currently between them.
- `Runtime State Machine.md`'s seven states are unaffected by either
  existing plugin phase — both occur entirely within `Starting`. The same
  is true here: both new phases this ADR introduces occur entirely within
  already-existing states (`Starting` and `Stopping` respectively).

## Decision

**Two new, decimal-numbered phases, inserted using exactly ADR-0026's own
precedent — no existing phase renumbered:**

| # | Phase | Host State |
|---|---|---|
| 8.1 | Hosted Services Started | `Starting` |
| 10.1 | Hosted Services Stopped | `Stopping` |

**Phase 8.1 — Hosted Services Started.**

- **Purpose.** Construct `HostedServiceManager` from the hosted service
  types discovered and registered during Platform Services Registered
  (Phase 6, per ADR-0029), and drive every one to started, in deterministic
  order.
- **Entry criteria.** Module Initialisation (Phase 8) has completed —
  every module has been given the chance to initialise and start,
  regardless of individual module outcomes (ADR-0013), exactly as
  `Runtime Host Architecture.md` already specified: background services
  start *after* modules, never interleaved with them.
- **Exit criteria.** `HostedServiceManager.StartAllAsync` has returned.
  This does **not** require every service to have started successfully —
  an isolated (non-critical) service's failure does not prevent this phase
  from completing, exactly as an individual module's failure does not
  prevent Module Initialisation from completing.
- **Failure behaviour.** An isolated service's `StartAsync` failure: not
  Host-fatal, logged, that service marked `Failed`, batch continues
  (ADR-0021/ADR-0029). A **critical** service's failure: Host-fatal,
  `Starting → Faulted` — the identical transition every other startup
  phase already uses, requiring no new state or transition.

**Phase 10.1 — Hosted Services Stopped.**

- **Purpose.** Stop every started hosted service, in the reverse of Phase
  8.1's own order, before any module is stopped.
- **Entry criteria.** Shutdown Requested (Phase 10) has occurred —
  whether from a graceful, post-`Running` shutdown request, or from
  cancellation/an early shutdown request during `Starting` (ADR-0018).
  **If `HostedServiceManager` was never constructed** (the Host faulted, or
  was cancelled, before Phase 8.1 ever ran), this phase is a no-op — there
  is nothing to stop, mirroring exactly how Module Disposal already
  tolerates `_lifecycleManager` never having been constructed.
- **Exit criteria.** `HostedServiceManager.StopAllAsync` has returned.
  Does not require every service to have stopped cleanly — an individual
  service's isolated stop failure does not prevent this phase from
  completing.
- **Failure behaviour.** An isolated service's `StopAsync` failure: not
  Host-fatal, logged, batch continues — mirroring Module Disposal's own
  already-established policy for individual module stop/dispose failures.
  A **critical** service's `StopAsync` failure: Host-fatal,
  `Stopping → Faulted` — a transition `Runtime State Machine.md` already
  defines for "a genuine Host-level defect during shutdown orchestration
  itself." Cleanup guarantees are unaffected: `Faulted → Disposed` remains
  always legal and disposal of every module, and every other hosted
  service already stopped, is still attempted afterward (ADR-0004,
  ADR-0019) — a critical service's own stop failure aborts only the
  *remainder* of this one phase, never the platform's overall ability to
  reach `Disposed`.

**No renumbering of the existing thirteen (now fifteen) phases.** Every
existing cross-reference in `Host Lifecycle.md`, `Runtime State Machine.md`,
`Startup Sequence.md`, `Shutdown Sequence.md`, `Failure Behaviour.md`, prior
ADRs, and every prior Academy retrospective that cites a phase by number
remains correct, unchanged, and valid — the identical outcome ADR-0026
already achieved once, now demonstrated to compose with a second,
independent insertion elsewhere in the same table without conflict.

**No new `HostState`, no new transition.** Both new phases occur entirely
within states the Host already has (`Starting`, `Stopping`); the seven-state
machine ADR-0012 established is unaffected.

**Post-Fault Teardown, extended.** *Shutdown Sequence.md*'s Post-Fault
Teardown path (a platform-service, Module Discovery/Registration, or
critical-hosted-service-start failure aborting `Starting`) now additionally
attempts `HostedServiceManager.StopAllAsync(CancellationToken.None)` for
whichever services successfully started before the fault, if
`HostedServiceManager` exists at all — mirroring exactly how that same path
already conditionally attempts `ModuleLifecycleManager.DisposeAllAsync` only
if `ModuleLifecycleManager` was constructed before the fault occurred.

## Alternatives Considered

**A single combined phase for both Hosted Services Started and Hosted
Services Stopped**, reasoning that both concern "background service
lifecycle" and could share one entry in the table. Rejected — these occur
in entirely different Host states (`Starting` versus `Stopping`), at
entirely different points in the Host's own run, and conflating them into
one table row would misstate that they are two, temporally distant events,
not one.

**Numbering the new phases `13.1`/`13.2`** (after the existing final phase,
Host Disposed) rather than `8.1`/`10.1`, on the reasoning that "new"
capabilities should append rather than interleave. Rejected —
`Runtime Host Architecture.md`'s own, already-established intent places
Hosted Services Started *between* Module Initialisation and Runtime
Running, and Hosted Services Stopped *between* Shutdown Requested and
Module Disposal; numbering them after Host Disposed would misstate *when*
they actually occur, which is precisely what phase numbers in this table
exist to communicate.

**Renumbering all fifteen (thirteen plus the two existing plugin) phases
sequentially**, now that a second decimal insertion is being made.
Rejected, for the identical reason RD-0013 already rejected it for Plugin
Discovery/Loading: the blast radius (every cross-reference across five
architecture documents, prior ADRs, and prior Academy retrospectives) is
entirely disproportionate to what remains a pure insertion.

## Consequences

**Positive:**

- Realises `Runtime Host Architecture.md`'s own, long-standing Future
  Extensibility sentence exactly, rather than reinterpreting it.
- Demonstrates decimal sub-numbering composes correctly across two,
  independent insertions (Plugin Discovery/Loading at `3.1`/`3.2`; Hosted
  Services at `8.1`/`10.1`) in the same table, without either insertion
  needing to account for the other.
- No renumbering, no new `HostState`, no new transition — the smallest
  change that fully realises the design ADR-0029 already committed to.

**Negative:**

- `Host Lifecycle.md`'s table now has fifteen numbered phases where it
  once had thirteen — a real, if well-precedented, growth in what a new
  reader must take in to understand the Host's complete sequence.
- A reader unfamiliar with decimal phase numbering must, a second time,
  understand that `8.1` means "between 8 and 9," not "a sub-version of
  phase 8" — the identical, already-accepted cost ADR-0026 named for its
  own insertion.

## Future Considerations

Any future capability needing its own place in the Host's startup or
shutdown sequence should follow this same precedent — decimal
sub-numbering, no renumbering, explicit entry/exit criteria stated in the
same form every existing phase already uses — rather than re-deriving how
to insert a phase from first principles a third time.
