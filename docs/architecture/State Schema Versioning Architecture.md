# State Schema Versioning Architecture

**Status:** Designed — `WP 16.3A` (`ADR-0120`). Implementation —
`WP 16.3B` (not yet started; closes `TD-87`). ·
**Debt:** `TD-87` ·
**Code:** `Tempest.Core.Persistence`, `Tempest.Core.EngineeringDomain`,
`Tempest.Core.Settings`

## The question this answers

`ADR-0113` made engineering object state durable and disclosed, in its
own Consequences, that the record it introduced is versionless: an
unreadable record already degrades safely (`TD-60`), but a record that
parses correctly and means something *different* — because the shape
changed underneath it — does not. `ADR-0114` repeated the same
disclosure a second time. This document is the design `WP 16.3B` builds:
where the version lives, how a migration is written and found, what the
golden corpus is and where it lives, and the exact files this touches.
The decision itself, and why each part of it is shaped the way it is, is
`ADR-0120` — this document does not re-argue it, only lays it out as
something to build.

## Record shapes, before and after

### `EngineeringObjectState` — today

```csharp
// src/Tempest.Core/EngineeringDomain/Implementation/EngineeringObjectState.cs
public sealed record EngineeringObjectState(
    Guid Id,
    string Kind,
    string? Identifier,
    string DisplayName,
    EngineeringObjectMetadata Metadata,
    LifecycleState Status,
    Guid? ParentId,
    bool IsDeleted,
    EngineeringObjectBomLineState BomLine,
    IReadOnlyList<EngineeringObjectTransitionState> History,
    IReadOnlyList<EngineeringObjectAttachmentState> Attachments,
    IReadOnlyDictionary<string, string?> TypeState);
```

Written by `JsonSerializer.Serialize(state)` with no options
(`EngineeringObjectStateStore.cs:54`) — `Status`, and every `History[].From`/`To`,
serialise as the `LifecycleState` ordinal.

### `EngineeringObjectState` — after `WP 16.3B`

```csharp
public sealed record EngineeringObjectState(
    int SchemaVersion,           // new — first component; 1 is the floor
    Guid Id,
    string Kind,
    string? Identifier,
    string DisplayName,
    EngineeringObjectMetadata Metadata,
    LifecycleState Status,
    Guid? ParentId,
    bool IsDeleted,
    EngineeringObjectBomLineState BomLine,
    IReadOnlyList<EngineeringObjectTransitionState> History,
    IReadOnlyList<EngineeringObjectAttachmentState> Attachments,
    IReadOnlyDictionary<string, string?> TypeState);
```

`EngineeringObjectBase.CaptureState()`
(`EngineeringObjectBase.cs:73-107`) gains one more argument:
`CurrentSchemaVersion` (a `const int`, `1` at first release, bumped only
when a real migration exists). Every other component is unchanged —
this is the only member `WP 16.3B` adds to the record itself.
`EngineeringObjectAttachmentState` and `EngineeringObjectBomLineState`
are **not** touched (`ADR-0120` Decision 3) — they version with their
parent.

### `SettingsDocument<T>`'s six object-shaped documents — after `WP 16.3B`

No change to `SettingsDocument<TDocument>` itself beyond an optional
constructor parameter (below). Each of the six DTOs *may* add its own
`SchemaVersion` when it first needs one — `WP 16.3B` adds the plumbing,
not a `SchemaVersion` property to a DTO that has no migration to run yet.
`ADR-0120` Decision 6 lists exactly which six, and which three are out of
scope.

## The read path

```
EngineeringObjectStateStore.Deserialise(objectId, json)
  1. JsonSerializer.Deserialize<EngineeringObjectState>(json, StateJsonOptions)
       StateJsonOptions = { Converters = { JsonStringEnumConverter } }
       — catch JsonException → log Warning, return null   (unchanged, TD-60)
  2. state.SchemaVersion <= 0 ? state with { SchemaVersion = 1 } : state
       — absent-property normalisation, explicit (ADR-0120 Decision 1)
  3. while a migration exists for (state.Kind, state.SchemaVersion):
       try   state = migration.Migrate(state) with SchemaVersion = FromVersion + 1
       catch → log Warning naming Id/Kind/stuck version, return null
  4. return state
```

Both existing callers — `FindAsync` (single object) and `ListAsync`
(startup rehydration, via `EngineeringObjectRehydrationService.RehydrateAsync`,
`EngineeringObjectRehydrationService.cs:69-151`) — already call this one
private method, so no caller changes.

```
SettingsDocument<TDocument>.LoadAsync
  1. JsonSerializer.Deserialize<TDocument>(json)  — catch JsonException → log, return null (unchanged)
  2. if TDocument declares SchemaVersion and a migration chain was supplied:
       normalise absent → 1, apply the chain the same way as step 3 above
  3. return document
```

## The write path

Unchanged in shape. `EngineeringObjectBase.PersistStateAsync`
(`EngineeringObjectBase.cs:172-175`) always calls
`store.SaveAsync(CaptureState(), ...)`, and `CaptureState()` always
stamps `CurrentSchemaVersion` — an object in memory has exactly one
shape, the current one, whether it arrived via a factory or a rehydrator.
**No migration ever runs on write.** `SaveAsync` serialises with the same
`StateJsonOptions` (`JsonStringEnumConverter`) so every new write uses
string-valued enums; nothing else changes.

## Migration mechanism

```csharp
namespace Tempest.Core.EngineeringDomain;

public interface IStateMigration
{
    string? Kind { get; }       // null = applies to every Kind (a common field)
    int FromVersion { get; }    // applied when state.SchemaVersion == FromVersion
    EngineeringObjectState Migrate(EngineeringObjectState state);
}

public interface IStateMigrationRegistry
{
    void Register(IStateMigration migration);
    IStateMigration? Find(string kind, int fromVersion);  // Kind-specific first, then common
}
```

`EngineeringObjectStateStore` takes an optional
`IStateMigrationRegistry? migrations = null` constructor parameter,
mirroring `ADR-0113`'s own optional-collaborator pattern for
`EngineeringDomainContext.ObjectStateStore` — a store built without one
runs steps 1-2 of the read path above and stops; every existing
hand-assembled test context and sample pipeline keeps compiling and
passing unchanged. Composition (`TempestHost` / `EngineeringWorkspaceComposer`)
registers `IStateMigrationRegistry` as a singleton and passes it to the
one real `EngineeringObjectStateStore`, the same way
`IEngineeringObjectRehydratorRegistry` is composed today.

A migration is written next to the type it affects — the same locality
`IRehydratable<T>`/`CaptureTypeState` already established — registered by
that Kind's own declaring class, in the same registration pass that
already calls `RegisterRehydrators`.

`SettingsDocument<TDocument>` gains one optional constructor parameter,
`IReadOnlyList<ISettingsMigration<TDocument>>? migrations = null`
(default: no migrations, no behaviour change), used only by whichever of
the six object-shaped consumers is the first to need one.

## Golden corpus

**Location:** `tests/Tempest.Core.Tests/EngineeringDomain/SchemaVersioning/GoldenCorpus/v1/*.json`
— one committed JSON file per representative record shape, written in
the **exact byte-for-byte shape `v1` ever produced** (numeric enums, no
`SchemaVersion` property at all — that is what `v1` looked like before
this ADR, and the corpus's whole job is to prove that shape still loads).

Minimum fixture set (each a real `EngineeringObjectState` a `v0.15.0`
build could have produced):

| File | What it proves |
|---|---|
| `part-minimal.json` | The smallest legal record: no attachments, no history, default BOM line. |
| `part-with-history.json` | Multiple `LifecycleTransitionState` entries, numeric `From`/`To`. |
| `part-with-attachment-no-hash.json` | An attachment predating `ADR-0114` — `ContentHash` absent. |
| `part-with-attachment-and-hash.json` | An attachment written after `ADR-0114` — `ContentHash` present. |
| `assembly-with-parent.json` | `ParentId` set, non-default structural state. |
| `project-with-typestate.json` | A non-empty `TypeState` dictionary, a second canonical Kind. |
| `deleted-object.json` | `IsDeleted: true` — a soft-deleted record must still load. |

A second directory, added only once the first real migration exists,
holds the equivalent `v2/` (or later) shapes the same way — each
version's corpus is additive; `v1/` is never edited once a `v2/` exists,
because editing it would stop it proving what `v1` actually produced.

**Build wiring:** `tests/Tempest.Core.Tests/Tempest.Core.Tests.csproj`
gains one `<None Include="EngineeringDomain/SchemaVersioning/GoldenCorpus/**/*.json" CopyToOutputDirectory="PreserveNewest" />`
item so the fixtures are plain files next to the test binaries at run
time, read with `Path.Combine(AppContext.BaseDirectory, "EngineeringDomain", "SchemaVersioning", "GoldenCorpus", "v1")` —
no embedded-resource indirection, matching how every other test in this
suite reads its own inputs directly.

## Test list (`WP 16.3B`)

All under `tests/Tempest.Core.Tests/EngineeringDomain/SchemaVersioning/`
unless noted.

- **`GoldenCorpusTests`** — parameterised over every file in `GoldenCorpus/v1/`:
  loads through the real `EngineeringObjectStateStore.Deserialise` path
  (via a real `IPersistenceStore` pointed at a temp copy, not a hand-built
  in-memory string) and asserts the result is non-null with
  `SchemaVersion == EngineeringObjectStateStore.CurrentSchemaVersion` — the
  standing regression check that must pass after every future bump, per
  `ADR-0120`/the Release Plan's own acceptance criterion.
- **`SchemaVersionDefaultingTests`** — a hand-built JSON string with no
  `SchemaVersion` property deserialises to `SchemaVersion == 1` (the
  explicit `<= 0` normalisation, `ADR-0120` Decision 1) and identically
  for `"SchemaVersion": 1` written out explicitly.
- **`EnumSerialisesAsStringTests`** — `SaveAsync` then a raw
  `IPersistenceStore.ReadAsync` (bypassing the store's own deserialiser)
  asserts the stored JSON contains `"Status":"Released"`, not a digit; a
  hand-built record with `"Status":3` still deserialises to
  `LifecycleState.Released` (the backward-compatible read, `ADR-0120`
  Decision 4).
- **`StateMigrationChainTests`** — a fake `IStateMigration` registered
  for `FromVersion: 1` on a test-only Kind is applied exactly once,
  in order, when a `v1` record for that Kind is read; a second migration
  chained at `FromVersion: 2` runs after the first, not instead of it;
  a common (`Kind: null`) migration runs for every Kind.
- **`BrokenMigrationIsSkippedNotFatalTests`** — the "deliberately-broken
  migration" acceptance test the Release Plan names: a migration
  registered for a Kind that throws is caught, the record is logged and
  skipped (`Deserialise` returns `null`), and — critically — reading the
  *next* record in a `ListAsync` batch still succeeds. A second variant:
  a record whose `SchemaVersion` is higher than any migration or the
  store's own `CurrentSchemaVersion` (a record from a *newer* build) is
  treated identically — skipped, not thrown.
- **`RehydrationSurvivesAStuckRecordTests`** (extends
  `EngineeringObjectRehydrationTests.cs`) — a rehydration batch containing
  one record with no migration path still returns every other object,
  and `EngineeringRehydrationResult` accounts for the stuck one without
  `IsComplete` silently reading `true`.
- **`SettingsDocumentSchemaVersionTests`** (new, under
  `tests/Tempest.Core.Tests/Settings/`) — the identical defaulting and
  migration-chain behaviour, proven once against a test-only DTO passed
  to `SettingsDocument<TestDto>`, not against all six real consumers
  individually — the six are proven by construction (they share the one
  generic type), not by six near-duplicate test classes.
- **A full-suite regression run** — every existing Core test green,
  unchanged, per the Release Plan's own acceptance line (3,088 tests);
  this is the proof that an additive, opt-in mechanism actually stayed
  opt-in.

## Files `WP 16.3B` touches

**`src/Tempest.Core/Persistence/`**
- No contract change. `IPersistenceStore`/`IBinaryPersistenceStore` are
  untouched — versioning is a concern of the JSON shape stored *through*
  them, not of the store itself.

**`src/Tempest.Core/EngineeringDomain/`**
- `Implementation/EngineeringObjectState.cs` — add `SchemaVersion` as the
  record's first component.
- `Implementation/EngineeringObjectStateStore.cs` — add
  `StateJsonOptions` (`JsonStringEnumConverter`), the `SchemaVersion`
  normalisation, and the migration-application loop inside `Deserialise`;
  add the optional `IStateMigrationRegistry?` constructor parameter.
- `Implementation/EngineeringObjectBase.cs` — `CaptureState()` stamps
  `CurrentSchemaVersion`.
- `Contracts/Rehydration.cs` (or a new sibling `Contracts/StateMigration.cs`
  — `WP 16.3B`'s own choice) — `IStateMigration`, `IStateMigrationRegistry`.
- Each discipline registry (`MechanicalObjectFactoryRegistry`,
  `DocumentObjectFactoryRegistry`, etc., per the table in
  `docs/architecture/Engineering Object Rehydration Architecture.md`) —
  a `RegisterStateMigrations` method alongside its existing
  `RegisterRehydrators`, populated only for a Kind that actually has one.

**`src/Tempest.Core/Settings/`**
- `SettingsDocument.cs` — optional `IReadOnlyList<ISettingsMigration<TDocument>>?`
  constructor parameter; the same explicit defaulting and chain
  application inside `LoadAsync`.

**`tests/Tempest.Core.Tests/`**
- `EngineeringDomain/SchemaVersioning/GoldenCorpus/v1/*.json` — new,
  committed fixtures (see above).
- `EngineeringDomain/SchemaVersioning/GoldenCorpusTests.cs`,
  `SchemaVersionDefaultingTests.cs`, `EnumSerialisesAsStringTests.cs`,
  `StateMigrationChainTests.cs`, `BrokenMigrationIsSkippedNotFatalTests.cs`
  — new.
- `EngineeringDomain/EngineeringObjectRehydrationTests.cs` — extended
  with the stuck-record rehydration case.
- `Settings/SettingsDocumentSchemaVersionTests.cs` — new.
- `Tempest.Core.Tests.csproj` — the `CopyToOutputDirectory` item for the
  corpus.

**Not touched:** `src/Tempest.App/`, `src/Tempest.Desktop/` (the six
`SettingsDocument<T>` object-shaped call sites there gain nothing until
one of them actually needs a `SchemaVersion` — the plumbing they'd use
lives entirely in `Tempest.Core.Settings`), and the three list-shaped
`SettingsDocument<T>` consumers (`MacroManager`, `FavouriteObjectsState`,
`RecentObjectsState`), which are explicitly out of scope (`ADR-0120`
Decision 6).

## What this does not attempt

- **Write-time migration or rewrite-on-touch.** Every write always
  writes the current shape (see "The write path," above); there is
  nothing to migrate on write.
- **Versioning `IPersistenceStore` collections themselves**, or any
  bulk, startup-time rewrite pass — rejected in `ADR-0120` on `TD-88`
  grounds (eager, whole-store startup cost).
- **The three list-shaped `SettingsDocument<T>` documents.** Deferred to
  whichever future Work Package first needs a breaking change to one of
  them (`ADR-0120` Decision 6, Future Considerations).
- **Write batching (`TD-86`) or lazy/paged rehydration (`TD-88`).**
  Unrelated costs, not reopened here.
- **A version bump itself.** `WP 16.3B` ships `CurrentSchemaVersion = 1`
  and the mechanism to move past it; it does not itself change any
  record's shape.

## Related Documents

- `ADR-0120` — the decision this document implements.
- `ADR-0113`, `ADR-0114`, `ADR-0116` — the durable-state, attachment, and
  rehydration decisions this extends.
- `docs/architecture/Engineering Object Rehydration Architecture.md` —
  the read path this design's migration step is inserted into.
- `docs/governance/Quality/Technical Debt Register.md` — `TD-87`.
- `docs/releases/v0.16.0/v0.16.0 Release Plan.md` — `WP 16.3A`/`WP 16.3B`.
