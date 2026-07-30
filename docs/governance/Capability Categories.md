# Capability Categories

## Register Metadata

| Field | Value |
|---|---|
| **Register Name** | Capability Categories |
| **Purpose** | The fixed vocabulary `Future Capability Register.md` classifies every capability against — one category per row, so related capabilities can be found, grouped, and sequenced together regardless of which Work Package first identified them. |
| **Scope** | Every category a future TempestOS capability could belong to, whether a capability exists in the register yet or not. |
| **Owner** | Project Maintainer. |
| **Source of Truth** | This document; `VISION.md` (the product ambition each category exists to serve); `docs/security/Threat Model.md` (the assumptions that first named several of these domains). |
| **Review Frequency** | Reviewed whenever a new category is needed — additive only; an existing category is never renamed or removed once a capability references it, per Governance Philosophy's own "never silently invalidate a prior reference" discipline. |
| **Last Reviewed** | 2026-07-30 (`WP 7.0B`, Engineering Foundation Planning & Capability Architecture) — `Materials` and `Quality` categories each populated with one entry (`FCR-0031`, `FCR-0033`). Previously reviewed 2026-07-30 (`WP 7.0A`, established). |
| **Related Documents** | `Future Capability Register.md`; `Product Roadmap.md`; `VISION.md`; `docs/security/Threat Model.md`; `docs/security/Security Roadmap.md`. |
| **Related ADRs** | ADR-0013 (Platform Service vs. Module classification — the categorisation this document performs at the capability level, ADR-0013 performs at the individual-service level). |
| **Related Academy Articles** | None yet — this register is new. |
| **Coverage Status** | Complete as a category list; **most categories are not yet populated with a real capability** — see `Future Capability Register.md`'s own Coverage Note for which categories have zero entries today, disclosed explicitly rather than filled with invented candidates. |

---

## Purpose of This Document

TempestOS has, until this Work Package, only ever named specific
capabilities in fragments: a Technical Debt Register entry here, a
"Future Extension Points" section in a Contract Review document there,
one aspirational sentence in `PROJECT_STATUS.md`'s own Long-Term Vision.
`WP 7.0A` establishes a single, permanent classification model so every
future capability — however it is first identified — lands in exactly
one place, categorised consistently, findable by discipline.

This document does not itself list capabilities (see `Future Capability
Register.md` for that); it defines the categories a capability is
classified against, and is deliberately structured so a new category can
be added later without restructuring anything that already exists —
adding a row to the table below, never renumbering or removing one.

## Category Model

Two kinds of category exist side by side, because TempestOS itself is
two things: a software platform, and (per `Threat Model.md`'s own
governing assumptions, established at `WP 5.0S`) the eventual host for
real engineering-discipline capability. A capability is classified as
**Platform** category when it is infrastructure the platform itself
needs regardless of which engineering discipline eventually runs on it;
it is classified as an **Engineering Discipline** category when it is
domain-facing capability a specific kind of engineering practice would
recognise as its own.

### Platform Categories

| Category | Scope |
|---|---|
| **Platform** | Core runtime, hosting, and platform-service capability — the kind every `v0.4.0`–`v0.6.0` Work Package has shipped so far (Configuration, Logging, DI, the Event Bus, Navigation, the Command Framework, Diagnostics, Settings, Audit, Notifications, Reporting, the REST API, Export/Import, Licensing). A capability belongs here when the rest of the platform, including every engineering discipline module, needs it to function at all — mirroring `ADR-0013`'s own platform-service test. |
| **Infrastructure** | Cross-cutting technical capability that is not itself a platform *service* in `ADR-0013`'s sense, but that many future capabilities depend on regardless of discipline — multi-user/tenant isolation, cloud synchronisation, offline/mobile client support, deployment and hosting concerns beyond the single-process model `v0.3.0`–`v0.6.0` have used. |
| **Integrations** | Capability whose primary purpose is connecting TempestOS to something outside itself — a third-party plugin ecosystem, a webhook/callback consumer, an external CAD or PLM tool, an identity provider. Distinct from Infrastructure: an Integration is always *to* a specific external system; Infrastructure is internal cross-cutting capability. |
| **AI** | Capability that adds an AI or automation-driven caller or capability to the platform — not a redesign of any existing service, but a new kind of consumer or capability layered on top of what already exists (the Command Framework's own design already anticipates this, see `FCR-0024`). |
| **Academy** | Capability that extends how TempestOS teaches itself to contributors — beyond the existing retrospective/concept-guide model, should a concrete need for something more (interactive onboarding tooling, for example) ever be identified. |
| **Commercial** | Capability supporting how TempestOS is licensed, sold, deployed, or operated as a commercial product — licensing tiers, remote activation, compliance postures (including the defence-sector posture `Threat Model.md` assumption 10 names), contract/customer data handling. |

### Engineering Discipline Categories

| Category | Scope |
|---|---|
| **Systems Engineering** | Requirements management, verification and validation, traceability across a real engineering programme — the domain `PROJECT_STATUS.md`'s own Long-Term Vision names as the "Requirements Engine," and `Threat Model.md` assumption 1 (requirements, analysis, verification records) anticipates. |
| **Project Management** | Programme/project-level planning, scheduling, and tracking for an engineering effort — the domain `PROJECT_STATUS.md`'s own Long-Term Vision names as the "Project Engine," and the domain the bootstrap-era, currently-dead `ProjectModel`/`JsonProjectRepository` code already modelled in outline (`Classification`, `Customer`, `ContractNumber`). |
| **Mechanical Engineering** | Mechanical design, analysis, and documentation capability. |
| **Structural Engineering** | Structural design, analysis, and documentation capability. |
| **Electrical Engineering** | Electrical design, analysis, and documentation capability. |
| **Building Services / HVAC** | Building-services and HVAC design, analysis, and documentation capability. |
| **Materials** | Material selection, specification, and traceability capability. |
| **Manufacturing** | Manufacturing process planning, work instructions, and production-facing capability. |
| **Quality** | Engineering quality management — inspection, non-conformance, verification records — distinct from this repository's own internal software-quality governance (`docs/governance/Quality/`), which tracks TempestOS's own development quality, not a customer engineering programme's. |

## Coverage Note

**Updated `WP 7.0B`:** `Materials` and `Quality` each now have exactly
one entry — `FCR-0031` (Materials Framework) and `FCR-0033`
(Verification & Validation Framework) respectively — identified by `WP
7.0B`'s own Capability Dependency Analysis as cross-cutting foundation
capabilities every eventual discipline module in these categories would
structurally require, not a discipline-specific capability within
either. Both are marked **Inferred** in `Future Capability Register.md`,
not Verified.

**No capability has yet been identified for five of the nine Engineering
Discipline categories** (Mechanical, Structural, Electrical, Building
Services/HVAC, Manufacturing) — recorded honestly, per this project's
own standing discipline of disclosing an Unknown rather than inventing a
plausible-sounding candidate to fill it. `Threat Model.md` assumption 1
confirms these disciplines are within TempestOS's eventual mission
("engineering intellectual property (CAD, requirements, analysis,
verification records)"), but no Work Package, retrospective, ADR, or
Contract Review document reviewed through `WP 7.0B` names a specific
capability within any of them. These five categories exist now so a
future capability-identification exercise — almost certainly its own
dedicated Work Package, per this document's own extensibility goal — has
a fixed place to classify what it finds, not so this register can claim
false completeness today. `WP 7.0B`'s own Engineering Discipline
Assessment confirmed this gap cannot be closed by further documentation
mining — see `docs/releases/v0.7.0/WP7.0B Engineering Discipline
Assessment.md`.

`AI` and `Academy` each have at most one sourced candidate (`AI`: one,
see `FCR-0024`; `Academy`: none) — also disclosed rather than padded.

## Extensibility

A new category is added as a new row in the appropriate table above,
never by renumbering or restructuring an existing row. No capability in
`Future Capability Register.md` references a category by number, only by
name, so adding a category never invalidates an existing entry.

## Related Documents

`Future Capability Register.md`; `Product Roadmap.md`; `VISION.md`;
`docs/security/Threat Model.md`; `docs/security/Security Roadmap.md`;
`ADR-0013`; `PROJECT_STATUS.md` (Long-Term Vision section).
