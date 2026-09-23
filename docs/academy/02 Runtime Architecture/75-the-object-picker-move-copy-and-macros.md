# The Object Picker, Move and Copy, and Macros That Replay

**Release:** release candidate on `release/v0.20.0` (head `3ce8f20`,
2026-09-15) · **Work Package(s):** `WP 20.2A` (`c1a2642`, `0c54f26`,
`68006a7`, `9235fa1`) · `WP 20.2C` (`95e6bbc`, `faa7098`, `c675a9d`) ·
**Debt:** `TD-77` (closed), `TD-115` (closed), `S2-2` (closed) ·
**Decision:** `ADR-0099` (addendum) · **Code:**
`Tempest.Desktop.Views.ObjectPickerDialog`,
`Tempest.Desktop.Views.CommandPaletteOverlay`,
`Tempest.Workspace.Workspace.WorkspaceCommandBindings`,
`Tempest.Core.Commands.ICommandRegistry`, `Tempest.Core.Macros`

**In plain terms.** Some commands need two objects, not one: "move this
Part" has to say *where to* — "copy this document" has to say *into
what*. Before this release, TempestOS had no screen anywhere for
choosing that second object, so fifteen commands that were fully built
and tested sat permanently switched off, and the Command Palette (the
`Ctrl+K` box that lists what you can do) showed most of its own list
greyed out because it couldn't ask the follow-up question either. One
small dialog — pick an object from a list, with a search box — fixed
all fifteen at once, because they all needed the exact same missing
piece. This chapter is that dialog, the keyboard shortcuts it unlocked,
and a related fix that lets a recorded macro (a saved sequence of
commands you can replay) actually carry the details you filled in the
first time, not just the bare list of steps.

## A gap the project had already found and named twice

This was not a surprise defect. `v0.14.0`'s own remediation programme
found it and wrote it down. `43-one-way-to-run-a-command.md` records
that when every real command was given a proper binding, 56 were bound
outright and 18 were "declared unavailable": fifteen needed an object
picker "that does not exist" (`FCR-0073`), and three needed a kind of
input a plain text prompt cannot collect. `44-invariants-that-fail-the-build.md` went further and pinned three of those fifteen —
`LinkRequirementCommand`, `AddRequirementToCollectionCommand`,
`CompareBaselinesCommand` (`TD-115`) — by name, in a test asserting
nothing could ever construct one, on purpose, so the day a picker
existed the test would fail and hand the next reader an invitation to
retire the row. Twelve more sat the same way under a second name,
`S2-2`: every Move and Copy command across six disciplines,
keyboard-inaccessible and without a working path anywhere in the
Desktop.

Four released versions — `v0.15.0` through `v0.18.0` — passed with
these fifteen commands built, registered, and permanently refused.
`WP 20.2A` is the Work Package that finally built the missing piece.

## What an object picker actually is

An "object picker" is nothing more than a list of every live object in
the platform — every Part, Document, Requirement and so on — with a
filter box to narrow it, so a person can point at one.
`ObjectPickerDialog` (`src/Tempest.Desktop/Views/ObjectPickerDialog.cs`)
is modelled directly on two dialogs the platform already had for a
narrower job, `ProjectPicker` and `SubjectPicker`: it lists candidates
grouped by Kind, puts the currently open project's own objects first,
and narrows by name or Kind as you type.

The detail that made it cheap to wire in everywhere is what it returns —
`PickAsync` gives back a plain string: the chosen object's `Guid` as
text, or an empty string for "no destination" (top level), exactly the
shape `InputDialog.PromptAsync` already returns for a typed value, with
`null` still meaning "the person cancelled." The contract that collects
a command's values (`CommandParameterPrompt`) never had to grow a new
case or a new branch for "this parameter is an object, not text"; the
picker is simply substituted in wherever one asks for it.

One gap is disclosed directly in the class's own remarks: the picker
does not filter out the object being moved, or its own descendants,
from the candidate list. Choosing a Part's own child as its new parent
is a doomed choice — but it already fails cleanly, because
`IHasParent.MoveAsync` refuses a circular assignment with
`CircularParentAssignmentException`, exactly as the Project Explorer's
own drag-and-drop reparenting already leaves it. A second, duplicate
tree-walk here, just to catch what the Domain already catches, was
judged not worth building.

## One parameter kind, fifteen commands

`CommandParameter` (`src/Tempest.Core/Commands/CommandParameter.cs`) gained
one new optional field, `ObjectPickerKinds`: `null` for an ordinary typed
value, or a list of Kind names (empty for "any Kind") marking this
parameter as an object chosen from the picker rather than typed. Two
small helpers in `WorkspaceCommandBindings` build the two shapes every
one of the fifteen commands needed — `Destination` (optional, blank
means top level, for a Move or Copy) and `RequiredObjectReference`
(mandatory — there is no "link to nothing"), each pairing an
`ObjectPickerKinds` declaration with the validation a picker-collected
value actually needs: a blank string, or a parseable `Guid`.

A real binding, unchanged from Mechanical's own registration, shows how
little each of the twelve Move/Copy commands needed:

```csharp
Binding = new CommandBinding(
    CommandContextRequirement.SelectedObject,
    (context, values) => new MoveMechanicalObjectCommand(
        WorkspaceCommandBindings.Target(context).ObjectId,
        WorkspaceCommandBindings.Target(context).Kind,
        WorkspaceCommandBindings.ParseDestination(values["destinationId"])),
    [WorkspaceCommandBindings.Destination("destinationId", "Destination")],
    boundKinds, mutates: true),
```

`calculations.move/copy`, `documents.move/copy`, `manufacturing.move/copy`,
`mechanical.move/copy`, `verification.move/copy`, `requirements.move` and
`requirements.move-group` each replaced an `Unavailable` binding with
exactly this shape — closing `S2-2`. `TD-115`'s own three
(`requirements.link`, `requirements.add-to-collection`,
`mechanical.compare-baselines`) turned out to need nothing beyond the
same picker parameter kind with `RequiredObjectReference` in place of
`Destination`: their handlers had been registered since `WP 9.0B`/`9.1A`,
waiting only for a construction path. **One mechanism, reused without
modification fifteen times, is what let one Work Package close two
backlog rows that had nothing else in common.**

## The keyboard shortcut that didn't fit the existing mechanism

TempestOS already had a way to bind a fixed keyboard gesture to one
fixed command Id (`KeyboardCommandBindingProvider`, covered in
`30-command-execution-and-productivity-experience.md`). Move and Copy
could not use it, because `Ctrl+Shift+M`/`Ctrl+Shift+C` has to resolve
to a *different* command Id depending on what Kind of object is
selected — twelve Ids across six disciplines, not one.

The answer was a small, explicit exception, not a change to the fixed
mapping: a raw `KeyDown` handler reads the current selection's Kind,
resolves the matching Move or Copy command Id, and invokes it through
the identical `Evaluate`/`InvokeAsync(id, context, prompt)` path every
other surface already uses — so the destination is collected by the same
`ObjectPickerDialog`, whichever surface asked for it. It deliberately
does not register through `KeyboardCommandBindingProvider.Bind`, and so
carries no entry in `DormantKeyboardBindingTests`'s own disclosed
allow-list: that guard polices a gesture bound to one fixed Id, which a
Kind-resolved dispatch genuinely is not. Closing the keyboard gap
without pretending it is the same shape as the existing one is what kept
the guard test honest rather than stretching its own definition to fit.

## The Palette becomes contextual — closing `TD-77`

The Command Palette's own gap was different in kind: an empty-query open
listed *every* registered command, most of them visibly disabled,
because most commands need a selection the empty query hadn't made. That
row, `TD-77`, is the one `62-findability.md`'s own Objects section sits
beside, and it too traces back to `v0.14.0`.

`CommandPaletteOverlay.RenderAvailableGrouped` closes it: an empty query
now asks `ICommandRegistry.Evaluate` which commands are actually
available against the real selection, keeps only those, orders them by
`CommandDescriptor.Category`, and within a category sorts the command
most recently invoked this session to the top, by its own position in a
small session-only recency list. A typed query is untouched — every
command, available or not, each with its own reason, so a search still
finds the one you cannot yet run and tells you why. Only the
empty-query "what can I do right now" view changed.
`CommandPaletteOverlayTests.Open_WithEmptyQuery_ListsOnlyAvailableCommands_GroupedByCategory`
reconstructs the whole listing independently from the real registry and
asserts it exactly; `InvokingACommand_MovesItToTheFrontOfItsOwnGroup_OnTheNextEmptyQueryOpen`
proves the recency ordering. One side effect the full Desktop suite
caught: grouping means every empty-query render now starts with a
header row, and the existing Up/Down key handler had never had to skip
one before — `NextSelectableIndex` was rewritten to step over a header
in either direction, and its own test rewritten to assert the invariant
(forward progress, never landing on a header) rather than three row
indices hardcoded to a count the grouping no longer holds fixed.

## Reconciling the numbers, and retiring the test that predicted this day

Doing all fifteen at once, and the Palette change alongside them, meant
the platform's own pinned descriptor counts had to move together rather
than drift. The lead's own reconciliation: 105 invocable, 3 unavailable,
108 production commands in total — the eighteen that used to read
"unavailable" collapse to three (the kind of input a text prompt
genuinely cannot collect), with no descriptor added or removed to get
there.

`FutureCapabilityCommandTests.cs` — the test `44-invariants-that-fail-the-build.md` describes, pinning `TD-115`'s three commands by name and
asserting nothing could construct one — was deleted outright, not
edited. Its own remarks had named exactly this outcome as the
retirement trigger: the day a picker exists and a real construction
path appears, the test fails, and that failure is the invitation to
remove it, not a regression to chase.

## `WP 20.2C`: closing `ADR-0099`'s own two disclosed gaps

`ADR-0099` (`30-command-execution-and-productivity-experience.md`
explains the original decision) chose to realise a macro as an ordinary
registered command, and disclosed two costs up front, back in `v0.10.0`:
`ICommandRegistry` had no way to remove a descriptor, so a deleted
macro's own entry stayed registered forever, "harmless" only because it
failed gracefully; and a macro step had to be `CreateDefault`-eligible,
which no real discipline command has ever set, so a macro could only
ever sequence `Tempest.Samples` demo commands. Both close in this
release, in the same Work Package.

**A ghost that finally goes away.** `ICommandRegistry.Unregister(string
id)` removes a descriptor from the registry's own dictionary outright.
No second change-notification mechanism was needed, because every
consumer — the Ribbon, the Palette — already reads `Items` fresh on
every render, so a removal is invisible everywhere the instant it
happens. `MacroManager.DeleteAsync` calls it, so a deleted macro's
descriptor is now genuinely gone, not a permanent, graceful-failing
entry left cluttering the list.

**A step now remembers what you told it.** `MacroStep` pairs a command
Id with the values recorded for it at Add Step time, collected through
the identical `CommandParameterPrompt` seam a live invocation already
uses — never a second collection mechanism. `IMacroManager.CreateAsync`
gained an overload taking `MacroStep`s (the old plain-Id overload still
works, wrapping each Id in a step recording nothing, so nothing existing
broke); it refuses a step whose binding is declared `Unavailable` —
today, the object-picker set is the only one — naming the reason, before
the macro is ever created:

```csharp
if (descriptor.Binding is { IsInvocable: false } binding)
    throw new ArgumentException(
        $"'{step.CommandId}' cannot be a macro step: {binding.UnavailableReason}", nameof(steps));
```

Eligibility in `MacroManagerDialog.IsMacroEligible` widens to match: a
binding that needs parameters is now offered as a possible step, where
before only a parameterless one qualified. **A binding that needs a
confirmation is still excluded, and that line was drawn deliberately,
not left over from the old rule** — a recording can answer "what value
goes here," collected once when the step was added, but nothing can
answer "yes, really do this" on a person's behalf. `RunMacroCommandHandler`
replays a step's recorded values silently and asks an optional fallback
prompt only for a value never recorded; with no fallback supplied,
which is every real composition today, a step still missing something
is refused rather than run guessing. **A macro therefore still never
runs unattended past the point a person would have had to say yes** —
the same principle `ADR-0099` stated for the original design, now
proven against real values instead of only against Sample commands.

## What was deliberately not built

No pre-filtering of a Move's own descendants from the picker's candidate
list — a doomed choice fails cleanly downstream instead. No persistence
of the Palette's recency ranking; it resets on every restart, exactly as
Command History and Undo/Redo already do. No way for a macro step to
answer a confirmation on a person's behalf, and no unattended macro run
that can still surprise someone with an action they never agreed to —
that boundary is the one place this release left `ADR-0099`'s own "never
runs unattended" exactly where it was.

## What to take away

- **One missing mechanism can block many unrelated features at once —
  and fixing the mechanism, not each feature, is what unblocks all of
  them together.** Fifteen commands across six disciplines and two
  unrelated backlog rows (`S2-2`, `TD-115`) needed nothing but the same
  small dialog.
- **A test that asserts an absence should say, in its own remarks, what
  would make it fail — and treat that failure as a retirement, not a
  regression.** `FutureCapabilityCommandTests` did exactly that in
  `v0.14.0`, and the day it failed was the day it was deleted.
- **A recorded workaround should name the exact day it can be removed,
  not just the fact that it exists.** `ADR-0099` disclosed two costs at
  the moment it accepted them; both closed the day the platform actually
  had what they were waiting for.

## Related documents

`30-command-execution-and-productivity-experience.md` (the Macro
foundation's own origin, `ADR-0098`/`ADR-0099`); `43-one-way-to-run-a-command.md` (`TD-77`'s own binding-contract remediation, the 56/18
split this chapter closes the second half of); `44-invariants-that-fail-the-build.md` (`TD-115`'s three commands, pinned by name, and the guard
that predicted its own retirement); `62-findability.md` (the Palette's
Objects section, alongside the grouping this chapter adds);
`70-quotations-change-orders-and-the-project-lifecycle.md` (the
archived-project guard `ICommandRegistry.Evaluate` also consults, which
a Move, Copy or macro step invokes through the same path); `74-the-debt-tranche.md` (this same candidate's other technical-debt closures).
