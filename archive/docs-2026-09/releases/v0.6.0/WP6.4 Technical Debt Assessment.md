# WP 6.4 — Settings Framework — Technical Debt Assessment

## Purpose

Updates the architecture-phase `Technical Debt Assessment.md`'s own
predictions against what `WP 6.4` actually shipped, mirroring `WP6.1
Technical Debt Assessment.md`'s own format.

## Existing Debt: What Actually Happened

### `R4` (`docs/releases/v0.6.0/Risk Register.md`) — Persistence reinvented ad hoc

**Prediction:** the un-owned Persistence abstraction risked being
reinvented independently by `WP 6.4` and `WP 6.5` if `ADR-0041`'s
recommendation wasn't followed.

**What actually happened:** `ADR-0041` was ratified and implemented
exactly as recommended. **Partially Retired** — the abstraction now
exists; the residual risk (whether `WP 6.5` actually depends on it,
rather than building its own) remains open until that Work Package
begins.

### `R8` (`docs/releases/v0.6.0/Risk Register.md`) — Persistence too minimal for Audit's needs

**Prediction:** the shared Persistence abstraction's first iteration
would be minimal (key-value only) and might not satisfy Audit's own
anticipated query needs.

**What actually happened:** Confirmed exactly as predicted —
`IPersistenceStore` ships with key lookup and full-collection
enumeration only, no filtered or range query capability. **Not
retired** — this was a confirmation of an anticipated limitation, not
a resolution of the risk it names; the risk itself (does this suffice
for Audit) stays open until `WP 6.5` actually attempts to build
`IAuditQuery` against this shape.

## New Debt/Trade-offs Actually Disclosed by This Work Package

### No sensitive-value flag on `ISettingDefinition`

Anticipated in kind by the architecture phase (`Platform Service
Contracts.md`'s own Security Considerations named this as "a required
decision for `WP 6.4`'s own architecture phase"), now confirmed as a
genuine, disclosed limitation: every setting change logs both old and
new values, unredacted. **New debt item, disclosed here and in
`ADR-0042`**: *No mechanism exists to mark a setting sensitive for
logging/redaction purposes.* Revisit trigger: a real setting needing to
hold sensitive data (a credential, an API key) is registered by any
future Work Package.

### "User settings" and "strongly typed settings" not built

Not anticipated as debt by the architecture phase, since neither was
part of any approved contract to begin with — surfaced during
implementation when the brief's own deliverable list named both.
**New debt item, disclosed here**: *Settings is global (not
per-principal) and string-valued (not generically typed).* Both are
already-named Future Extension Points in `Platform Services
Overview.md`, not newly discovered gaps — this entry exists to confirm
neither was silently built as an unapproved addition, and both remain
open for a future, explicitly-scoped Work Package.

### File-per-key storage has no specific scale target

Anticipated in kind (`Platform Service Contracts.md`'s own Performance
Expectations named "no specific throughput target... `WP 6.4`'s own
architecture phase should set a concrete target once a storage backend
is chosen"), not fully resolved here — a concrete target was not set,
since no named `v0.6.0` Work Package currently has a scale requirement
Settings or Audit must meet. **Not treated as new debt**, since the
architecture phase itself deferred setting a target rather than
requiring one now.

## Summary Table

| Item | Predicted by architecture phase? | Actual outcome |
|---|---|---|
| `R4` — Persistence reinvented ad hoc | Yes | Partially Retired — abstraction exists |
| `R8` — Persistence too minimal for Audit | Yes | Confirmed, not retired |
| No sensitive-value flag | Partially (named as an open question) | New, disclosed (`ADR-0042`) |
| No user-scoped or strongly-typed settings | No — surfaced when the implementation brief named both | New, disclosed; both already Future Extension Points |
| No concrete performance target | Yes (explicitly deferred) | Unchanged, not new debt |

## Related Documents

`docs/releases/v0.6.0/Technical Debt Assessment.md` (the architecture-
phase document this one updates); `docs/releases/v0.6.0/Risk
Register.md` (`R4`, `R8`); `ADR-0041`; `ADR-0042`; `WP6.4 Implementation
Report.md`; `WP6.4 Engineering Review Report.md`; `WP6.4 Lessons
Learned.md`.
