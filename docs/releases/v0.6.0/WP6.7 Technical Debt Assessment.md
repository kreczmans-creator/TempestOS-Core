# WP 6.7 — Export/Import — Technical Debt Assessment

## Purpose

Updates the architecture-phase `Technical Debt Assessment.md`'s own
predictions against what `WP 6.7` actually shipped, mirroring every
prior Work Package's own Technical Debt Assessment format.

## Existing Debt: What Actually Happened

No existing Risk Register or Technical Debt item named Export/Import as
its own predicted revisit point — confirmed directly by grep against
both `docs/releases/v0.6.0/Risk Register.md` and
`docs/governance/Quality/Technical Debt Register.md`, neither of which
contains any Export/Import-specific row prior to this Work Package.
There is therefore no existing-debt table to update this time — every
disclosed item below is new to this Work Package.

## New Debt Actually Disclosed by This Work Package

### `AT-11` — No compression or encryption of exported artifact content

**Anticipated as a future-release concern**, not a `WP 6.7`-own
requirement — `Platform Service Contracts.md`'s own Future Extension
Points name both explicitly. Tracked as a trade-off (not tracked debt)
because this is a deliberate, approved-contract scope decision, not an
oversight — an `IExportable` implementation is individually responsible
for redacting or refusing to export sensitive content, mirroring how
Persistence imposes no content-level policy on Settings/Audit.

### `AT-12` — No schema-upgrade/migration path

**Anticipated as a future-release concern**, not a `WP 6.7`-own
requirement — `Platform Service Contracts.md`'s own Versioning Policy
states an incompatible version "must reject... not silently downgrade
or upgrade," and Future Extension Points name a migration path
explicitly as future scope. `IImportService.ImportAsync` rejects any
schema-version mismatch outright, by exact-equality check — no
partial-compatibility behaviour exists in this release.

## A Genuine, Disclosed Process Finding (Not Platform Debt)

Three genuine, pre-existing governance-documentation drifts were found
during this Work Package's own repository review, none related to its
own scope:

1. `docs/architecture/Platform Service Map.md`'s own Audit and
   Notifications "Consumers" entries had read "none yet implemented"
   since before `WP 6.0` first shipped a real consumer of each —
   corrected in this Work Package's own commit.
2. `docs/governance/Engineering/Interface Register.md`,
   `Dependency Injection Register.md`, and `Module Register.md` had
   each gone stale since `WP 5.2`, missing every public interface (23),
   DI registration call site (10), and sample module (6) `WP 6.1`
   through `WP 6.3` added. Each register's own Coverage Status is
   corrected from "Complete" to "Partial," disclosing the exact gap,
   with only this Work Package's own new entries added — the larger,
   six-Work-Package backfill is explicitly recommended as `WP 6.8`
   (Platform Services Integration Review)'s own closing-audit task,
   not retrofitted here under a different Work Package's own scope.

These are **governance-documentation** findings, not platform
architecture debt, and are not registered in `Technical Debt
Register.md` for that reason — corrected (item 1) or explicitly
deferred with a named owner (item 2) in this Work Package's own
retrospective and `PROJECT_STATUS.md` instead.

## Summary Table

| Item | Predicted by architecture phase? | Actual outcome |
|---|---|---|
| `AT-11` — No compression/encryption of exported content | Anticipated as a future-release concern (Future Extension Points) | New, disclosed trade-off |
| `AT-12` — No schema-upgrade/migration path | Anticipated as a future-release concern (Versioning Policy, Future Extension Points) | New, disclosed trade-off |
| `Platform Service Map.md` Audit/Notifications consumer staleness | Not anticipated | Found, fixed — a governance-documentation finding, not platform debt |
| `Interface Register.md`/`Dependency Injection Register.md`/`Module Register.md` staleness since `WP 5.2` | Not anticipated | Found, disclosed, only this Work Package's own entries added — full backfill deferred to `WP 6.8` |

## Related Documents

`docs/releases/v0.6.0/Technical Debt Assessment.md` (the
architecture-phase document this one updates); `docs/governance/
Quality/Technical Debt Register.md` (`AT-11`, `AT-12`); `ADR-0051`;
`WP6.7 Implementation Report.md`; `WP6.7 Engineering Review Report.md`;
`WP6.7 Platform Integration Demonstration.md`; `WP6.7 Platform Impact
Assessment.md`; `WP6.7 Lessons Learned.md`.
