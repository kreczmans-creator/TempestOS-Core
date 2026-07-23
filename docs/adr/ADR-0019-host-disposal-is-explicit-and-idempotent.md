# ADR-0019: Host Disposal Is Always an Explicit, Idempotent Call

## Status

Accepted — WP 2.7B (Runtime Host Implementation), 2026-07-22. This ADR
records a genuine architectural decision this work package's implementation
was required to make, because the frozen WP 2.7A architecture left it
ambiguous between two of its own documents — see Context.

## Context

WP 2.7A's architecture is frozen and this work package's brief is explicit:
implement it exactly, and stop rather than invent a resolution if
implementation reveals a contradiction. Implementing `TempestHost.DisposeAsync`
surfaced exactly such a contradiction, between two frozen documents that were
never read against each other closely enough during WP 2.7A to notice they
disagreed:

**`Runtime State Machine.md`'s transition table** describes both
`Stopped → Disposed` and `Faulted → Disposed` with the identical trigger
text: *"Disposal is invoked"* — worded as an explicit, separate action,
distinct from whatever caused `Stopped` or `Faulted` to be reached in the
first place.

**`Shutdown Sequence.md`'s "Post-Fault Teardown" diagram**, however, shows
`DisposeAllAsync`, Service Disposal, and the `-> Disposed` transition all
happening within the *same* continuous sequence as catching the Host-fatal
exception — with no separate, later "Dispose() invoked" step drawn as a
distinct interaction. Read literally, this diagram implies fault handling
disposes the host automatically, with no further call required.

These cannot both be literally true. If a fault automatically disposes the
host, "Disposal is invoked" is not really a separate trigger for the
`Faulted → Disposed` edge — it already happened as a side effect of the
fault. If disposal is genuinely a separate, explicit call as the state
table's wording says, the Post-Fault Teardown diagram's ending at
`-> Disposed` is illustrative shorthand for "disposal, whenever it is
eventually invoked, does this," not a literal claim that it happens without
a call.

A second, related question arose alongside this one: ADR-0004's WP 2.7
update states the Host's own disposal "reuses" ADR-0004's exact reasoning,
which for modules includes "only calling it a second time, once a module is
already `Disposed`, throws." Applied literally to the Host, a second
`DisposeAsync()` call would need to throw — directly contradicting the
universal .NET convention that `IAsyncDisposable.DisposeAsync()` must be safe
to call more than once.

## Decision

**Disposal is always an explicit, separate call — `RunAsync` never disposes
the host automatically**, whether it ends at `HostState.Stopped` (a graceful
stop, or a cancelled/early-shutdown startup) or `HostState.Faulted` (a
platform-service failure). A caller must call `DisposeAsync()` — typically
via `await using` — to reach `HostState.Disposed` in every case, uniformly.
This resolves the contradiction in `Runtime State Machine.md`'s favour: its
transition table is treated as the more precise, authoritative statement;
`Shutdown Sequence.md`'s Post-Fault Teardown diagram is read as illustrative
of what disposal *does*, once invoked, not as a claim that it is automatic.

This also gives `TempestHost` one single, symmetrical caller contract for
every exit path — `await using var host = builder.Build(); await
host.RunAsync(...);` — rather than an asymmetric one where a fault silently
disposes but a graceful stop doesn't (or vice versa).

**`DisposeAsync()` is idempotent**: calling it again once the host is already
`HostState.Disposed` is a safe no-op, not an exception. This deviates from a
literal reading of ADR-0004's reuse (which would throw, mirroring
`DisposeModuleAsync`'s behaviour for an already-disposed module) in favour of
the standard, universal `IAsyncDisposable` convention. Disposal remains
permissive from every *other* state (`Created` through `Faulted`), exactly as
ADR-0004 establishes — only the already-`Disposed` case's behaviour is
decided differently here than a literal transplant of ADR-0004's module-level
wording would produce.

## Consequences

**Positive:**

- One disposal contract, not two: every path to `Disposed` — graceful stop,
  cancelled startup, or platform fault — is reached the same way, by the
  same explicit call, performing the same shared teardown routine.
- Matches the established `await using` idiom other .NET hosts use, so a
  caller does not need to remember "don't bother disposing after a fault, it
  already happened" as a special case.
- Idempotent disposal avoids a surprising exception in ordinary,
  defensive `try`/`finally`-adjacent disposal code that many callers
  reasonably assume is safe to run more than once — a real, practical
  correctness benefit that outweighs a literal transplant of ADR-0004's
  module-level wording.

**Negative:**

- `TempestHost`'s disposal behaviour is not a perfectly literal transplant of
  ADR-0004's module-level rule — a reader who reads only ADR-0004's WP 2.7
  update note without also reading this ADR could reasonably expect a second
  `DisposeAsync()` call to throw, and be surprised that it doesn't. Mitigated
  by this ADR and by cross-referencing it from ADR-0004's own note.
- `Shutdown Sequence.md`'s Post-Fault Teardown diagram, taken completely
  literally, now reads as slightly imprecise about *when* disposal happens
  relative to fault detection — worth a documentation touch-up (see the WP
  2.7B Academy retrospective) rather than a diagram redraw, since the
  sequence of *steps* it depicts is otherwise accurate.

## Future Considerations

If a future work package finds a real need for disposal to happen
automatically on fault (for example, a hosted-service supervisor that cannot
guarantee its own `await using` block runs), that would be a deliberate,
new decision to layer on top of this one — not a reason to reinterpret this
ADR's resolution retroactively. Any future ADR revisiting Host disposal
should reference this one rather than re-deriving the Runtime State
Machine/Shutdown Sequence tension from scratch.
