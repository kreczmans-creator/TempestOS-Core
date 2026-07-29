# ADR-0045: Audit Is a Durable, Queryable, Append-Only Record, Distinct From Logging and Diagnostics — Recording Model, Permission Gating, and Persistence Sufficiency

## Status

Accepted — `WP 6.5` (Audit Framework), 2026-07-29.

## Context

`v0.6.0`'s own architecture package anticipated this decision but
deliberately left it unratified pending `WP 6.5`'s own implementation
phase. `Required ADRs.md` named the core orthogonality question
(Logging vs. Diagnostics vs. Audit) as this Work Package's own required
ADR. Implementation surfaced three further genuine decisions the
Contract Review explicitly flagged as open: whether a `RecordAsync`
failure should be allowed to abort the action being audited or must be
isolated from it; whether the write path should be literally
fire-and-forget or awaited; and how `IAuditQuery` access should be
permission-gated. This Work Package was also explicitly tasked with
validating whether `WP 6.4`'s own Persistence abstraction
(`IPersistenceStore`) is sufficient for Audit's own needs, recording
whether `docs/releases/v0.6.0/Risk Register.md`'s `R8` can be retired.

## Decision

**Audit is implemented exactly as `Public Interface Catalogue.md`
drafted** — `IAuditRecord`, `IAuditRecorder`, `IAuditQuery`,
`AuditQueryCriteria` — with zero signature deviation. `IAuditRecord`
remains distinct from both `ILogger` (developer-facing, not
guaranteed durable) and `IDiagnosticsProvider` (a live snapshot of
*current* state) — Audit alone is the durable, queryable *history* of
attributable actions, exactly as `Required ADRs.md` anticipated.

**`RecordAsync` propagates failures; it is not fire-and-forget.** A
`PersistenceStoreUnavailableException` from the underlying store
propagates to `RecordAsync`'s own caller unchanged — never swallowed,
never isolated automatically. Whether a *specific* calling Work Package
(Reporting, the REST API, Licensing, Export/Import, an engineering
module) chooses to let that exception abort its own primary operation,
or to catch it and continue, is that calling Work Package's own risk
decision — not something `AuditRecorder` itself can or should decide
universally, since the right answer genuinely differs by caller (a
REST request recording a failed login attempt has different stakes
than a background report generation logging its own start). The
performance goal ("should not meaningfully slow down the action it is
recording") is met by keeping the write itself minimal — a single,
append-only file write, no read-before-write, no cache to invalidate —
not by discarding the returned `Task` and racing ahead, which would make
a genuine storage failure invisible to the exact system meant to catch
this class of gap.

**The current principal is resolved automatically, with a fail-open-to-a-named-sentinel default.**
`AuditRecorder.RecordAsync` reads `ICurrentPrincipalAccessor.Current`;
if no principal is established, `AuditRecorder.UnknownActorId`
(`"unknown"`) is recorded instead of failing the write. An audit record
with an unknown actor is judged more useful than no record at all —
this is the one place Audit deliberately does *not* fail closed,
because the alternative (refusing to record anything without an
established principal) would silently create gaps in the very audit
trail this framework exists to guarantee.

**`IAuditQuery.QueryAsync` is permission-gated**, via the same, single
enforcement point every other authorization check in this platform
uses (`IPermissionEvaluator.RequirePermission`, `ADR-0044`) — a new
permission, `AuditQuery.QueryPermission` (`"audit.query"`), must be
held by the current principal. If no principal is established, an
anonymous, zero-permission principal is checked instead of skipping the
check entirely — reusing `PermissionDeniedException`'s own existing
failure path rather than inventing a second "not authenticated" error
condition, mirroring the same pattern this Work Package's own
`AuditQuery` implementation already needed for consistency with
`ADR-0044`'s own precedent.

**Correlation identifiers are carried in `Detail`, under a well-known
key (`AuditRecorder.CorrelationIdDetailKey`), not as a dedicated
`IAuditRecord` property.** `Public Interface Catalogue.md`'s own
Versioning Policy for `IAuditRecord` already states `Detail`'s own
per-action content "may evolve without changing `IAuditRecord`
itself" — a correlation identifier is exactly this kind of evolution,
requiring no interface change at all.

**Persistence Validation.** `IPersistenceStore` is judged *adequate* for
this release's own correctness requirements — every approved
`IAuditQuery` filter (`ActorId`, `Action`, `From`, `To`) is fully,
correctly satisfiable via `ListKeysAsync` plus a per-key `ReadAsync`,
filtered client-side. **No extension to `IPersistenceStore` was made.**
This is a considered judgment, not an oversight: client-side filtering
has a real, disclosed performance characteristic (a query's own cost
scales linearly with the total number of stored audit records), but no
concrete scale or performance requirement exists anywhere in this
release's own approved scope to justify adding a query capability now —
doing so would be exactly the "speculative capability" this Work
Package's own instructions warn against introducing.
**`docs/releases/v0.6.0/Risk Register.md`'s `R8` is therefore not
retired** — its own anticipated limitation is confirmed exactly as
predicted, and it remains Open, with its own revisit trigger sharpened:
revisit when a real, measured performance problem or a concrete scale
requirement is named, not preemptively.

## Consequences

**Positive:**

- Every approved interface is implemented with zero deviation, so any
  future consumer (Reporting, the REST API, Licensing, Export/Import,
  an engineering module) can depend on `IAuditRecorder`/`IAuditQuery`
  with full confidence in their shape.
- The correlation-identifier convention requires no interface change,
  and is immediately usable by any caller today, simply by populating
  `Detail[AuditRecorder.CorrelationIdDetailKey]`.
- Reusing `IPermissionEvaluator.RequirePermission` for query gating
  means Audit's own security posture is enforced through the identical,
  single mechanism every other permission check in this platform uses —
  no second, parallel authorization concept was introduced.
- The Persistence-sufficiency judgment is explicit and disclosed, not
  silently assumed — a future reader can see exactly why no extension
  was made and exactly what would change that judgment.

**Negative:**

- A calling Work Package that does not itself decide how to handle a
  `RecordAsync` failure could let an audit-write outage silently abort
  an unrelated primary operation — this ADR names the tension and
  assigns the decision to each caller, but does not eliminate the risk
  of a caller getting it wrong.
- `UnknownActorId` records mean a compliance-sensitive consumer cannot
  assume every record has a genuinely identified actor — a real,
  disclosed limitation of this release's own local-only identity model
  (`ADR-0043`), not something Audit itself can fix.
- Query performance degrades linearly with total record count — fine at
  this release's own expected scale, but a future high-volume consumer
  (a long-running production deployment) could encounter a real,
  noticeable slowdown before any Work Package revisits `R8`.

## Alternatives Considered

**Extending Diagnostics to retain historical snapshots instead of
introducing Audit.** Rejected — `Diagnostics`'s entire design
(`ADR-0039`) is built around lazy, `Func<T>`-projected *current* state
with no persistence layer at all; retrofitting durability into it would
contradict its own founding ADR.

**Making `RecordAsync` genuinely fire-and-forget** (not awaited, or
internally swallowing its own failure). Rejected — this would make a
storage failure unobservable, directly contradicting the approved
Failure Behaviour ("an audit record that silently fails to record is a
worse outcome than a loud failure the caller must handle").

**Throwing when no principal is established, rather than recording
`UnknownActorId`.** Rejected — this would make Audit itself a source of
gaps in the very audit trail it exists to guarantee, whenever a caller
happens to invoke it outside an established-principal context.

**Adding a dedicated `CorrelationId` property to `IAuditRecord`.**
Rejected — `Detail`'s own documented extensibility already covers this
exact case; adding a new property would be an unnecessary change to an
approved interface for a need the existing contract already satisfies.

**Extending `IPersistenceStore` with a native query/filter method now.**
Rejected — no concrete scale or performance requirement exists to
justify it; `Detail`'s own client-side filtering satisfies every
approved `IAuditQuery` requirement correctly today. Revisit only when a
real need is demonstrated (see this Work Package's own Future
Capability Recommendations).

**Skipping the permission check when no principal is established**
(treating "no principal" as equivalent to "internal, trusted caller").
Rejected — this would be a fail-open default for the one operation
(`IAuditQuery`) this release's own architecture explicitly named as
needing permission-gating; reusing the zero-permission-principal
pattern preserves fail-closed behaviour uniformly.

## Related Documents

`docs/releases/v0.6.0/Required ADRs.md` (this decision's own anticipated
form); `Platform Service Contracts.md` (Audit's own 15-dimension
contract this ADR implements); `ADR-0041` (Persistence, reused not
reinvented); `ADR-0044` (the enforcement point Audit's own permission
gating reuses); `docs/releases/v0.6.0/Risk Register.md` (`R8`);
`docs/governance/Quality/Technical Debt Register.md`; `docs/academy/03
Work Packages/WP6.5-audit-framework-implementation.md`.
