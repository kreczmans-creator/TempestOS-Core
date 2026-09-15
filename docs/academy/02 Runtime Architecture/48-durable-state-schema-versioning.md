# Durable State Schema Versioning

**Release:** `v0.16.0` · **Work Package(s):** `WP 16.3A` (architecture),
`WP 16.3B` (implementation), `WP 16.4B-R1` ·
**Debt:** `TD-87` (resolved) ·
**Decision:** `ADR-0120`, `ADR-0121`, `ADR-0123` ·
**Code:** `Tempest.Core.EngineeringDomain`, `Tempest.Core.Settings`

**In plain terms.** Every saved part, drawing or setting in TempestOS is
a record with a particular shape — which fields it has, and what each
means. That shape is a "schema". The code that reads a record back is
almost always a newer version than the code that wrote it, so a change
to the code's idea of the shape can make it misread data it saved
correctly before. This release gives every saved engineering record an
explicit version number, so the software knows exactly which shape it is
looking at, and teaches it to translate an old record forward rather
than guess. The promise: your saved work means the same thing after
every future update, or the platform says so and skips it — it never
quietly gets it wrong.

## Data outlives the code that wrote it

A schema is the shape of a saved record: which fields exist, what each
value means. TempestOS writes engineering objects — parts, assemblies,
requirements — as JSON, a plain text format of named fields and values.
The record on disk does not change when the code changes; it sits in
whatever shape it was written in until something reads it again,
possibly years and several releases later.

That gap is where a quiet category of bug lives. A record that cannot
parse at all fails loudly. A record that parses fine and means something
*different* fails silently: the software reads a value, believes it, and
is wrong — nobody notices until an engineer sees their released part
reading as cancelled.

## The concrete risk: an enum that reorders itself

`EngineeringObjectState` (`ADR-0113`; see
`34-engineering-object-rehydration.md`) already made engineering-object
state durable, and its own Consequences disclosed what it had not
solved: the record was versionless, and `TD-87` named the specific way
that could bite. `LifecycleState` is an eight-member `enum` — Draft,
InReview, Approved, Released, and so on — with no explicit numeric
values, so C#'s default serialiser wrote it as a plain number: whichever
position it occupied in the source file. Reorder those members, and
every saved `Status` and transition silently reinterprets as a
different, equally valid-looking value.

## The design: a version number that is never assumed

`ADR-0120` gives `EngineeringObjectState` an integer `SchemaVersion` as
its first field. The interesting decision is how an *absent* one is
treated: a record written before the field existed leaves it at C#'s
default, `0` — and the store's read path normalises that to `1`
explicitly, one `if`, rather than trusting a serialiser's own handling
of a missing constructor argument. Correctness should never depend on a
detail of the JSON library's own version.

Migrations are an ordered chain, one plain function per version step
(`IStateMigration`: a `Kind`, a `FromVersion` it applies from, and a
`Migrate` method), kept per Kind. A common, Kind-less chain runs first
for shared fields; a Kind's own chain runs after. This is deliberately
not a version-keyed type hierarchy (`V1EngineeringObjectState`,
`V2...`), which would need a new type per breaking change for a shape
nothing in the platform ever constructs directly — an old version only
ever exists as bytes on disk.

## On read only — never a bulk rewrite at startup

Migration runs once, inside the store's read path, the moment a record
is actually loaded — never as a batch job over every saved object at
startup. `ADR-0120` rejected that alternative directly: rewriting
everything up front forces a whole-store pass before the platform can
read anything, turning a schema bump into an event the user sits
through. Migrating on read means an object nobody reopens never pays the
cost. And because `EngineeringObjectBase.CaptureState()` always writes a
fresh record from live fields, nothing ever *writes* an old shape on
purpose — migration is a read-path problem, once, by construction.

A record whose version cannot be bridged — no migration reaches it, or
one throws — is logged with the object's Id, Kind and stuck version,
and skipped: the same discipline already applied to JSON that will not
parse at all (`TD-60`). One unreadable object must never cost the user
every other object they own, and must never abort startup.

## Enums as strings: the fix that needed no migration

Decision 4 closes `TD-87`'s named risk with nothing built above it:
every enum reachable from `EngineeringObjectState` now serialises as its
member name, via a shared `JsonStringEnumConverter`. That converter
reads *both* a name and a number, so an old record holding `"Status": 3`
and a new one holding `"Status": "Released"` deserialise identically —
no migration chain required. `LifecycleState` can now be reordered
safely; the persisted value no longer depends on where a member sits in
the source file.

## Two Technical Review rejections, and what each taught

`WP 16.3B` implemented this in three commits; the first two were sent
back.

**Round 1** found the "ahead of current version" check (`2e3ec8e`) let a
record with no migration path slip through unmigrated: if a future
build's version moved past a record with no migration bridging the gap,
the loop found nothing to apply and exited, silently handing back an
unmigrated record as current. `2225d2e` fixed it by checking the
*postcondition* — did the record actually reach target — not just
whether the loop stopped.

**Round 2** rejected that same fix. To let a test drive a target version
other than the fixed `CurrentSchemaVersion = 1`, it had registered a
value container-wide, `services.AddInstance(typeof(int?), ...)`. Any
other class anywhere declaring its own unrelated `int?` constructor
parameter would silently receive this value too — a local test need
turned into a platform-wide hazard. `67e7ce3` replaced it with a second,
`internal` constructor reachable only from the test assembly, while the
public constructor kept its original shape; the container's own rule —
exactly one *public* constructor — became the seam, for free.

That fix, defended under review but never written down, was exactly
what the ADR rule exists for. `ADR-0121` records it afterwards: **a
test-only construction seam is an internal constructor, never a
container-visible registration.**

## The collision guard nobody needed yet

`StateMigrationRegistry` collected migrations in a plain dictionary, so
two registrations could collide silently: the same version registered
twice (last one wins), or a common and a Kind-specific migration both
claiming the same `FromVersion` — in which case the Kind-specific one
would never run, since the common chain is always checked first, while
the record still advanced and looked fully migrated. Nobody had hit
this; `CurrentSchemaVersion` was still `1` and no real migration
existed. That is what made it dangerous — a trap for whoever wrote the
*first* real migration, invisible until they did.

`WP 16.4B-R1` (`4f31591`) closed it: `Register` now throws
`DuplicateStateMigrationException` or `ConflictingStateMigrationException`,
checked in both registration orders, with **no opt-out**. `ADR-0123`
explains why this diverges from the DI container's own equivalent guard
(`ADR-0122`), which does allow a replace — that guard needed one because
roughly 330 real registrations already depended on it. Here, a
repository-wide search found zero real migrations registered anywhere,
so no escape hatch was added for a hazard that would only reopen what
was just closed.

## `SettingsDocument` had the identical gap

`SettingsDocument<T>` — behind window layouts, recent-object lists and
other saved settings — got the same `SchemaVersion` treatment for its
object-shaped documents, but its migration walk lacked the postcondition
check `EngineeringObjectStateStore` had already been sent back once for
omitting: it would stop wherever the chain stopped and hand the document
back at that version regardless. Commit `6cd31eb` fixed it to match: a
document the supplied chain cannot carry all the way to its own highest
reachable version is discarded and logged, and the caller falls back to
its documented defaults — never returned half-migrated at the wrong
version.

## The golden corpus: a claim that keeps re-proving itself

Seven real pre-`ADR-0120` records — genuine JSON this platform actually
produced, numeric `Status`, no `SchemaVersion` at all — are committed
under `GoldenCorpus/v1/`. Every one loads through the real read path on
every test run, with every field it declares checked against what comes
back, not merely its Id and version. A `RestartProofTests` case goes
further: a real object is created through a real host, its saved record
overwritten with the exact pre-`v0.16.0` byte shape, and a second,
independent host rehydrates it with identical status. This is
deliberately not a one-time manual check — it is a standing regression
test every future schema bump must keep passing.

The store underneath changed again at `v0.17.0`
(`53-sqlite-persistence.md`) — records moved from one file per object to
rows in a SQLite database. `SchemaVersion` is unaffected: it lives
inside the JSON text a row stores, not in the storage mechanism, so the
switch changed nothing about how a record's shape is read or migrated.

## The one-way door

Upgrading to `v0.16.0` is safe — the golden corpus is the proof.
Downgrading to `v0.15.0` is not, and this was verified rather than
inferred: `v0.15.0`'s JSON reader accepts only numeric enum values, and
any object `v0.16.0` re-saves now holds a string-valued `Status`. Read
by `v0.15.0`, that value fails to parse — and its read path was already
built to treat a failure as "log a warning, return null" rather than
raise an error. The object does not become merely unreadable; it
**silently disappears** from that older workspace, with no crash to
alert anyone. Take a copy of your data directory before upgrading if you
might need to go back.

## What this deliberately did not build

No schema registry service resolved through the container — one store
owns exactly one collection. No collection-level versioning (a second
`.v2` table per breaking change) — it reintroduces the eager,
whole-store pass the read-only design avoids. No envelope wrapping for
the three settings documents that are bare JSON arrays on disk
(`MacroDto`, `FavouriteObjectEntry`, `RecentObjectEntry`) — every
installed copy already holds those as plain arrays, and wrapping one now
changes a wire shape for a need that does not yet exist. And no
migration ships this release: `CurrentSchemaVersion` stays `1`
throughout `v0.16.0`, so the mechanism is proven only against tests that
deliberately construct a future version.

## What to take away

- **A missing value should never inherit its meaning from a library's
  own default behaviour** — decide and code what "absent" means, so a
  future dependency upgrade cannot quietly change it.
- **Migrate data only at the moment it is actually read**, not on a
  schedule or in a batch — cheaper, and an unused record never pays a
  cost nobody asked it to pay.
- **A decision Technical Review specifically challenged and defended is
  itself worth writing down as an ADR** — the internal-constructor seam
  was correct the moment it was made, and still needed `ADR-0121` before
  it counted as a decision the next engineer could find.
- **A guard against a hazard nobody has hit yet is not premature — it
  is the only time it is cheap.** The collision guard cost one Work
  Package before a real migration existed; finding it afterwards would
  have meant finding it against live data.
