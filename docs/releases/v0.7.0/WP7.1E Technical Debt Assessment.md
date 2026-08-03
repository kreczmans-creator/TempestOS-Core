# WP 7.1E — Verification Framework — Technical Debt Assessment

## Purpose

Discloses every new debt item or trade-off this Work Package's own
implementation and Security Review introduce, and confirms which
existing debt items (if any) it touches — mirroring `WP7.1A`/`WP7.1B`/
`WP7.1C`/`WP7.1D Technical Debt Assessment.md`'s own format.

## Existing Debt: What Actually Happened

**No existing Technical Debt Register item (`TD-01` through `TD-22`) is
touched by this Work Package.** `Tempest.Core.Verification` depends only
on `IEngineeringDocumentStore`, `ICurrentPrincipalAccessor`, and
`IPermissionEvaluator`, none of which this Work Package modifies.

## New Debt Disclosed by This Work Package

### TD-23 — `RecordAsync`'s Own Multi-Link Sequence Is Not Transactional

**What.** Creating a verification record, linking it to its subject, and
linking it to every additional document/calculation record are several
separate, sequential operations against `IEngineeringDocumentStore` — a
failure partway through leaves a partially-linked record.

**Why this is debt, not merely a limitation.** A caller cannot assume
"the verification record was created" implies "every intended link was
also created" if an exception was thrown partway through.

**Revisit trigger.** A real, demonstrated need for transactional
multi-document operations — `FCR-0036`, raised directly from this
finding.

### TD-24 — `VerificationContext` Imposes No Bound on Recorded Data Volume; `GetVerificationHistoryAsync` Scales With Total Reference Count

**What.** A caller may record an unbounded number of criteria, evidence
entries, or links in one `VerificationContext`; separately,
`GetVerificationHistoryAsync` reads every reference from a subject
document (not only verification references) and a full revision
history per matching record.

**Revisit trigger.** A real, measured performance problem, or a real
need for bounded recording.

## New Accepted Trade-off Disclosed by This Work Package

### AT-17 — No Dependency on Materials for Material-Reference Validation

**What.** `VerificationContext.ReferenceMaterial` accepts any string,
unverified against `Tempest.Core.Materials` — identical to
`Calculations.CalculationContext.ReferenceMaterial`'s own precedent
(`AT-16`).

**Revisit trigger.** A real, demonstrated need for framework-internal
reference validation.

## Summary Table

| # | Item | Status | Revisit Trigger |
|---|---|---|---|
| TD-23 | `RecordAsync`'s own multi-link sequence is not transactional | New, Open | A real, demonstrated need for transactional multi-document operations |
| TD-24 | No bound on `VerificationContext`-recorded data volume; history read scales with reference count | New, Open | A real, measured performance problem, or a real need for bounded recording |
| AT-17 | No dependency on Materials for material-reference validation | New, Accepted Trade-off | A real, demonstrated need for framework-internal validation |

**Total: 2 new debt items disclosed, 1 new accepted trade-off disclosed,
0 existing items worsened.**

## Related Documents

`docs/governance/Quality/Technical Debt Register.md` (updated with
`TD-23`/`TD-24`/`AT-17` in this same Work Package); `ADR-0057`; `WP7.1E
Implementation Report.md`; `WP7.1E Security Review Report.md`.
