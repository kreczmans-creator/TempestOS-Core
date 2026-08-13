# TempestOS Governance Index

## Purpose

One navigable table of contents for the entire Governance suite — every
register, organised by category, so a reader can find "where is X
tracked" without knowing which category folder it lives in first. This
index is itself governance material, maintained under the same
obligation as every register it indexes: a Work Package that adds or
changes a register updates this index as part of the same change.

This suite was established by `WP 4.5A` (Governance Register Baseline,
2026-07-25) — the first complete governance baseline TempestOS has
produced. See `Governance Philosophy.md` for *why* this suite exists, and
`Governance Audit Report.md` for the validation performed when it was
built.

## How to Read This Suite

Every register begins with the same metadata block — Register Name,
Purpose, Scope, Owner, Source of Truth, Review Frequency, Last Reviewed,
Related Documents, Related ADRs, Related Academy Articles, Coverage
Status. **Read the Source of Truth field before trusting a register's own
content**: many registers here are deliberately thin governance indexes
over a fuller, pre-existing document (`Platform Service Map.md`,
`Risks.md`, `Rejected Designs.md`) rather than a duplicate of it — the
register adds Verified/Inferred/Unknown status and cross-referencing, the
source document keeps the full reasoning.

Every entry in every register is one of **Verified** (read directly from
a repository artifact), **Inferred** (a reasonable conclusion from
available evidence, not directly stated), or **Unknown** (evidence does
not exist to establish it, recorded honestly rather than invented) — see
`Governance Philosophy.md` for why this discipline matters more than
completeness.

## Architecture

Registers tracking *decisions* — what was decided, what was rejected, and
where the reasoning lives.

- [ADR Register](Architecture/ADR%20Register.md) — all 39 Architecture Decision Records.
- [Rejected Designs Register](Architecture/Rejected%20Designs%20Register.md) — all 45 Rejected Designs entries.
- [Architecture Document Register](Architecture/Architecture%20Document%20Register.md) — all 20 standing architecture documents.
- [Decision Register](Architecture/Decision%20Register.md) — significant process/sequencing decisions that don't meet ADR criteria.

## Engineering

Registers tracking *what exists in code* — services, modules, contracts,
and the dependency graph between them.

- [Platform Services Register](Engineering/Platform%20Services%20Register.md)
- [Module Register](Engineering/Module%20Register.md)
- [Hosted Services Register](Engineering/Hosted%20Services%20Register.md)
- [Plugin Register](Engineering/Plugin%20Register.md)
- [Event Catalogue](Engineering/Event%20Catalogue.md)
- [Dependency Injection Register](Engineering/Dependency%20Injection%20Register.md)
- [Namespace Register](Engineering/Namespace%20Register.md)
- [Interface Register](Engineering/Interface%20Register.md)
- [Exception Register](Engineering/Exception%20Register.md)
- [Architectural Dependency Register](Engineering/Architectural%20Dependency%20Register.md)
- [Engineering Vocabulary Register](Engineering/Engineering%20Vocabulary%20Register.md) — every live Kind, `Classification`, and `RelationshipKind` string value, its one canonical declaring class, and its meaning (`ADR-0105`, `WP 12.1B`).

## Quality

Registers tracking *whether the platform can be trusted* — risk, debt,
validation gates, and test coverage.

- [Risk Register](Quality/Risk%20Register.md)
- [Technical Debt Register](Quality/Technical%20Debt%20Register.md)
- [Validation Register](Quality/Validation%20Register.md)
- [Test Register](Quality/Test%20Register.md)
- [Repository Metrics Register](Quality/Repository%20Metrics%20Register.md)

## Documentation

Registers tracking *the documentation itself* — where every document
lives and whether the Academy's own maintenance obligation is being met.

- [Documentation Register](Documentation/Documentation%20Register.md)
- [Academy Register](Documentation/Academy%20Register.md)
- [Engineering Standards Register](Documentation/Engineering%20Standards%20Register.md)
- [Governance Register](Documentation/Governance%20Register.md)

## Security

Standing documents tracking *what TempestOS protects, against whom, and
to what standard* — the security baseline every future Work Package's
Definition of Done is checked against.

- [Threat Model](../security/Threat%20Model.md) — assets, actors, trust boundaries, and threat scenarios.
- [Security Principles](../security/Security%20Principles.md) — the standing security principles the platform is designed against.
- [Platform Security Review v0.5.0](../security/Platform%20Security%20Review%20v0.5.0.md) — the first comprehensive security audit; establishes the v0.5.0 Security Baseline.
- [Security Roadmap](../security/Security%20Roadmap.md) — prioritised future security work, sequenced against the Threat Model's own assumptions.

## Delivery

Registers tracking *what shipped, when, and why* — features, releases,
the project's own evolving discipline, and end-to-end traceability.

- [Feature Register](Delivery/Feature%20Register.md)
- [Release Register](Delivery/Release%20Register.md)
- [Engineering Evolution Register](Delivery/Engineering%20Evolution%20Register.md)
- [Traceability Matrix](Delivery/Traceability%20Matrix.md) — the capstone: Requirement → Work Package → ADR → Architecture → Implementation → Tests → Academy → Release, per major capability.

## Product & Roadmap

Registers tracking *what TempestOS builds next, and why* — established
`WP 7.0A` (Future Capability Register & Product Vision), replacing every
informal "Car Park" discussion with a permanent, cited register.

- [Future Capability Register](Future%20Capability%20Register.md) — every identified future capability, `FCR-0001` onward, each traced to a specific prior document.
- [Capability Categories](Capability%20Categories.md) — the fixed classification vocabulary the Future Capability Register uses.
- [Product Roadmap](Product%20Roadmap.md) — high-level, phase-based sequencing of what has shipped and what may come next.

## Top-Level Governance Documents

- [Governance Philosophy](Governance%20Philosophy.md) — why this suite exists, why Unknown beats invented data, ownership and review expectations.
- [Governance Audit Report](Governance%20Audit%20Report.md) — the validation performed when this suite was first built (`WP 4.5A`).
- [Repository Maturity Report](Repository%20Maturity%20Report.md) — a point-in-time maturity assessment across every major repository area.
- [Future Work Package Guidelines](Future%20Work%20Package%20Guidelines.md) — the standing, mandatory expectations for every Work Package from `WP 4.6A` onward, established at the Foundation phase's close (`WP 4.5B`).

## Related to This Suite, Outside `docs/governance/`

- [`VISION.md`](../../VISION.md) (repository root) — the permanent product vision document, established `WP 7.0A`; the "why" behind every entry in the Future Capability Register, above.
- [`docs/engineering/Engineering Principles.md`](../engineering/Engineering%20Principles.md) — the principles engineering-domain content itself must uphold, established `WP 7.1A`; distinct from `docs/academy/06 Engineering Standards/` (which governs how TempestOS is built as software, not what its engineering-domain content must be).
- [`PROJECT_STATUS.md`](../../PROJECT_STATUS.md) (repository root) — the primary status dashboard; its Repository Metrics and Governance Status sections are generated from this suite and should be updated together with it.
- [`docs/releases/Platform Foundation Completion Report.md`](../releases/Platform%20Foundation%20Completion%20Report.md) — the Foundation phase closeout narrative (`WP 4.5B`), summarising everything this suite tracks in aggregate.
- [`docs/academy/Contributor Learning Path.md`](../academy/Contributor%20Learning%20Path.md) — the repository-wide onboarding sequence, which routes a new contributor into this suite at the appropriate point.

## Maintaining This Suite

Every register's own "Review Frequency" field states when it should next
be revisited — most commonly, "whenever the Work Package that changes
this subject matter lands." A Work Package that changes a platform
service, adds an ADR, ships a new module, or otherwise touches something
a register tracks updates that register as part of its own Definition of
Done — exactly the same discipline Engineering Governance §6 already
requires for the Academy and `Platform Service Map.md`, now extended
explicitly to this suite.

## Related Documents

`docs/academy/06 Engineering Standards/Engineering Governance.md` (the
constitution this suite operationalises); `docs/academy/Academy Index.md`
(the Academy's own navigation index, this suite's sibling); `docs/architecture/Platform Service Map.md`,
`Rejected Designs.md`, `Engineering Glossary.md` (the deepest sources of
truth several registers here index).
