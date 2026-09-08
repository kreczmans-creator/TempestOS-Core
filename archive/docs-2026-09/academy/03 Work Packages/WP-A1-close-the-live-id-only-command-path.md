# WP-A1 — Close the Live Id-Only Command Path

## 1. Introduction

`WP-A1` (`b3a6c7e`) migrated the last *live* callers of the obsolete
Id-only `ICommandRegistry.InvokeAsync(string, CancellationToken)` overload
onto the canonical surface path, and installed two guard rails that stop a
future surface adopting a shortcut silently. It closed `TD-106` and
`TD-113`, and it found a fourth broken surface the preceding audit had
missed.

## 2. Purpose

To make the canonical path — `Evaluate(id, context)` then
`InvokeAsync(id, context, prompt, ct)` — the only path any surface takes,
and to make a departure from it fail the build rather than fail a user.

## 3. Background

`TD-77` established the binding contract and made it canonical, but retired
none of what it replaced. The obsolete overload throws `CommandException`
for every descriptor without a `CreateDefault` — which is all 74 production
discipline commands — so a surface still on it was not degraded but
entirely non-functional.

## 4. The Problem

Three call sites were classified, and the classifications differ:

- **`EngineeringCockpit.cs:613` — LIVE.** Reached from
  `WorkspaceShell.HandleRunCommandAsync` via `cockpit.InvokeCommandAsync(index)`.
  Threw for every real command.
- **`ApiRequestHandler.cs:151` — SANCTIONED.** This corrected audit finding
  `F-17`, which claimed the REST transport is "never composed".
  `HostedServiceDiscoveryService` defaults to scanning the current
  `AppDomain`, so `RestApiHostedService` **is** discovered and started. It
  is sanctioned for the real reason instead: `AT-10` means no
  request-parameter binding exists, and an HTTP call has no selection, so
  there is no context to build.
- **`InputBindingRouter.cs:89` — DORMANT, trigger-gated.** Zero production
  `Bind(gesture, commandId)` calls (`AT-23`), so `CommandRequested` never
  fires. Deferred to `WP-A2`.

**A fourth live site existed that the audit missed, and the new guard test
found it.** `MainWindow`'s Macro Manager "Run" path did *not* throw — a
macro descriptor does carry `CreateDefault` — so it failed quietly instead:
every step of the macro ran against no context at all, and each
object-scoped step reported "needs a selected object" however the workspace
was selected.

## 5. The Design

`EngineeringCockpit.AvailableCommands` became `AvailableCommands(context)`
and now filters on `Evaluate` rather than on `CanExecute` alone, so it can
no longer advertise a command `Evaluate` reports unavailable.
`InvokeCommandAsync` takes the context and an optional prompt and returns
`CommandInvocation`. `WorkspaceShell` builds the context through the
existing `WorkspaceCommandContext` adapter — **option (c) as ruled**;
`ISelectionService` was not added to the Cockpit and its dependency surface
is unchanged.

The Macro Manager path was migrated to match the Palette's own macro path,
which captures the context at macro start and replays it (`ADR-0098`),
passing no prompt so an unattended run fails honestly rather than
prompting nobody.

Two guard rails:

**`IdOnlyInvocationGuardTests`** — source-level, because both overloads are
legitimate API and no runtime assertion can distinguish a deliberate legacy
caller from an accidental one. The allow-list is keyed by file with each
entry carrying its reason, not by count.

**`SurfaceCommandPolicyCompletenessTests`** (`F-10`) — set equality between
`DeleteCommandIds` and the delete commands the registry actually carries,
so all four failure directions fail the build. A delete is recognised
*structurally*, from `WorkspaceCommandBindings.DeleteConfirmation`'s own
wording read out of the helper — never by parsing an Id, which is the
defect `TD-77` Stage 5 removed. A floor on the recognised set stops a
detector that matches nothing from passing.

## 6. Alternatives Considered

**Delete the obsolete overload.** Rejected: it remains legitimate API for
legacy and degenerate callers, and `ApiRequestHandler` is a sanctioned
consumer.

**A runtime assertion instead of a source test.** Rejected because it
cannot work — both overloads are valid, and the Id-only path's failure mode
for a sanctioned caller is indistinguishable at runtime from its failure
mode for an accidental one.

**Give the Cockpit an `ISelectionService`.** Rejected by ruling — option
(c) keeps context construction in `WorkspaceShell` and does not widen the
Cockpit's dependency surface.

**An allow-list by count.** Rejected: a count tells you something changed,
not what or why. Keying by file with a reason makes each exception a
decision somebody made.

## 7. Why This Solution Was Chosen

Because the failure this closes had already happened once, silently, for
three release cycles, and the ordinary defences did not catch it. The
contract tests passed; nothing asked whether the surfaces agreed with each
other. A source-level guard is unusual, and it is warranted precisely where
the distinction being enforced is one only a human decision can draw.

The guard's value was demonstrated immediately: it found the fourth site
during its own introduction.

## 8. Architectural Principles

`ADR-0098` (a macro replays its captured context) governed the Macro
Manager migration. `ADR-0070` (an unavailable command is disabled with its
reason, not hidden) is what `AvailableCommands(context)` now honours by
filtering on `Evaluate`.

The wider principle: an exception to an architectural rule must be named,
reasoned and located. An allow-list entry carrying a file and a reason is a
decision; a count is a tolerance.

## 9. Benefits

Every live surface now takes one path. The Macro Manager's macros actually
receive context. A future surface reaching for the shortcut fails the
build, with a message naming the canonical path. And `F-17`'s incorrect
premise is corrected in the register rather than propagated.

## 10. Trade-offs

Source-level tests are coupled to source text and can be defeated by
formatting a call across lines. Accepted deliberately, and stated in the
test's own remarks: the alternative is no enforcement at all, since no
runtime assertion can make this distinction.

## 11. Common Mistakes

**Trusting an audit's classification without re-deriving it.** `F-17` was
wrong about the REST transport being uncomposed, and the right answer
(`AT-10`, no parameter binding) sanctions the same line for a different
reason.

**Assuming a non-throwing path is a working path.** The Macro Manager did
not throw; it silently did nothing useful. That is harder to find than a
crash.

**Recognising a command by its Id suffix.** Explicitly avoided — the
completeness test reads the delete confirmation's own wording out of the
helper instead.

## 12. Future Evolution

`WP-A2` took the dormant `InputBindingRouter` entry: it is now migrated and
its allow-list entry removed. `AT-10`'s `ApiRequestHandler` entry remains
the sole survivor, reclassified by `WP-A2` from "not yet" to a decided
position. `WP-H` later added
`NoShippedAssembly_MapsAnHttpRouteOntoACommand` to pin the premise that
entry rests on.

## 13. Key Takeaways

- A canonical path that retires nothing is not canonical; it is one option
  among several.
- The guard rail found a defect the audit missed, in the same commit that
  introduced it. That is the argument for guard rails over audits.
- An exception list keyed by file-and-reason ages better than one keyed by
  count.
- A quiet failure — the Macro Manager's — outlives a loud one.

## Related Documents

- `docs/governance/Quality/Technical Debt Register.md` — `TD-105`, `TD-106`,
  `TD-113`, `AT-10`, `AT-23`
- `ADR-0098` — macros replay a captured context
- `ADR-0070` — an unavailable command is disabled with its reason
- `WP-A2` retrospective — the dormant entry, taken
- Commit `b3a6c7e`
