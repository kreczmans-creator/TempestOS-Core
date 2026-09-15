# The Screen Follows the Store: Sequence, Change Feed and Snapshot

**Release:** `v0.18.0` (`WP 18.1A`, `WP 18.1A-R1`) ·
**Debt:** `TD-66` (closed), `TD-108` (closed), `TD-118` (closed), `TD-172`
(residual — see below), `TD-28` (judgement call), `TD-90` (claimed, not
closed) · **Decision:** `ADR-0144` (extended) · **Code:**
`Tempest.Core.Persistence.SqlitePersistenceStore`,
`Tempest.Core.Events.IWorkspaceChanges`/`WorkspaceChange`,
`Tempest.Workspace.WorkspaceSnapshot`,
`Tempest.Desktop.Views.ProjectExplorerView`/`CockpitView`/`PropertyInspectorView`,
`Tempest.Desktop.Editors.ObjectEditorView`

**In plain terms.** Picture two people sharing one address book: one
holds the real book, and everyone else works from a photocopy that only
gets updated when somebody remembers to hand them a fresh one. That is
how Tempest's screens behaved before this release — the explorer tree,
the cockpit's cards, the inspector and every open editor were
photocopies, redrawn only where a programmer had remembered to insert a
"please refresh yourself" instruction. This release makes every one of
them genuinely follow the one real book: whenever anything is saved,
every screen showing related information updates itself, with nobody
having to remember to ask. It also finishes a promise from an earlier
release: reading or saving a project no longer freezes the window while
the disk is busy — the program stays responsive throughout.

## A number that means "the store is at version N"

`53-sqlite-persistence.md` covers the database itself; this chapter
starts at one thing it did not yet do: say what version of the data is on
screen. `WP 18.1A` (`326d0d0`) gives `SqlitePersistenceStore` a
`store_sequence` table, a single row advanced by one inside the exact
transaction that just wrote something else:

```csharp
await ExecuteNonQueryAsync(
    connection, "UPDATE store_sequence SET value = value + 1 WHERE id = 1;",
    cancellationToken).ConfigureAwait(false);
return await ReadSequenceAsync(connection, cancellationToken).ConfigureAwait(false);
```

The store's own comment explains why this is a table and not SQLite's
built-in `PRAGMA user_version`: a pragma cannot be advanced inside the
transaction it counts, and a coherent read (below) needs to read it back
through the same mechanism an ordinary query uses. `CurrentSequence`
exposes the latest value on `IQueryablePersistenceStore` — a single
increasing number, "the store is at version 47," and everything else here
is built on being able to say that and mean it.

## One event per commit, naming what it touched

`EngineeringDomainContext.ExecuteWriteAsync` is the one place, since
`54-one-transaction-per-engineering-change.md`, that every engineering
change commits through. `WP 18.1A` adds a fourth step to its
project/commit/apply sequence — announce — still inside the same lock
hold:

```csharp
await PersistenceStore.ExecuteInTransactionAsync(work, cancellationToken).ConfigureAwait(false);
afterCommit?.Invoke();

// Raised only now — after memory agrees with the commit, and still
// under the write lock, so the sequence this reports can never be
// superseded by a second writer's commit before a subscriber hears
// about this one.
if (touched?.Invoke() is { Count: > 0 } entries && WorkspaceChanges is not null)
    WorkspaceChanges.Publish(new WorkspaceChange(PersistenceStore.CurrentSequence, entries));
```

A `WorkspaceChange` carries the sequence the commit landed at and a list
of touched objects — id, kind, and what happened (Created, Updated,
Moved, Deleted, AttachmentAdded, StatusChanged). One event per commit,
never one per object: a rename that also updates a relationship is one
fact, not two.

`IWorkspaceChanges` is deliberately **not** the platform's own
`IEventBus`, whose sequential, awaited dispatch would make a subscriber's
own rendering part of the commit's critical section. `WorkspaceChangeFeed`
is a bare .NET event instead, raised synchronously on whatever thread
completed the commit, so every subscriber marshals to the UI thread
itself — the exact discipline `46-the-ui-thread-and-blocking-calls.md`
named as `ADR-0119`: the layer that owns the dispatcher owns the
marshalling. `ProjectExplorerView`'s handler is typical:

```csharp
private void OnWorkspaceChanged(WorkspaceChange change) =>
    Dispatcher.UIThread.Post(async () =>
    {
        try { await LoadAsync().ConfigureAwait(true); }
        catch (Exception ex) { ActionCompleted?.Invoke($"Explorer refresh failed: {ex.Message}", ActionOutcome.Failed); }
    });
```

The explorer and the cockpit reload **unconditionally** on every commit;
the inspector and every open editor are pickier, reloading only when the
commit's touched set names the object on screen. All four are wired once,
in `MainWindow`, from `DesktopCompositionRoot.WorkspaceChanges` — left
`null` (a harmless no-op) for a test that builds a view directly.

## One coherent read, never several that might straddle a commit

A feed that says "something changed" only helps if the read that follows
cannot land half in the old world and half in the new. `WP 18.1A`
(`8f9f77c`) adds `ExecuteInReadTransactionAsync`: a **write-lock-free**
read transaction — the ordinary `BEGIN`, not the write path's `BEGIN
IMMEDIATE` — so a read never waits for a writer and never makes one wait.

In one plain sentence: SQLite's write-ahead log gives every read
transaction its own frozen view of the database from the instant it
starts, so nothing committed afterwards can be seen inside it, and
nothing inside it ever has to wait its turn. `WorkspaceSnapshot`
(`Tempest.Workspace`) is one such read, for one of four requests —
`ExplorerTree`, `Cockpit` counts, `ObjectEditorState`, `FacetSet` — every
field read inside that single call, so a view built from one snapshot
can never disagree with itself. `WorkspaceSnapshotReader` reads
`EngineeringDomain.ObjectState` directly rather than the in-memory object
cache — a second, independent path to the same fact, proving coherence
against reality rather than assuming it from a cache that might itself be
stale. A dedicated test proves the claim deterministically, through WAL's
own snapshot timing, rather than as a race that merely tends not to
happen.

## The screen never freezes while the disk is busy

`46-the-ui-thread-and-blocking-calls.md` left two rows open: `TD-108`,
corrected rather than closed — sixty blocking
`.GetAwaiter().GetResult()` calls remained, thirty-six of them waiting on
an already-completed `Task` and therefore harmless — and `TD-118`, the
Cockpit's read surface staying synchronous by shape because a C# property
cannot be `async`. `WP 18.1A` converts every remaining genuinely blocking
call bar one disclosed seam, pinned by a **structural guard test**,
`NoBlockingPersistenceCallsTests`: a plain scan of the source text of
`src/Tempest.Desktop`, because — its own remarks say — there is no
failing runtime behaviour to assert against a blocking call that happens
to complete instantly today, only the shape of the source, which a future
edit could silently reintroduce.

Two exceptions are named, not hidden: `App.cs` and
`DesktopSessionState.cs` run before Avalonia's dispatcher loop starts
pumping, so there is no UI thread yet to contend with, and
`ObjectEditorView.TryCreate` keeps exactly one blocking existence check,
`domainContext.Repository.FindAsync(objectId).GetAwaiter().GetResult()`,
because its signature is fixed by `DocumentAreaView`'s synchronous
content-builder contract — a boundary this Work Package does not own. A
dedicated test pins that exception list to exactly two files and one
named call.

## Closed, caught not being closed, and closed for real

`WorkPackages.md` listed `TD-108` and `TD-118` in `WP 18.1A`'s own
"Closes" column. They were not, in fact, closed, and the register says so
plainly: `WP 18.9.0`'s backlog reconciliation, re-reading the code rather
than trusting the claim, found `EngineeringCockpit`'s dozens of blocking
calls unchanged and `CockpitView.Refresh()` still running them on the UI
thread. `WP 18.1A-R1` (`046c78f`, `3da87ac`) then closed both for real:
`EngineeringCockpit` and all six per-discipline `*CockpitReadModel`
collaborators gained an async `LoadAsync`/`PrimeAsync`, composed by
`EngineeringCockpit.PrimeAsync` and awaited once per render by
`CockpitView.RefreshAsync` — twenty-five blocking sites closed across
seven files. The guard test was extended to scan `src/Tempest.Workspace`
too, with eleven remaining sites disclosed by name: six belong to
`IWorkspaceViewFactory.Create`'s own frozen, synchronous factory contract
from `WP8.0B`, one to the parallel `WP 18.2B`, the rest predate this row
entirely. Every count is pinned, so it can only fall from here.

Three other rows moved, each honestly, not all the same way. `TD-66` —
refresh-architecture debt beyond `Cockpit`, `Explorer`, open tabs — is
closed outright: those three views now refresh from one
`IWorkspaceChanges.Changed` event each. `TD-28`, bulk requirement
commands not auto-refreshing an open view, is a **judgement call**: it
sits in the mechanism this Work Package replaces but is not literally
named in its "Closes" column. `TD-172` is subtler — `v0.17.0`'s release
notes named a residual after `WP 17.9.3`/`17.9.4` fixed creation
placement: an object created while another discipline tab is active is
not shown where the user is looking, deferred by name to `WP 18.1A`. The
fix did not arrive as a new mechanism — the navigation call that switches
area and reveals the object is explicitly *kept*, "because the
subscription's own async reload cannot give that ordering guarantee."
What the feed adds instead is the wider guarantee: whichever tab a user
is looking at can no longer disagree with the store, since every view
reacts to every commit, not only the one that caused it. `BACKLOG.md`
still credits `TD-172`'s formal closure to `WP 17.9.3`/`17.9.4` alone.
Left genuinely untouched despite sharing `TD-108`'s own "Closes" row:
`TD-90`, a docking re-render not restoring keyboard focus — reconciliation
found no focus-capture mechanism anywhere in the docking subsystem.

## The file-per-key store's last day

`ADR-0144` retained the old file-per-key `PersistenceStore` for exactly
one release as a fallback. `WP 18.1A` deletes it on schedule (`326d0d0`):
`PersistenceStore.cs` and `Persistence:Backend=files` are gone,
`RootPathConfigurationKey` moves onto `SqlitePersistenceStore`, and about
90 test files with an incidental reference to the deleted class are
re-pointed. The Execution Plan's own decision 9 records why this moved
earlier than planned: the deletion was "already inside the store," so
there was nothing to gain by waiting for the release's final Work
Package. The re-point surfaced a small defect of its own: `d6f9bcd` found
41 "second lifetime over the same root" tests across 13 files that had
only worked by accident against the old store's absence of locking, and
now correctly hit `SqlitePersistenceStore`'s exclusive instance lock —
each fixed by disposing the first lifetime's store before opening the
second, the restart these tests actually claim to model.

## What changed for a developer, and what was deliberately left alone

Before this release, showing something new on screen meant finding every
place that could have caused it and adding a call to refresh the right
view. After it, a view takes one coherent `WorkspaceSnapshot` read at a
sequence and reacts to `IWorkspaceChanges.Changed`; there are no manual
refresh calls left to remember, and a structural test forbids a blocking
call from creeping back into `Tempest.Desktop` or `Tempest.Workspace`.
Nineteen of twenty-seven manual refresh sites were deleted outright; the
eight that remain are each commented with why — every one is triggered by
navigation or the window's first paint, never by a reaction to a commit,
so no `WorkspaceChange` would ever have fired for it anyway. The
acceptance journey (`15b0b9e`) proves the whole chain at once: it creates
a Part from the Ribbon through the real `MainWindow`, watches the
Explorer reveal it, the Cockpit's Recent Activity carry it and the
Property Inspector show it selected, then renames it and undoes the
rename through the real, production `UndoRedoCoordinator` — with no
explicit reload call anywhere, before or after either write.

Left alone, on purpose: the six `IWorkspaceViewFactory.Create`
implementations stay synchronous, because converting the interface would
ripple into every caller across the shell for a `WP8.0B` contract this
Work Package does not own; `EvidenceObjectView.cs`'s one blocking call
belongs to the parallel `WP 18.2B`, under an explicit "do not touch any
Evidence view" brief; and `TD-90`'s focus restoration was not attempted
alongside this work, even though the same "Closes" row named it, rather
than folding an unrelated docking fix into a substrate change already
large enough to review on its own.

## What to take away

- **A screen that must be told to refresh is not following its data; it
  is guessing at it**, correctly only until someone forgets to add the
  call. A feed that fires on every commit cannot be forgotten, because
  nothing downstream decides whether to raise it.
- **A coherent read means every field comes from one transaction.** WAL
  snapshot isolation makes such a read never block and never miss a
  concurrent commit — but only for calls made inside it; composing two
  separate reads can still straddle a commit between them.
- **Claiming a defect is closed and it actually being closed are
  different facts, and only re-reading the code tells them apart.** The
  register catching `WP 18.1A`'s own false claim on `TD-108`/`TD-118`,
  and `WP 18.1A-R1` then closing it for real, is the discipline working
  exactly as it should.
