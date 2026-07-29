# ADR-0038: Command Dispatch Propagates Handler Exceptions to the Caller, Diverging Deliberately from the Event Bus's Per-Subscriber Isolation

## Status

Accepted — `WP 5.1A` (Command Framework Architecture), 2026-07-28.

## Context

`docs/academy/02 Runtime Architecture/08-failure-isolation.md` documents
four prior instances of the same recurring question — *when something
fails, is it isolated or fatal, and why* — and explicitly names "a
Command Framework handler" as a future test of the same pattern. The
Event Bus (`ADR-0028`) answers this question for its own shape by
isolating every subscriber failure unconditionally: a subscriber's
exception is caught, logged, and never rethrown, because an event has
zero or more subscribers and no expected result. A command is
different, by the Engineering Glossary's own already-established
distinction: it has exactly one handler and **an expected result** — the
caller genuinely needs to know whether the command it asked for
succeeded.

## Decision

**`ICommandDispatcher.DispatchAsync`/`ICommandRegistry.InvokeAsync` let a
handler's own exception propagate directly to the caller. They do not
catch, log, and isolate it the way `EventBus.PublishAsync` isolates a
subscriber's exception.**

A handler that wants to report a *business*-level failure (an invalid
save, a validation error) returns `CommandResult.Failure(message)`
without throwing at all — this is the expected, ordinary path for a
foreseeable failure. An exception thrown out of `HandleAsync` is
reserved for a genuine defect in the handler's own execution, and is
allowed to propagate so the caller — a menu, a keyboard-shortcut
handler, an automation script, a future AI service — is never left
believing a command succeeded, or silently unaware it failed, when it
did not.

`OperationCanceledException` propagates identically to every other
cancellable operation already in this platform — checked before
dispatch, never swallowed.

This is **Case 5** of `Failure Isolation Across TempestOS`'s own
recurring pattern — the first of the five cases where the answer is
neither "isolated like a module" (Cases 1–4) nor "no new case needed,
like Navigation," but a third, deliberately different outcome:
**propagate, do not isolate.**

## Consequences

**Positive:**

- "An expected result" (the property that already distinguishes a
  command from an event, per `Risks.md` R3) is made real, not merely
  asserted — a caller can trust that the absence of a thrown exception,
  combined with `CommandResult.Succeeded`, means the command actually
  ran to completion.
- Extends `Failure Isolation Across TempestOS`'s own recurring pattern
  honestly: this decision was reached by asking the same question fresh
  (does a command handler have the properties that would justify
  isolation?), not by assuming the Event Bus's answer must transfer —
  and, this time, finding a genuinely different answer, not merely
  confirming the prior one.

**Negative:**

- A module's command handler that throws an unhandled exception can
  propagate that exception out to whatever called `DispatchAsync`/
  `InvokeAsync` — typically the Shell's own input-handling code, in a
  future Work Package. Unlike a module's own lifecycle failure or an
  event subscriber's own failure, this is **not** contained by
  `ModuleLifecycleManager`'s per-module isolation, since it does not
  occur during a module's own lifecycle method — it occurs during an
  ordinary, synchronous-from-the-module's-perspective method call at
  some arbitrary later point. The caller (the Shell, or any future
  consumer) is responsible for its own exception handling around
  `DispatchAsync`/`InvokeAsync` — this is a normal, expected
  responsibility for any caller of a fallible operation, not a gap in
  this design.

## Alternatives Considered

**Isolate command handler failures exactly like Event Bus subscriber
failures.** Rejected — see Context. Would make "an expected result" a
fiction, since a caller could never distinguish "the command succeeded"
from "the command failed but its failure was silently absorbed."

**A per-handler critical opt-in, mirroring `ICriticalBackgroundService`
(`ADR-0021`).** Not applicable — that mechanism exists to let a live,
self-supervising component escalate its *own* judgement that the
platform itself is no longer trustworthy. A command handler is invoked
synchronously by a caller that is already present and already able to
observe the outcome directly (through the propagated exception or
`CommandResult.Failure`) — there is no "self-assessing component"
question here to opt into, the same reasoning `Failure Isolation Across
TempestOS`'s own Case 3/Case 4 already applied when declining to mirror
Case 2's opt-in for a different, unrelated reason each time.

## Related Documents

`ADR-0021` (Background Service critical opt-in — considered and found
inapplicable, for a stated reason); `ADR-0028` (Event Bus's own,
deliberately different, unconditional isolation); `Failure Isolation
Across TempestOS` (Academy concept guide — updated with this decision as
Case 5); `Command Framework Architecture.md`.
