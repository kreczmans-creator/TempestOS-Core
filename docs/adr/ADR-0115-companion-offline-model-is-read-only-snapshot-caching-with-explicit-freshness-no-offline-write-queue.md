# ADR-0115: Companion Offline Model Is Read-Only Snapshot Caching With Explicit Freshness — No Offline Write Queue

## Status

Accepted — `WP 14.0A` (TempestOS Companion — Mobile Companion
Application), 2026-08-28.

Defines the Companion's entire connectivity/offline architecture. The
platform side is untouched: TempestOS has no synchronisation semantics
today (`FCR-0022` Cloud Synchronisation and `FCR-0023` Offline
Synchronisation are both unstarted), and this ADR deliberately does not
invent server-side sync to serve one client.

## Context

A phone is sometimes offline, and an engineering companion that goes
blank whenever connectivity drops is useless in exactly the workshop and
site situations it exists for. But the platform is the system of record
for engineering IP — the commissioning brief is explicit: *"Do not
pretend that the phone has authoritative data when it does not"* and *"Do
not implement a simplistic 'save locally and hope it syncs' model."*

The platform offers nothing to build offline writes on honestly: no
server-side operation queue, no conflict detection, no idempotency keys,
no synchronisation vocabulary of any kind. Requirements concurrency
(`ADR-0060`) is optimistic and in-process; nothing exposes it over the
wire.

## Decision

**The Companion caches exactly one thing — the last successful response
per query endpoint, stamped with when it was fetched — and every screen
discloses one of four explicit freshness states: `Live`, `Cached`,
`Stale` (past a 15-minute threshold), or `Unavailable`. Writes are never
queued: a quick action either reaches the authoritative platform now, or
it fails visibly, immediately. There is no offline write path at all —
recorded as deliberate trade-off `AT-24`, not as debt.**

### The fetch-with-fallback state machine

`CompanionDataService` implements one sequence for every endpoint: try
the live platform; on success, store the snapshot (`SnapshotCache`, one
JSON file per endpoint under the Companion's own app-data folder) and
report `Live`; on an unreachable/server failure, fall back to the stored
snapshot as `Cached` or `Stale` by age; report `Unavailable` only when
nothing was ever stored. A corrupt cache file reads as "no snapshot" —
the offline path never crashes the app. Reconnection needs no ceremony:
the next successful fetch is `Live` and replaces the snapshot.

### Authorization failures fail closed, past the cache

A `401`/`403` never falls back to the cache: a caller the platform just
refused must not continue reading previously cached engineering data.
The result is `Unavailable` with the refusal's reason — the security
review's one non-negotiable interaction between the offline model and
the identity model.

### Freshness is disclosed, not implied

Every non-`Live` screen renders a `FreshnessBanner` naming the state, the
fetch time, and why live data is unavailable — text plus colour, never
colour alone. The app bar's connection pill (`LIVE`/`OFFLINE`) reflects
`CompanionDataService`'s connection-transition events. `Stale` is a
disclosure threshold, not an expiry: old data remains visible, flagged,
because old awareness beats no awareness — the phone's claim is only ever
"fetched from the authoritative platform at this moment", made visible.

### No offline writes, by design (`AT-24`)

A queued offline write requires idempotency, conflict detection, and
server-side authority semantics the platform does not have; faking them
client-side manufactures a second source of truth on the least trusted,
most losable device in the system. The Companion's mutations are
observe→decide→act against the live platform only. Revisit trigger:
real platform synchronisation semantics (`FCR-0022`/`FCR-0023`).

### Local data hygiene

The cache and settings hold no credential (none exists in the platform's
identity model to hold). Cached snapshots are engineering data, so the
More page's "Clear Local Data" removes every snapshot — the
lost/lent-device path — and clearing is confirm-gated.

## Consequences

**Positive:**

- Offline behaviour is honest by construction: every screen states
  exactly what it knows and how old that knowledge is.
- There is nothing to conflict, merge, or replay — the cache only ever
  holds what the authoritative platform already served, so the platform's
  system-of-record status is structurally unthreatened.
- The whole model is client-side; no platform service changed for it,
  and future real sync (`FCR-0022`/`FCR-0023`) replaces a small, isolated
  layer.
- The state machine is a plain, clock-injectable class — proven by unit
  tests across every transition, plus a real-server shutdown/fallback
  integration test.

**Negative:**

- A user cannot complete an action offline and have it apply later — the
  action must be re-initiated once connected. This is the accepted cost
  of never diverging from the system of record (`AT-24`).
- Cached engineering data rests on the device's own filesystem
  protections (no app-level encryption) — bounded today by the loopback
  deployment reality (`TD-58`) and named in the security review as a
  precondition for any off-box future.
- The 15-minute staleness threshold is a fixed heuristic, not yet a user
  setting.

## Alternatives Considered

**An offline write queue with retry.** Rejected: without server-side
idempotency and conflict semantics, a replayed queue silently applies
stale intent to a moved world — the exact "save locally and hope"
anti-model the brief prohibits. Building the server semantics for it is
`FCR-0022`/`FCR-0023`-scale work, far beyond a client Work Package.

**A local database (SQLite) mirroring domain objects.** Rejected: a
queryable local mirror is a second source of truth in embryo, invites
client-side domain logic over stale rows, and buys nothing for a client
whose screens render whole summaries the API already shapes.

**No cache at all (online-only).** Rejected: a companion that goes blank
in a workshop with weak connectivity fails its one purpose; last-known
awareness with explicit staleness is strictly more honest than a spinner.

**Time-based cache expiry (delete past a TTL).** Rejected: deleting old
data converts "old awareness, disclosed" into "no awareness" — `Stale` as
a disclosure state preserves utility without overclaiming currency.

## Related Documents

`ADR-0113` (the Companion boundary), `ADR-0114` (the query surface this
caches), `ADR-0060` (the platform's only concurrency vocabulary, not
exposed over the wire), `docs/governance/Future Capability Register.md`
(`FCR-0022`, `FCR-0023`),
`docs/governance/Quality/Technical Debt Register.md` (`AT-24`, `TD-58`),
`docs/architecture/TempestOS Companion Architecture.md`,
`tests/Tempest.Companion.Tests/CompanionDataServiceTests.cs` and
`CompanionIntegrationTests.cs` (the state machine's own proof, including
the real-server shutdown fallback).
