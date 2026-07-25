# Future Work Package Guidelines

## Status

**Standing instruction**, effective `WP 4.6A` onward, established at the
close of the Foundation phase (`WP 4.5B`). Every future Work Package is
expected to meet every mandatory expectation below, in addition to (not
instead of) Engineering Governance's own existing rules.

## Purpose

The Foundation phase (`WP 2.1` through `WP 4.5A`) built the platform,
proved the process works, and made the process itself visible and
checkable (`docs/governance/`). This document exists so that every Work
Package from `WP 4.6A` onward inherits that discipline explicitly, rather
than each one having to re-derive "what's expected of me" from reading
twenty-two prior retrospectives.

## Mandatory Expectations

Every future Work Package must:

1. **Maintain the Academy baseline.** Produce or update a Work Package
   retrospective as part of the same change that produces the code or
   decision it documents — never a follow-up pass (Engineering Governance
   §6). Update any Engineering Principle, Design Pattern, or concept guide
   this Work Package's own change affects.

2. **Maintain the Governance baseline.** Update every governance register
   this Work Package's own subject matter touches
   (`docs/governance/Governance Philosophy.md`, "How Contributors
   Maintain Governance") — a new ADR updates the ADR Register; a new
   platform service updates the Platform Services Register and the
   Traceability Matrix; and so on. Re-run the affected register's own
   Cross-Reference Check, do not just append a row.

3. **Maintain traceability.** Any new capability should be traceable,
   Requirement → Work Package → ADR → Architecture → Implementation →
   Tests → Academy → Release, per `docs/governance/Delivery/Traceability
   Matrix.md`'s own established chain. If a link in that chain does not
   exist yet for a genuine reason (a capability's catalogue is
   deliberately empty, say), record that reason explicitly rather than
   leaving a silent gap.

4. **Update documentation as part of the same change.** Any architecture
   document, root document (`README.md`, `PROJECT_STATUS.md`), or release
   document (`WorkPackages.md`, `CHANGELOG.md`, `Risks.md`) this Work
   Package's own change affects is updated in the same commit — this
   Work Package (`WP 4.5B`) found and fixed two stale top-level status
   lines specifically because a prior Work Package's own documentation
   update had not kept every downstream reference current; do not
   reintroduce that pattern.

5. **Cross-reference ADRs.** A decision meeting Engineering Governance
   §5's criteria gets an ADR before the code that depends on it, cited
   from every architecture document and retrospective that relies on it.

6. **Avoid documentation debt.** Do not leave a "Future Evolution"
   prediction stale once the change it predicted actually happens
   (Engineering Governance §6) — revisit and update the retrospective
   that made the prediction, with a note connecting the two.

7. **Avoid governance debt.** A register left unmaintained is worse than
   no register at all, because it is still trusted (`Governance
   Philosophy.md`). Outstanding Governance Debt is expected to remain
   **NONE** at every future baseline review, exactly as `WP 4.5A`
   established it.

8. **Prefer evidence over speculation.** Investigate the actual
   repository before assuming a premise (`WP 4.4C`'s own precedent —
   stopped without implementation once investigation found its assumed
   premise false). Mark a claim **Verified**, **Inferred**, or **Unknown**
   per `Governance Philosophy.md`'s own discipline; never present a guess
   as established fact.

9. **No architectural redesign during implementation.** If implementation
   surfaces a genuine need to revisit an already-approved architecture,
   the Work Package stops and reports the issue — it does not quietly
   redesign mid-implementation. This has been the explicit instruction
   for every implementation-phase Work Package since `WP 4.2`, and applies
   without exception going forward.

10. **Review before merge.** Every Work Package passes the Build Gate (0
    warnings, 0 errors) and the Test Gate (100% pass, including every
    pre-existing test) before being considered done (Engineering
    Governance §2, §3) — verified directly, not assumed from a prior
    Work Package's own passing state.

## What Changes for Future Work Packages

The Foundation phase's own Work Packages had to *establish* each of the
disciplines above, often discovering the right shape through genuine
trial (see `docs/releases/Platform Foundation Completion Report.md`'s own
"Lessons Learned"). Future Work Packages do not need to re-derive any of
this — the shape is now fixed, documented, and cross-referenced. The
expectation for `WP 4.6A` onward is **build capability against this
foundation, not revisit it**, unless a specific, documented piece of
evidence requires otherwise (see `Platform Foundation Completion
Report.md`'s own closing recommendation).

## Related Documents

`docs/academy/06 Engineering Standards/Engineering Governance.md`;
`docs/academy/06 Engineering Standards/Engineering Lifecycle.md`;
`docs/governance/Governance Philosophy.md`; `docs/releases/Platform
Foundation Completion Report.md`; `PROJECT_STATUS.md`.
