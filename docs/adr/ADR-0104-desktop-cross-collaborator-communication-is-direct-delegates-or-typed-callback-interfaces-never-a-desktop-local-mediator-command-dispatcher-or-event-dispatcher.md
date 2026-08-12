# ADR-0104: Desktop Cross-Collaborator Communication Is Direct Delegates or Typed Callback Interfaces — Never a Desktop-Local Mediator, Command Dispatcher, or Event Dispatcher

## Status

Accepted — `v0.12.0`, `WP 12.4A` (Desktop Command & Event Wiring
Architecture), 2026-08-12. Architecture only at acceptance; no
production code accompanied this decision at that time — `WP 12.4B`
(Desktop Command & Event Wiring Implementation), same date, realises it
exactly as approved: `RibbonObjectActionHandlers`'s own report-then-
refresh consolidation, and `WP 12.0B`'s own architecture review Finding
5 closed in `UndoRedoCoordinator`/`WorkspaceViewCoordinator`. No typed
callback interface introduced anywhere by `WP 12.4B` — assessed and
deferred for `WorkspaceViewCoordinator` as not yet materially necessary,
per this decision's own §"Consequences" below.

## Context

`WP 12.0B` decomposed `MainWindow`/`EngineeringCockpit` into eighteen
collaborators under `ADR-0103`. `ADR-0103` already states, as one of its
Dependency rules, that a collaborator "never depends on a sibling
collaborator directly" and that cross-collaborator data flows "through
the composition root's own wiring — an event, a callback delegate, or a
value passed once at construction." `WP 12.4A` was commissioned to
investigate the Desktop layer's own command/event wiring a second time,
specifically to prepare for future Desktop feature growth — and, unlike
`WP 12.0B`'s own review, to evaluate named alternative communication
patterns on their own merits rather than assume `ADR-0103`'s existing
"delegate over reference" rule settles every future question by
implication.

**What `ADR-0103` already decided, and what it does not.** `ADR-0103`
names "an event, a callback delegate, or a value passed once at
construction" as the sanctioned mechanisms, and separately rejects a
declarative/reflective composition *framework* for constructing
collaborators. Neither statement, read literally, evaluates whether a
non-reflective, compile-time, hand-rolled Mediator, a second
Desktop-local Command Dispatcher, or a Desktop-local Event Dispatcher —
none of which is inherently reflective — would also satisfy
`ADR-0103`'s own rules while reducing `MainWindow`'s own wiring code.
`WP 12.4A`'s own investigation found this a real, open question worth
answering once, explicitly, rather than leaving a future contributor to
re-derive it from `ADR-0103`'s more general principle under time
pressure, the identical reasoning `ADR-0032` already applied when it
answered "should Navigation reuse Module/Plugin Discovery's own
reflection mechanism" explicitly rather than leaving it implied.

**Six candidate mechanisms were evaluated directly, on their own
individual merits** (full comparison in `Desktop Command & Event Wiring
Architecture.md` §"Candidate Communication Mechanisms — Full
Evaluation"): direct delegates (the status quo since `WP 10.0B`), typed
callback interfaces, a Desktop-local Mediator, a Desktop-local Command
Dispatcher, a Desktop-local Event Dispatcher, and reuse of the existing
platform-wide `ICommandRegistry`/`ICommandDispatcher`/`IEventBus` for
Desktop-local wiring.

## Decision

**Desktop cross-collaborator communication uses direct delegates
(`Action`/`Action<T>`/plain C# events) as the default, and a small,
purpose-named typed callback interface only when a single collaborator
genuinely needs three or more logically-related, delegate-shaped
callback parameters bundled together.** **The threshold counts callback
parameters specifically — `Action`/`Action<T>`-typed constructor
arguments a collaborator uses to notify or query another — not a
constructor's total parameter count.** `WorkspaceViewCoordinator`'s own
18-parameter constructor, named in `WP 12.0B`'s own architecture review
Finding 4, is cited throughout this ADR as motivating evidence of the
general complexity problem this rule addresses; at this ADR's own
acceptance (`WP 12.4A`) only 2 of those 18 parameters were genuinely
delegate-shaped callbacks (`refreshStatusBar`, `recordHistory`) — below
this threshold, not "already overdue" for it. `WP 12.4B` brought the
count to 3 by adding a third callback, `refreshCockpit`; see that Work
Package's own retrospective for why introducing the interface in the
same change that first crosses the threshold was deferred as a deliberate
engineering judgement rather than applied speculatively. **A
Desktop-local Mediator, a second Desktop-local Command Dispatcher, and a
Desktop-local Event Dispatcher are each explicitly rejected as Desktop
composition mechanisms — none may be introduced, now or in any future
Desktop Work Package, without first superseding this ADR.** The existing
platform-wide `ICommandRegistry`/`ICommandDispatcher` and
`Tempest.Core.Events.IEventBus` remain correctly scoped exactly where
they already sit — genuinely discipline-facing commands, and the one
genuinely platform-wide notification bridge
(`PlatformNotificationToastBridge`) respectively — and must **not** be
extended to carry purely Desktop-local, single-consumer wiring.

### Why direct delegates remain the default

Zero new abstraction; compiler-checked (a missing or wrong-shaped
delegate is a compile error, not a runtime discovery failure); a stack
trace shows the real call chain directly, not dispatch machinery; every
collaborator's own dependency list is fully visible in its own
constructor signature — no hidden registration anywhere to go looking
for; trivially testable with a plain lambda, consistent with this
project's own "prefer real implementations over mocks" convention
(`Desktop Composition Architecture.md`'s own Testing Strategy already
claims this benefit; this ADR is what keeps the claim true going
forward). This is `ADR-0103`'s own already-stated preference, reaffirmed
here after being checked against five concrete alternatives rather than
merely carried forward unexamined.

### Why typed callback interfaces, narrowly

A single named interface (e.g., grouping the several related
operations one collaborator needs from another) reduces constructor
parameter sprawl and gives a call site a self-documenting name in
exchange for one more type to maintain and one more hop between a call
site and its real implementation. Below the three-callback threshold —
counting delegate-shaped callback parameters specifically, not a
constructor's total parameter count — that trade is not worth making: a
single `Action` reads at least as clearly as a single-method interface,
with strictly less ceremony. Above it, the trade is worth making.
`WorkspaceViewCoordinator`'s own 18-parameter constructor is direct,
present evidence of the general complexity this rule exists to address;
its own genuine callback count crossed this specific threshold only at
`WP 12.4B`, when a third callback (`refreshCockpit`) was added — see
that Work Package's own retrospective for the deliberate, disclosed
decision to defer introducing the interface at that same point rather
than apply it speculatively.

### Why a Desktop-local Mediator is rejected

A Mediator needs some mechanism to route a request to its handler —
either a runtime registry the composition root must still populate
handler-by-handler (moving `MainWindow`'s own wiring code sideways into
a different collaborator's constructor, in a harder-to-read, more
generic shape, without actually removing it), or reflection-based
auto-discovery of handler implementations, which is precisely the
"declarative/reflective composition" `ADR-0103` already rejects for
lacking a genuine extensibility trigger — this platform has one fixed,
small, compile-time-known Desktop composition root, not an open,
third-party-extensible set of request handlers. Either shape also
directly regresses `ADR-0103`'s own named, structural testability
benefit: a collaborator that "sends" through a Mediator needs a working,
populated Mediator instance to test in isolation, not a plain lambda.

### Why a second, Desktop-local Command Dispatcher is rejected

The real `ICommandRegistry`/`ICommandDispatcher` already exists, and
`ADR-0099` already made the deliberate, general choice to route even a
Desktop-triggered action (a Macro) through it rather than inventing a
parallel mechanism, specifically to avoid the confusion `ADR-0070`'s own
"no second registration mechanism" rule exists to prevent. A
Desktop-scoped command dispatcher for pure UI actions (open palette,
switch document tab, toggle favourite) would reintroduce exactly that
already-rejected confusion — a future contributor would need to learn
which of two dispatchers a given action goes through, and why — for a
set of actions `KeyboardShortcutActions` already serves today, correctly
scoped, with zero framework.

### Why a Desktop-local Event Dispatcher is rejected

`Tempest.Core.Events.IEventBus` already exists as the platform-wide
publish/subscribe mechanism (`ADR-0028`), already consumed by a real
Desktop bridge (`PlatformNotificationToastBridge`) for the one
genuinely platform-wide case. A second, Desktop-only event bus would
either misuse that platform-wide channel for a purely Desktop-local
concern it was never scoped for, or stand up an entirely new, parallel
pub/sub mechanism for a fixed, small, known-in-advance set of
publishers and subscribers — `ADR-0103`/`ADR-0032`'s own "no genuine
extensibility trigger" judgement, applied a further time. Publish/
subscribe also structurally weakens `ADR-0103`'s own "smallest public
surface... never a surface sized for a hypothetical future caller"
rule: a published event's own consumer list becomes invisible at the
publish site, the opposite of the delegate model's own "read the
constructor, see every consumer" property `WP 12.4A`'s own investigation
confirmed holds for every existing cross-collaborator bridge in the
current codebase.

### Why the existing platform Command/Event Framework is not extended to Desktop-local wiring

Both `ICommandRegistry`/`ICommandDispatcher` and `IEventBus` are
Platform Services — `ADR-0009`-governed, DI-resolved, genuinely
platform-wide in scope. Routing new, purely Desktop-local,
single-consumer wiring through either would misclassify Desktop-local
presentation wiring as a platform capability, the identical
misclassification `ADR-0103`'s own "Why this pattern is preferred over
service extraction" section already warns a DI-registered collaborator
against — reached here for reusing an *existing* platform service for
the identical wrong reason, not only for registering a *new* one.

## Consequences

**Positive:**

- **A fourth, fifth, and sixth alternative are now each foreclosed by
  name**, not merely by inference from `ADR-0103`'s more general rule —
  a future contributor considering a Mediator, a second Command
  Dispatcher, or a Desktop-local Event Dispatcher to tame Desktop wiring
  growth has a direct, citable answer, checked against this specific
  temptation, not a principle they must first re-derive under time
  pressure.
- **A concrete, threshold-based rule for typed callback interfaces**
  (three or more related callbacks) gives `WP 12.0B`'s own Finding 4
  (`WorkspaceViewCoordinator`'s 18-parameter constructor) a named,
  citable remedy, rather than leaving "reduce constructor width" as an
  unscoped aspiration.
- **Zero new abstraction, zero new mechanism, zero new failure mode** —
  identical to `ADR-0103`'s own Consequences, for the identical reason:
  every rule here is either already-proven precedent (`ADR-0103`'s own
  delegate rule) made more specific, or a rejection of something never
  built.

**Negative:**

- **A fourth ADR in the `ADR-0103` family** (`ADR-0009`, `ADR-0103`, now
  `ADR-0104`) a future contributor must read in sequence to understand
  the complete Desktop composition picture — mitigated by this ADR's own
  Related Documents section and by `Desktop Command & Event Wiring
  Architecture.md`'s own role as the single place the full evaluation is
  recorded once, not restated per-ADR.
- **The three-callback threshold for typed interfaces is a judgement
  call, not a provable line** — recorded as a starting heuristic, not a
  mechanically-enforced rule; a future Work Package finding it wrong in
  practice should revise this ADR's own Decision, not quietly deviate
  from it.

## Alternatives Considered

Recorded in full, with advantages/disadvantages/layering/ownership/
lifetime/testing implications for each, in `Desktop Command & Event
Wiring Architecture.md`'s own "Candidate Communication Mechanisms — Full
Evaluation" section: **direct delegates** (accepted, as the default);
**typed callback interfaces** (accepted, narrowly, at three or more
bundled callbacks); **a Desktop-local Mediator** (rejected — see "Why a
Desktop-local Mediator is rejected," above); **a Desktop-local Command
Dispatcher** (rejected — see the identically-named section, above); **a
Desktop-local Event Dispatcher** (rejected — see the identically-named
section, above); **reuse of the existing platform-wide Command/Event
Framework for Desktop-local wiring** (rejected for new Desktop-local
wiring specifically; the framework's own existing, genuinely
platform-wide uses are unaffected and correct).

## Future Considerations

**This ADR governs Desktop-local, in-process, cross-collaborator
communication only.** It does not reopen, narrow, or extend `ADR-0103`'s
own composition-root/collaborator construction rules, `ADR-0070`'s
Command Palette design, `ADR-0099`'s Macro-as-Command decision, or
`ADR-0028`'s `IEventBus` design — each remains entirely its own ADR's
territory, cited, not modified, here.

**If a genuine, demonstrated need for a Mediator/Command-Dispatcher/
Event-Dispatcher-shaped mechanism ever arises** — for instance, a future
Desktop feature genuinely requiring many-to-many, discovery-time-unknown
publisher/subscriber relationships, the concrete "genuine extensibility
trigger" `ADR-0032`/`ADR-0103` already name as the bar — that is a new
architectural question to be decided on real evidence, with its own ADR
explicitly superseding this one's own rejection, never assumed or
routed around silently.

**The three-callback threshold for typed callback interfaces** is
explicitly named as a starting heuristic in this ADR's own Negative
Consequences, above — a future Work Package is free to propose a
revision to this same ADR (not a new, competing one) if practice shows
it wrong.

## Related Documents

`ADR-0103` (the general composition-root/collaborator pattern this ADR
applies more specifically to Desktop cross-collaborator communication);
`ADR-0009` (composition root owns externally-created services — the
shared root principle); `ADR-0032` (reflection-based discovery is not
reused without a genuine extensibility trigger — the direct precedent
this ADR's Mediator/Event-Dispatcher rejections both extend); `ADR-0070`
(Command Palette — the "no second registration mechanism" precedent the
Command Dispatcher rejection extends); `ADR-0099` (Macro is a registered
Command — the direct precedent for routing even Desktop-triggered
actions through the existing Command Framework rather than a parallel
one); `ADR-0028` (`IEventBus` design — the platform-wide scope this ADR
declines to extend to Desktop-local wiring); `ADR-0023` (four-layer
platform model — the layering concern behind this ADR's rejection of
repurposing Platform Services for Desktop-local wiring); `FOUNDATION.md`
non-negotiables 2 and 9; `docs/architecture/Desktop Command & Event
Wiring Architecture.md` (this ADR's own realisation, containing the
full six-option evaluation); `docs/architecture/Desktop Composition
Architecture.md`; `docs/academy/03 Work
Packages/WP12.4A-desktop-command-and-event-wiring-architecture.md`;
`docs/academy/03 Work
Packages/WP12.4B-desktop-command-and-event-wiring-implementation.md`
(this ADR's own implementation retrospective); `docs/releases/v0.12.0/WorkPackages.md`
(`WP 12.4A`, `WP 12.4B`).
