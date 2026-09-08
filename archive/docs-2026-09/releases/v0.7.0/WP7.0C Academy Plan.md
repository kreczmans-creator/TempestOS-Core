# WP 7.0C — Academy Plan

## Purpose

Identifies, for each of the five proposed Engineering Foundation
frameworks, the existing Academy material an engineer should read
before starting its own future implementation Work Package, and the new
Academy material that Work Package is expected to produce once it
completes — mirroring `docs/releases/v0.6.0/Academy Plan.md`'s own role
for that release's nine services, named here in advance so it is a
known Definition-of-Done item, not a follow-up pass discovered after the
fact.

This document does not itself add anything to `docs/academy/Academy
Index.md` — no new concept guide exists yet for any of the five
frameworks, since none is implemented. Each owning Work Package's own
completed retrospective and concept guide earn their own `Academy
Index.md` entry only once they actually exist.

## Cross-Cutting Required Reading (Every Engineering Foundation Work Package)

- `docs/academy/06 Engineering Standards/Engineering Governance.md` —
  the constitution every future Work Package still operates under.
- `VISION.md` — the product ambition the Engineering Foundation exists
  to serve.
- `docs/governance/Future Capability Register.md` (`FCR-0029`–`FCR-0033`)
  and `docs/governance/Capability Categories.md`.
- `WP7.0B Engineering Foundation Architecture.md` — why these five
  frameworks, in this order, are the minimum foundation.
- `WP7.0C Engineering Foundation Contracts.md`, `Cross-Framework
  Dependency Report.md`, and `Required ADR Catalogue.md` — the specific,
  proposed design for *this* framework and the open questions its own
  architecture phase must resolve.

## Per-Framework Plan

### Engineering Data Model & Document Management Foundation

**Required Reading (in addition to the cross-cutting list).**
`ADR-0041` (the shared-Persistence-abstraction precedent this
framework's own storage-substrate question mirrors); `docs/architecture/
Platform Service Map.md`'s own Persistence entry.

**Required Output.** A 13-section implementation retrospective under
`docs/academy/03 Work Packages/`; a new concept guide teaching the
document/revision/reference pattern — this platform's first
data-modelling abstraction beyond flat key-value storage, and therefore
the highest-priority new Academy content this entire programme
produces.

### Units & Quantities Framework

**Required Reading.** No existing platform precedent closely mirrors
this framework's own value-type-only shape — the closest analogues are
`CommandResult`/`LicenseValidationResult` (result types, not services),
worth reading for the "not every public type needs DI registration"
principle, though neither is a dimensioned-quantity pattern.

**Required Output.** A 13-section implementation retrospective; a new
concept guide teaching the generic-dimension-marker (phantom-type-style)
pattern — this platform's first use of that pattern, and worth its own
dedicated explanation independent of any specific dimension
implementation.

### Materials Framework

**Required Reading.** The Engineering Data Model's own concept guide
(once it exists) is a hard prerequisite — Materials is deliberately
presented as a worked example building on it, not a new pattern.

**Required Output.** A 13-section implementation retrospective. **No
new concept guide** — per `WP7.0C Cross-Framework Dependency Report.md`'s
own Reuse Opportunities finding, a separate Materials concept guide
would substantially repeat the Data Model's own content; the
retrospective itself, cross-referencing that guide, is sufficient.

### Engineering Calculation Framework

**Required Reading.** `ADR-0037`/`ADR-0038` (Command Framework
dispatch) — the precedent this framework's own registration/dispatch
shape mirrors; `11-command-framework.md` (existing Academy concept
guide).

**Required Output.** A 13-section implementation retrospective; a new
concept guide teaching the Calculation-vs-Command distinction (a pure
function producing a value vs. an imperative action with a
success/failure result) as a worked comparison, mirroring this
project's own repeated practice of distinguishing structurally similar
pairs (`08-failure-isolation.md`'s own Command Dispatch case, most
directly).

### Verification & Validation Framework

**Required Reading.** `WP6.5-audit-framework-implementation.md` (the
existing Audit retrospective) — required specifically to understand why
Audit and Verification are *not* the same concept before implementing
either alongside the other.

**Required Output.** A 13-section implementation retrospective; a new
concept guide distinguishing Verification from Audit and from a
Calculation Record — three structurally similar "record what happened"
types with genuinely different semantics, the single most
important piece of new Academy content this framework's own
implementation must produce, per its own disclosed risk of being
conflated with Audit if not explained clearly.

## Summary Table

| Framework | New Concept Guide? | Rationale |
|---|---|---|
| Engineering Data Model | Yes | Genuinely new pattern for this platform |
| Units & Quantities | Yes | Genuinely new pattern (phantom-type-style dimension safety) |
| Materials Framework | **No** | Worked example of the Data Model; a separate guide would duplicate content |
| Calculation Framework | Yes | New pattern, but explained via comparison to the existing Command Framework guide |
| Verification & Validation | Yes | New pattern, but explained via comparison to the existing Audit guide |

## Related Documents

`docs/releases/v0.6.0/Academy Plan.md` (the precedent this document's
own structure follows); `WP7.0C Engineering Foundation Contracts.md`;
`docs/governance/Documentation/Academy Register.md`.
