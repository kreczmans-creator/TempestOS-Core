# SQLite Persistence

**Release:** `v0.17.0` (`WP 17.1A`), file-per-key deletion `v0.18.0` (`WP 18.1A`) · **Debt:** `TD-12`, `TD-137`, `TD-88` (closed), `TD-59` (retired by removal) · **Decision:** `ADR-0144` · **Code:** `Tempest.Core.Persistence.SqlitePersistenceStore`, `IQueryablePersistenceStore`

**In plain terms.** Persistence just means saving something so it is still
there after the program stops and starts again — a document saved to disk, not
one that only exists while a window is open. Until this release, TempestOS
saved everything as one small file per record in a folder, which is simple but
has a real weakness: if the power cuts out at exactly the wrong moment, a file
being written can be left half-finished, and the record it held is gone or
garbled. A proper database avoids that by making sure a change either fully
lands or does not happen at all, even through a power cut, and by letting
several related changes be saved together as one step that cannot
half-succeed. This release replaced TempestOS's home-made folder of files with
SQLite — a small, thoroughly proven database engine that runs inside the
application itself, with nothing to install and no server to run — and the
difference a user feels is that a crash or power failure can no longer leave
engineering data corrupted or half-saved.

## A folder of files was never a database

`Tempest.Core.Persistence.PersistenceStore` had been the platform's only
durable store since `WP 6.4` (`v0.6.0`) — the archived Academy index's
"Persistence and Settings" section records it plainly: "a minimal, file-backed
key/value store", built under `ADR-0041`/`ADR-0042`. One file per
`collection`/`key` pair, written to a temporary file and renamed over the
target. Settings, Audit, the Engineering Data Model, attachment bytes
(`37-attachment-content-storage.md`) and the reference-data catalogues were
all built directly on it — one store rather than six, the right call from the
start. What was not right, twelve releases later, was the store itself.

## What a power cut does to a half-written file

The rename-based write is **crash-safe**: a process that dies mid-write leaves
the old file untouched, because the rename that swaps in the new one is a
single atomic step at the file-system level. Crash-safe is not the same as
**power-loss durable**, though. The `v0.16.0` release notes say so plainly:
the writes are "crash-safe but not power-loss durable — there is no `fsync`
before or after the rename." `fsync` is the instruction that tells the disk to
actually write data to the physical medium now, rather than leaving it in a
cache a power cut can erase; without it, a write can report success and still
vanish.

`WP 17.0A` closed half of this as a stopgap: the staged file is opened
`WriteThrough` and flushed before the rename, so the *data* reaches the
medium. The *rename itself* still gets no `fsync`, so a loss between flush and
rename could still lose a write already reported as landed. It was always a
stopgap.

## Five things a store either has or does not

`ADR-0144` frames the `v0.17.0` review's findings this way, and the framing
matters: not a wish-list, but properties a store either has or lacks.
`PersistenceStore` lacked five — no `fsync` (`TD-137`); `O(N)` directory scans
for any whole-collection read (`TD-12`); no query narrower than "one key" or
"every key"; no way to make two writes land together or not at all; and no
lock stopping a second TempestOS instance interleaving writes into the same
folder (`TD-88`). A sixth cost, paid but not a defect, is worth naming too:
making a key survive as a file name needed reserved-device-name encoding,
trailing-dot encoding, a legacy-path fallback and a case-collision refusal
(`TD-59`) — all correct, all tested, all existing purely because a key was
being asked to survive a file system.

## What WAL and `synchronous=FULL` buy

`SqlitePersistenceStore` (`4e0a928`, `2ccfb8d`) answers all five at once with
one file, `tempest.db`, in **WAL** mode — write-ahead log. Instead of editing
the database file in place, a change is first appended to a separate log and
folded back in later, so a reader never sees a half-made change and a crash
mid-write leaves the original file exactly as it was. `synchronous=FULL`
spends the `fsync` `PersistenceStore` never paid for: in WAL mode it makes
every commit reach the disk before the store reports it committed, closing
`TD-137` at the root rather than half-way.

Every connection applies four pragmas — settings that configure how SQLite
behaves — and the order is load-bearing:

```csharp
command.CommandText =
    "PRAGMA busy_timeout = 5000; " +
    "PRAGMA journal_mode = WAL; " +
    "PRAGMA synchronous = FULL; " +
    "PRAGMA foreign_keys = ON;";
```

SQLite's default busy timeout is zero, so a pragma issued before
`busy_timeout` can fail outright the instant another connection holds a lock.
`2ccfb8d`'s own tests found this race once, live — "which is once more than a
persistence layer gets" — and fixed the order rather than adding a retry.

## One instance, three shapes

`IPersistenceStore` and `IBinaryPersistenceStore` are **unchanged** — the same
four and three methods `ADR-0041`/`TD-31` drafted, so every existing consumer
compiles and runs unedited. `IQueryablePersistenceStore` is new, giving
consumers the four things they had been simulating in application code:
`ListKeysAsync` with a prefix, `ReadAllAsync` for a whole collection in one
query, `ReadManyAsync` for a named set of keys, and
`ExecuteInTransactionAsync`, which commits when the work returns and rolls
back if it throws.

Collections and keys are now stored verbatim, case-sensitively — `CON`, `..`,
`Rev1.` and `Steel`/`steel` are simply four ordinary, distinct records,
because nothing here is a file name. The whole `TD-59` encoding apparatus is
gone, not fixed.

`TempestHost` builds **one** store and registers it under all three interfaces
with `AddInstance`, the dual-registration pattern `ADR-0044` already used for
`CurrentPrincipalAccessor`. The two `Singleton<..., PersistenceStore>()` lines
this replaced had quietly built *two* store objects over one folder — harmless
for a file store, impossible for one holding an exclusive lock on one database
file. That lock — `<root>/tempest.lock`, held open for the store's whole
lifetime — is what makes a second TempestOS on the same data folder refused
outright rather than allowed to interleave writes, closing `TD-88`. Disposal
is no longer a no-op either: the Host's Service Disposal phase now disposes
every registered instance, in reverse registration order, closing `TD-03` for
instance registrations (not, disclosed honestly, for the container's own
singletons).

## The numbers

Performance was measured, not assumed. Seeding 10,000 keys in one collection,
in one transaction: 127 ms. `ListKeysAsync` over all 10,000: 5 ms.
`ReadAllAsync`: 8 ms. The Core suite grew from 5,159 tests to 5,314 in the
same Work Package, all green (`2ccfb8d`) — the store contract tests were
rewritten to run once per backend over a shared fixture, so every claim
already true of the file store is proven true of SQLite too, rather than
assumed carried across.

## One release to change your mind, then none

`Persistence:Backend` selects `sqlite` (the default) or, for exactly
`v0.17.0`, `files` — a place to stand if a site hit an unforeseen SQLite
problem. An unrecognised value is a fatal configuration error, never a silent
fallback: a deployment that asked for a backend and got a different one would
be writing its data somewhere its operator never chose. **No data was
migrated.** The Product Owner ruled the existing `persistence-data/` folders
were test data, so `v0.17.0` starts every install with a fresh database; a
one-shot importer would have meant a second reader of a format being retired,
with its own bugs, to migrate data nobody wanted.

`v0.18.0` (`WP 18.1A`, `326d0d0`) then deleted `PersistenceStore.cs` outright,
on the schedule `ADR-0144` set: `Persistence:RootPath` moved onto
`SqlitePersistenceStore`, `Persistence:Backend=files` became unknown
configuration, and roughly 90 test files with an incidental reference to the
old class's root-path constant were re-pointed. The store sequence number and
the FTS5 search index `IQueryablePersistenceStore` later grew are covered in
`61-the-screen-follows-the-store.md` and `62-findability.md` — this chapter
stops at the substrate they are built on.

## Two defects, and an intermittent that had already been explained wrongly

Two real defects surfaced after `WP 17.1A` shipped. First (`94bf990`): a
parallel test run reproduced, once, a first-use initialisation race in the
native SQLite provider — two stores opening on two threads at the same instant
could race `Microsoft.Data.Sqlite`'s lazy binding. The fix calls the
idempotent `Batteries_V2.Init()` from the store's own static constructor,
binding the provider eagerly before any connection opens.

Second, and the more interesting lesson: disposal originally called
`SqliteConnection.ClearAllPools()` to let a deleted test folder go free. That
call is **process-wide** — disposing one store's connection pool disposed the
native handle under every *other* live store in the same process (`1dcf8df`),
surfacing as `ObjectDisposedException` in roughly one of every four parallel
Core runs. This exact symptom had already been seen and misdiagnosed: the
`v0.17.0` release notes' Warnings section had recorded
`ReconciliationHostRegistrationTests` failing "in 2 of 6 full runs … on SQLite
lock contention during parallel host starts … Watch it in CI." It was not lock
contention. `d7c641e` corrects the record once the real cause was found: the
fix (`ClearPool` on this store's own connection string, not `ClearAllPools`)
held for eight consecutive clean runs, and the Warnings section was rewritten
to say so rather than leave the wrong explanation standing.

## What was deliberately not built

No importer for the old file trees, by the Product Owner's own ruling. No
secondary-index table, even though `WP 17.1A`'s own Work Package row
anticipated one — `ReferenceDataCatalog`'s hand-maintained index still works,
and a real index belongs with the schema that will use it, not ahead of it. No
consumer was migrated onto `IQueryablePersistenceStore` in this Work Package;
that is each service's own change to make. And no second, separate
transactional store was built beside this one — `ADR-0053`'s own reasoning
again: two durable stores means two roots and a class of bug where a record
exists in one and not the other.

## What to take away

**A property a store either has or lacks is not improved by working around the
gap — it is only replaced.** Five separate defects, patched individually,
would still have been five defects; one substrate swap closed all five at once
because they were never really separate problems.

**A plausible-sounding explanation for an intermittent failure is not the same
as the real one** — "lock contention" was recorded and left open in a release
note before the actual cause, a dispose call reaching into every other store's
connection pool, was found underneath it.

**A dependency position worth holding is not the same as one worth defending
absolutely.** `Tempest.Core`'s package-free stance held for every release
until this one, and was then spent, once and deliberately, on
`Microsoft.Data.Sqlite` — the one thing a hand-written layer would only have
rebuilt with the platform's own bugs in it.
