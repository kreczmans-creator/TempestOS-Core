# ADR-0144: Persistence Is SQLite in WAL Mode with Full Synchronous Writes, and the File-per-Key Store Is Retired

## Status

Accepted — `WP 17.1A` (SQLite persistence), 2026-09-08. The file-per-key
backend this decision retained for exactly one release is deleted —
`WP 18.1A`, 2026-09-09: `PersistenceStore.cs`, `Persistence:Backend=files`,
and the dual-backend test fixture are gone; `SqlitePersistenceStore` is
the platform's only store, exactly as this ADR's own Decision said it
would be.

Supersedes `ADR-0041` in part (the storage backend it chose; the
`IPersistenceStore` shape it drafted stands unchanged) and `ADR-0053` in
part (its premise that the one persistence abstraction has no query and
no transaction, which the Engineering Data Model was built to work
around).

## Context

`Tempest.Core.Persistence.PersistenceStore` has been this platform's only
durable store since `WP 6.4`: one file per `collection`/`key` pair under
a configured root, percent-encoded, written to a temporary file and
renamed over the target. Settings, Audit, the Engineering Data Model, the
Engineering Object state store, attachment content, the reference-data
catalogues and the requirements index are all built directly on it —
deliberately, one store rather than six, which was and remains the right
call. What was not right was the store.

The `v0.17.0` review found five defects in it, and they are not a list of
improvements. Each is a property a store either has or does not.

1. **No fsync.** A write returned once the rename had been issued.
   `WP 17.0A` closed half of this by opening the staged temporary file
   `WriteThrough` and flushing before the rename, so the *data* reaches
   the medium; the *rename* still does not, so a power loss between them
   loses the write while having reported it as landed. `TD-137`.
2. **O(N) directory scans.** `ListKeysAsync` enumerates a directory and
   percent-decodes every entry. `AuditQuery`, `ReferenceDataCatalog` and
   `EngineeringObjectStateStore` all read a whole collection this way,
   which is one `readdir` plus one `open`/`read`/`close` per record.
   `TD-12` recorded the limitation on the day it was written and it has
   been carried since.
3. **No query.** The store answers "this key" and "every key". Anything
   narrower is done above it, in application code, by listing everything
   and filtering — which is why `ReferenceDataCatalog` maintains a
   hand-written secondary-index collection, itself another set of files
   to keep consistent with the first.
4. **No multi-key transaction.** Nothing can write two records so that
   either both land or neither does. `EngineeringObjectBase`'s mutators
   compensate with an in-memory rollback that is correct only because
   `WP 16.4B-R7` went through the store line by line establishing exactly
   when a write has committed; `AttachmentWriteIntentStore` and a
   reconciliation sweep exist solely to detect the one interleaving that
   compensation cannot cover. `TD-149`, `TD-156`.
5. **No cross-process lock.** Two TempestOS instances over one root
   interleave writes into one another's records, silently. `TD-88`.

Two further properties were paid for expensively and are worth naming,
because retiring the store retires them too. Because a key had to become
a file name, `TD-59` needed reserved-Win32-device-name encoding, terminal
dot encoding, a legacy-path fallback for records written before that
encoding, and a documented refusal to overwrite when two keys differing
only in case collide on a case-insensitive file system. All of it is
correct, all of it is tested, and all of it exists because a caller's key
was being asked to survive a file system.

`WP 17.1B` builds a transactional engineering object store. It cannot be
built on a substrate with no transaction. This decision is therefore
taken now, on the critical path, rather than discovered mid-migration.

## Decision

**The platform's durable store is SQLite: one database file,
`tempest.db`, under the configured persistence root.**
`SqlitePersistenceStore` implements `IPersistenceStore` and
`IBinaryPersistenceStore` **with no change to either interface**, plus a
new `IQueryablePersistenceStore`.

**The root is unchanged.** `Persistence:RootPath`, defaulting to
`persistence-data` relative to the working directory, exactly as
`ADR-0041` set it. The folder is created on first use and now holds
`tempest.db`, its `-wal` and `-shm` companions while the process runs,
and `tempest.lock`.

**Every connection sets four pragmas, in this order:**
`busy_timeout=5000`, `journal_mode=WAL`, `synchronous=FULL`,
`foreign_keys=ON`. The order is load-bearing: SQLite's default busy
timeout is zero, so a pragma issued before it fails outright rather than
waiting. `synchronous=FULL` in WAL mode fsyncs each commit before
reporting it committed, which is defect 1 closed at the root rather than
half closed.

**Schema, version 1:**

```sql
CREATE TABLE records (
    collection  TEXT NOT NULL,
    key         TEXT NOT NULL,
    text_value  TEXT NULL,
    blob_value  BLOB NULL,
    updated_utc TEXT NOT NULL,
    PRIMARY KEY (collection, key)
);

CREATE TABLE schema_info (version INTEGER NOT NULL);
```

One table carries both value shapes. A record's name, its concurrency
behaviour and its transaction membership do not depend on whether its
value happens to be text — the same argument `IBinaryPersistenceStore`
already made for not introducing a second store. The shapes stay separate
at the *value*: a text write clears `blob_value` and a byte write clears
`text_value`, so a key written as text reads `null` as bytes and the
reverse. That separation was always the intent
(`IBinaryPersistenceStore`'s own remarks: "bytes that happen to be valid
UTF-8 are still bytes"); this is the first backend able to enforce it
rather than rely on collections being owned by one service.

**Names are exact and case-sensitive.** Collection and key are stored
verbatim as `TEXT` under the default `BINARY` collation, so any Unicode
string is a legal name. `CON`, `..`, `Rev1.`, a key with a newline in it,
a key longer than any file name, and `Steel` versus `steel` are now
ordinary distinct keys. **The whole `TD-59` encoding apparatus, its
legacy-path fallback and the case-collision refusal are therefore gone**,
not because they were wrong but because nothing here is a file name.
`PersistenceStoreHostileNameTests` still passes, unchanged in what it
claims: the claim was never "the encoding is correct", it was "the
caller's key comes back".

**`IQueryablePersistenceStore` is new** and adds the four things
consumers have been simulating above the store: `ListKeysAsync(collection,
keyPrefix)`, `ReadAllAsync(collection)` (every text record in one query,
ordered by key), `ReadManyAsync(collection, keys)` (one entry per
requested key, `null` where absent), and
`ExecuteInTransactionAsync(work)`, which commits when `work` returns and
rolls back if it throws. The transaction runs on **its own connection
under `BEGIN IMMEDIATE`**: the write lock is taken up front, so a
transaction that will contend fails at its start rather than half way
through, and a second call made *through the transaction handle* can
never wait on a lock the same connection holds. A call made to the outer
store from inside the body is a bounded busy-timeout failure, not a
deadlock, and the interface says so.

**Instance lifetime.** The store is `IAsyncDisposable`/`IDisposable`. It
holds `<root>/tempest.lock` open with `FileShare.None` for its lifetime,
so a second instance over the same root is refused with
`PersistenceStoreUnavailableException` naming the root and the lock,
rather than allowed to share a database whose in-memory caches it cannot
coordinate with (defect 5). Disposal calls
`SqliteConnection.ClearAllPools()` before releasing the lock, so no
pooled connection is left holding `tempest.db` and the root directory can
be deleted — which is what a test's temporary root and a user's "delete
`persistence-data` to reset" both need.

**One instance, three shapes.** `TempestHost` constructs one store and
registers it under `IPersistenceStore`, `IBinaryPersistenceStore` and
`IQueryablePersistenceStore` via `AddInstance`, the dual-registration
pattern `ADR-0044` established for `CurrentPrincipalAccessor`. The two
`Singleton<..., PersistenceStore>()` lines this replaces quietly built
*two* stores over one directory tree — harmless for a file store,
impossible for one holding an exclusive lock.

**Service Disposal is no longer a no-op.** `TempestHost`'s Service
Disposal phase now disposes every registered instance implementing
`IAsyncDisposable`/`IDisposable`, in reverse registration order. This
closes `TD-03` **for instance registrations**, which is where the
platform's disposable services are. It does **not** close it for
container-constructed singletons: `TempestServiceProvider` keeps no
record of what it built, and giving it one is a change to the container,
not to the Host. That half stays open and is not claimed here.

**`Persistence:Backend` selects the backend**: `sqlite` (default) or
`files` (the file-per-key `PersistenceStore`, registered under the same
three interfaces). An unrecognised value is a fatal configuration error,
not a silent fall back — a deployment that asked for a backend and got a
different one would be writing its data somewhere its operator did not
choose (`ADR-0013`).

**The `files` backend is retained for exactly one release and is deleted
in `v0.18.0`,** together with `PersistenceStore`, its `TD-59` encoding
and its file-store-only tests. It exists so that a site hitting an
unforeseen SQLite problem in `v0.17.0` has somewhere to stand while it is
fixed. Nothing new may be built on it, and `WP 17.1B` is built on SQLite
alone. `PersistenceStore` does implement `IQueryablePersistenceStore`, in
the obvious O(N) way, so a consumer written against the interface still
runs there — but `ExecuteInTransactionAsync` on that backend **is not
atomic and cannot be made so**. It runs the unit of work, each write
lands as it is made, and a failure part way through leaves the earlier
writes standing. This is documented in capitals on the method and
asserted by a test that names the missing rollback, so that no reader can
acquire the opposite belief from a green suite.

**There is no importer, and this is a Product Owner decision, not an
omission.** The `persistence-data/` folders that existed were test data;
they have been deleted and `v0.17.0` starts every installation with a
fresh database. Writing a one-shot JSON-tree importer would have been a
second reader of a format that is being retired, with its own encoding
bugs to find, to migrate data nobody wants.

**`Microsoft.Data.Sqlite` is the first NuGet package in `Tempest.Core`,
pinned to 10.0.11.** Until now this assembly carried a single
`FrameworkReference` to `Microsoft.AspNetCore.App` and nothing else — a
deliberate position, recorded in `ADR-0049`, that the platform's own
substrate is written here rather than assembled. That position is worth
holding and is not being abandoned; it is being spent, once, on the one
thing it cannot buy. The alternative to this package is a hand-written
P/Invoke layer over `sqlite3` plus the native binary for every target
platform, which is this package with our name on it and our bugs in it.
The package is published by the .NET team, ships in the same cadence as
the SDK this repository already pins, and carries exactly one transitive
dependency (`SQLitePCLRaw` — the native library and its shim). It is
referenced by exactly one type, `SqlitePersistenceStore`, and appears in
no public signature, so replacing it later is a change to one file.

## Consequences

**Closed.** `TD-12` (O(N) collection scans — a whole collection is now
one indexed query; measured at 10,000 keys in one collection: list 5 ms,
read-all 8 ms, prefix listing 3 ms). `TD-137` (no fsync). `TD-88` (no
cross-process lock). `TD-59` and its whole apparatus, by removal.
`TD-03`, for registered instances.

**Enabled, not closed.** `TD-149`, `TD-156`, `TD-23`, `TD-32`, `TD-68`
and the `TD-135`–`TD-148` cluster all name defects whose fix is a
transaction. `WP 17.1B` closes them; this Work Package gives it the
transaction to close them with, and closes none of them itself.

**Semantics that changed.** Keys and collections are exact,
case-sensitive and unrestricted; two keys differing only in case are two
records everywhere, and no write is ever refused for a name collision. A
record written as text now reads as `null` bytes and the reverse, where
the file store returned the raw file either way. `ListKeysAsync` returns
keys in ascending ordinal order rather than directory order. A store now
has a lifetime, holds a lock, and must be disposed. Two stores over one
root in one process are refused exactly as two processes are — correct,
and it obliged every Host-building test in `Tempest.Core.Tests` to use a
persistence root of its own, which is the rule `WP 17.0A` had already set
for `PersistenceStoreTests` and had no way to enforce.

**Not changed.** The four `IPersistenceStore` members and the three
`IBinaryPersistenceStore` members are byte-for-byte the shapes
`ADR-0041` drafted and `TD-31` added. Every consumer compiles and passes
unchanged. `Persistence:RootPath` and its default are unchanged.
`ADR-0053`'s decision — that the Engineering Data Model is built on the
one persistence abstraction and introduces no second storage mechanism —
is not merely preserved but strengthened, since that abstraction can now
carry the revision and reference structure natively instead of
serialising around it.

**Costs accepted.** `synchronous=FULL` makes each individual committed
write an fsync, so a caller that writes 10,000 records one at a time pays
10,000 of them; the answer is one transaction, which the store now has.
The root now contains an opaque database rather than browsable files, so
a support question that used to be answered with a text editor is now
answered with a SQL client — accepted, because the property being bought
is that a half-written state cannot exist, and browsability was never a
requirement. A second TempestOS instance on one root is now refused
rather than tolerated; that is the point, and the message names the root
and the lock so the refusal is actionable.

**Deferred deliberately.** The secondary-index table `WP 17.1A`'s own
Work Package row anticipated is not built. `ReferenceDataCatalog`'s
hand-maintained secondary-index collection still works unchanged, and
`IQueryablePersistenceStore` already removes the scan that made an index
worth having; a real index belongs with the schema that will use it
(`WP 17.1B`, `WP 17.2A`'s audit query), not ahead of it. No consumer has
been migrated onto `IQueryablePersistenceStore` in this Work Package
either: the interface exists and is tested against both backends, and
moving `AuditQuery`, `ReferenceDataCatalog` and
`EngineeringObjectStateStore` onto it is a change to those services'
behaviour that belongs in their own Work Packages rather than smuggled
into a substrate swap.

## Alternatives Considered

**Keep the file-per-key store and add fsync, an index file and a lock
file.** Rejected. Each of the five defects is individually fixable and
the composite is a database, hand-written, in this repository, with a
crash-consistency story nobody here can prove. The rename-based atomic
write already took two Work Packages and a review board to get right for
*one* key.

**A second, transactional store beside the existing one.** Rejected on
`ADR-0053`'s own reasoning: two durable stores means two roots, two
encodings, two failure modes, and a class of bug where a record exists in
one and not the other. `WP 17.1B`'s whole premise is that one store is
authoritative.

**LiteDB, or another embedded document database.** Rejected. The data is
key/value with a collection scope, which SQLite models exactly; a
document database would be a larger dependency for a shape it does not
need to be good at, and SQLite's durability behaviour under power loss is
the most thoroughly documented and independently tested of any embedded
engine.

**A hand-written P/Invoke layer over `sqlite3`, to keep the assembly
package-free.** Rejected. It keeps a property of the build file at the
cost of owning the marshalling, the connection pooling, the native binary
per platform, and every bug in all of it. The position `ADR-0049`
recorded is about not assembling this platform out of other people's
frameworks; it was never about refusing the C library that every other
option would end up wrapping anyway.

**An importer for existing `persistence-data/` trees.** Declined by the
Product Owner, on the grounds that the existing trees are test data. See
the Decision.

## Related Documents

- `docs/adr/ADR-0041-shared-persistence-abstraction.md` — superseded in
  part: its backend choice, not its interface.
- `docs/adr/ADR-0053-engineering-data-model-is-built-directly-on-the-existing-persistence-abstraction.md`
  — superseded in part: its premise about what the abstraction cannot do.
- `docs/adr/ADR-0049-adopting-aspnetcore-kestrel-for-the-rest-api.md` — the
  dependency position this ADR spends once.
- `docs/adr/ADR-0044-authorization-enforcement-point.md` — the one-instance-under-several-interfaces
  registration pattern reused here.
- `docs/architecture/Engineering Object Rehydration Architecture.md` —
  updated for the new substrate.
- `PHYSICAL_REVIEW.md` §4, §5, §6 — where runtime data lives and how to
  reset it.
- `docs/releases/v1.0.0/WorkPackages.md`, `WP 17.1A` and `WP 17.1B`.
