# ADR-0041: A Shared Persistence Abstraction Serves Settings and Audit

## Status

Accepted — `WP 6.4` (Settings Framework), 2026-07-29.

## Context

`v0.6.0`'s own architecture package (`docs/releases/v0.6.0/Release
Architecture.md`, `Required ADRs.md`) anticipated this decision but
deliberately left it unratified pending `WP 6.4`'s own implementation
phase. Neither Settings nor the future Audit Framework (`WP 6.5`) has
anywhere to durably store its own state — nothing in this platform
persists anything since the bootstrap-era `JsonProjectRepository` went
dead (confirmed unreferenced since `WP 5.0D`). `Platform Service
Contracts.md`'s own Persistence section named the storage backend
itself ("file path, connection string, or equivalent... `WP 6.4`'s own
architecture phase should set a concrete target once a storage backend
is chosen") as a decision this Work Package's own implementation phase
must make.

## Decision

**`Tempest.Core.Persistence`/`IPersistenceStore` is established as part
of `WP 6.4`'s own scope**, exactly as `Required ADRs.md` anticipated —
implemented with zero deviation from `Public Interface Catalogue.md`'s
own drafted signature (`ReadAsync`, `WriteAsync`, `DeleteAsync`,
`ListKeysAsync`, scoped by `collection` and `key`).

**Storage backend: one file per `collection`/`key` pair, under a
configured root directory.** The root path is read once from
`IConfigurationProvider` (key `Persistence:RootPath`), defaulting to
`persistence-data` (relative to the working directory) if unconfigured
— the same "read once, from Configuration, with a sensible default"
convention `LoggerFactory` already established for
`Runtime:Logging:MinimumLevel`. Both `collection` and `key` are
percent-encoded (`Uri.EscapeDataString`) before becoming a path segment,
so an arbitrary caller-supplied name can never produce an invalid or
unintended file-system path — this was not named as a requirement in
`Platform Service Contracts.md`, but is a direct, necessary consequence
of choosing a file-backed implementation for an abstraction whose own
contract places no restriction on what characters a `collection`/`key`
may contain.

**Concurrency: a per-`collection`/`key` in-process asynchronous lock**
(`Tempest.Core.Concurrency.AsyncKeyedLock`, internal), acquired by every
`Read`/`Write`/`Delete` operation before touching the file system. This
satisfies `Platform Service Contracts.md`'s own Thread Safety
Expectations (concurrent writes to the same key never corrupt or
interleave; a concurrent read never observes a partially-written file)
without serialising access to two different keys against each other,
and without requiring any caller to hold a lock itself.

**Failure classification: any file-system exception during
`Read`/`Write`/`Delete`/`ListKeysAsync` is wrapped in
`PersistenceStoreUnavailableException`**, with the original exception
preserved as `InnerException` and logged at Warning before propagating —
exactly `Platform Service Contracts.md`'s own stated Failure Behaviour,
verified directly by three failure-injection tests that force a real
I/O failure (a blocking file lock, a file occupying a path a directory
needs) rather than simulating one through a fake.

**Settings (`WP 6.4`) is the only currently-existing consumer.** `WP 6.5`
(Audit) is expected to depend on this same abstraction, per this ADR's
own title and `Required ADRs.md`'s own anticipated form — not
implemented or verified by this Work Package, since `WP 6.5` has not
begun.

## Consequences

**Positive:**

- Neither Settings nor a future Audit Framework needed to invent its own
  incompatible storage mechanism — the exact avoidable architectural debt
  `Required ADRs.md` named this decision to prevent.
- The percent-encoding scheme makes the store safe against any
  `collection`/`key` string a caller might supply, including one
  containing path separators or reserved characters, without needing a
  restrictive validation rule on the public contract itself.
- Failure-injection tests exercise genuine OS-level failures (file
  locks, blocked directory creation), not a mocked failure path — a
  higher-confidence proof that `PersistenceStoreUnavailableException` is
  reachable in practice, not merely reachable in principle.

**Negative:**

- No querying beyond key lookup and full-collection key enumeration —
  disclosed explicitly in `Technical Debt Assessment.md`'s own
  anticipated debt, confirmed here as the actual shipped shape. `WP 6.5`
  (Audit)'s own filtered-query need (`IAuditQuery`) will need to either
  filter client-side over `ListKeysAsync`, or this abstraction will need
  to grow a query capability at that point — not resolved here.
- File-per-key storage means a `collection` with a very large number of
  keys results in a directory with an equally large number of files — no
  specific scale target was set (`Platform Service Contracts.md`'s own
  Performance Expectations named this as an open question); acceptable
  for this release's own scope (Settings' own key count is expected to
  be small — one entry per registered `ISettingDefinition`).

## Alternatives Considered

**Each of `WP 6.4` and `WP 6.5` building its own independent storage
mechanism.** Rejected — this is the exact scenario `Required ADRs.md`
itself named and rejected in advance: two incompatible, ad hoc
persistence layers for the same underlying need, discovered only after
both had already shipped.

**A single JSON file per collection, holding every key's value.**
Considered as an alternative to one file per key. Rejected: it would
require reading and rewriting the entire collection's own file on every
single-key write, directly working against the per-key concurrency
model this ADR's own Thread Safety Expectations require (two concurrent
writes to two different keys in the same collection would serialise
against each other unnecessarily, contradicting `Platform Service
Contracts.md`'s own explicit requirement that they must not).

**A global, single-collection lock, rather than a per-key lock.**
Rejected — this would serialise every operation against a `collection`,
including two writes to entirely different keys, an unnecessary and
explicitly disallowed bottleneck per this namespace's own Thread Safety
Expectations.

## Related Documents

`docs/releases/v0.6.0/Required ADRs.md` (this decision's own anticipated
form); `Platform Service Contracts.md` (Persistence's own 15-dimension
contract this ADR implements); `ADR-0042` (Settings, decided alongside
this one); `docs/governance/Quality/Technical Debt Register.md`;
`docs/releases/v0.6.0/Technical Debt Assessment.md` (the anticipated
"no native query/filter capability" debt this ADR confirms).
