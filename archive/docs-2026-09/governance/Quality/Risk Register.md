# Risk Register (Governance Index)

## Register Metadata

| Field | Value |
|---|---|
| **Register Name** | Risk Register (Governance Index) |
| **Purpose** | A governance-level summary and cross-reference over the release's own risk register — status at a glance, without duplicating each risk's full mitigation history. |
| **Scope** | Every risk (`R1`–`R10`) in `docs/releases/v0.4.0/Risks.md`. |
| **Owner** | Project Maintainer. |
| **Source of Truth** | `docs/releases/v0.4.0/Risks.md`. This governance register is an index over that document; the full likelihood/impact/mitigation history for each risk lives only there, per that document's own "rows are never deleted" rule. |
| **Review Frequency** | Updated whenever `Risks.md` itself is updated. |
| **Last Reviewed** | 2026-07-28 (WP 5.1A) — R3 retired. |
| **Related Documents** | `docs/releases/v0.4.0/Risks.md`; `Decision Register.md`; `Technical Debt Register.md`. |
| **Related ADRs** | ADR-0020, ADR-0022, ADR-0025, ADR-0026, ADR-0029, ADR-0030 (each risk below cites the ADR that retired or reduced it). |
| **Related Academy Articles** | Every retrospective for the Work Package named in each risk's own "Affects" column. |
| **Coverage Status** | Complete. |

---

## Entries

| Risk | Summary | Status |
|---|---|---|
| R1 | Background Services (WP 4.5) touching the Host's frozen startup/shutdown sequence | **Retired** (2026-07-25, WP 4.5 implementation) |
| R2 | Navigation Framework had no existing architectural grounding | **Retired** (2026-07-27, WP 5.0A — `ADR-0031`/`ADR-0032`) |
| R3 | Event Bus (WP 4.4) / Command Framework (WP 4.7, renumbered WP 5.1A) risk overlapping | **Retired** (2026-07-28, WP 5.1A) |
| R4 | Plugin Manifest (WP 4.2) and Background Services (WP 4.5) both extending `Host Lifecycle.md`'s frozen phase table | **Retired in full** (2026-07-25) |
| R5 | Scope creep into legacy `LoggingService`/bootstrap migration (WP 4.8) | **Retired** (2026-07-28, WP 5.2 — decision `D-020`, no migration attempted) |
| R6 | Sample Module (WP 4.3) starting before dependencies stabilise | **Retired**, superseded by R9 (2026-07-23) |
| R7 | Release adds several genuinely new architectural surfaces at once | **Retired in full** (2026-07-28, WP 5.4) |
| R8 | Governance discipline more expensive to sustain across eleven Work Packages | **Retired in full** (2026-07-28, WP 5.4) |
| R9 | Sample Module's early-build benefit lost if not actually extended | **Retired in full** (2026-07-28, WP 5.4) |
| R10 | Navigation's dependency on Command Framework, unresolved | **Retired** by ADR-0022 (2026-07-23) |

**Total: 10 risks — all 10 Retired.**

**Correction, 2026-08-28 (independent closure-verification review):** the
five rows above (R2, R5, R7, R8, R9) had stood as "Open" in this index
since WP 5.1A while the register's own Source of Truth
(`docs/releases/v0.4.0/Risks.md`) showed every one of them Retired in
full by 2026-07-28 (WP 5.0A/WP 5.2/WP 5.4) — and the Cross-Reference
Check below claimed the two documents had been verified consistent "and
vice versa", which was false in that direction for all five rows. Both
the rows and the claim are corrected here; the retirement narratives
live in the Source of Truth, unchanged.

## Cross-Reference Check

Every risk's retirement date and retiring decision above is **Verified**
directly against `Risks.md`'s own strikethrough-and-update convention — no
risk is marked Retired here that `Risks.md` itself still shows Open, and
(re-verified 2026-08-28, after the correction above) no risk stands Open
here that `Risks.md` shows Retired. R1 and R4's retirement is the direct governance consequence of
`WP 4.5`'s implementation, cross-checked against
`docs/academy/03 Work Packages/WP4.5-background-services-implementation.md`.
R3's retirement is the direct governance consequence of `WP 5.1A`'s own
design work, cross-checked against `docs/architecture/Command Framework
Architecture.md` and `docs/academy/03 Work Packages/
WP5.1A-command-framework-architecture.md`.
