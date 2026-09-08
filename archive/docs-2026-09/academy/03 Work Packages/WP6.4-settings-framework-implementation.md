# WP 6.4 — Settings Framework Implementation

## 1. Introduction

WP 6.4 delivers the Settings Framework — the second Work Package of the
Platform Services phase (`v0.6.0`) to ship real code, and the second to
be implemented ahead of its own nominal numeric order
(`WP 6.0` is listed first in `WorkPackages.md`), following
`Platform Service Implementation Order.md`'s own explicit recommendation.
As part of its own scope (`ADR-0041`), this Work Package also establishes
`Tempest.Core.Persistence` — the shared durable-storage abstraction the
release's own architecture package deferred to this Work Package to
build. Implemented in a single pass, directly against the already-
approved architecture and Contract Review packages — no separate
architecture phase, mirroring `WP 6.1`'s own precedent.

## 2. Purpose

To build `Tempest.Core.Settings` exactly as the approved architecture
specified — `ISettingDefinition`, `ISettingsProvider`,
`ISettingsChangedEvent` — and, as part of the same scope,
`Tempest.Core.Persistence`/`IPersistenceStore`, the abstraction
`Required ADRs.md` named this Work Package as responsible for
establishing on behalf of both Settings and the future Audit Framework
(`WP 6.5`); to wire both into the real, unmodified `TempestHost`; and to
do so without redesigning any approved architecture or changing any
approved public interface absent a genuine implementation defect.

## 3. Background

`WP 6.1` (Permissions & Identity) was implemented first, per
`Platform Service Implementation Order.md`'s own recommendation. `WP 6.4`
is the second Work Package in that same recommended first wave —
placed after `WP 6.1` in the sequencing rationale specifically so any
lesson from Identity's own early implementation could inform Settings'
own architecture-adjacent decisions before they were finalised
(`Platform Service Implementation Order.md`'s own "Why Settings Second,
Not Tied for First" reasoning). No such lesson turned out to be
necessary in practice — Settings' own implementation questions
(caching, event-publication defaults, storage backend) were independent
of Identity's own.

## 4. The Problem

Two things needed to exist, neither of which this platform has ever
had:

1. **Durable, platform-owned storage.** Nothing in this codebase
   persists anything since the bootstrap-era `JsonProjectRepository`
   went dead (confirmed unreferenced since `WP 5.0D`, re-confirmed
   directly here). Settings cannot be "runtime-mutable... surviving a
   process restart" without one.
2. **Runtime-mutable configuration, distinct from `IConfigurationProvider`.**
   Configuration is deliberately immutable once built (Case Study 05).
   Settings needed its own service, with its own storage, its own
   change-notification path, and its own explicit boundary against
   Configuration so the two are never confused.

## 5. The Design

**Implemented exactly as drafted, zero signature deviation:**
`ISettingDefinition` (`Key`, `DisplayName`, `DefaultValue`),
`ISettingsProvider` (`RegisterDefinition`, `GetValueAsync`,
`SetValueAsync`), `ISettingsChangedEvent : IEvent` (`Key`, `OldValue`,
`NewValue`). `IPersistenceStore` (`ReadAsync`, `WriteAsync`,
`DeleteAsync`, `ListKeysAsync`) likewise implemented with zero
deviation.

**Persistence's storage backend: one file per `collection`/`key` pair**,
under a configurable root path (`Persistence:RootPath`, defaulting to
`persistence-data`), both segments percent-encoded
(`Uri.EscapeDataString`) to guarantee a valid file-system path regardless
of what a caller supplies. A small, internal, shared
`Tempest.Core.Concurrency.AsyncKeyedLock` serialises operations against
the same key without serialising access to two different keys — used by
both `PersistenceStore` and `SettingsProvider`, placed in its own small,
neutral namespace once it became clear both services genuinely needed
the identical pattern.

**Settings' own in-memory cache**, invalidated only by this instance's
own writes, sits over `IPersistenceStore` — satisfying the Contract
Review's own Performance Expectations (`GetValueAsync` as a likely
hot-path call) without hitting the file system on every read. The same
per-key lock serialises a cache-miss read's "populate from storage"
sequence against a write's "store, then update cache" sequence, closing
a genuine race a naive implementation would have: a slow concurrent
cache-miss read could otherwise overwrite a just-written cache entry
with a stale value it read moments earlier.

**`SetValueAsync` always publishes `ISettingsChangedEvent`**, even when
the new value equals the current one — the Contract Review's own
explicit default, not overridden here. Publication happens through
`IEventBus.PublishAsync<ISettingsChangedEvent>` with the interface as
the explicit generic type argument — proven to dispatch correctly to a
subscriber that calls `Subscribe<ISettingsChangedEvent>`, under the
Event Bus's own exact-type dispatch model (`AT-03`), since both sides
agree on the same compile-time type.

**No sensitive-value flag was added to `ISettingDefinition`.** The
Contract Review named this as "a required decision for `WP 6.4`'s own
architecture phase" but did not mandate building it, and no setting
registered in this release actually holds sensitive data. Adding a
member to an approved interface for a speculative future need would
itself have been the kind of unrequested architectural change this Work
Package's own instructions require minimising.

**`SettingsSampleModule`** (`Tempest.Samples`, the ninth production
sample module) registers a setting definition, subscribes to
`ISettingsChangedEvent`, and registers two commands (get/set)
demonstrating the Command Framework and Settings interacting, and
proving the value survives across two independent, sequential pipelines
over the same underlying storage — a direct, tested proof of
Persistence's own "survives an ordinary process restart" requirement.

## 6. Alternatives Considered

See `ADR-0041` and `ADR-0042` for the complete reasoning. In summary: a
single JSON file per collection was rejected because it would force a
full-collection rewrite on every single-key write, directly working
against the per-key concurrency model the Contract Review requires; a
global, collection-wide lock was rejected as an unnecessary bottleneck
for the same reason; extending `IConfigurationProvider` itself to permit
runtime writes was rejected because it would break every existing
consumer's own immutability assumption; suppressing a no-op write's own
change event was rejected because the Contract Review already named the
opposite as its own default; and adding an `IsSensitive` flag now was
rejected as a speculative interface change for a need this release does
not have.

## 7. Why This Solution Was Chosen

Every approved interface is implemented with zero deviation, so `WP 6.5`
(Audit) can depend on `IPersistenceStore` with full confidence in its
shape. The per-key `AsyncKeyedLock`, shared between two services rather
than duplicated, follows `Reuse Before Invention` applied at the exact
moment a second real consumer existed — not before, and not left
duplicated after. The in-memory cache and always-publish default both
directly satisfy explicit Contract Review requirements rather than
introducing new, undiscussed behaviour.

## 8. Architectural Principles

- **Reuse Before Invention** — the shared `AsyncKeyedLock`; the
  Persistence abstraction itself, reused by Settings rather than each
  building its own storage.
- **Fail Loudly, Not Silently** — every Persistence I/O failure becomes
  `PersistenceStoreUnavailableException`, verified by tests that force a
  genuine OS-level failure (a file lock, a blocked directory path), not
  a simulated one.
- **Minimise Deviation From Approved Contracts** — the deliberate choice
  not to add a sensitive-value flag, disclosed as a limitation rather
  than quietly worked around.
- **Verify Before Trusting a Tentative Suggestion** — the in-memory
  cache's own correctness (the per-key lock closing the stale-cache
  race) was reasoned through explicitly, not assumed safe by default.

## 9. Files Added

`src/Tempest.Core/Persistence/IPersistenceStore.cs`;
`src/Tempest.Core/Persistence/PersistenceStore.cs`;
`src/Tempest.Core/Persistence/PersistenceException.cs`;
`src/Tempest.Core/Persistence/PersistenceStoreUnavailableException.cs`;
`src/Tempest.Core/Concurrency/AsyncKeyedLock.cs`;
`src/Tempest.Core/Settings/ISettingDefinition.cs`;
`src/Tempest.Core/Settings/SettingDefinition.cs`;
`src/Tempest.Core/Settings/ISettingsProvider.cs`;
`src/Tempest.Core/Settings/SettingsProvider.cs`;
`src/Tempest.Core/Settings/ISettingsChangedEvent.cs`;
`src/Tempest.Core/Settings/SettingsChangedEvent.cs`;
`src/Tempest.Core/Settings/SettingsException.cs`;
`src/Tempest.Core/Settings/DuplicateSettingDefinitionException.cs`;
`src/Tempest.Core/Settings/SettingNotFoundException.cs`;
`src/Samples/Tempest.Samples/SettingsSampleModule.cs`;
`src/Samples/Tempest.Samples/GetSampleSettingCommand.cs`;
`src/Samples/Tempest.Samples/GetSampleSettingCommandHandler.cs`;
`src/Samples/Tempest.Samples/SetSampleSettingCommand.cs`;
`src/Samples/Tempest.Samples/SetSampleSettingCommandHandler.cs`;
`tests/Tempest.Core.Tests/Persistence/PersistenceStoreTests.cs`;
`tests/Tempest.Core.Tests/Settings/InMemoryPersistenceStore.cs`;
`tests/Tempest.Core.Tests/Settings/FailingPersistenceStore.cs`;
`tests/Tempest.Core.Tests/Settings/RecordingSettingsChangedEventHandler.cs`;
`tests/Tempest.Core.Tests/Settings/SettingsProviderTests.cs`;
`tests/Tempest.Core.Tests/Settings/SettingDefinitionTests.cs`;
`tests/Tempest.Core.Tests/Settings/SettingsChangedEventTests.cs`;
`tests/Tempest.Core.Tests/Settings/ExceptionTests.cs`;
`tests/Tempest.Core.Tests/Runtime/SettingsHostRegistrationTests.cs`;
`tests/Tempest.Core.Tests/Samples/SettingsSampleModuleIntegrationTests.cs`;
`docs/adr/ADR-0041-shared-persistence-abstraction.md`;
`docs/adr/ADR-0042-settings-is-di-public-distinct-from-configuration.md`;
this retrospective. **Modified:**
`src/Tempest.Core/Runtime/TempestHost.cs` (registration);
`tests/Tempest.Core.Tests/Samples/ClockModuleDiscoveryTests.cs`
(sample-module count, 8 → 9).

## 10. Trade-offs

- **No native query/filter capability on `IPersistenceStore`** beyond
  key lookup and full-collection enumeration — disclosed explicitly in
  `ADR-0041` and confirmed in `docs/releases/v0.6.0/Risk Register.md`'s
  own `R8`. Whether this suffices for `WP 6.5` (Audit)'s own
  `IAuditQuery` needs remains open until that Work Package begins.
- **No sensitive-value redaction.** Every setting change is logged with
  both old and new values, unredacted — acceptable only because no
  setting registered in this release holds sensitive data.
- **File-per-key storage** means a collection with a very large number
  of keys produces an equally large number of files — no specific scale
  target was set; acceptable for Settings' own expected key count
  (one entry per registered `ISettingDefinition`).
- **The in-memory cache assumes exactly one `SettingsProvider` instance
  per process** (guaranteed by its own singleton registration) — not a
  real risk given the container's own lifetime guarantee, but a named
  boundary of the cache's own correctness assumption.

## 11. Common Mistakes

- **Assuming Persistence needs a database or a serialization library**
  — its own scope is deliberately minimal (no schema, no querying beyond
  key lookup); a plain, percent-encoded, file-per-key store satisfies it
  completely.
- **Assuming a global lock is sufficient for Persistence's own
  concurrency requirement** — the Contract Review explicitly requires
  that two different keys never serialise against each other; only a
  per-key lock satisfies this.
- **Assuming `SetValueAsync` should skip publishing when the new value
  matches the current one** — the Contract Review's own explicit default
  is the opposite; suppressing publication would be an unrequested
  behavioural change.
- **Assuming Settings should read `IConfigurationProvider` for its own
  default values** — a setting's default is supplied in code, by
  whichever module registers its own `ISettingDefinition`, never from
  Configuration.

## 12. Future Evolution

A query/filter capability on `IPersistenceStore`, if `WP 6.5` (Audit)
needs one beyond client-side filtering; a sensitive-value flag on
`ISettingDefinition`, once a real sensitive setting is named; per-
principal (rather than global) settings, once Identity & Permissions
matures; a settings-management REST surface, once `WP 6.3` exists — all
named explicitly as future, separately-scoped Work Package
responsibilities, not designed now.

## 13. Key Takeaways

1. A shared internal utility (`AsyncKeyedLock`) is worth promoting to
   its own small, neutral namespace the moment a second real consumer
   needs the identical pattern — not before, and not left duplicated
   after.
2. Two explicit defaults a Contract Review names in advance ("cache
   invalidated on write," "always publish, even for a no-op write") are
   not suggestions to reconsider during implementation — they are
   decisions already made, to be implemented as stated unless a genuine
   defect is found.
3. Declining to add a member to an approved interface for a
   speculative future need (the sensitive-value flag) is itself a
   legitimate architectural decision, worth recording with the same
   care as an interface change would receive.

## Architectural Debt Assessment

`R4` (`docs/releases/v0.6.0/Risk Register.md`) — **Partially Retired.**
The Persistence abstraction now exists, exactly as recommended; whether
`WP 6.5` actually reuses it (rather than inventing its own) remains the
only residual risk, since that Work Package has not begun. `R8` —
**Confirmed, not retired.** The anticipated "minimal, key-lookup-only"
limitation shipped exactly as predicted; this risk stays open until
`WP 6.5` actually attempts to build `IAuditQuery` against this shape. No
new Technical Debt Register item was found stale or drifted during this
Work Package's own repository review.

## Observations

No pre-existing governance drift was found during this Work Package's
own repository review beyond the expected, recurring maintenance point
every sample-module-adding Work Package encounters:
`ClockModuleDiscoveryTests.cs`'s own assembly-wide discovery test
required updating for the ninth sample module, exactly as every prior
sample-module-adding Work Package (`WP 4.4E`, `WP 5.0B`, `WP 5.1B`,
`WP 5.2`, `WP 6.1`) has had to update the same test in turn.

## Related Documents

`docs/releases/v0.6.0/Release Architecture.md` and companions (the
architecture package this Work Package implemented); `Platform Service
Contracts.md` and companions (the Contract Review package); `ADR-0041`;
`ADR-0042`; `docs/architecture/Platform Service Map.md` (Persistence and
Settings entries); `docs/releases/v0.6.0/Risk Register.md` (`R4`, `R8`);
`docs/academy/05 Case Studies/` Case Study 05 (Configuration
immutability); `docs/academy/03 Work Packages/
WP6.1-permissions-and-identity-implementation.md` (the precedent this
Work Package's own single-pass implementation approach follows).
