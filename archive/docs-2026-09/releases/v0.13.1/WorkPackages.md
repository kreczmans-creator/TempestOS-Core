# v0.13.1 — Work Packages

## Status

**In preparation.** `v0.13.1` — "Trust & Deployment Hardening" (corrected
release) — prepared on branch `release/v0.13.1`, cut from `main` at
`ea3fe07`. Not yet tagged, merged, or published; `origin/main` remains
`6089a218`.

## Scope of this release

`v0.13.1` is **not a new body of work**. It is the corrected, publishable
form of `v0.13.0`'s content, carrying that release's own tagged commit
`6089a218` in its ancestry unchanged, plus exactly two commits:

| Commit | Work Package | Change |
|---|---|---|
| `7449756` | `WP 13.12.9` | Desktop async test determinism remediation — test-only, one method in one file, `+28/−2` |
| `ea3fe07` | `WP 13.12.10` | `v0.13.0` Release Register closure — documentation-only, `+7/−3` |

Combined delta over the `v0.13.0` tagged tree: **2 files, +35/−5, zero
`src/` files.** No product change of any kind.

`v0.13.1` exists solely because `v0.13.0`'s tag-triggered `release.yml`
run (`32146823154`) failed at its `Test (Release)` step on a single
Desktop test, so packaging and publication never ran. `v0.13.0`'s tag
remains immutable at `6089a218` per Engineering Governance §7.4 — not
amended, moved, deleted, recreated, or retried.

## Where the Work Package ledger lives

**All twenty-eight `v0.13.0` Work Packages and the ten delivered since
(`WP 13.12.3`–`WP 13.13.1`) are recorded in
`docs/releases/v0.13.0/WorkPackages.md`**, which is the `v0.13`-series
ledger. That table is deliberately not duplicated here: `v0.13.1` shares
`v0.13.0`'s entire scope, and two copies of the same ledger would be two
things to keep in sync.

The Work Packages specific to this release's own delta are:

| Work Package | Scope | Type | Status |
|---|---|---|---|
| `WP 13.12.8` | v0.13.0 Release Test Failure Investigation — diagnosed the `release.yml` failure as a genuine intermittent flake, not a product defect | Verification/Governance | **Complete** |
| `WP 13.12.9` | Desktop Async Test Determinism Remediation — replaced a fixed `Task.Delay(50)` with the bounded poll `TD-46` established | Implementation | **Complete** |
| `WP 13.12.10` | v0.13.0 Release Register Closure — recorded `v0.13.0` as tagged and merged but not published | Governance/Process | **Complete** |
| `WP 13.13.0` | v0.13.0 Release Failure Disposition & v0.13.1 Planning — analysis only; established that re-tagging is barred and a new patch version is the only remedy | Architecture/Security | **Complete** |
| `WP 13.13.1` | v0.13.1 Release Preparation — this release's own preparation: `VERSION`, Release Notes, register updates | Governance/Process | **Complete** |

## Related Documents

`docs/releases/v0.13.1/Release Notes.md`;
`docs/releases/v0.13.0/WorkPackages.md` (the full `v0.13`-series ledger);
`docs/releases/v0.13.0/Release Notes.md` (the full scope account);
`docs/releases/v0.13.0/WP13.12.2 Engineering Release Report.md` (the
authoritative readiness record, applying unchanged to this release);
`docs/governance/Delivery/Release Register.md`.
