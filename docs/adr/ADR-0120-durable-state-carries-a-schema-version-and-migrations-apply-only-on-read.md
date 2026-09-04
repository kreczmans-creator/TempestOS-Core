# ADR-0120: Durable State Carries a Schema Version, and Migrations Apply Only on Read

## Status

Accepted — `WP 16.3A` (Architecture), 2026-09-04. Addresses `TD-87`,
disclosed by `ADR-0113` and restated by `ADR-0114`. Extends `ADR-0113`
(durable object state) and `ADR-0114` (durable attachment content)
without reopening either; builds on `ADR-0116`'s rehydration boundary,
which this ADR's migration step sits inside. Implementation is
`WP 16.3B`'s, realising `docs/architecture/State Schema Versioning
Architecture.md` exactly; `WP 16.3B` closes `TD-87`, not this ADR. Serves
the `v1.0.0` scope `D-021` (proposed, `WP 16.0A`) governs: a locally
trusted, single-user desktop engineering platform whose durable state
must outlive the process that wrote it across every release between now
and `v1.0.0`.

## Context

`ADR-0113` made `EngineeringObjectState` durable and named, in its own
Consequences, exactly what it did not do:

> "`EngineeringObjectState` is a schema. It is versionless today: an
> unreadable or partial record degrades to skipped-or-defaulted rather
> than migrated. Adequate while the shape is additive; a real migration
> story is disclosed debt (`TD-87`)."

`TD-87` names two distinct risks, and the code confirms both are real
today, not hypothetical:

**A structurally broken record is already handled — this part is not new
work.** `EngineeringObjectStateStore.Deserialise`
(`src/Tempest.Core/EngineeringDomain/Implementation/EngineeringObjectStateStore.cs:95-106`)
catches `JsonException` and returns `null`; `ListAsync`
(lines 66-89) skips a `null` result rather than failing the whole read.
One record that will not parse at all already cannot abort startup or
cost the user any other object — `TD-60`'s discipline, already applied
here.

**A record that parses but means something different is not handled at
all — this is the actual gap.** `EngineeringObjectStateStore.SaveAsync`
(line 54) calls `JsonSerializer.Serialize(state)` with no
`JsonSerializerOptions` — no converter, no version stamp — and
`Deserialise` (line 99) reads it back the same way. `LifecycleState`
(`src/Tempest.Core/EngineeringDomain/Contracts/Lifecycle.cs:3-13` — an
eight-member `enum` with no explicit numeric values) therefore
serialises as its ordinal position. `EngineeringObjectState.Status` and
every `EngineeringObjectTransitionState.From`/`To`
(`EngineeringObjectState.cs:57,139-144`) carry this risk today: reordering
the `enum` would make every persisted status and every recorded
transition **silently reinterpret as a different, valid-looking value**
— not a `JsonException`, not a skipped record, a wrong one, indistinguishable
from a correct one until an engineer notices their released part reads
as cancelled. `ADR-0114` added `EngineeringObjectAttachmentState.ContentHash`
(`EngineeringObjectState.cs:151-163`) and stated the same thing again in
its own Consequences: "That the schema carries no version of its own
remains `TD-87`; this field was deliberately chosen to be additive so
that it does not need one." Two ADRs in a row have now built around this
gap rather than into it.

**The write path always writes the current in-memory shape, never an old
one.** `EngineeringObjectBase.PersistStateAsync`
(`EngineeringObjectBase.cs:172-175`) calls `store.SaveAsync(CaptureState(), ...)`,
and `CaptureState()` (lines 73-107) always builds a fresh
`EngineeringObjectState` from the object's live fields. There is no code
path that writes an old shape on purpose — an object is always
constructed from either a factory or a rehydrator into the current type,
then captured fresh. **Migration is therefore only ever a read-path
problem**: a record written by an older build of TempestOS, read by a
newer one, once, on its way back into memory. `ADR-0113`'s "no save step"
design (state is written at creation and after every mutation) makes this
true by construction, not by convention — there is nothing analogous to
a stored procedure or a schema-locked table that a write could target an
old version of.

**Rehydration is the one place every record passes through.**
`EngineeringObjectRehydrationService.RehydrateAsync`
(`EngineeringObjectRehydrationService.cs:69-151`) reads every persisted
state via `stateStore.ListAsync` before resolving a rehydrator for it —
`ListAsync` is upstream of Kind resolution, upstream of the document
lookup, upstream of `IRehydratable<T>.Rehydrate`. A migration applied
inside `EngineeringObjectStateStore`'s own read path — before the state
record is ever handed to a caller — reaches every consumer of
`EngineeringObjectState`, not `RehydrateAsync` alone: `FindAsync`
(single-object reads) and `ListAsync` both call the same private
`Deserialise`.

**The same fragility exists outside the Engineering Domain, on the
identical mechanism.** `SettingsDocument<TDocument>.SaveAsync`/`LoadAsync`
(`src/Tempest.Core/Settings/SettingsDocument.cs:89-122`) call
`JsonSerializer.Serialize`/`Deserialize<TDocument>` with no options
either, and one of its nine real callers already stores an `enum`
this way: `ShellLocationDto(ShellArea Area, Guid? ProjectId, ProjectArea?
ProjectArea)` (`src/Tempest.App/Shell/ShellNavigator.cs:179`). `LoadAsync`
already degrades a corrupt value to `null` and logs it (lines 100-110) —
the identical "skip, don't abort" contract `EngineeringObjectStateStore`
independently arrived at. This is the same problem, once, not two
problems that happen to look alike.

## Decision

**1. `EngineeringObjectState` gains an integer `SchemaVersion`. Absent
means 1, explicitly, not by relying on a serialiser default.**

`SchemaVersion` becomes the record's first component. A record written
before this ADR has no such property; deserialising it leaves
`SchemaVersion` at the CLR default for `int`, which is `0` — not `1` —
because `System.Text.Json`'s handling of a missing property against a
positional record constructor is a detail of the serialiser version, not
a contract this platform should depend on. `EngineeringObjectStateStore`'s
read path therefore normalises explicitly: `state.SchemaVersion <= 0`
is treated as `1` before anything downstream sees the value. This is one
`if` and it removes every question about what a future .NET upgrade
does with a missing constructor argument.

**2. Migrations are an ordered chain per Kind, applied once, on read,
inside `EngineeringObjectStateStore`.**

```csharp
public interface IStateMigration
{
    /// The Kind this migration applies to, or null to apply to every Kind
    /// (a change to a field EngineeringObjectState itself owns — Status,
    /// DisplayName, Metadata, BomLine, History, Attachments — rather than
    /// a Kind's own TypeState).
    string? Kind { get; }

    /// The version this migration upgrades from. It is applied when a
    /// record's current SchemaVersion equals this value, and its own
    /// output's SchemaVersion is FromVersion + 1.
    int FromVersion { get; }

    EngineeringObjectState Migrate(EngineeringObjectState state);
}
```

An `IStateMigrationRegistry` collects these, keyed by Kind (`null` held
separately as the common chain). `EngineeringObjectStateStore`'s read
path — after JSON deserialisation and the `SchemaVersion` normalisation
in Decision 1 — repeatedly finds and applies the migration whose
`FromVersion` matches the record's current version (common chain first,
then that Kind's own chain) until no further migration applies. This
stays "per Kind" as `TD-87` and the Release Plan name it: a Kind's own
`TypeState` shape is that Kind's own business, exactly as `ADR-0113`
decided capture and restore are (`EngineeringObjectState.cs`'s own
`Type`/`TypeGuid`/`TypeJson` readers already read `TypeState` per Kind,
never centrally) — a common-field migration is the one deliberate
exception, registered once and reached by every Kind rather than copied
into each Kind's own chain.

**Why not a version-keyed DTO hierarchy (a `V1EngineeringObjectState`,
`V2EngineeringObjectState`, ...).** That is the heavier, more general
migration framework this ADR deliberately does not build. It would need
a second full type per breaking change, forever, to describe a shape
this platform never round-trips through code — nothing ever constructs a
`V1` object; a `V1` record only ever exists as bytes on disk. An ordered
function `EngineeringObjectState → EngineeringObjectState`, one per
version step, says the same thing with one method, no parallel type
hierarchy, and composes: version 1 to 4 is three single-step migrations
applied in sequence, each independently testable against its own fixed
input and output shape.

**3. Attachment content is nested state, not a second stamped record.**

`EngineeringObjectAttachmentState` has no independent storage: it lives
only inside `EngineeringObjectState.Attachments`
(`EngineeringObjectState.cs:62`), written by `EngineeringObjectBase`'s
own `CaptureState` (`EngineeringObjectBase.cs:93`) and read back as part
of the same JSON document. It has no collection key of its own in
`IPersistenceStore` and no read path independent of its parent object's.
A second `SchemaVersion` on it would version something that is never
independently deserialised — a Kind's own migration, applied to the
parent `EngineeringObjectState`, is the correct and only place to
rewrite a nested attachment's shape, exactly as it is the correct place
to rewrite any other nested field (`BomLine`, `History`). The bytes
`IAttachmentContentStore` holds (`AttachmentContentStore.cs`) are a
different question entirely: they are not JSON, and are already
versioned by the mechanism `ADR-0114` chose for them — a SHA-256 content
hash checked on every read, `Available`/`Missing`/`Corrupt`, never a
schema in the sense this ADR addresses.

**4. Enums serialise as strings, going forward; reading a numeric-valued
record still works.**

`EngineeringObjectStateStore` gains one static, shared
`JsonSerializerOptions` carrying `JsonStringEnumConverter`, used by both
`SaveAsync` and `Deserialise`. `LifecycleState` and every other `enum`
reachable from `EngineeringObjectState` write as their member name from
this point on. The built-in `JsonStringEnumConverter` reads both forms —
a name, or a number — so a record written before this ADR, still holding
`"Status": 3`, deserialises identically to one written after, holding
`"Status": "Released"`. **No migration is needed for this change
specifically**: it is a write-path change with a read path that was
always backward-compatible, which is why it is listed as "cheap, do it
first" in `TD-87`'s own closing note. It closes the actual named risk —
a future re-ordering of `LifecycleState`'s members can no longer silently
reinterpret a persisted status, because the persisted value no longer
depends on member order at all.

**5. A record whose version cannot be bridged is logged and skipped —
extending, not duplicating, the existing discipline.**

`Deserialise` already treats a `JsonException` this way (`TD-60`'s
established pattern, Context above). The migration step is wrapped in
the identical shape: a record whose `SchemaVersion` has no migration
path to the store's current version for its Kind, or whose migration
throws, is caught, logged as a `Warning` naming the object Id, Kind and
stuck version, and the record is treated as unreadable — `Deserialise`
returns `null`, exactly as it does for malformed JSON today. This is the
one behaviour `WP 16.3B`'s "deliberately-broken migration" test exists
to prove, and it is a small extension of code that already exists, not a
new failure-handling mechanism next to it.

**6. `SettingsDocument<T>` joins the same scheme — for a document that is
a JSON object. It does not, yet, for a document that is a bare JSON
array, and that is a real seam, not an oversight.**

Six of the nine real `SettingsDocument<T>` consumers store a single DTO
record: `CurrentProjectDto` (`ProjectContext.cs:133`), `WorkspaceStateDto`
(`WorkspaceStateDto.cs:4`), `ShellLocationDto` (`ShellNavigator.cs:179`),
`DesktopPanelUiStateDto` (`DesktopPanelUiState.cs:126`), `UserSettingsDto`
(`UserSettings.cs:85`), `WindowUiStateDto` (`WindowUiState.cs:163`). For
these, the identical pattern applies directly: the DTO itself gains an
optional `SchemaVersion` (absent/`0` → `1`, normalised the same explicit
way as Decision 1), and `SettingsDocument<TDocument>` gains an optional,
constructor-supplied migration chain — `IReadOnlyList<ISettingsMigration<TDocument>>`,
applied inside `LoadAsync` after deserialisation, before the document is
handed to the caller. No Kind dispatch is needed here at all: `T` is
fixed per call site, so "per Kind" degenerates to "per `TDocument`",
which the generic parameter already gives for free.

Three consumers store a **bare JSON array**, not an object:
`MacroManager` (`List<MacroDto>`, `Macros/MacroManager.cs:15`),
`FavouriteObjectsState` (`List<FavouriteObjectEntry>`,
`FavouriteObjectsState.cs:32`), `RecentObjectsState`
(`List<RecentObjectEntry>`, `RecentObjectsState.cs:41`). A JSON array has
no place for a sibling `SchemaVersion` property without wrapping it in an
envelope object — and every currently-stored value for these three keys
is a bare `[...]`, so introducing an envelope changes the wire shape
those keys already hold on every installed copy of TempestOS, not merely
adds a field to it. That is a materially different, and materially
riskier, change than the six object-shaped documents need, and this ADR
does not make it. These three are explicitly out of scope for
`WP 16.3B`: if one of `MacroDto`, `FavouriteObjectEntry` or
`RecentObjectEntry` ever needs a breaking change, the version question is
answered then, at the element level (mirroring
`EngineeringObjectAttachmentState`'s own nested pattern) or by a
deliberate envelope migration, by whichever future Work Package needs it
— not pre-built here against a need that does not yet exist.

**The Release Plan recommended "yes, same seam" without qualification;
the actual code has two seams, not one, and only one of them is the seam
`EngineeringObjectState` uses.** This ADR follows the recommendation
where the seam actually matches, and names, rather than papers over,
where it does not.

## Consequences

**Positive:**

- The one risk `TD-87` names concretely — `LifecycleState` reordering
  silently reinterpreting persisted status — is closed by Decision 4
  alone, with no migration required, because the chosen converter reads
  both the old and the new representation.
- A genuine future breaking change (a renamed `TypeState` key, a changed
  field type) now has a real, ordered, testable seam to land in, instead
  of the current choice between "make it additive forever" and "silently
  lose or misread old records."
- The golden corpus (`docs/architecture/State Schema Versioning
  Architecture.md`) turns "this migration works" into a repeatable,
  regression-tested claim rather than a one-time manual check at the
  moment a migration is written.
- Nothing changes about *when* state is written — Decision statement in
  Context ("migration is only ever a read-path problem") is a structural
  fact of `ADR-0113`'s own "no save step" design, not a new constraint
  this ADR imposes.
- `IStateMigrationRegistry` is an optional collaborator, mirroring
  `ADR-0113`'s own `EngineeringDomainContext.ObjectStateStore` pattern: a
  store constructed without one behaves exactly as it does today, so
  every existing hand-assembled test context keeps working unchanged.

**Negative / accepted cost:**

- `EngineeringObjectStateStore` grows a second responsibility beyond
  read/write: applying migrations. It stays a small, additive method
  next to `Deserialise`, not a new class, because the chain it walks is
  supplied, not authored, by the store.
- Nine registries now theoretically exist across the platform once
  `SettingsDocument<T>`'s six object-shaped consumers each register their
  own migration chain — in practice, zero exist until one of those six
  DTOs makes a breaking change, since an empty chain is a legal,
  zero-cost default.
- The three list-shaped `SettingsDocument<T>` consumers remain exactly as
  fragile as they are today. This is disclosed, not fixed, by this ADR —
  see Decision 6. Whichever Work Package first needs to change one of
  their element shapes inherits an unversioned document and must decide
  the envelope question at that time.
- A record stuck with no migration path is now actively distinguished
  from one that merely failed to parse, in the log line a caller reads —
  a small increase in the vocabulary `EngineeringObjectStateStore`'s
  logging carries, in exchange for a diagnosable "this object predates a
  migration nobody wrote" instead of an indistinguishable "this object's
  JSON is broken."

## Alternatives Considered

**A schema registry service, resolved through DI, replacing
`EngineeringObjectStateStore`'s own migration call.** Rejected: one
concrete store already owns exactly one collection
(`EngineeringObjectStateStore.StateCollectionName`,
`EngineeringObjectStateStore.cs:31`); a separate service would be a new
collaborator with nothing else to do, the same shape of unnecessary
indirection `ADR-0104` already rejected for the Desktop layer, one layer
down.

**Versioning at the `IPersistenceStore` collection level (a
`EngineeringDomain.ObjectState.v2` collection per breaking change).**
Rejected: it would require a startup copy-and-migrate pass across every
record before the platform could read anything, reintroducing exactly
the eager, whole-store startup cost `TD-88` already discloses as a
separate, undesirable property of rehydration — and it makes a
migration a one-time event a user must sit through, rather than a cheap
per-record correction applied the moment that record is actually read.

**Versioning the whole `EngineeringObjectState` record via a
`[JsonConverter]` that dispatches to versioned sub-types.** Rejected as
the version-keyed DTO hierarchy named in Decision 2 — heavier than an
ordered list of one-argument functions, for no behaviour a converter
does that a store-level check does not.

**Migrating on write instead of read (rewrite every old record to the
current shape the first time anything touches it).** Rejected: `TD-86`
already discloses that state is written per mutation with no batching;
adding "and rewrite it to the newest schema" to every write would tie
schema currency to how often an object happens to be touched, so an
untouched object could sit at an old version indefinitely regardless —
which is exactly the read-time migration already has to handle, so the
write-time version buys nothing and adds a write nobody asked for.

**A single global `SchemaVersion` for every Kind, rather than per-Kind
chains.** Rejected because it does not match the actual failure surface:
a Kind's own `TypeState` shape is that Kind's own business (Decision 2),
and a global version would force every Kind's migration to move in
lock-step even when only one Kind's fields changed — the opposite of
`ADR-0113`'s "each type owns its own persistence" decision, applied one
level up.

**Wrapping the three list-shaped `SettingsDocument<T>` consumers in an
envelope now, to make "same seam" literally uniform.** Rejected — see
Decision 6. It would change the wire format every existing installation
already holds for three keys with no current need driving it, the
precise kind of building-ahead-of-demonstrated-need `D-023` (the plugin
platform decision, same register) already rejects on identical grounds.

## Future Considerations

If a genuine breaking change reaches a `TypeState` field before
`WP 16.3B` lands, that field should stay additive (a new, separately
named key, read with a fallback) rather than wait — the discipline
`ADR-0114`'s `ContentHash` already used, and the one this ADR's own
mechanism exists to make unnecessary going forward, not mandatory in the
interim.

If one of the three list-shaped `SettingsDocument<T>` documents
(`MacroDto`, `FavouriteObjectEntry`, `RecentObjectEntry`) needs a
breaking change, the Work Package that needs it should decide between an
envelope migration (breaking the wire shape once, deliberately, with its
own fixture-backed test) and a per-element version field, on that
document's own concrete requirement — not by extending this ADR's
mechanism onto a shape it was not designed for.

If a second Domain-layer collection ever needs the identical treatment
`EngineeringObjectState` gets here — durable, Kind-keyed, read far more
often than written — `IStateMigrationRegistry` should be reused rather
than reinvented; nothing about it is specific to `EngineeringObjectState`
beyond the type parameter.

## Related Documents

- `ADR-0113` — durable object state and the Kind-keyed rehydration
  registry this migration step reads through.
- `ADR-0114` — durable attachment content; its own `ContentHash`
  disclosure is the second citation of the gap this ADR closes.
- `ADR-0116` — the production rehydration boundary; unaffected in shape,
  now reading through one more, transparent step.
- `docs/architecture/Engineering Object Rehydration Architecture.md` —
  the read path this ADR's migration step is inserted into.
- `docs/architecture/State Schema Versioning Architecture.md` — the
  design `WP 16.3B` implements exactly.
- `docs/governance/Quality/Technical Debt Register.md` — `TD-87`
  (addressed here; closed by `WP 16.3B`), `TD-60` (the passive-read
  discipline this extends), `TD-86`/`TD-88` (the write-batching and
  eager-startup costs this ADR deliberately does not touch).
- `docs/releases/v0.16.0/v0.16.0 Release Plan.md` — `WP 16.3A`/`WP 16.3B`.
- `docs/releases/v0.16.0/WP16.0A v0.16.0 Scope Decision.md` — `D-021`
  (proposed), the `v1.0.0` scope this ADR serves.
