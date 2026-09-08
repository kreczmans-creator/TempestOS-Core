# WP-Z2 — The Undo/Redo Toolbar Refresh Comes Back to the UI Thread

## 1. Introduction

`WP-Z2` (`121efea`) fixed `TD-117`: `UndoRedoCoordinator.RefreshButtons`
now marshals to the UI thread when `IUndoRedoStack.Changed` is raised from
a thread-pool thread. Five lines of behaviour in one Desktop file.
`Tempest.App` was not touched. It produced `ADR-0119` and closed `TD-117`
with two corrections to that row's own claims.

## 2. Purpose

To fix a defect in the layer that can see it, rather than in the layer
where it manifests.

## 3. Background

`UndoRedoStack` (`Tempest.App`) awaits the undone action with
`ConfigureAwait(false)` and then raises `Changed`. That await is correct
and consistent — `Tempest.Core` and `Tempest.App` use `ConfigureAwait(false)`
on every awaited statement but one, precisely because neither may know a UI
thread exists.

`UndoRedoCoordinator` (Desktop) subscribes once to `Changed` and refreshes
the two Quick Access Toolbar buttons — the reactive design `ADR-0103`
deliberately chose so that no caller has to remember to refresh anything.

## 4. The Problem

An action that genuinely yields resumes on the thread pool, so `Changed`
was raised there, and the coordinator set `Button.IsEnabled` from a non-UI
thread. Avalonia's `VerifyAccess` threw.

**Both real undoable actions do yield** — the favourite toggle writes a
file, and the Object Editor's rename undo dispatches through the document
store — so this was not hypothetical.

**Two corrections to what `TD-117` first claimed**, both established while
fixing it:

*Age.* Not new to `v0.13.1`. `UndoRedoStack.cs` has been unmodified since
commit `0f125a3` (`v0.10.0`), and both halves of the defect were present at
that tag, so it was reachable in **every release from `v0.10.0` to
`v0.13.1`**.

*Symptom.* Not "an error dialog". The exception escaped through a
fire-and-forget `_ = UndoAsync()` into
`TaskScheduler.UnobservedTaskException`, which fires only at GC
finalisation, so the dialog was nondeterministically delayed and often
never seen. What happened **every time** was quieter and worse: the data
change landed, then the toast, status bar, history entry, Explorer reload
and Cockpit refresh never ran and the buttons went stale. Undo appeared to
do nothing while having done something.

## 5. The Design

```csharp
private void RefreshButtons()
{
    if (Dispatcher.UIThread.CheckAccess())
        RefreshButtonsCore();
    else
        Dispatcher.UIThread.Post(RefreshButtonsCore);
}
```

This is the **third instance of an idiom the Desktop already used twice**,
for the identical cause: `PlatformNotificationToastBridge.HandleAsync`
(publishers may run on background threads) and `ThemeService.Apply`, whose
own comment already diagnosed it — *"Tempest.Core's own async methods …
`ConfigureAwait(false)` internally, so a caller … resumes on a thread-pool
thread … Marshalling explicitly here makes `Apply` correct regardless of
which thread calls it."* `ADR-0119` writes the rule down rather than
inventing one.

`Post` rather than `Invoke`: nothing awaits the refresh, and `Invoke` from
a pool thread would block it to no purpose.

**The `CheckAccess` fast path is load-bearing, not decoration.** `Record`
is synchronous and raises `Changed` on its caller's thread, always the UI
thread, and callers rely on the buttons being correct the instant it
returns.

## 6. Alternatives Considered

**`ConfigureAwait(true)` in `UndoRedoStack`.** Rejected: it would work only
by accident of this caller being on the UI thread, make a shared
`Tempest.App` type's correctness depend on an unstated property of whoever
calls it, contradict the discipline the rest of Core/App keeps, and still
break if `Record` were ever called from a background thread.

**Marshal at the subscription** (`Changed += () => Post(RefreshButtons)`).
Rejected: unconditional posting breaks `Record`'s synchronous contract.

**Drop the subscription; refresh inside the coordinator's own
`UndoAsync`/`RedoAsync`.** Rejected — abandons the reactive design, and
`Record` callers would stop refreshing.

**An `IUiDispatcher` abstraction.** Rejected: one implementation, one
consumer, and `ADR-0104` already refuses Desktop-local indirection layers
that buy nothing.

## 7. Why This Solution Was Chosen

Because the subscriber is the only party that knows it is touching UI
state. `Tempest.App` has no Avalonia reference and must not acquire one —
enforced at build time by `WP-H`'s `DependencyDirectionTests` — so it
cannot marshal, and arranging for a UI thread implicitly is worse than not
knowing about one at all.

## 8. Architectural Principles

`ADR-0119`, produced here: **the layer that owns the dispatcher owns the
marshalling.** It builds on `ADR-0023` (dependencies flow downward),
`ADR-0103`, and `ADR-0104`'s refusal of unnecessary Desktop-local
indirection.

## 9. Benefits

Undo and Redo both fixed through the single `Changed` subscription, as is
any future publisher of it. `Tempest.App` untouched: no `IUndoRedoStack`
contract change, no `UndoRedoStack` change, no Avalonia reference, and its
`ConfigureAwait(false)` discipline intact. The two pre-existing marshalling
sites now read as instances of a stated rule rather than as local caution.

## 10. Trade-offs

A refresh raised from a background thread completes asynchronously, so a
caller cannot assume the buttons are up to date the instant `UndoAsync`
returns. Nothing in the product does; a test that wants to observe it
drains the dispatcher with `Dispatcher.UIThread.RunJobs()`. Disclosed in
`ADR-0119` rather than hidden, because it is the one behavioural difference
the fix introduces.

## 11. Common Mistakes

**Fixing it where it manifests.** The exception surfaces in Avalonia; the
tempting fix is in `Tempest.App`, which is the layer that must not know.

**Assuming a fire-and-forget exception is visible.**
`UnobservedTaskException` is GC-timed. The real symptom was silence.

**Treating a guard branch as prudence.** Removing the `CheckAccess` fast
path fails `MainWindowCompositionTests` as well as the new undo test —
proven by mutation, not asserted.

## 12. Future Evolution

`ADR-0119` states what would reopen it: a second event crossing from
`Tempest.App` into the Desktop, or a UI-facing subscriber needing the
refresh to have completed synchronously from a background thread. Neither
exists — `UndoRedoStack.Changed` is the only event declared anywhere in
`Tempest.Core` or `Tempest.App`, and of 51 event raises following an await
in `Tempest.Desktop`, none follows a `ConfigureAwait(false)`.

## 13. Key Takeaways

- Fix a threading defect in the layer that is allowed to know about
  threads.
- An idiom used twice without being written down is a rule waiting to be
  broken a third time.
- A quiet failure is worse than a loud one, and this one was quiet by
  construction.
- Two mutations, two killed: reverting to the direct refresh fails both new
  tests with the original `Call from invalid thread`; removing the fast
  path fails an existing test too.

## Related Documents

- `ADR-0119` — UI-thread marshalling is the Desktop's job
- `docs/governance/Quality/Technical Debt Register.md` — `TD-117`
- `WP-E` retrospective — where `TD-117` was discovered
- `WP-H` retrospective — `DependencyDirectionTests`, which keeps App Avalonia-free
- Commit `121efea`
