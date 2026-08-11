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
| **Last Reviewed** | 2026-08-11 (WP 11.1A, Continuous Integration & Build Verification) — `04-continuous-integration.md` added; entries table updated in the same change. Previously reviewed 2026-07-28 (WP 5.3, Developer Experience Improvements) — §11 amended (adds `src/Templates/`). |
| **Related Documents** | `Academy Register.md`; `Governance Register.md`; `Validation Register.md`; `Future Work Package Guidelines.md`. |
| **Related ADRs** | None directly — these are process/coding standards, not architectural decisions. |
| **Related Academy Articles** | This register's entire scope, plus every retrospective that references Engineering Governance's own sections. |
| **Coverage Status** | Complete. |

---

## Entries

| Document | Scope | Sections |
|---|---|---|
| `Engineering Governance.md` | The project's constitution — Work Package lifecycle, Review Gates, Definition of Done, Documentation Requirements, ADR Creation Rules, Academy Maintenance, Release Approval Process, Coding Standards Hierarchy, Decision Authority, Rejected Designs Log, Repository Organisation, Naming Conventions | 12 numbered sections (§1–§12, extended `WP 4.5B` with §11/§12; §11 amended `WP 5.3` to add `src/Templates/`) |
| `01-exception-design.md` | How and when to define a new custom exception type, and where it sits in a hierarchy | Standalone standard |
| `02-testing-strategy.md` | The internal-test-seam pattern, test-double conventions, "prefer real implementations over mocks" | Standalone standard |
| `03-governance-registers.md` | Why and how to maintain the governance register suite | Standalone standard, added `WP 4.5A` |
| `Engineering Lifecycle.md` | The canonical Idea→...→Maintenance engineering pipeline, elaborating Governance §1 | Standalone standard, added `WP 4.5B` |
| `04-continuous-integration.md` | CI philosophy, the `.github/workflows/ci.yml` build pipeline, release verification, and the engineering workflow around it — the machine-verified realisation of Governance §2's Build Gate and Test Gate | Standalone standard, added `WP 11.1A` |

**Total: 6 documents.** (This register's own prior total of 5, recorded
at `WP 4.5B`, already omitted `04-continuous-integration.md` for the
obvious reason that it did not exist yet — added within `WP 11.1A`,
same change that produced the document itself, per Governance §6.)

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
