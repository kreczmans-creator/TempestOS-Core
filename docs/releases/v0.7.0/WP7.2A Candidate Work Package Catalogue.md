# WP 7.2A — Candidate Work Package Catalogue

## Status

**Candidates, not approvals.** Every entry below requires its own
Architecture, Planning, and Contract Review phase before a real Work
Package number is assigned, per this project's standing discipline
(`FOUNDATION.md` §1) — mirroring `WP7.0B Candidate Work Package
Catalogue.md`'s own identical discipline for the Engineering Foundation
programme. Each entry includes objective, dependencies, expected
outputs, Academy impact, engineering complexity, and security
considerations, per this Work Package's own controlling instruction.
**No implementation is approved by this document.**

## Candidates for Programme A (Requirements & Verification Platform)

### Candidate K — Requirements Engine Architecture

| Field | Value |
|---|---|
| **Objective** | The first real architecture phase for `FCR-0027` — decide `ADR-0013` classification (platform service vs. module set), scope, and relationship to `Tempest.Core.EngineeringData` and `Tempest.Core.Verification`, mirroring `WP7.0B Candidate Work Package Catalogue.md`'s own "Candidate I" entry, carried forward unchanged in substance. |
| **Dependencies** | `FCR-0029` (Engineering Data Model) — Implemented, certified. `FCR-0033` (Verification Framework) — Implemented, certified. No outstanding technical dependency. |
| **Expected Outputs** | An architecture document; one or more ADRs; the `ADR-0013` classification decision `FCR-0027`'s own register entry names as still open; a Contract Review package mirroring `WP7.0C Engineering Foundation Contracts.md`'s own precedent. |
| **Academy Impact** | First Academy content for the Systems Engineering category — a genuinely new kind of content, not an extension of an existing pattern. |
| **Engineering Complexity** | Unknown at architecture time, by design — `WP7.0B Candidate Work Package Catalogue.md` disclosed this as "the least architecturally grounded candidate in this catalogue," and this review confirms nothing has changed that assessment since. |
| **Security Considerations** | A requirement is a new asset class (traceability/programme data) not previously threat-modelled at this level of detail — `Threat Model.md`'s own assumption 1 names "requirements" directly, but no dedicated requirements-specific threat model addendum exists yet. Recommend one as part of this candidate's own architecture phase, mirroring `WP 6.1`'s own addendum for Identity. No new trust boundary is anticipated — this remains trusted, first-party, in-process code. |

### Candidate L — Requirements Engine Implementation

| Field | Value |
|---|---|
| **Objective** | Implement whatever Candidate K's own architecture phase approves — a real `IRequirementsService` (or equivalent), storing requirements as `IEngineeringDocument`s, consuming `IVerificationService` for demonstrated-status recording exactly as `WP7.1E Future Capability Recommendations.md` Recommendation 1 specifies. |
| **Dependencies** | Candidate K (Requirements Engine Architecture) must complete and pass Engineering Review first. |
| **Expected Outputs** | A new `Tempest.Core.Requirements` (or equivalently-named) namespace; a sample module demonstrating the full requirement-record-verify cycle; a full test suite mirroring the Engineering Foundation's own testing discipline (unit, integration, failure injection, concurrency, traceability). |
| **Academy Impact** | A 13-section implementation retrospective; a new concept guide, likely distinguishing a Requirement from an `IEngineeringDocument` generally (the same "worked example vs. genuinely new pattern" judgement `WP7.0C Academy Plan.md` applied to Materials). |
| **Engineering Complexity** | Depends entirely on Candidate K's own scope decision — likely Medium-High, mirroring the Engineering Foundation's own more substantial frameworks (Calculation, Verification) rather than its simplest (Units & Quantities). |
| **Security Considerations** | A dedicated Security Review is recommended, mirroring `WP 7.1D`/`WP 7.1E`'s own precedent — this would be the third consecutive Engineering Foundation-adjacent Work Package to include one, continuing what `WP7.1E Lessons Learned.md` itself recommended ("a dedicated Security Review should continue for any future Engineering Module built on this foundation"). |

### Candidate M — Traceability Reporting

| Field | Value |
|---|---|
| **Objective** | A read-side capability presenting the full chain — requirement, verification record, and (where applicable) calculation record — as a coherent traceability report, consuming `Tempest.Core.Reporting` (`WP 6.0`) rather than inventing new presentation logic, mirroring `Tempest.Core.Verification`'s own explicit "no report formatting" exclusion (Principle 28). |
| **Dependencies** | Candidate L (Requirements Engine Implementation); `Tempest.Core.Reporting` (`WP 6.0`) — already Implemented. |
| **Expected Outputs** | A new `IReportDefinition`/`IReportRenderer<T>` pair specific to traceability reporting; a sample module demonstrating end-to-end requirement-to-verification report generation. |
| **Academy Impact** | Likely folded into Candidate L's own retrospective as a worked example of Reporting composed with a new Engineering Module, rather than a standalone concept guide — the same judgement `WP7.0C Academy Plan.md` applied to Materials. |
| **Engineering Complexity** | Low-Medium — primarily composition of two already-proven mechanisms (Reporting, Requirements/Verification), not new infrastructure. |
| **Security Considerations** | None beyond what Reporting and Verification already establish — permission-gating at the calling layer, mirroring every existing Reporting consumer. |

## Explicitly Not Recommended at This Time

- **Any Candidate for Programme F (Platform Hardening)** — not because it
  lacks merit (`WP7.2A Programme Comparison Matrix.md` scores it second-
  highest), but because `WP7.2A Recommended Programme.md` sequences it
  after Programme A, at `v0.9.0`. `WP7.0B Candidate Work Package
  Catalogue.md`'s own Candidates A–C remain valid, unmodified
  descriptions of this future work — this catalogue does not restate
  them.
- **Any Candidate for Programmes B, C, D, E, or G** — no capability
  exists to scope a Work Package against; scoping one now would require
  inventing the very capability `WP7.0B Engineering Discipline
  Assessment.md` found no evidence for.
- **Candidate J (Project Engine Architecture)**, from `WP7.0B Candidate
  Work Package Catalogue.md` — remains a valid future candidate, not
  rejected, but this Work Package's own seven named programmes do not
  include Project Management as a distinct option; Candidate J is
  unchanged and available whenever a future roadmap review selects
  Project Management specifically.

## Related Documents

`WP7.2A Recommended Programme.md`; `WP7.0B Candidate Work Package
Catalogue.md` (Candidates A–C, I, reused/extended here as K–M);
`docs/governance/Future Capability Register.md` (`FCR-0027`); `WP7.1E
Future Capability Recommendations.md`.
