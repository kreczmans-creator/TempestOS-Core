# The UI Thread and Blocking Calls

**Release:** v0.14.0 · **Work Package(s):** WP-E, WP-Z2 ·
**Debt:** `TD-108`, `TD-117` (resolved), `TD-118`, `TD-121` (resolved) ·
**Decision:** `ADR-0119` · **Code:** `Tempest.Workspace.UndoRedoStack`,
`Tempest.Desktop.Composition.UndoRedoCoordinator`,
`Tempest.Desktop.Views.CockpitView`,
`Tempest.Desktop.Docking.WorkspaceLayoutController`

**In plain terms.** A running application does several things at
once — reading a file, waiting on the network, drawing the window —
and each runs on its own "thread", like several people sharing one
desk. Only one of them may touch the actual screen. When the wrong one
reaches for it, or one just sits and waits instead of getting on with
something else, a user feels one of two things: the screen freezes for
no visible reason, or an action looks like it did nothing when it
quietly did something. This chapter is about two real bugs of exactly
that shape, and the rule TempestOS now follows to stop a third.

## A thread is a worker, and only one of them may touch the screen

A **thread** is a single sequence of instructions a computer runs. A
desktop application has many running at once — one reading a settings
file, one talking to a server — and exactly one allowed to draw the
window and respond to clicks: the **UI thread**. Avalonia, the
framework the desktop shell is built on, enforces this with a runtime
check, `Dispatcher.VerifyAccess`: touch a button from any other
thread, and it throws rather than silently doing the wrong thing,
because the screen is shared, ordered state and letting several
threads touch it at once is nearly impossible to reproduce reliably.

A **blocking call** is different: code that sits and waits,
synchronously, for something designed to run asynchronously — a file
write, a network request — instead of getting on with something else
meanwhile. Its C# shape here is `.GetAwaiter().GetResult()`. It is
almost as damaging as touching the wrong thread: while it waits, the
UI thread cannot draw or respond to clicks, and the application
appears to freeze.

The dangerous case mixes the two. A background operation finishes on
whichever thread the runtime happened to resume it on — often not the
UI thread — and the code that runs next assumes it is still there and
touches a button directly. The data change has already happened; only
the *reaction* to it — the confirmation, the refreshed screen — throws
and never runs. The user sees an action that appears to do nothing,
when it actually did something. That is the exact defect `WP-Z2`
found and fixed.

## WP-E: the Cockpit stops re-reading itself, and Toggle Favourite stops blocking

`WP-E` (`e4bc3ee`) began as an inherited technical-debt row, `TD-108`,
recording blocking `.GetAwaiter().GetResult()` calls across the
Desktop. Rather than remove them by sweeping the file, it triaged each
one by what it actually does — and the triage is the valuable part.

Of the calls examined, **thirty-six wait on a `Task` that is already
complete** — the in-memory repositories return `Task.FromResult`
before there is anything to wait for — so they do not block on
anything. `Tempest.Core` and `Tempest.Workspace` (named `Tempest.App`
at the time) use `ConfigureAwait(false)` on every awaited statement
but one and neither references Avalonia, so no blocking call there
can deadlock. **The problem was latency, not correctness.**

Two calls were genuine defects. **Toggle Favourite** — Ctrl+D, or the
Explorer's context menu — blocked the UI thread on a real file write:
`_favouriteObjects.SaveAsync().GetAwaiter().GetResult()`, freezing the
window for a disk write on an interactive gesture. It is awaited now,
with `ConfigureAwait(true)` — deliberate, because everything after the
save (status bar, toast, history, the Undo/Redo record) touches
Avalonia state and must resume on the UI thread.

**The Cockpit was the worse problem, and the blocking call was not
its cause.** Every discipline card read its data through plain
properties over an uncached persistence read, so one
`CockpitView.Refresh()` re-read the same stored data eight or more
times, and the requirement-validation pass inside it — proportional
to the number of stored requirements — ran once per requirement
inside each of those reads. `CockpitReadScope` bounds one render
pass: the first read inside it wins and every other read in the same
pass reuses it, while a read taken outside a scope still goes live,
exactly as before. One Requirements refresh fell from **1,140
persistence reads to 104**, and the validation pass ran once instead
of about eight times.

Async-converting the Cockpit's read surface was rejected: a C#
property cannot be `async`, so it would have meant reshaping a
contract shared across two assemblies without removing a single
repeated read — the repetition, not the synchrony, was the actual
defect. That deferred conversion is `TD-118`. `TD-108` itself was
corrected rather than closed: **sixty executable blocking calls
remain, thirty-six of them waiting on an already-completed `Task`.**

## WP-Z2: the subscriber marshals, because the subscriber is the one who knows

`WP-E` found, without being asked to fix it, the defect `WP-Z2`
resolved. Testing the newly-awaited Toggle Favourite meant, for the
first time, driving `UndoRedoStack.UndoAsync()` with a coordinator
actually subscribed to it — and that alone exposed that
`UndoRedoStack.Changed` fires on whatever thread the undone action
happens to finish on, present, unmodified, since `v0.10.0`.

`UndoRedoStack.UndoAsync` awaits the undone action with
`ConfigureAwait(false)` and then raises `Changed`. That is correct: a
`Tempest.Workspace` type must not assume a UI thread exists. But both
real undoable actions genuinely yield — the favourite toggle writes a
file, an Object Editor rename dispatches through the document store —
so `Changed` fired from the thread pool, and `UndoRedoCoordinator`,
which owns the toolbar's Undo/Redo buttons, set `Button.IsEnabled`
from off the UI thread. The exception escaped through a
fire-and-forget call into `TaskScheduler.UnobservedTaskException`,
which only fires at garbage collection — so the symptom was silence,
not a dialog: the data changed, and the toast, status bar, history
entry and buttons never updated. **Undo appeared to do nothing while
having done something.**

`WP-Z2` (`121efea`) fixed five lines, in the layer that can see the
problem:

```csharp
private void RefreshButtons()
{
    if (Dispatcher.UIThread.CheckAccess())
        RefreshButtonsCore();
    else
        Dispatcher.UIThread.Post(RefreshButtonsCore);
}
```

`CheckAccess` asks "am I already on the UI thread?" — and the fast
path matters: `Record` (after every ordinary action, not just an
undo) raises `Changed` synchronously on the UI thread already, and
callers rely on the buttons being right the instant it returns.
Removing that branch fails an existing composition test as well as
the new one. `Post` schedules the update without waiting for it,
because nothing here needs to wait.

This produced `ADR-0119`: **the layer that owns the dispatcher owns
the marshalling; `Tempest.App` (now `Tempest.Workspace`) stays
dispatcher-free.** `Tempest.Workspace` has no Avalonia reference and
must not acquire one; only the Desktop may ask "which thread am I on"
and act on the answer. Changing `UndoRedoStack` to
`ConfigureAwait(true)` was rejected: it would work only by accident of
this caller being on the UI thread, and would still break the moment
anything else called `Record` from a background thread.

## The same lesson twice: the v0.15.0 Windows startup crash

The identical shape reappeared in `v0.15.0`, with a louder symptom.
`WorkspaceLayoutController.RestoreAsync` awaited a settings read with
`ConfigureAwait(false)` and then called `Load(tree)` directly:

```csharp
var saved = await _store.LoadAsync(cancellationToken).ConfigureAwait(false);
var tree = saved is null ? fallback : DropUnknownPanels(saved, fallback);

if (Dispatcher.UIThread.CheckAccess())
    Load(tree);
else
    Dispatcher.UIThread.Invoke(() => Load(tree));
```

That is the fix; before it, `Load` ran unconditionally, and `Load`
drives the visual tree directly. Once the settings read genuinely
completed asynchronously — reliably the case on Windows, rarely on
the Linux test environment where the defect went unnoticed — the
continuation resumed on a thread pool thread, `Load` touched Avalonia
objects from it, and the throw was unhandled inside an `async void`
window-opened handler: it killed the process moments after the window
appeared. Fixed as `TD-121` (commit `91cdbff`), the same idiom
`ADR-0119` had just named: keep `ConfigureAwait(false)` on the call
with no UI affinity, and marshal only the step that touches the
screen.

## What this deliberately did not do

`Task.Run` — moving a blocking call onto a background thread — was
rejected outright: hiding synchronous blocking behind a thread is not
removing it. A blanket sweep of every remaining blocking call was
rejected too: most wait on nothing, and forcing them async would have
reshaped public contracts for no gain. An `IUiDispatcher` abstraction
was rejected too: one implementation, one consumer, buying nothing
over calling Avalonia directly.

> **Postscript.** `TD-108` and `TD-118` did not stay open
> indefinitely: `WP 18.1A` and its follow-up `WP 18.1A-R1` (`v0.18.0`)
> removed every blocking UI call bar one disclosed seam and closed
> both rows behind a structural test that now forbids a blocking call
> from being reintroduced. See `61-the-screen-follows-the-store.md`.

## What to take away

- **A blocking call and a wrong-thread call are different faults that
  often travel together** — one freezes the screen, the other corrupts
  it, and fixing only the one you can see leaves the other live.
- **The layer that owns the dispatcher owns the marshalling**: a
  shared library that does not know a UI thread exists must never be
  made to assume one, however convenient it looks from one caller.
- **A silent failure is worse than a loud one.** The crash was found
  and fixed the same day; the toolbar bug shipped for five releases
  because nothing ever surfaced it.
