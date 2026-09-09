# WP 6.5 — Audit Framework Implementation

## 1. Introduction

WP 6.5 delivers the Audit Framework — the third Work Package of the
Platform Services phase (`v0.6.0`) to ship real code, and the third to
be implemented ahead of its own nominal numeric order (`WP 6.0` is
listed first in `WorkPackages.md`), following `Platform Service
Implementation Order.md`'s own recommendation. Implemented in a single
pass, directly against the already-approved architecture and Contract
Review packages — no separate architecture phase, mirroring `WP 6.1`'s
and `WP 6.4`'s own precedent. This Work Package also carried an explicit
mandate `WP 6.4` did not: validating whether the Persistence abstraction
that release established is actually sufficient, and recording the
answer plainly.

## 2. Purpose

To build `Tempest.Core.Audit` exactly as the approved architecture
specified — `IAuditRecord`, `IAuditRecorder`, `IAuditQuery`,
`AuditQueryCriteria` — reusing `WP 6.4`'s own `IPersistenceStore` rather
than introducing a second storage mechanism; to resolve the three
implementation questions the Contract Review explicitly left open
(failure-propagation model, permission-gating design, correlation
identifiers); and to perform and document a genuine Persistence
sufficiency validation, recording whether `docs/releases/v0.6.0/Risk
Register.md`'s `R8` can be retired.

## 3. Background

`WP 6.1` (Permissions & Identity) and `WP 6.4` (Settings Framework) were
both already implemented, each ahead of its own nominal order, per
`Platform Service Implementation Order.md`'s own recommendation. `WP 6.5`
is the natural next step in that same order: it is the second named
consumer of the Persistence abstraction `ADR-0041` anticipated, and its
own `IAuditQuery` permission-gating directly depends on `WP 6.1`'s own
`IPermissionEvaluator`. Both dependencies were already real, tested code
by the time this Work Package began — not merely approved design.

## 4. The Problem

Three things needed to exist, plus one validation this Work Package was
specifically tasked with performing:

1. **A durable, queryable history of actions** — nothing in this
   codebase records "who did what, when" durably; Logging is not
   guaranteed durable, and Diagnostics is a live snapshot of *current*
   state only.
2. **Automatic attribution** — an audit record needs to know who acted,
   without every caller manually supplying an actor id.
3. **Permission-gated querying** — an audit trail readable by anyone is
   itself a disclosure risk (`Platform Service Contracts.md`'s own
   Security Considerations).
4. **Persistence Validation** — this Work Package's own explicit
   mandate: confirm or deny whether `WP 6.4`'s `IPersistenceStore` is
   adequate for Audit's own needs, and record the answer, not leave it
   implicit.

## 5. The Design

**Implemented exactly as drafted, zero signature deviation:**
`IAuditRecord` (`ActorId`, `Action`, `OccurredAt`, `Detail`),
`IAuditRecorder.RecordAsync`, `IAuditQuery.QueryAsync`,
`AuditQueryCriteria` (`ActorId`, `Action`, `From`, `To`, all optional).

**Recording model (`ADR-0045`):** the current principal is resolved
automatically via `ICurrentPrincipalAccessor`; if none is established,
`AuditRecorder.UnknownActorId` (`"unknown"`) is recorded rather than
failing the write — an audit record with an unknown actor is judged
more useful than a missing one. `RecordAsync` is awaited, not literally
fire-and-forget: a storage failure must remain observable, so the
Contract Review's own "should not meaningfully slow the action down"
goal is met by keeping the write itself minimal (a single, append-only
file write), not by discarding the task.

**Permission gating (`ADR-0045`, reusing `ADR-0044`):** every
`IAuditQuery.QueryAsync` call requires a new permission,
`AuditQuery.QueryPermission` (`"audit.query"`), checked through the
existing, single enforcement point. If no principal is established, an
anonymous, zero-permission principal is checked instead of skipping the
check — reusing `PermissionDeniedException`'s own existing failure
path rather than inventing a second "not authenticated" condition.

**Correlation identifiers** are carried in `Detail`, under a documented,
well-known key (`AuditRecorder.CorrelationIdDetailKey`) — no change to
`IAuditRecord`'s own approved shape, since that contract's own
Versioning Policy already states `Detail`'s content "may evolve without
changing `IAuditRecord` itself."

**Storage:** each record is serialised to JSON (`System.Text.Json`,
already used elsewhere in this codebase — no new dependency) and stored
under its own `IPersistenceStore` collection (`"Audit"`, distinct from
Settings' own `"Settings"`), keyed by a UTC-ticks-plus-random-component
composite, guaranteeing no two records ever collide.

**Persistence Validation, performed and documented, not assumed:**
`IAuditQuery.QueryAsync` filters client-side, over `ListKeysAsync` plus
a per-key `ReadAsync` — proven, by direct test, to correctly satisfy
every approved filter (`ActorId`, `Action`, `From`, `To`), individually
and in combination. **Judgment: adequate for this release's own
correctness needs; not extended.** No concrete scale or performance
requirement exists anywhere in this release's approved scope to justify
adding a native query capability to `IPersistenceStore` now — doing so
would be the "speculative capability" this Work Package's own
instructions warned against. `docs/releases/v0.6.0/Risk Register.md`'s
`R8` is therefore confirmed a second time, not retired, with its
revisit trigger sharpened to "a real, measured performance problem or a
concrete scale requirement," and `docs/governance/Quality/Technical
Debt Register.md` gained a new, permanent item (`TD-12`) recording the
same characteristic as an ongoing, cross-release concern rather than a
release-scoped risk alone.

**`AuditSampleModule`** (`Tempest.Samples`, the tenth production sample
module) establishes its own principal, records an action during its own
initialisation, and registers two commands (record/query) demonstrating
both the denied-by-default and granted query paths, mirroring
`IdentitySampleModule`'s and `SettingsSampleModule`'s own conventions —
deliberately independent of either, so every sample module remains
usable on its own.

## 6. Alternatives Considered

See `ADR-0045` for the complete reasoning. In summary: extending
Diagnostics to retain historical snapshots was rejected as contradicting
its own founding design (`ADR-0039`); making `RecordAsync` genuinely
fire-and-forget was rejected because it would make a storage failure
unobservable; throwing when no principal is established was rejected as
creating gaps in the very audit trail Audit exists to guarantee; adding
a dedicated `CorrelationId` property to `IAuditRecord` was rejected
since `Detail`'s own documented extensibility already covers it; and
extending `IPersistenceStore` with a native query capability now was
rejected as unjustified speculation absent any concrete scale need.

## 7. Why This Solution Was Chosen

Every approved interface is implemented with zero deviation, so any
future consumer (Reporting, the REST API, Licensing, Export/Import, an
engineering module) can depend on `IAuditRecorder`/`IAuditQuery` with
full confidence in their shape. Reusing `IPermissionEvaluator` for query
gating and `IPersistenceStore` for storage means Audit introduces no
second authorization mechanism and no second storage mechanism — both
explicitly required by this Work Package's own brief. The Persistence
Validation was performed as a real, evidence-based check (a full
filter-correctness test suite), not asserted from reasoning alone.

## 8. Architectural Principles

- **Reuse Before Invention** — Persistence, the Command Framework, the
  permission-evaluation enforcement point: nothing new was invented
  where an existing, proven mechanism already served.
- **Fail Loudly for Storage, Fail Softly for Attribution** — a storage
  failure always propagates; a missing principal degrades to a named
  sentinel rather than blocking the record entirely — two different
  failure philosophies, each matched deliberately to what's actually at
  stake in that specific case.
- **Validate, Don't Assume, Especially When Explicitly Asked To** — the
  Persistence Validation this Work Package's own brief required was
  performed as a real test suite proving correctness, and the
  performance trade-off was named explicitly rather than glossed over.
- **Disclose Confirmed Limitations as Permanent Debt, Not Just
  Release-Scoped Risk** — `TD-12` exists precisely because a limitation
  confirmed as real, not merely anticipated, deserves to outlive the
  release-scoped Risk Register that first named it.

## 9. Files Added

`src/Tempest.Core/Audit/IAuditRecord.cs`;
`src/Tempest.Core/Audit/AuditRecord.cs`;
`src/Tempest.Core/Audit/AuditException.cs`;
`src/Tempest.Core/Audit/AuditQueryCriteria.cs`;
`src/Tempest.Core/Audit/IAuditRecorder.cs`;
`src/Tempest.Core/Audit/AuditRecorder.cs`;
`src/Tempest.Core/Audit/IAuditQuery.cs`;
`src/Tempest.Core/Audit/AuditQuery.cs`;
`src/Tempest.Core/Audit/AuditRecordDto.cs`;
`src/Samples/Tempest.Samples/AuditSampleModule.cs`;
`src/Samples/Tempest.Samples/RecordSampleAuditActionCommand.cs`;
`src/Samples/Tempest.Samples/RecordSampleAuditActionCommandHandler.cs`;
`src/Samples/Tempest.Samples/QuerySampleAuditRecordsCommand.cs`;
`src/Samples/Tempest.Samples/QuerySampleAuditRecordsCommandHandler.cs`;
`tests/Tempest.Core.Tests/Audit/InMemoryPersistenceStore.cs`;
`tests/Tempest.Core.Tests/Audit/FailingPersistenceStore.cs`;
`tests/Tempest.Core.Tests/Audit/AuditRecordTests.cs`;
`tests/Tempest.Core.Tests/Audit/AuditQueryCriteriaTests.cs`;
`tests/Tempest.Core.Tests/Audit/AuditRecorderTests.cs`;
`tests/Tempest.Core.Tests/Audit/AuditQueryTests.cs`;
`tests/Tempest.Core.Tests/Audit/ExceptionTests.cs`;
`tests/Tempest.Core.Tests/Runtime/AuditHostRegistrationTests.cs`;
`tests/Tempest.Core.Tests/Samples/AuditSampleModuleIntegrationTests.cs`;
`docs/adr/ADR-0045-audit-is-durable-queryable-distinct-from-logging.md`;
this retrospective. **Modified:**
`src/Tempest.Core/Runtime/TempestHost.cs` (registration only);
`tests/Tempest.Core.Tests/Samples/ClockModuleDiscoveryTests.cs`
(sample-module count, 9 → 10);
`tests/Tempest.Core.Tests/Runtime/SettingsHostRegistrationTests.cs`
(bug fix — see Section 11).

## 10. Trade-offs

- **`docs/releases/v0.6.0/Risk Register.md`'s `R8` remains Open.** The
  Persistence Validation this Work Package performed concluded
  "adequate, not extended" — a considered judgment, not a defect, but
  the underlying linear-scan query cost is real and now permanently
  tracked (`TD-12`).
- **`UnknownActorId` records mean not every record has a genuinely
  identified actor** — a direct, disclosed consequence of `WP 6.1`'s own
  local-only identity model (`ADR-0043`), not something Audit itself can
  fix.
- **Whether a caller's own `RecordAsync` failure should abort its own
  primary operation is left to each caller** — `AuditRecorder` itself
  only guarantees the failure propagates; it cannot and should not
  decide universally how every future consumer (Reporting, the REST
  API, Licensing, Export/Import, an engineering module) should react.

## 11. Common Mistakes

- **Assuming this Work Package retired `docs/releases/v0.6.0/Risk
  Register.md`'s `R8`** because it performed the Persistence Validation
  that risk called for — the validation concluded "adequate, not
  extended," which is a *confirmation*, not a retirement; read
  `ADR-0045`'s own Persistence Validation section before assuming
  otherwise.
- **Assuming `RecordAsync` is safe to call without awaiting** — it is
  not fire-and-forget; a storage failure is only observable if the
  returned `Task` is awaited.
- **Assuming an unrecognised or absent principal should block a
  recording** — `AuditRecorder` deliberately does not fail closed here;
  only `AuditQuery`'s own permission check does.
- **A genuine, found-not-invented lesson**: a `using`-scoped
  `TempDirectory` declared in a non-`async` test method that merely
  `return`s an awaited call's `Task` gets disposed the instant the
  method returns — not after the returned `Task` completes. This
  affected `WP 6.4`'s own `SettingsHostRegistrationTests.cs` and this
  Work Package's own initial `AuditHostRegistrationTests.cs` draft; both
  are fixed here. `WP 6.1`'s own `IdentityHostRegistrationTests.cs` was
  checked directly and found unaffected, since it never declares a
  `TempDirectory` at all.

## 12. Future Evolution

Retention/archival policy for audit records; a richer query capability
on `IPersistenceStore` if `TD-12`'s own revisit trigger fires; export of
audit records through `WP 6.7` (Export/Import) for compliance reporting;
real consumption by Reporting, the REST API, Licensing, and Export/
Import once each of those Work Packages actually begins — all named
explicitly as future, separately-scoped responsibilities, not designed
now.

## 13. Key Takeaways

1. A Work Package explicitly tasked with *validating* a prior Work
   Package's own design should produce a real, evidenced answer (a
   test suite proving correctness, a named judgment about the
   trade-off) — not merely a restatement of the prior risk in different
   words.
2. "Confirmed as anticipated" and "retired" are different outcomes for
   a risk — this Work Package's own `R8` update needed to say which,
   explicitly, twice (once at `WP 6.4`, again here), rather than letting
   the distinction blur.
3. A test suite that actually exercises a full, multi-step operation
   (establish principal, record, query, assert) is what surfaced a real,
   previously-uncaught bug in two already-committed Work Packages' own
   simpler tests — the bug existed the whole time; it took a test with
   enough intervening work between resource acquisition and use to
   expose it reliably.

## Architectural Debt Assessment

`docs/releases/v0.6.0/Risk Register.md`'s `R8` — **Confirmed, Not
Retired**, for the second time, with its own revisit trigger sharpened.
`docs/governance/Quality/Technical Debt Register.md` gained one new,
permanent item, `TD-12`, recording the same limitation as an ongoing
concern beyond this release's own scope. No `TD-09`/`TD-10`/`TD-11`
(`WP 6.1`'s own Identity & Permissions debt) was touched by this Work
Package — Audit consumes `IPermissionEvaluator` as an ordinary caller,
introducing no new instance of that same gap.

## Observations

This Work Package's own repository review found and fixed a genuine,
deterministic bug in two already-committed test files: `WP 6.4`'s
`SettingsHostRegistrationTests.cs` and this Work Package's own
first-draft `AuditHostRegistrationTests.cs` both declared a `using var
temp = new TempDirectory();` inside a non-`async` test method that
`return`ed (rather than `await`ed) a call to an async helper — the
`using` block's `Dispose()` executed the instant the method returned,
deleting the directory before the awaited operation inside actually
finished. This went unnoticed in `WP 6.4` because none of its own
Host-registration tests happened to need the directory to survive past
a single, fast operation; this Work Package's own round-trip test
(establish principal, record, query, assert) had enough intervening
work between resource acquisition and use to expose it reliably,
deterministically, on every run. Fixed by making every affected test
method genuinely `async Task`, awaiting the call directly, in both
files. `WP 6.1`'s own `IdentityHostRegistrationTests.cs` was checked
directly and confirmed unaffected, since it never declares a
`TempDirectory` at all (Identity has no Persistence dependency).

## Related Documents

`docs/releases/v0.6.0/Release Architecture.md` and companions (the
architecture package this Work Package implemented); `Platform Service
Contracts.md` and companions (the Contract Review package); `ADR-0041`;
`ADR-0044`; `ADR-0045`; `docs/architecture/Platform Service Map.md`
(Audit entry); `docs/governance/Quality/Technical Debt Register.md`
(`TD-12`); `docs/releases/v0.6.0/Risk Register.md` (`R8`);
`docs/academy/03 Work Packages/WP6.1-permissions-and-identity-
implementation.md`, `WP6.4-settings-framework-implementation.md` (the
precedents this Work Package's own single-pass implementation approach
follows).
