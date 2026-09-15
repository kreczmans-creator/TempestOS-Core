# One Way to Run a Command: the Binding Contract

**Release:** v0.14.0 · **Work Package(s):** `TD-77` Stages 2–5 (`6e3d6d5`,
`bb22983`, `1c38cb4`, `e72b933`) · `WP-A1` (`b3a6c7e`) · `WP-A2` (`7de6290`) ·
**Debt:** `TD-77`, `TD-105`, `TD-106`, `TD-113` · **Decision:** `ADR-0070`,
`ADR-0098`, `ADR-0111` · **Code:** `Tempest.Core.Commands` (`CommandContext`,
`CommandBinding`, `ICommandRegistry`),
`Tempest.Core.Input.InputBindingRouter`,
`Tempest.Desktop.Views.RibbonView`/`CommandPaletteOverlay`,
`Tempest.Desktop.Composition.SurfaceCommandPolicy`

**In plain terms.** A "command" is anything the product can be asked to do —
rename a part, delete a requirement, run a saved macro. Before this release,
the Ribbon, the Command Palette, the Macro Manager and a keyboard shortcut
each had their own private idea of how to check whether a command could run
and how to run it. The same button press could work in one place and quietly
fail in another, for no reason a user could see. This release found every
private copy, made them all ask the one place that actually knows, and built a
check into the build itself so a fifth copy cannot appear unnoticed.

## Four ways to press the same button

The audit that opened this remediation programme found four separate ways to
get from "the user asked for a command" to "the command ran" (`TD-105`): the
context-aware `InvokeAsync(id, context, prompt)`; an older, Id-only
`InvokeAsync(id)` that throws for every one of the 74 real discipline
commands, since none has a parameterless factory; the *typed*
`ICommandDispatcher.DispatchAsync<T>` a caller uses when it already holds a
real command object (`11-command-framework.md` explains why that one exists);
and `WorkspaceManager`'s own hand-written rename/delete/revise lookup tables,
one per discipline.

Four mechanisms is one job done four times, each free to disagree with the
others. This chapter closes the first two down to one, for every live surface.
The typed dispatcher was kept — it solves a different problem, a caller that
already holds data rather than only a string — and the fourth was judged,
separately, to be intentional rather than a duplicate (`ADR-0118`, see
`44-invariants-that-fail-the-build.md`). **Not consolidating something that
only looks the same is as much a decision as consolidating something that
actually is.**

## The missing half: a context and a binding

Stage 2 (`6e3d6d5`) is Core-only and changes nothing a user can see. It lets a
caller holding only a command's Id also hand over what it knows —
`CommandContext` (the current selection, nothing else), `CommandBinding` (how
a descriptor turns that context into a real command), `CommandAvailability`
(yes, or a reason a person can read), and `CommandInvocation`, recording one
of three outcomes: executed, declined, or unavailable. Three, not two, because
declining a prompt is not a failure, and reporting it as one would put an
error in front of someone who did nothing wrong.

The stage's own honest surprise: adding `Binding` as a new constructor
parameter compiled cleanly, and broke eight plugin tests anyway — they emit
machine code against the old constructor's exact shape, a plugin the platform
did not itself compile, precisely what `ADR-0111`'s trust model protects. The
fix was a settable property, not a constructor change. **A change that
"compiles everywhere" can still break a caller the compiler cannot see.**

## Naming what seventy-four commands actually need

Stage 3 (`bb22983`) is where the contract meets the real product: every
production command gets a working binding or a named reason it cannot run yet
— 56 bound, 18 declared unavailable (15 waiting on an object picker that does
not exist, `FCR-0073`; 3 needing input a text prompt cannot collect). Nothing
changes for a user yet.

Doing all 74 at once, rather than a convenient sample, surfaced a real defect:
`requirements.delete-group` and `requirements.revise` had been **permanently
unreachable** through the Ribbon's old dispatch, because its logic matched
"delete" and "edit" as literal Id suffixes, and neither Id ended that way —
found only because every command's actual requirement was named.

## Proving it against real data, not a sample

Stage 4 (`1c38cb4`) added no new capability — Stage 2 already had it all. It
added proof: all 74 real bindings reach the right handler through the
registry's own path; the availability check and the invocation check agree
across 296 comparisons; and two rules are pinned by tests rather than quietly
"fixed" — a multi-selection is judged by its first selected object only, and a
selection-blind command is still blocked while two unrelated objects are
selected. **A contract proven against fixtures, and a population declared
individually correct, do not together prove the two agree at scale.**

## Deleting the workaround instead of living beside it

Stage 5 (`e72b933`) is what a user could finally feel. The Palette had gated
on a factory no real command ever set, so pressing Enter on any of the 74
always reported "unavailable." The Ribbon's own 390-line
`RibbonObjectActionHandlers.cs` — a hand-rolled guess at what each command
needed — is deleted outright, not bypassed:

```csharp
var availability = _commandRegistry.Evaluate(descriptor.Id, context);
if (!availability.IsAvailable)
{
    ActionCompleted?.Invoke(availability.Reason!, ActionOutcome.Failed);
    return;
}
```

Two decisions once recovered by guessing at a command's Id are now named
explicitly in `SurfaceCommandPolicy`: Rename/Edit open the Object Editor
(`ADR-0096`/`ADR-0097`); Delete goes through
`IWorkspaceManager.DeleteObjectAsync`, where a successful delete clears the
selection (`TD-58`). Both previously unreachable commands now work. The stage
also disclosed a side effect rather than absorbing it: the deleted closures
had reported through a status-bar/toast helper that never raised the event
Command History listens for, so none of 31 wired commands was ever recorded
there. Removing the duplicate fixed it.

## The fourth broken surface — the one the audit missed

`TD-77` closed the framework gap but retired none of the paths it replaced, so
`WP-A1` (`b3a6c7e`) went looking for who still used them. Two calls were easy:
the Cockpit's `InvokeCommandAsync` was **live** and threw for every real
command; the REST endpoint was **sanctioned**, since an HTTP request genuinely
has no selection to build a context from. A fourth site turned up that the
audit had not named — the Macro Manager's "Run" button.

It did not throw. A macro's own descriptor does carry the parameterless
factory the old overload needs, so the call succeeded — then ran every step
**against an empty context**, so each object-scoped step reported "needs a
selected object," however the workspace was selected. Nothing crashed; it
quietly did the wrong thing, invisibly, for as long as `TD-77` had shipped —
found by the very guard test `WP-A1` was adding, in the same commit that
introduced it. The fix mirrors the Palette's own macro path: capture the
selection once, at the start, and replay it for every step (`ADR-0098`).

## A build-time guard against a fifth mechanism

Two tests now stand between a future shortcut and the next release.
`IdOnlyInvocationGuardTests` scans every production source file for a call to
the old Id-only overload and fails the build if one appears outside a small,
named allow-list — keyed by *file and reason*, not count, since a count says
something changed, never what or why. It is a source-level check on purpose:
both overloads are legitimate API, and no runtime assertion can tell a
deliberate legacy caller from an accidental one — only a recorded human
decision can. `SurfaceCommandPolicyCompletenessTests` (`TD-113`) recognises a
"delete" command **structurally**, from the wording of the confirmation the
platform already shows, never by parsing an Id — the exact habit that made two
commands unreachable in Stage 3. (By `v0.17.0` the allow-list is empty at HEAD
— `ApiRequestHandler` moved to `src/Frozen/Tempest.Core.Api` under `ADR-0146`,
see `56-frozen-layers.md` — the discipline is unchanged.)

## The keyboard reaches the path; REST is decided, not deferred

`WP-A2` (`7de6290`) closed the last two open questions. `InputBindingRouter`
had never been live — no gesture was ever bound to a real command — but it was
one line from being *live and broken*, exactly like the Macro Manager: its
throw was simply caught into a log line. It now takes the canonical path too,
supplied a context and a prompt from `MainWindow`, because **a person is
present when a key is pressed** — unlike a macro step or an HTTP request,
which deliberately pass neither.

REST was the harder call. The instinct is "one gap away from working"; the
audit found three obstacles: no contract for turning a request into a context,
no source for the 42 commands needing collected values, and no real
authentication — `ApiRequestHandler` trusts an identity header verbatim and,
by `ADR-0052`, never establishes who is calling, so a command invoked over
HTTP would be authored by whoever the desktop session happened to be. `AT-10`
is now a **decided position**: REST stays off until all three exist, named as
its own future feature.

## What this deliberately did not do

No default keyboard bindings ship, and no remapping screen exists to create
one (`AT-23`) — the mechanism works; nothing is bound to it, by choice, and a
test enforces that emptiness. REST was not switched on. `TD-77`'s own row
stays open even after all five commits: this programme delivered the mechanism
a contextual Palette needs, not every capability the brief asked for — a
recent-commands list and richer per-object suggestions are still unbuilt and
tracked.

## What to take away

- **A canonical path that leaves the old one standing is not canonical.** It
  is one option among several, and someone will find the other one — the
  Ribbon, the Macro Manager and the keyboard router each already had.
- **A guard rail earns its keep the moment it exists**, not the moment it
  prevents a hypothetical mistake — the new guard test found a real, live
  defect in the same commit that introduced it.
- **"Not yet, and here is exactly why" is a finished answer.** `AT-10` had
  been carried for years as an open gap; naming the three real obstacles,
  one a security question, turned it into a position nobody needs to
  re-investigate.

## Related documents

`11-command-framework.md` (`ICommandDispatcher`/`ICommandRegistry`, the two
contracts this binding sits on top of);
`30-command-execution-and-productivity-experience.md` (Undo/Redo, the
Macro foundation, `IInputBindingProvider`, unchanged here);
`44-invariants-that-fail-the-build.md` (`ADR-0118`, the fourth mechanism
kept, not merged); `56-frozen-layers.md` (`ApiRequestHandler`'s later
move); `58-where-things-land-and-open.md` (`CommandContext.ProjectId`,
`WP 17.9.2`, after this chapter).

## Postscript (release candidates, September 2026)

On the unreleased `v0.20.0` candidate, `TD-77` — this chapter's own open
row — closes: `WP 20.2A` (`c91d12a`) makes an empty-query Palette open
list only what `Evaluate` reports available, most-recently-invoked
first (a typed query is unchanged). `AT-23`'s deliberate emptiness
gains its first two disclosed exceptions: the same package's
Kind-resolved Move/Copy bindings (`Ctrl+Shift+M`/`C`) are a raw
`KeyDown` check, deliberately not a `KeyboardCommandBindingProvider.Bind`
call, so they need no `DisclosedBindings` entry; `WP 19.10O`'s earlier
rail-collapse shortcut (`Ctrl+B`) is the one gesture that does.
`WP 19.10R` (`033ac18`) adds a sixth binding-time question,
`CommandBinding.Mutates` and `ArchivedProjectCommandGuard`, inside
`CommandRegistry.Evaluate` itself. See `75-the-object-picker-move-copy-and-macros.md`.
Neither has shipped.


