# WP-A2 — The Keyboard Reaches the Canonical Path; REST Is Decided, Not Deferred

## 1. Introduction

`WP-A2` (`7de6290`) migrated the last dormant caller of the obsolete
Id-only command overload — `InputBindingRouter` — onto the canonical path,
and settled the REST question `AT-10` had been carrying as "not yet". It
closed `TD-105` and `TD-106`. Eight new router tests; three mutations, one
of which survived and was worth having.

## 2. Purpose

To take `WP-H`'s trigger. `DormantKeyboardBindingTests` had made `AT-23`'s
dormancy premise checkable; this work package established that the dormancy
was concealing a defect rather than recording a decision, and removed the
defect.

## 3. Background

`InputBindingRouter` routed through `InvokeAsync(id, ct)`, which throws for
all 74 production discipline commands. The throw was caught into a log
line, so **binding any real command would have produced a key that silently
did nothing.** The dormancy recorded in `AT-23` was a defect's shadow, not
a decision — nobody had bound a command, so nobody had found out.

## 4. The Problem

Two problems, and they needed opposite answers.

*The keyboard* was one line away from being live and broken. Its dormancy
was the only thing keeping it correct.

*REST* was assumed to be one gap away from working — `AT-10`'s missing
request-parameter binding. The audit found that assumption too generous.

## 5. The Design

**Keyboard.** The router now takes `Evaluate(id, context)` then
`InvokeAsync(id, context, prompt, ct)`, with an optional `ContextSource`
and `ParameterPrompt` on `IInputBindingRegistry`, supplied by `MainWindow`
from the same `WorkspaceCommandContext.From(workspace.Selection)` and
`DesktopCommandPrompt` the Command Palette already uses. `CommandContext`
is a `Tempest.Core` type, so the delegate crosses no layer: Core still
knows nothing about App.

**Neither is fabricated.** With no context source, a selection-scoped
command is refused with its own declared reason and never dispatched; with
no prompt, a parameterised command reports that it needs input rather than
running without asking; declining the prompt cancels. **A person is present
when a key is pressed**, which is why passing a real prompt here is honest
where a macro and an HTTP request deliberately pass none.

The router's allow-list entry in `IdOnlyInvocationGuardTests` is removed,
not re-justified, and a companion assertion pins the migration. The shipped
zero-binding state is unchanged — `DormantKeyboardBindingTests` still
enforces it — so `AT-23` now means only what it says: the keyboard ships
bound to nothing by product choice.

**REST is not activated, and the reasons are larger than `AT-10` recorded.**
No production command is reachable over HTTP at all: of 74 descriptors, 18
are declared unavailable, and every one of the 56 invocable ones either
needs a selected object (49) or declares parameters (the 7 creates). The
set invocable with an empty context and no prompt is **empty**.

Activation would need three things that do not exist: a request-to-context
contract, a parameter source for 42 prompt-bearing commands, and an
authentication mechanism — `ApiRequestHandler` trusts the identity header
verbatim and, by `ADR-0052`, never establishes the current principal, so a
REST-invoked command would be authored by whoever the desktop session
established rather than by the caller. Latent only because no shipped
assembly maps a route. `AT-10` is reclassified from "not yet" to a decided
position; its allow-list entry is retained and remains correct.

## 6. Alternatives Considered

**Activate REST alongside the keyboard.** Rejected on the audit's findings
— three missing mechanisms, one of them an authentication model.

**Fabricate a context for the keyboard** (e.g. an empty `CommandContext`).
Rejected: it converts an honest refusal into a command that runs against
the wrong thing.

**Re-justify the router's allow-list entry.** Rejected — the entry existed
because the path was broken, and the path is no longer broken.

**Ship default keyboard bindings.** Out of scope; default bindings,
persistence and a remapping UI remain feature work, and `AT-23` still
records that.

## 7. Why This Solution Was Chosen

Because "dormant" and "correct" are different states, and the difference
only became visible once `WP-H` forced the premise to be checked. Migrating
the router costs one Core contract addition (two optional properties, both
defaulted) and makes the extension point genuinely usable rather than a
trap.

The REST decision was chosen over further deferral because deferral had
been recording the wrong reason. `AT-10` said the parameter gap was what
stood in the way; the audit found three obstacles, one of which is a
security question.

## 8. Architectural Principles

`ADR-0100` (the keyboard is one input-binding provider among several) is
the decision the migration honours. `ADR-0052` (REST never establishes the
current principal) is the finding that reclassified `AT-10`. `ADR-0023`'s
dependency direction constrained the design: the context source is a
delegate returning a Core type, so no layer is crossed.

## 9. Benefits

Every surface in the product now reaches commands the same way. The
keyboard extension point can be used without producing a dead key.
`AT-23` means what it says. `AT-10` records a decision instead of an
intention, including the authorship limitation, which had not been written
down anywhere before.

## 10. Trade-offs

The Core contract grew by two optional, defaulted properties. Additive and
non-breaking, but it is still `Tempest.Core` surface changed to serve a
Desktop need — justified because the alternative was a Desktop-side
re-implementation of the routing the registry already owns.

## 11. Common Mistakes

**Reading "dormant" as "correct".** It meant "not yet exercised".

**Passing a prompt everywhere, or nowhere.** The right answer depends on
whether a person is present: a keypress has one, a macro step and an HTTP
request do not.

**Absorbing a surviving mutation.** The third mutation — removing the
`Evaluate` pre-check — changed nothing observable, because `InvokeAsync`
re-evaluates internally. Rather than accept it, the one case it genuinely
changes was identified: an **unregistered Id**, which the gate now refuses
cleanly instead of throwing `CommandNotFoundException` into the catch. A
test was added for exactly that, and the mutation is now killed.

## 12. Future Evolution

REST activation is named as a separate feature/design work package and is
not scheduled. Its prerequisites are recorded on `AT-10`. Default keyboard
bindings, binding persistence and a remapping UI remain feature work under
`AT-23`.

`WP-Z1` later corrected `DormantKeyboardBindingTests`' documentation, which
still described the router as using the obsolete overload — a piece of
prose this work package falsified and did not update.

## 13. Key Takeaways

- A surviving mutation is information. Investigate what it proves is
  untested before accepting it.
- Dormancy can hide a defect as easily as it can record a decision. The
  only way to tell is to make the premise checkable.
- Decide, or defer for the right reason. `AT-10` had been deferred for
  years on a reason that was not the binding one.
- Context and prompts should be supplied where a person exists to supply
  them, and honestly refused everywhere else.

## Related Documents

- `ADR-0100` — input binding providers
- `ADR-0052` — REST never establishes the current principal
- `docs/governance/Quality/Technical Debt Register.md` — `TD-105`, `TD-106`,
  `AT-10`, `AT-23`
- `WP-H` retrospective — the trigger this fired
- Commit `7de6290`
