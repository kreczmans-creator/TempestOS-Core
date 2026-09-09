# WP 5.1A — Command Framework Architecture

## 1. Introduction

WP 5.1A designs the Command Framework `ICommand` (`WP 4.0`) has been
waiting for since it was first declared: a handler contract and a
dispatcher, resolving the one open question its own doc comment already
named — "a handler contract describing how one is handled is
deliberately not defined yet — that is [this Work Package's] own design
work." This Work Package is architecture only: no production code
changes, no tests are added, and no implementation begins. `WP 5.1B`
implements exactly what this document decides.

## 2. Purpose

To design `ICommandDispatcher`, `ICommandRegistry`, and every supporting
type completely enough that `WP 5.1B` can proceed with every open
question already answered — mirroring exactly how `WP 5.0A` (Navigation)
and `WP 5.0C` (Shell) each preceded their own implementation phases —
and to do so while integrating cleanly with the Runtime Host, Event Bus,
Navigation, and Application Shell, without weakening the Platform
Security Baseline `WP 5.0S` established.

## 3. Background

By the time this Work Package began, TempestOS already had two
independently-designed, DI-public platform services with imperative
registration — the Event Bus (`WP 4.4D`) and Navigation (`WP 5.0A`/
`WP 5.0B`) — and one already-Accepted ADR (`ADR-0022`) declaring
Navigation and Commands orthogonal, years (in this project's own
compressed timeline) before either side of that orthogonality was fully
designed. `ICommand` itself had existed, untouched, since `WP 4.0`: a
plain marker interface, its own doc comment explicit that a command "is
dispatched by its own concrete type" and that the framework a later Work
Package builds "resolves exactly one handler for a given command type."
This Work Package is that later Work Package.

## 4. The Problem

The brief's own requirement — invocation from a menu, a toolbar, a
keyboard shortcut, a context menu, programmatic code, future automation,
and a future AI service, entirely UI-agnostic — immediately exposed a
tension `ICommand`'s own existing doc comment does not resolve on its
own: "dispatched by its own concrete type" serves a caller that already
has a concrete, typed instance, but says nothing about a caller that
only has a string (a keyboard-shortcut configuration entry cannot
reference a C# generic type parameter at runtime). See `docs/academy/
02 Runtime Architecture/11-command-framework.md` for the general
version of this problem and why the Command/Mediator pattern shape
solves it; this section states the TempestOS-specific version the
investigation actually found.

A second, more structural tension surfaced during the Repository
Investigation: `ICommand`'s own doc comment implies the dispatcher
"resolves exactly one handler for a given command type," a phrase that
reads naturally as "the DI container resolves `ICommandHandler<TCommand>`."
Direct inspection of `TempestServiceProvider`/`ServiceCollection` found
neither open-generic registration nor any mechanism for a module to
register a *new* service into the container after it has already been
frozen into a provider. The literal, most obvious reading of the
existing doc comment was, in that sense, insufficient against the
platform's actual, current architecture — exactly the condition this
Work Package's own brief anticipated ("if any approved ADR or
architectural decision proves insufficient... STOP, document the issue,
explain why, recommend the minimum architectural change required").

## 5. The Design

Rather than stopping to request a container redesign, the investigation
found a minimum-drift resolution already sitting in the codebase:
`IEventBus.Subscribe<TEvent>(IEventHandler<TEvent> handler)` already
proves that "exactly one [or, for the Event Bus, any number of]
registered instance per generic type" needs *zero* container capability
beyond what already exists — the handler *instance*, not its type, is
what gets registered, imperatively, at a call site the module already
controls. Applying the identical shape to Commands — `ICommandDispatcher.
RegisterHandler<TCommand>(ICommandHandler<TCommand> handler)` — preserves
`ICommand`'s own original "exactly one handler per type" invariant
without requiring the container to gain anything. This is the single
most important design decision this Work Package made, and it is a
direct, worked example of the brief's own instruction: an existing
decision (the implied DI-resolved-handler shape) proved insufficient
against real architectural constraints, and the correct response was a
minimum, reasoned substitution — reusing an existing pattern — not a
STOP-and-escalate, and not a silent redesign of the container either.

The second major decision — splitting dispatch (`ICommandDispatcher`)
from discovery (`ICommandRegistry`) into two contracts — resolves the
first tension directly: a typed caller uses the dispatcher; a
string-only caller (menu, keyboard shortcut, automation, AI) uses the
registry's `InvokeAsync(id)`, which internally resolves a
`CommandDescriptor`'s default-instance factory and dispatches through
the same underlying mechanism. See `Command Framework Architecture.md`
for the complete public surface, and `ADR-0036`–`ADR-0038` for the three
resulting architectural decisions (ownership, registration model,
failure model).

## 6. Alternatives Considered

Four genuinely-considered alternatives were rejected and recorded
permanently: declarative/attribute-based registration (`RD-0038`,
rejected because the instantiation-avoidance problem `ModuleMetadataAttribute`
solves does not exist at Command registration time), dispatching through
the Event Bus (`RD-0039`, rejected because an event's zero-or-more/
no-expected-result shape does not match a command's exactly-one/
expected-result shape), a DI-container-resolved generic handler
(`RD-0040`, rejected because the container does not support it and
extending it would be a redesign out of scope), and silent-override
registration (`RD-0041`, rejected because every existing registry in
this platform rejects, rather than silently accepts, a duplicate).

## 7. Why This Solution Was Chosen

Every alternative above was rejected for a stated, specific reason tied
to either a real constraint already present in the codebase (the
container's own capabilities) or a real semantic mismatch (Event Bus
arity/result-expectation) — not because it was unfamiliar or because a
prior Work Package's pattern was assumed to transfer automatically. The
chosen design reuses two already-proven shapes (Event Bus's imperative
instance registration; Navigation's Id-keyed catalogue) rather than
inventing a third, novel mechanism, keeping the Command Framework's own
learning curve for a future contributor close to zero if they already
understand either precedent.

## 8. Architectural Principles

Separation of Concerns (dispatch vs. discovery, two contracts); Single
Responsibility (a descriptor describes, a handler behaves, a dispatcher
routes); Fail Fast (duplicate registration rejected immediately); Reuse
Before Invention (both registration and discovery shapes are reused, not
invented); Avoid Speculative Design (no result-value generics, no
undo/redo, no permission model — all explicitly deferred, none guessed
at ahead of a real need).

## 9. Benefits

A framework that serves every named caller (menu, toolbar, keyboard
shortcut, context menu, programmatic, automation, AI) through exactly
two contracts, requiring zero new capability anywhere else in the
platform — the DI container, Runtime Host, Event Bus, and Navigation are
all unchanged by this design. `ICommand`'s own original, `WP 4.0`
invariant ("exactly one handler for a given command type") is honoured,
not weakened, by the substitution described in Section 5.

## 10. Trade-offs

Two registration calls, not one, for a module wanting both typed and
Id-based invocation of the same command. More significantly: this Work
Package's own Security Review surfaced a genuine, previously-undisclosed
gap — see Section 11 and the Architectural Debt Assessment, below — that
this design does not close, correctly, since closing it requires an
architectural ownership/priority decision out of this Work Package's own
scope.

## 11. Common Mistakes

See `docs/academy/02 Runtime Architecture/11-command-framework.md`
Section 11 for the general version of these (reaching for the Event Bus
by surface-level analogy; assuming command failures should be isolated
like event-subscriber failures; assuming a DI container as rich as
TempestOS's own richer relatives). The TempestOS-specific mistake this
Work Package itself avoided: assuming "first registration wins" (a
sound, precedent-consistent rule for rejecting *accidental* duplicates)
is a *complete* answer to command-Id ownership. It is not — a plugin
whose own module Id happens to sort before a first-party module's, in
`ModuleLifecycleManager`'s existing ascending-Id initialisation order,
can legitimately "win" a well-known command Id first, exploiting the
rule's own stated behaviour rather than breaking it. This is recorded
honestly as `CMD-1`/`TD-11`, not silently designed around, and — a
genuinely important, disclosed finding — the identical mechanism already
applies, unnoticed until now, to `NavigationService.Register` today.
`WP 5.0S`'s own comprehensive security audit did not examine
registration-*order*, only registration-*ownership-on-removal* (`NAV-1`)
— a good, honest illustration that even a comprehensive audit does not
exhaust every angle a later, more specific design exercise can still
surface.

## 12. Future Evolution

See `Command Framework Architecture.md`'s own "Required for v0.5 vs.
Deferred Beyond v0.5" and "Future Extensibility" sections for the
complete list. The single most consequential deferred item is `CMD-1`/
`TD-11` — a future Architecture Work Package must design a command
(and, retroactively, Navigation) Id ownership/priority model before
third-party plugins are a real, live actor, not merely a future one.

## 13. Key Takeaways

1. An existing decision's most obvious reading (`ICommand`'s own doc
   comment suggesting DI-resolved handlers) can prove insufficient
   against the platform's actual, current constraints without requiring
   either a STOP-and-escalate or a silent redesign — reusing an
   already-proven pattern from a structurally similar prior decision
   (the Event Bus) resolved it within existing architecture.
2. Two contracts, not one, when a single interface would otherwise have
   to compromise between two genuinely different callers' needs (a typed
   caller; a string-only caller).
3. A security review conducted as part of a design Work Package can
   surface a real, previously-undisclosed finding (`CMD-1`) even in an
   *already-implemented*, previously-audited component (Navigation) —
   because a broad audit (`WP 5.0S`) and a narrow, mechanism-specific
   design review (this Work Package) ask different questions, and both
   are worth asking.

## Architectural Debt Assessment

One new debt item disclosed by this Work Package, recorded in
`Technical Debt Register.md`:

- **TD-11** — Command (and Navigation) registration-order squatting:
  "first registration wins" rejects a later duplicate but does not
  establish that the first registrant was the intended owner of a
  well-known Id; a plugin whose module Id sorts earlier can legitimately
  claim a well-known command or navigation Id ahead of its real owner.
  Open; requires a future Architecture Work Package (ownership/priority/
  reservation model); paired with `TD-09` (no plugin isolation boundary)
  as a joint trigger.

`TD-09`'s own existing entry is updated, not superseded, to name the
Command Framework as a second affected surface alongside Navigation —
the same underlying architectural gap, not a second, separately-counted
one.

## Observations

This Work Package is a small, concrete demonstration of the standing
Definition-of-Done clause the Platform Security Baseline (`WP 5.0S`)
introduced: a security review was mandatory here not because anyone
suspected a problem existed, but because the baseline now requires every
Work Package to check. It found one — in a component (Navigation) that
had already passed a comprehensive, dedicated security audit two Work
Packages earlier. This is exactly the outcome a continuously-maintained
baseline is supposed to produce over a one-off audit: not "we checked
once and it was clean forever," but "we keep checking, from different
angles, and sometimes that finds something a prior, differently-scoped
check did not."

## Related Documents

`Command Framework Architecture.md`; `ADR-0022`, `ADR-0036`–`ADR-0038`;
`Rejected Designs.md` (`RD-0038`–`RD-0041`); `docs/academy/
02 Runtime Architecture/11-command-framework.md`; `docs/academy/
02 Runtime Architecture/08-failure-isolation.md` (Case 5);
`docs/security/Platform Security Review v0.5.0.md` (`TD-09`, `NAV-1`);
`docs/security/Security Roadmap.md` (item 10); `docs/governance/Quality/
Technical Debt Register.md` (`TD-09`, `TD-11`).
