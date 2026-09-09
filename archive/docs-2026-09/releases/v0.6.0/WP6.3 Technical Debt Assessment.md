# WP 6.3 — REST API — Technical Debt Assessment

## Purpose

Updates the architecture-phase `Technical Debt Assessment.md`'s own
predictions against what `WP 6.3` actually shipped, mirroring every
prior Work Package's own Technical Debt Assessment format.

## Existing Debt: What Actually Happened

### `R1` (`docs/releases/v0.6.0/Risk Register.md`) — `CurrentPrincipalAccessor`'s ambient design under request concurrency

**Prediction:** `WP 6.1`'s own residual risk named this Work Package as
the point where `CurrentPrincipalAccessor`'s ambient (not
`AsyncLocal<T>`) design would need "real reconsideration," and where a
decision on request-scoping must be made.

**What actually happened:** Reconsidered directly and empirically — an
`AsyncLocal<T>`-backed alternative was built and tested against the
full pre-existing suite, regressed 17 tests, and was rejected.
`CurrentPrincipalAccessor` remains unchanged. The REST API instead
resolves identity per-request without touching the ambient state at
all (`ADR-0052`). **Confirmed and resolved** — not by changing the
component `R1` was worried about, but by proving a different resolution
(avoid touching it) was both sufficient and safer.

### `R3` — ASP.NET Core/Kestrel integration boundary risk

**Prediction:** this release's first substantial dependency on a
pre-built framework component carried real integration risk (hosting
model conflicts, Composition Root interaction) with no direct
precedent, requiring the boundary to be prototyped explicitly before
committing to it.

**What actually happened:** Prototyped and verified directly — no
hosting-model conflict was found; `WebApplication`'s own internal
container was confirmed, by direct code inspection, never to resolve
any `Tempest.Core` service. **Confirmed, resolved cleanly** — the
predicted risk category was real to consider, but no actual conflict
materialised.

## New Debt Actually Disclosed by This Work Package

### `TD-13` — No real authentication for the REST API

**Not anticipated as a standalone item by the architecture phase** —
`Platform Service Contracts.md`'s own Security Considerations named the
REST API as "the highest-security-sensitivity service in this release"
but did not itself mandate a specific authentication mechanism for
`WP 6.3`'s own first pass. Disclosed as tracked debt (not a mere
trade-off) because a production REST API without real authentication is
something that should eventually be addressed, not a permanent,
accepted design boundary.

### `TD-14` — No TLS configured

**Anticipated as a future-release concern**, not a `WP 6.3`-own
requirement — `Platform Service Contracts.md`'s own Security
Considerations state "TLS should be the default expectation for
anything beyond local development." Tracked as debt for the same reason
as `TD-13`.

### `TD-15` — Ambient-principal Audit attribution gap under REST invocation

**A direct, disclosed consequence of `ADR-0052`'s own resolution** — not
anticipated by the architecture phase, since the underlying
`CurrentPrincipalAccessor`-avoidance strategy is itself an
implementation-phase decision. Tracked as debt because a future command
handler could genuinely need correct per-request attribution when
invoked via REST, and the current mitigation (a `Detail`-carried
identity on the REST API's own audit entry) does not automatically
propagate to a different command's own separate `RecordAsync` call.

## A Genuine, Disclosed Process Finding (Not Platform Debt)

`docs/governance/Engineering/Hosted Services Register.md` had gone
stale since `WP 4.5A`, never updated when `WP 6.2` shipped this
codebase's first real hosted service. This is a **governance-
documentation** finding, not platform architecture debt, and is not
registered in `Technical Debt Register.md` for that reason — corrected
directly in that register itself, disclosed in this Work Package's own
retrospective and `PROJECT_STATUS.md` instead.

## Summary Table

| Item | Predicted by architecture phase? | Actual outcome |
|---|---|---|
| `R1` — `CurrentPrincipalAccessor` request-scoping decision | Yes, explicitly, as this Work Package's own required decision | Resolved empirically: migration rejected (regressed 17 tests); avoided touching ambient state instead |
| `R3` — ASP.NET Core/Kestrel integration boundary | Yes, as a real risk category | Prototyped and verified; no actual conflict found |
| `TD-13` — No real authentication | Named as a security-sensitivity concern, not a specific mechanism requirement | New, tracked debt item |
| `TD-14` — No TLS | Anticipated as a future-release concern | New, tracked debt item, matching that anticipation |
| `TD-15` — Ambient-principal Audit attribution gap | Not anticipated | New, tracked debt item, direct consequence of `ADR-0052` |
| `Hosted Services Register.md` staleness | Not anticipated | Found, fixed — a governance-documentation finding, not platform debt |

## Related Documents

`docs/releases/v0.6.0/Technical Debt Assessment.md` (the architecture-
phase document this one updates); `docs/releases/v0.6.0/Risk
Register.md` (`R1`, `R3`); `docs/governance/Quality/Technical Debt
Register.md` (`TD-13`, `TD-14`, `TD-15`); `ADR-0049`; `ADR-0052`;
`WP6.3 Implementation Report.md`; `WP6.3 Engineering Review Report.md`;
`WP6.3 Platform Integration Demonstration.md`; `WP6.3 Platform Impact
Assessment.md`; `WP6.3 Lessons Learned.md`.
