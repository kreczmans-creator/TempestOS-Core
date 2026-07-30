# ADR-0042: Settings Is DI-Public and Distinct From Configuration

## Status

Accepted — `WP 6.4` (Settings Framework), 2026-07-29.

## Context

`v0.6.0`'s own architecture package anticipated this decision but
deliberately left it unratified pending `WP 6.4`'s own implementation
phase. Settings and Configuration (`WP 2.5`) sound similar enough that a
future reader could reasonably ask why both exist — `Public Interface
Catalogue.md` drafted `ISettingDefinition`/`ISettingsProvider`/
`ISettingsChangedEvent`, but left several concrete implementation
questions open: whether a read should be cached, whether a no-op write
(the same value as current) should still publish a change event, and
whether a setting needs a way to mark itself sensitive for logging and
future REST-exposed redaction.

## Decision

**Configuration remains read-only, immutable, loaded once at startup —
completely unchanged.** Settings is read-write, at runtime, implemented
with zero deviation from the approved `ISettingDefinition`/
`ISettingsProvider`/`ISettingsChangedEvent` signatures.

**`ISettingsProvider` is backed by the Persistence abstraction
(`ADR-0041`) plus an in-memory cache**, invalidated only by this
instance's own writes — satisfying `Platform Service Contracts.md`'s own
Performance Expectations (`GetValueAsync` as a likely hot-path call)
without hitting the underlying store on every read. A per-key
`AsyncKeyedLock` (the same internal utility `PersistenceStore` uses,
`ADR-0041`) serialises the "populate cache from storage" sequence in a
cache-miss `GetValueAsync` against the "write storage, update cache"
sequence in `SetValueAsync`, for the same key, so a slow concurrent
cache-miss read can never overwrite a newer write's own cache entry with
a stale value.

**`SetValueAsync` always publishes `ISettingsChangedEvent`, even when
the new value equals the current one** — exactly `Platform Service
Contracts.md`'s own explicit default ("should still publish, for
simplicity and predictability, unless a future ADR decides otherwise").
This ADR does not decide otherwise; the default stands.

**No sensitive-value flag is added to `ISettingDefinition` in this
release.** `Platform Service Contracts.md` named this as "a required
decision for `WP 6.4`'s own architecture phase" but did not mandate
implementing it — and adding a member to `ISettingDefinition` would be a
change to an approved public interface's own shape, which this Work
Package's own instructions require be minimised and justified, not made
for a speculative future need. Every setting change is logged at
Information level with both old and new values, unredacted, in this
release. This is a disclosed limitation (already named as a Future
Extension Point in `Platform Services Overview.md`), not a defect.

## Consequences

**Positive:**

- No existing consumer of `IConfigurationProvider` is affected in any
  way — its own immutability guarantee (Case Study 05) is untouched.
- The in-memory cache means a hot-path `GetValueAsync` call after the
  first read never touches the file system again until the next write —
  `SettingsProviderTests`' own unit tests isolate this behaviour from
  real file I/O entirely, using a hand-written `InMemoryPersistenceStore`
  test double, exactly to verify the provider's own caching and
  event-publication logic independent of `PersistenceStore`'s own,
  separately-tested storage concerns.
- The always-publish default is simple and predictable, exactly as
  `Platform Service Contracts.md` intended — a subscriber never needs to
  wonder whether a `SetValueAsync` call that happened to match the
  current value would silently not notify it.

**Negative:**

- Every setting change is logged with its full old and new value,
  unredacted — a real, disclosed risk if a future setting genuinely
  needs to hold a credential or API key. `WP 6.4`'s own scope does not
  include any setting of that kind, so this is accepted for now, not
  ignored; see this Work Package's own Technical Debt Assessment and
  Future Capability Recommendations.
- The in-memory cache means two `SettingsProvider` instances within the
  same process (which should never legitimately occur, since it is
  registered as an ordinary singleton) would not see each other's
  writes — not a real risk given the container's own singleton lifetime
  guarantee, but worth naming as a boundary of the cache's own
  correctness assumption.

## Alternatives Considered

**Extending `IConfigurationProvider` itself to permit runtime writes.**
Rejected — this would break every existing consumer's own assumption of
immutability (Case Study 05's own stated reasoning), a far larger blast
radius than introducing one new, narrowly-scoped service.

**No in-memory cache — read `IPersistenceStore` on every
`GetValueAsync` call.** Rejected as unnecessary I/O for an anticipated
hot-path call, per `Platform Service Contracts.md`'s own Performance
Expectations naming a cache as "a plausible implementation detail `WP
6.4`'s own architecture phase should consider."

**Comparing old and new values before publishing, suppressing a
no-op change's own event.** Rejected — `Platform Service Contracts.md`
already named the opposite as its own default, absent a future ADR
deciding otherwise; introducing that comparison now would be an
unrequested behavioural change beyond this Work Package's own scope.

**Adding an `IsSensitive` flag to `ISettingDefinition` now, to redact
logged values.** Rejected — no setting registered in this release
actually holds sensitive data, so building the redaction mechanism now
would be speculative, and doing so requires modifying an approved public
interface's own shape, which this Work Package's own instructions
require minimising absent genuine necessity. Deferred explicitly to a
future Work Package with a real, named sensitive-setting need.

## Related Documents

`docs/releases/v0.6.0/Required ADRs.md` (this decision's own anticipated
form); `Platform Service Contracts.md` (Settings' own 15-dimension
contract this ADR implements); `ADR-0041` (Persistence, decided
alongside this one); `docs/academy/05 Case Studies/` Case Study 05
(Configuration immutability); `docs/releases/v0.6.0/Platform Services
Overview.md` (the sensitive-value-redaction Future Extension Point this
ADR defers).
