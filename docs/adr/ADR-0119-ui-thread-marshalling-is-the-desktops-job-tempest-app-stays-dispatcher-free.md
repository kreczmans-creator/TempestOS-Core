# ADR-0119: UI-Thread Marshalling Is the Desktop's Job — `Tempest.App` Stays Dispatcher-Free

## Status

Accepted — `WP-Z2` (`TD-117`), 2026-09-01. Resolves `TD-117`. Builds on `ADR-0023` (dependencies flow downward), `ADR-0099` (Undo/Redo), `ADR-0101` (the Desktop is the presentation shell), `ADR-0103` (composition roots own collaborators) and `ADR-0104` (Desktop cross-collaborator communication is direct delegates, never a Desktop-local event dispatcher).

## Context

`UndoRedoStack` lives in `Tempest.App` and raises one event, `Changed`, after every `Record`, `UndoAsync` and `RedoAsync`. `UndoRedoCoordinator` (Desktop) subscribes once and refreshes the two Quick Access Toolbar buttons from it — the reactive design `ADR-0103` deliberately chose so that no caller has to remember to refresh anything.

`UndoAsync` awaits the undone action with `ConfigureAwait(false)`, which is correct and consistent: `Tempest.Core` and `Tempest.App` use `ConfigureAwait(false)` on every awaited statement but one, precisely because neither may know a UI thread exists. But an action that genuinely yields then resumes on the thread pool, so `Changed` was raised there, and the coordinator set `Button.IsEnabled` from a non-UI thread. Avalonia's `VerifyAccess` threw.

Both real undoable actions genuinely yield — the favourite toggle writes a file, and the Object Editor's rename undo dispatches through the document store — so this was not hypothetical. It was reachable in every release from `v0.10.0` to `v0.13.1`. The symptom was worse than a crash: the exception escaped through a fire-and-forget `_ = UndoAsync()` into `TaskScheduler.UnobservedTaskException`, which fires only at GC finalisation, so the data change landed while the toast, status bar, history entry, Explorer reload and Cockpit refresh never ran and the buttons went stale. Undo looked like it had done nothing.

The question this ADR settles is not *whether* to fix it but **which layer owns the fix**.

## Decision

**1. The layer that owns the dispatcher owns the marshalling.**

`Tempest.App` has no reference to Avalonia and must not acquire one — `ADR-0023`'s dependency direction, enforced at build time by `DependencyDirectionTests.NoAvaloniaPackage_ReachesCoreOrApp` and `TheBuiltAssemblies_CarryNoUpwardOrPresentationReference`. A `Tempest.App` type therefore cannot marshal to a UI thread, and should not try to arrange for one implicitly either. The subscriber knows it is a UI component; the publisher does not and must not. **The subscriber marshals.**

**2. The mechanism is `Dispatcher.UIThread.CheckAccess()` and then `Post`, in the subscriber.**

```csharp
private void RefreshButtons()
{
    if (Dispatcher.UIThread.CheckAccess())
        RefreshButtonsCore();
    else
        Dispatcher.UIThread.Post(RefreshButtonsCore);
}
```

This is not a new mechanism. It is the third instance of one the Desktop already used twice, for the identical problem: `PlatformNotificationToastBridge.HandleAsync` (publishers may run on background threads) and `ThemeService.Apply`, whose own comment already diagnoses this exact cause — *"Tempest.Core's own async methods … `ConfigureAwait(false)` internally, so a caller … resumes on a thread-pool thread … Marshalling explicitly here makes `Apply` correct regardless of which thread calls it, rather than requiring every caller to remember to do so itself."*

**3. The `CheckAccess` fast path is load-bearing, not defensive.**

`Record` is synchronous and raises `Changed` on its caller's thread, which is always the UI thread, and callers rely on the buttons being correct the instant it returns. Posting unconditionally would make that asynchronous. Two independent tests fail when the fast path is removed — `MainWindowCompositionTests.QuickAccessToolbar_UndoRedoButtons_…` and `UndoRedoThreadingTests`' own undo case — which is how we know the branch earns its place rather than merely reading as prudent.

**4. `Post`, not `Invoke`.** Nothing awaits the refresh, and `Invoke` from a pool thread would block it to no purpose. This matches `PlatformNotificationToastBridge`'s choice; `ThemeService` uses `Invoke` because a theme must be applied before its method returns, which is the distinguishing question.

**5. Changing `UndoRedoStack` is rejected.**

Awaiting with `ConfigureAwait(true)` there would work for this caller — but only by accident of that caller being on the UI thread. It would make a shared `Tempest.App` type's correctness depend on an unstated property of whoever calls it, contradict the `ConfigureAwait(false)` discipline the rest of `Tempest.Core`/`Tempest.App` keeps, and still break if `Record` were ever called from a background thread. It fixes the symptom in the layer that cannot see the problem.

Three further alternatives were rejected: marshalling at the subscription rather than inside the handler (unconditional posting, which breaks `Record`'s synchronous contract as §3 shows); dropping the subscription and refreshing inside the coordinator's own `UndoAsync`/`RedoAsync` (abandons the reactive design, and `Record` callers would stop refreshing); and introducing an `IUiDispatcher` abstraction (one implementation, one consumer, and `ADR-0104` already refuses Desktop-local indirection layers that buy nothing).

## Consequences

**Good.** `Tempest.App` stays exactly as it was — no contract change, no threading assumption, no Avalonia. Undo and Redo are both fixed through the single `Changed` subscription, as is any future publisher of it, because the fix is at the one place that knows it is touching UI state. The two pre-existing marshalling sites now read as instances of a stated rule rather than as two independent pieces of local caution.

**Accepted cost.** A refresh raised from a background thread completes asynchronously, so a caller cannot assume the buttons are up to date the instant `UndoAsync` returns. Nothing in the product does; a test that wants to observe it drains the dispatcher with `Dispatcher.UIThread.RunJobs()`. This is disclosed rather than hidden because it is the one behavioural difference the fix introduces.

**Scope, stated honestly.** This ADR governs the boundary; it does not sweep the codebase. A scan found `UndoRedoStack.Changed` to be the only event declared anywhere in `Tempest.Core` or `Tempest.App`, and of 51 event raises following an await in `Tempest.Desktop`, none follows a `ConfigureAwait(false)` — the Desktop's own `BackgroundTaskRunner` already raises its `Changed` after `ConfigureAwait(true)`. There was one occurrence of this defect and it is fixed.

**What would reopen this.** A second event crossing from `Tempest.App` into the Desktop, or a UI-facing subscriber that needs the refresh to have completed synchronously from a background thread. Neither exists. If several such sites ever appear, the rule stays and only its packaging becomes a question.
