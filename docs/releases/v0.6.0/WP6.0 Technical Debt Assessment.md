# WP 6.0 — Reporting Framework — Technical Debt Assessment

## Purpose

Updates the architecture-phase `Technical Debt Assessment.md`'s own
predictions against what `WP 6.0` actually shipped, mirroring `WP6.1`/
`WP6.4`/`WP6.5`/`WP6.2 Technical Debt Assessment.md`'s own format.

## Existing Debt: What Actually Happened

No existing Risk Register row or Technical Debt Register item named
Reporting specifically as its own subject prior to this Work Package
(`R7`'s own Reporting-adjacent boundary-blurring risk had already been
retired, in full, by `WP 6.2`'s own implementation of the Notifications
half — Reporting's own half of that risk was never separately named,
since `R7` concerned Audit/Notification/Settings-vs-Logging/Event-Bus/
Configuration specifically, not Reporting-vs-Export/Import). No prior
Work Package's own Technical Debt Assessment made a prediction this
Work Package needed to confirm or revise.

## New Debt Actually Disclosed by This Work Package

### `AT-09` — No delivery-channel abstraction or durable report history

**Not anticipated as a standalone item by the architecture phase** —
`Platform Service Contracts.md`'s own Future Extension Points for
Reporting named both explicitly ("Report generation progress/streaming
for a long-running renderer; scheduled/recurring report generation")
alongside a narrower, adjacent concern than delivery/history, and the
brief's own Template Strategy section did not itself demand a delivery
mechanism. Recorded as a disclosed, accepted trade-off (mirroring
`AT-06`'s own "no real plugin yet" precedent) rather than a defect,
since the approved contract explicitly scopes delivery and history out
of this release ("does not itself provide a delivery mechanism").

### The deliberate non-delivery of an "Export abstraction"

**Not a Technical Debt Register item** — this is a genuine, disclosed
*scope decision* (`ADR-0040`), not an unresolved gap or a defect. The
brief named "Export abstraction" as scope, but `Platform Service
Contracts.md`'s own orthogonality decision and `WP 6.7`'s own reserved
future scope make clear this capability belongs to Export/Import, not
Reporting. Recording this as debt would misrepresent a deliberate
architectural boundary as an unaddressed problem.

## A Genuine, Disclosed Engineering-Review Finding (Not Platform Debt)

None. Unlike `WP 6.2`'s own exact-static-type-dispatch defect (found
and fixed within that Work Package's own sample consumers) and `WP
6.5`'s own premature-resource-disposal bug (found in two already-
committed prior Work Packages' own test files), this Work Package's own
repository review found no comparable process or test-infrastructure
defect, in either its own new code or any pre-existing file it touched.

## Summary Table

| Item | Predicted by architecture phase? | Actual outcome |
|---|---|---|
| `AT-09` — No delivery-channel abstraction or durable report history | Named as a Future Extension Point, not a standalone debt item | New, disclosed trade-off, promoted here to a permanent register entry |
| "Export abstraction" non-delivery | Anticipated as this Work Package's own required-ADR question | Resolved as a deliberate scope decision (`ADR-0040`), not a debt item |
| Cross-service integration defect (à la `WP 6.2`'s own finding) | Not anticipated | None found — first-attempt success, verified by testing, not assumed |

## Related Documents

`docs/releases/v0.6.0/Technical Debt Assessment.md` (the architecture-
phase document this one updates); `docs/governance/Quality/Technical
Debt Register.md` (`AT-09`); `ADR-0040`; `WP6.0 Implementation
Report.md`; `WP6.0 Engineering Review Report.md`; `WP6.0 Platform
Integration Demonstration.md`; `WP6.0 Platform Impact Assessment.md`;
`WP6.0 Lessons Learned.md`.
