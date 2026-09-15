# Findability: Full-Text Search and Recently Changed

**Release:** `v0.18.0` (`WP 18.1B`) · **Code:**
`Tempest.Core.Persistence.SqlitePersistenceStore`,
`IQueryablePersistenceStore`, `Tempest.Desktop.Views.CommandPaletteOverlay`,
`Tempest.Desktop.Views.ProjectExplorerView`,
`Tempest.Workspace.EngineeringCockpit`, `Tempest.Workspace.CreationPlacement`

**In plain terms.** Before this release, if you created a bracket, closed
the tab, and later wanted it back, there was no way to type its name and
get it — you had to remember which discipline it lived under and scroll
its tree by eye. This Work Package added a real search: type a fragment
of a name or a part number into the same box you already use to run
commands (`Ctrl+K`), and the object itself appears, ready to open. It
also added a "Recently changed" list on the home screen that survives
closing and reopening the application, because it reads the durable
record of every change, not whatever happened to still be in memory.

## The gap the audit could name but not excuse

The design-freeze audit of 2026-09-08 tried every journey a real user
would try, and one failed outright: *"type 'bracket' and find the
bracket."* `Ctrl+K` opened the Command Palette, which searched
**commands** — "Create Mechanical Object" — never objects. `Ctrl+F`
filtered whichever single discipline tree happened to be open. Neither
answered the question a person actually has: not "what can I run" but
"where is the thing I made." The audit quoted the Product Owner's first
Windows session directly: *"create mechanical object tells me that
something has been made, but it doesn't open, I can't find it, can't
even search for it anywhere."* The full audit is
`docs/reviews/design-freeze-2026-09/Design Freeze Review.md`; the Academy
tells that session's own story in
`05 Case Studies/07-the-design-freeze-review.md`. This chapter is the
Work Package the audit itself recommended to close this one line.

## What a search index actually is

A search index is a second, small table built purely to make one
question fast: "which records contain this word." Without one, finding
"bracket" means opening every object and reading its name — fine for a
handful, hopeless for thousands. An index turns that same information
inside out ahead of time: for every word, the records that contain it.
`53-sqlite-persistence.md` explains why data now lives in SQLite; SQLite
also ships a full-text-search engine, FTS5, and this is where the
platform first uses it — one virtual table, `search_index`, one row per
engineering object, with `title`/`identifier`/`refs` (Kind-specific extra
text — for Evidence, its citations and figure names) as the columns FTS5
actually tokenises and searches.

**An index kept separately from the data it describes will eventually
disagree with it.** So the index is not written afterwards, or by a
background job — it is written inside the very same transaction as the
object state (`54-one-transaction-per-engineering-change.md`'s subject).
`IPersistenceTransaction` gained `IndexTextAsync`/`RemoveFromIndexAsync`,
and `EngineeringObjectStateStore`'s one save path — the single place
every mutator's write already funnels through — calls one of them on
every save:

```csharp
if (state.IsDeleted)
    await transaction.RemoveFromIndexAsync(state.Id, cancellationToken)...;
else
    await IndexStateAsync(transaction, state, cancellationToken)...;
```

A rolled-back mutation therefore leaves no index row; a committed one is
searchable the instant the commit lands. There is no moment where the two
can disagree, because there is only ever one write.

## The kill switch, checked before anything was built on it

The Execution Plan's own risk register (`§6`) named a real danger before
work started: FTS5 might not be compiled into the bundled SQLite. The
agent was told to prove that before relying on it, not assume it and find
out later. `SearchIndexTests.Fts5IsCompiledIntoTheBundledSqlite` does
exactly that — it reads the bundled native library's own list of
compiled-in features before any other test in the class runs:

```csharp
command.CommandText = "PRAGMA compile_options;";
Assert.Contains(options, o =>
    o.Contains("ENABLE_FTS5", StringComparison.OrdinalIgnoreCase));
```

**Checking a risk is real costs minutes; discovering it was real after
building on it costs the whole Work Package.**

## Self-healing: an empty index looks exactly like a lost one

What if the index was never built, or was somehow lost while the object
store was not? `EngineeringObjectStateStore.RebuildIndexAsync` asks one
honest, narrow question — is the index empty while the object state is
not? — and if so, walks every live object and reindexes it in one
transaction. It runs from `EngineeringWorkspaceComposer`'s own startup
sequence, the same one that already reconstructs every persisted object
at host start (`34-engineering-object-rehydration.md`), and does nothing
once the index is already populated. A self-healing rebuild on an empty
index buys the same fix whether the index was never built, was deleted by
accident, or its schema changed underneath it — no person ever has to
notice and run a repair tool.

## Two searches, two jobs, and a race worth naming

The Command Palette's new **Objects** section (the Palette itself
originates in `30-command-execution-and-productivity-experience.md`)
searches the FTS5 index — every object, in every project — as a
background task, never blocking typing. The Explorer's filter instead
matches a node's title *or* its `Identifier` in memory, over whichever
tree is already on screen — no FTS5, because it answers "where in what
I'm already looking at," not "anywhere in the platform."

The Palette's background search creates a race: type "br" then quickly
finish "bracket", and the slow first search (matching far more) can
return *after* the fast second one. Showing whichever lands last would
show stale results for what you actually typed. A generation counter
fixes it — each keystroke's search carries a number, and a result only
renders if its number is still current:

```csharp
var generation = ++_searchGeneration;
hits = await ObjectSearchSource(query, CancellationToken.None)...;
if (generation != _searchGeneration)
    return;   // a newer query has already superseded this one
```

**The latest query always wins**, however the underlying searches happen
to finish.

## Opening the object right up, and recently changed

Finding an object is half the job; the other half is that selecting it
behaves identically wherever it was found. `MainWindow.OpenObjectAsync`
(renamed from the narrower `OpenCreatedObjectAsync`, kept as an alias) is
now the one method every "found this" surface calls: switch to the
object's discipline area, reveal it in the Explorer, select it, open its
editor tab. The Palette's Objects section, the cockpit's new card, and
the Ribbon's create-and-open path all call it — one behaviour regardless
of the door, the same idea `58-where-things-land-and-open.md` states for
where a created object lands.

The cockpit already had "Recent Engineering Activity" — what *this
session* opened, forgotten on close. The new **Recently changed** card
answers a different question: what changed at all, read from the durable
audit trail every mutator already writes (`ADR-0145`), newest first,
capped at ten, each row's title read live so a later rename shows
correctly against its own earlier rows. It needs no manual refresh either
— the same async read surface `61-the-screen-follows-the-store.md`
describes.

## The acceptance journey, and a correction worth keeping

An acceptance test records a Part, a Document and a piece of Evidence in
a project, **restarts** the whole host, then finds the Part by a title
fragment through the Palette, by its identifier through the Explorer
filter, and reads all three back from the audit trail in the correct
order — the same trail the cockpit reads. It disclosed its own limit
honestly: a sample-data assembly loaded by the test process recreates
unrelated objects on every restart, which would out-rank this journey's
own objects in a *capped* card — so the exact capped/ordering contract is
proved separately, without that noise, by two Core-level tests.

A related check — does every discipline's create command place a new
object somewhere findable? — took a real, disclosed misstep in the same
Work Package. A table-driven test covers all seven disciplines; four
resolve their parent through `CreationPlacement.ParentFor`
(`58-where-things-land-and-open.md`), three (Requirements, Manufacturing,
Verification) have their own, already-correct rule with no `ParentId`
involved. The first attempt routed Manufacturing and Verification through
`ParentFor` too, on the reasonable-looking assumption that this was the
same class of gap seen elsewhere. A narrow test caught a hole that opened
(an uncaught exception where a clean refusal belonged) and patched it —
but the release gate's *full* Desktop suite then caught what the narrow
test had only partly diagnosed: both disciplines' own explorer trees pool
un-parented objects under a category root, so giving one a real
`ParentId` nests it where its tree never drills in, permanently
unreachable. The routing — and the patch that only existed to cover for
it — was reverted, and the test rewritten to assert the correct contract
instead.

**A correction of one's own earlier change, inside the same Work
Package, is not a failure of the process — it is the process working.**
The three commits in sequence show not just what shipped, but which
shape was tried and rejected, and why, so nobody re-discovers the same
dead end by hand.

## What was deliberately not built

No fuzzy or typo-tolerant matching — FTS5's prefix match ("bra" finds
"Bracket") narrows as you type, deliberately, rather than guessing. No
result ranking configuration, no saved searches, no search across
attachment file contents. A Requirement found this way still opens into
the generic editor body rather than a requirement-specific one — a
pre-existing, disclosed gap (`TD-41`) this Work Package did not close,
because it belongs to the Object Editor's own declarations, not to
findability.

## What to take away

- **An index that can be written separately from the data it describes
  will eventually disagree with it — one transaction is what makes that
  impossible, not merely unlikely.**
- **Check that a named risk is actually true before building on the
  assumption that it is not.**
- **A correction of your own change, recorded honestly in the same Work
  Package, is what makes the mistake cheap rather than repeatable.**

## Postscript (release candidates, September 2026)

`TD-77` closes on the unreleased `v0.20.0` candidate (`WP 20.2A`,
`c91d12a`), but only for the Palette's empty-query command list — the
Objects section this chapter covers still searches only on a typed,
non-empty query, unaffected. `TD-177` closes on `v0.19.1`: `WP 19.10C`
(`12f0f14`) makes the header's own global search seed the Palette's
query instead of blanking it, the exact D1 gap the design-freeze
audit's own script named. `CreationPlacement.ParentFor`, described here
as serving four disciplines, is no longer four: `WP 19.0A` routes
Timesheets' own creation through it too, alongside Mechanical,
Documents, Calculations and Evidence — the three with their own rule
(Requirements, Manufacturing, Verification) are unchanged. See
`75-the-object-picker-move-copy-and-macros.md`. Neither has shipped.
