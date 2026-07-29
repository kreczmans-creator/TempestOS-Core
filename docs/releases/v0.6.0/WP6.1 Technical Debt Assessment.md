# WP 6.1 — Permissions & Identity — Technical Debt Assessment

## Purpose

Updates the architecture-phase `Technical Debt Assessment.md`'s own
predictions against what `WP 6.1` actually shipped — re-verifying each
prediction directly rather than assuming it held, per this project's own
re-derivation discipline.

## Existing Debt: What Actually Happened

### `TD-09`, `TD-10`, `TD-11`

**Architecture-phase prediction:** "Positioned for resolution via `WP
6.1`/`ADR-0044` — not resolved by this document."

**What actually happened:** Confirmed exactly as predicted. `WP 6.1`
built `IPermissionEvaluator`/`ADR-0044` — the enforcement point — and
did not retrofit a call into `NavigationService.Unregister`, Command/
Navigation registration, or plugin loading. All three items remain
Open. `Technical Debt Register.md`'s own entries for all three were
updated in place to reflect the mechanism's existence without claiming
resolution.

### `AT-07`

**Architecture-phase prediction:** Positioned for retirement via `WP
6.3` (REST API), not this Work Package.

**What actually happened:** Unaffected by `WP 6.1`, exactly as
predicted — `WP 6.1` ships no hosted service of any kind.

## New Debt/Trade-offs Actually Disclosed by This Work Package

### `IIdentityService.GetPrincipal` trusts its caller completely

Not named as a specific anticipated item in the architecture-phase
assessment (which discussed Licensing's and the REST API's own
anticipated debt, not Identity's). Genuinely surfaced during
implementation: this release's own local-only scope (`ADR-0043`) means
no authentication step exists, so any caller can ask to become any
identity id. Acceptable only because no untrusted caller exists yet.
**New debt item, disclosed here and in `ADR-0043`**: *Identity
establishment has no authentication or caller-trust boundary.* Revisit
trigger: `WP 6.3` (REST API) exposing identity establishment to a
network caller — that Work Package's own architecture phase must
resolve this directly before shipping, not inherit it silently.

### `CurrentPrincipalAccessor` is ambient, not request-scoped

Also not named as a specific anticipated item in the architecture-phase
assessment. Genuinely surfaced during implementation, with a concrete
regression test proving the tentative `AsyncLocal<T>` alternative would
not have served this release's own real need. **New debt item,
disclosed here and in `ADR-0044`**: *`CurrentPrincipalAccessor` cannot
currently isolate concurrent, per-request principals.* Revisit trigger:
`WP 6.3` (REST API) introducing genuine concurrent requests, each
potentially authenticated as a different principal.

### Role/principal configuration requires a restart to change

Anticipated in kind by the architecture phase (`ADR-0041`'s own family
of "initially minimal" disclosures for other services), now confirmed
concretely for Identity specifically: `IConfigurationProvider`'s
existing immutability (Case Study 05) means no role or principal
assignment can change without a full process restart. No new debt
register entry is needed beyond what Configuration's own existing
design already discloses — this is a direct, expected consequence of an
already-understood constraint, not a new one.

## Summary Table

| Item | Predicted by architecture phase? | Actual outcome |
|---|---|---|
| `TD-09` | Yes — positioned, not resolved | Confirmed: still Open, mechanism now exists |
| `TD-10` | Yes — positioned, not resolved | Confirmed: still Open, mechanism now exists |
| `TD-11` | Yes — positioned, not resolved | Confirmed: still Open, mechanism now exists |
| `AT-07` | Yes — unaffected by `WP 6.1` | Confirmed: unaffected |
| No authentication / caller-trust boundary | No — surfaced during implementation | New, disclosed (`ADR-0043`) |
| Ambient, not request-scoped, `CurrentPrincipalAccessor` | Partially — Contract Review flagged the design question, not this specific outcome | New, disclosed (`ADR-0044`) |

## Related Documents

`docs/releases/v0.6.0/Technical Debt Assessment.md` (the architecture-
phase document this one updates); `docs/governance/Quality/Technical
Debt Register.md` (`TD-09`, `TD-10`, `TD-11`); `ADR-0043`; `ADR-0044`;
`WP6.1 Implementation Report.md`; `WP6.1 Engineering Review Report.md`;
`WP6.1 Lessons Learned.md`.
