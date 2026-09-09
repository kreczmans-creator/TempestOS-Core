# WP 6.6 — Licensing Framework — Technical Debt Assessment

## Purpose

Updates the architecture-phase `Technical Debt Assessment.md`'s own
predictions against what `WP 6.6` actually shipped, mirroring every
prior Work Package's own Technical Debt Assessment format.

## Existing Debt: What Actually Happened

### `R5` (`docs/releases/v0.6.0/Risk Register.md`) — License validation being too aggressively Host-fatal

**Prediction:** the architecture phase's own anticipated decision
(`ADR-0050`) treated any invalid license as startup-aborting, mirroring
`ADR-0013`'s existing classification — but flagged this as a genuinely
open question, since Licensing is a new *kind* of failure (a business/
entitlement condition, not a technical fault) and an overly strict
interpretation could make the platform impossible to run in a
degraded-but-useful state.

**What actually happened:** Resolved precisely, not vaguely. A missing
license file is explicitly not invalid — it resolves to a valid,
unrestricted-but-uncapable default; a broken one (unreadable, not valid
JSON, missing its own required field, or expired) is Host-fatal.
Verified directly: all 24 pre-existing tests that build a real
`TempestHost` continue to pass unmodified. **Confirmed and resolved** —
the risk's own concern was legitimate, and the resolution answers it
completely, not by weakening the Host-fatal classification but by
defining precisely which cases it actually applies to.

## New Debt Actually Disclosed by This Work Package

### `TD-16` — No cryptographic license file signature verification

**Anticipated as a genuine, undecided question by the architecture
phase** — `Platform Service Contracts.md`'s own Security Considerations
named this exact question explicitly ("must decide whether license
validation includes a cryptographic signature check... and disclose
whichever it chooses as a named trade-off if the answer is [trusting at
face value]"). Disclosed as tracked debt (not a mere trade-off) because
a production licensing mechanism with no tamper-resistance is something
that should eventually be addressed once a concrete distribution
scenario exists, mirroring `TD-13`'s own precedent for the REST API's
undisclosed-authentication gap.

### `AT-13` — No remote validation/activation, floating/seat-based licensing, or renewal/grace-period model

**Anticipated as a future-release concern**, not a `WP 6.6`-own
requirement — `Platform Service Contracts.md`'s own Future Extension
Points name all three explicitly. Tracked as a trade-off for the same
reason as `WP 6.0`'s/`WP 6.7`'s own analogous Future-Extension-Point
trade-offs (`AT-09`, `AT-11`, `AT-12`).

## A Genuine, Disclosed Design Finding (Not Platform Debt)

The approved contract's own Configuration Requirements ("Configuration
itself is not yet built at the point Licensing validates") and Service
Lifecycle's own sequencing description ("immediately after Configuration
is built and before Logging Built") describe the same actual placement
from two different angles that could, read literally, seem to
contradict each other. Resolved by direct implementation: since
`LicenseValidator` never reads `IConfigurationProvider` at all, the two
readings produce zero observable behavioural difference — not a design
gap, and not registered as debt, since nothing about the platform's own
behaviour is actually ambiguous once implemented. Disclosed in
`ADR-0050`'s own Decision section as a governance-documentation
observation, not a functional concern.

## Summary Table

| Item | Predicted by architecture phase? | Actual outcome |
|---|---|---|
| `R5` — Host-fatal classification precision | Yes, explicitly, as this Work Package's own required decision | Resolved precisely: missing file is a valid default; broken file is Host-fatal — verified against the full pre-existing test suite |
| `TD-16` — No cryptographic signature verification | Named as an undecided security question, not a specific mechanism requirement | New, tracked debt item |
| `AT-13` — No remote validation/floating licensing/grace period | Named explicitly as future scope | New, tracked trade-off, matching that anticipation |
| `Service Lifecycle.md`/`Platform Service Contracts.md` sequencing-description discrepancy | Not anticipated | Found, resolved by implementation — zero behavioural difference, not registered as debt |

## Related Documents

`docs/releases/v0.6.0/Technical Debt Assessment.md` (the
architecture-phase document this one updates); `docs/releases/v0.6.0/
Risk Register.md` (`R5`); `docs/governance/Quality/Technical Debt
Register.md` (`TD-16`, `AT-13`); `ADR-0050`; `WP6.6 Implementation
Report.md`; `WP6.6 Engineering Review Report.md`; `WP6.6 Platform
Integration Demonstration.md`; `WP6.6 Platform Impact Assessment.md`;
`WP6.6 Lessons Learned.md`.
