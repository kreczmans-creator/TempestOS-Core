# WP 6.5 — Audit Framework — Technical Debt Assessment

## Purpose

Updates the architecture-phase `Technical Debt Assessment.md`'s own
predictions against what `WP 6.5` actually shipped, mirroring `WP6.1`/
`WP6.4 Technical Debt Assessment.md`'s own format.

## Existing Debt: What Actually Happened

### `R8` (`docs/releases/v0.6.0/Risk Register.md`) — Persistence too minimal for Audit's needs

**Prediction:** the shared Persistence abstraction's first iteration
would be minimal (key-value only) and might not satisfy Audit's own
anticipated query needs; `WP 6.4`'s own implementation confirmed the
minimal shape shipped exactly as anticipated, leaving open whether it
would actually suffice once `WP 6.5` tried to build against it.

**What actually happened:** Confirmed adequate for correctness, via a
real, passing filter-correctness test suite (`AuditQueryTests`) — every
approved `IAuditQuery` filter is satisfiable through
`IPersistenceStore`'s existing surface. **Not retired** — the
underlying performance characteristic (linear-scan query cost) is real,
confirmed, and now tracked permanently as its own Technical Debt item
(`TD-12`), not merely a release-scoped risk.

## New Debt Actually Disclosed by This Work Package

### `TD-12` — `IPersistenceStore` has no native query/filter capability

**Not anticipated as a standalone, permanent Technical Debt item by the
architecture phase** — that phase's own Technical Debt Assessment
discussed this as part of `R8`'s own release-scoped risk, not as a
cross-release debt item in its own right. Promoted to a permanent
Technical Debt Register entry here because this Work Package's own
Persistence Validation *confirmed* (not merely anticipated) the
limitation, and a confirmed, real architectural characteristic that
could affect a future high-volume consumer deserves to outlive the
release-scoped Risk Register that first named it. Revisit trigger: a
real, measured performance problem or a concrete scale requirement is
named by any future Work Package.

### Whether a `RecordAsync` failure should abort its own caller's primary operation

**Not resolved universally** — `ADR-0045` names this as each individual
caller's own decision, not something `AuditRecorder` itself can decide
for every future consumer. **Not registered as a new Technical Debt
item**, since this is a genuine design decision (deliberately delegated
per-caller), not an unresolved gap — a future Work Package
(Reporting, the REST API, Licensing, Export/Import, an engineering
module) that gets this wrong for its own specific use case would be
that Work Package's own defect to fix, not a defect in Audit itself.

## A Genuine, Disclosed Process Finding (Not Platform Debt)

This Work Package's own repository review found and fixed a real,
deterministic bug in two already-committed test files
(`WP 6.4`'s `SettingsHostRegistrationTests.cs`; this Work Package's own
initial `AuditHostRegistrationTests.cs` draft) — a `using`-scoped
`TempDirectory` disposed before its own awaited operation completed.
This is a **test-infrastructure** finding, not a platform architecture
debt item, and is not registered in `Technical Debt Register.md` for
that reason — it is fully disclosed in this Work Package's own
retrospective, Lessons Learned, and PROJECT_STATUS.md instead, since
those are the correct homes for a process/practice finding rather than
an architectural one.

## Summary Table

| Item | Predicted by architecture phase? | Actual outcome |
|---|---|---|
| `R8` — Persistence too minimal for Audit | Yes (confirmed once already at `WP 6.4`) | Confirmed a second time, with stronger evidence; still not retired |
| `TD-12` — No native query capability | Discussed under `R8`, not as a standalone permanent item | New, permanent Technical Debt item, promoted here |
| Per-caller `RecordAsync` failure handling | Named as an open question in the Contract Review | Resolved as "each caller's own decision" — not a debt item |
| Premature-dispose test bug | Not anticipated at all | Found, fixed, disclosed as a process finding — not platform debt |

## Related Documents

`docs/releases/v0.6.0/Technical Debt Assessment.md` (the architecture-
phase document this one updates); `docs/releases/v0.6.0/Risk
Register.md` (`R8`); `docs/governance/Quality/Technical Debt
Register.md` (`TD-12`); `ADR-0045`; `WP6.5 Implementation Report.md`;
`WP6.5 Engineering Review Report.md`; `WP6.5 Platform Impact
Assessment.md`; `WP6.5 Lessons Learned.md`.
