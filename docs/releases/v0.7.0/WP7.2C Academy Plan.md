# WP 7.2C — Academy Plan

## Purpose

Confirms and extends `WP7.2B Academy Plan.md`'s own recommendation, now
that the Requirements Platform's own complete contracts exist, mirroring
`WP7.0C Academy Plan.md`'s own role for the original five Engineering
Foundation frameworks.

This document does not itself add anything to `docs/academy/Academy
Index.md` — no new concept guide exists yet for the Requirements
Platform, since none is implemented. This Work Package's own
retrospective (a whole-review, contract-review-only document, not a
13-section implementation template) is this Work Package's own sole
Academy contribution — see `WP7.2C Lessons Learned.md`.

## Required Reading (Owning Implementation Work Package)

Unchanged from, and extending, `WP7.2B Academy Plan.md`'s own list:

- `docs/academy/06 Engineering Standards/Engineering Governance.md`;
  `VISION.md`; `docs/engineering/Engineering Principles.md`.
- `WP7.2B Requirements Platform Architecture.md` and its ten companion
  architecture deliverables.
- `WP7.2C Requirements Platform Contracts.md` and its eleven companion
  contract-review deliverables — the specific, proposed contracts and
  the open questions its own architecture-confirmation pass must
  resolve (`WP7.2C Required ADR Catalogue.md`).
- `13-calculation-framework.md`, `14-verification-framework.md` — the
  two closest existing "why this is not the same as its structurally
  similar sibling" concept guides.
- `WP7.1C-materials-framework-implementation.md` — required reading for
  the business-identifier index pattern this Platform's own
  `IRequirementsService.FindByIdentifierAsync` directly reuses.

## Required Output (Owning Implementation Work Package)

- A 13-section implementation retrospective under `docs/academy/03 Work
  Packages/`, following the standard template.
- **A new concept guide is required**, confirmed unchanged from
  `WP7.2B Academy Plan.md`'s own recommendation — teaching the
  three-layer model (`WP7.2B Systems Engineering Architecture.md`) as
  its primary content, with a worked comparison distinguishing a
  `Requirement` from an ordinary `IEngineeringDocument`, and a
  Requirement/Verification-Link/Audit three-way distinction extending
  `14-verification-framework.md`'s own existing comparison.
- **A second, focused concept-guide section (or a standalone short
  guide) on the relationship-kind vocabulary and traceability model**
  (`WP7.2C Relationship Model.md`, `WP7.2C Traceability Contract.md`) —
  newly identified as a required output by this Work Package, not named
  by `WP7.2B Academy Plan.md`, since the complete relationship
  vocabulary and its own traceability guarantees (and one disclosed
  limitation — reverse allocation traceability against an open-string
  target) did not exist in enough concrete detail to teach until this
  contract review produced it.
- Updates to `docs/architecture/Platform Service Map.md` and
  `Engineering Glossary.md`, following the identical pattern every prior
  Platform Service addition already established.

## Summary Table

| Deliverable | New Concept Guide Content? | Rationale |
|---|---|---|
| Requirements Engine / Requirement (primary concept) | **Yes** | Confirmed unchanged from `WP7.2B Academy Plan.md` |
| Relationship Model / Traceability | **Yes — newly identified by this Work Package** | The concrete relationship vocabulary and its own reverse-traceability limitation are contract-review-stage findings, not available at the architecture stage |
| Verification Integration | **No — a section within the primary guide** | Extends `14-verification-framework.md`'s own existing comparison; does not warrant a fully separate guide |

## Engineering Principles Review

**Finding: unchanged from `WP7.2B Academy Plan.md` — no extension to
`docs/engineering/Engineering Principles.md` is warranted, and none is
added.** This Work Package, like `WP 7.2B`, produced contracts only, no
implementation — there remains no working code to derive a genuine
Systems Engineering principle from. This finding is re-confirmed, not
merely repeated: this Work Package's own contract-level detail (the
lifecycle model, the relationship vocabulary, the traceability
guarantees) could plausibly have tempted a premature principle
statement (e.g., "every requirement's own status is caller-driven, never
inferred") — considered and declined, since the document's own governing
rule requires derivation from *implemented* architecture, not from a
well-reasoned but still-unimplemented contract.

## Related Documents

`WP7.0C Academy Plan.md`; `WP7.2B Academy Plan.md` (the immediate
precedent this document confirms and extends); `WP7.2C Requirements
Platform Contracts.md`; `WP7.2C Relationship Model.md`; `WP7.2C
Traceability Contract.md`; `docs/engineering/Engineering Principles.md`.
