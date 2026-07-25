# Engineering Standards Register

## Register Metadata

| Field | Value |
|---|---|
| **Register Name** | Engineering Standards Register |
| **Purpose** | The index of every binding coding/process standard TempestOS maintains, distinct from architecture (what the system does) and Academy teaching material (why it does it that way). |
| **Scope** | Every document under `docs/academy/06 Engineering Standards/`. |
| **Owner** | Project Maintainer. |
| **Source of Truth** | `docs/academy/06 Engineering Standards/`. |
| **Review Frequency** | Updated whenever a new coding standard is adopted or an existing one materially changes. |
| **Last Reviewed** | 2026-07-25 (WP 4.5A). |
| **Related Documents** | `Academy Register.md`; `Governance Register.md`; `Validation Register.md`. |
| **Related ADRs** | None directly — these are process/coding standards, not architectural decisions. |
| **Related Academy Articles** | This register's entire scope, plus every retrospective that references Engineering Governance's own sections. |
| **Coverage Status** | Complete. |

---

## Entries

| Document | Scope | Sections |
|---|---|---|
| `Engineering Governance.md` | The project's constitution — Work Package lifecycle, Review Gates, Definition of Done, Documentation Requirements, ADR Creation Rules, Academy Maintenance, Release Approval Process, Coding Standards Hierarchy, Decision Authority, Rejected Designs Log | 10 numbered sections (§1–§10) |
| `01-exception-design.md` | How and when to define a new custom exception type, and where it sits in a hierarchy | Standalone standard |
| `02-testing-strategy.md` | The internal-test-seam pattern, test-double conventions, "prefer real implementations over mocks" | Standalone standard |

**Total: 3 documents.**

## Coding Standards Hierarchy (Engineering Governance §8)

Engineering Governance §8 establishes the authority order these standards
sit within — **Verified** directly from the document itself, not
duplicated here in full; see `Engineering Governance.md` §8 for the
complete hierarchy text.

## Cross-Reference Check

Every retrospective in `Academy Register.md`'s "03 Work Packages" table
cites at least one section of `Engineering Governance.md` (most commonly
§3 Definition of Done, §5 ADR Creation Rules, §6 Academy Maintenance) —
confirmed by direct grep of the retrospectives for "Governance §". No
Work Package retrospective was found to have skipped a governance
obligation without an explicit, documented reason.
