# WP 7.1A — Engineering Data Model — Lessons Learned

## Status

Complete.

## 1. A contract written without reading real source code will miss real conventions

`WP7.0C Engineering Foundation Contracts.md` proposed `EngineeringDataException`
as `public abstract class` — a reasonable-sounding choice in isolation,
but inconsistent with the actual, universal convention every existing
exception hierarchy in this codebase follows (`PersistenceException`,
`SettingsException`, `AuditException`, all `public class`, non-abstract,
by convention rather than compiler enforcement). This was only caught
because implementation requires actually opening the files a Contract
Review can cite without reading in full. The lesson generalises: a
Contract Review's own proposed signatures are a strong default, not a
guarantee — implementation is still the point where a proposal meets
the codebase's own real, accumulated conventions, and a genuine,
disclosed correction at that point is a normal, healthy outcome, not a
failure of the review that preceded it.

## 2. Revision-number atomicity was easier to guarantee correctly than to test convincingly

Implementing the per-document lock in `ReviseAsync` was straightforward
— `SettingsProvider`'s own existing per-key locking pattern transferred
directly. Writing a test that would actually *fail* without the lock
(rather than merely exercising the locked path without stressing it)
required running twenty concurrent `ReviseAsync` calls and asserting no
two shared a revision number — a genuinely different kind of test than
most of this codebase's own existing unit tests, closer to
`AuditRecorder`'s or `PersistenceStore`'s own concurrency tests than to
the simpler create/read round-trip tests that make up most of this
Work Package's own suite.

## 3. A small design choice not specified in the contract turned out to matter

`WP7.0C`'s own contract specified *what* `GetRevisionHistoryAsync` and
`GetReferencesAsync` return, not *how* they find it. Implementing them
naively (list every key in a collection, filter by document Id) would
have reproduced `IAuditQuery`'s own disclosed linear-scan limitation
(`TD-12`) for this framework too. Choosing instead to encode the
document Id into the collection name itself (for references) and to
read exactly the known, sequential keys directly (for revisions)
avoided that limitation entirely, for this framework's own access
pattern — a decision `WP7.0C`'s own contract review did not anticipate
because it was a genuine implementation-time discovery, not something
visible from the interface signature alone.

## 4. The scope boundary ("no calculations, no standards, no disciplines") was easy to hold, once made explicit

No moment during implementation created real temptation to add a
calculation, a unit, or a discipline-specific concept — the boundary
this Work Package's own controlling instruction drew was clean enough,
and `WP7.0C`'s own contract narrow enough, that "stay within scope" was
never a difficult judgment call. This is itself worth recording: a
well-scoped Contract Review makes the following implementation Work
Package's own scope discipline close to automatic, rather than
something requiring constant vigilance.

## Recommendations

- **Update `docs/governance/Quality/Technical Debt Register.md` with
  `TD-17`/`TD-18` in the same commit as this Work Package** (not
  deferred) — per this project's own standing discipline of maintaining
  governance registers as part of the same change, not a follow-up
  pass.
- **Candidates `E` (Units & Quantities) and `G` (Materials) are the
  strongest next Work Packages** — see `WP7.1A Engineering Foundation
  Impact Assessment.md` for the full reasoning.
- **Future Contract Reviews should spend a small, explicit verification
  pass reading real, existing sibling-namespace source files** before
  proposing a new namespace's own exception hierarchy or base-class
  modifiers — the one deviation this Work Package found would have been
  caught during `WP 7.0C` itself with a five-minute check.

## Related Documents

`WP7.1A Implementation Report.md`; `WP7.1A Engineering Review
Report.md`; `ADR-0053`; `docs/academy/03 Work Packages/
WP7.1A-engineering-data-model-implementation.md`.
