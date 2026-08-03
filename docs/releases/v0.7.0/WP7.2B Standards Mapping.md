# WP 7.2B — Standards Mapping

## Status

Architecture only. **This document does not implement any specific
engineering standard and remains strictly industry-neutral** — it
identifies the generic architectural capability a future standards-
compliance need would draw on, never a specific clause, checklist, or
compliance workflow for any named standard.

## Purpose

Per this Work Package's own controlling instruction, identifies the
architectural support a future engineering standard would require of
the Requirements & Verification Platform — using the seven example
standard families named (ISO 9001, ISO 15288, IEC 61508, DO-178, Medical
Device standards, Defence standards, Nuclear standards) only as
illustrative categories, never as specific compliance targets this
architecture designs toward.

## Generic Architectural Capabilities Every Standard Family Would Draw On

Reviewed across all seven named families, four generic capabilities
recur — each already provided, at the architectural level, by this
Work Package's own design:

| Generic Capability | Already Provided By | Evidence |
|---|---|---|
| **Bidirectional traceability** (a requirement traces to what satisfies it and to what it derives from) | Requirement Trace Link (`WP7.2B Requirements Domain Model.md` §6), reusing `LinkAsync`/`GetReferencesAsync` | Already proven at the mechanism level by `Verification`'s own identical reuse (`ADR-0057`) |
| **Baseline / configuration management** (a defined, frozen set of requirements at a point in time) | Requirement Collection (§2) combined with the Engineering Data Model's own revision history — a baseline is a named collection at a specific point in each member's own revision timeline | Inherited from `IDocumentRevision`'s own existing immutability guarantee (Principle 4) |
| **Independent verification/review evidence** (the verifier is not necessarily the author) | `IVerificationRecord.VerifiedByPrincipalId`, already distinct from a requirement's own `CreatedByPrincipalId` | Already implemented, `Tempest.Core.Verification` |
| **Objective evidence retention** (a documented, retrievable basis for every claim) | Requirement Evidence (§8), aggregating `VerificationRecord.Evidence` and linked calculation/document data | Already implemented at the underlying-fact level; aggregation is this Platform's own new, but non-duplicative, contribution |

## Per-Family Architectural Implications (Illustrative Only, Not a Compliance Target)

| Standard Family | Generic Domain | Architectural Implication | Status |
|---|---|---|---|
| **ISO 9001** (Quality Management) | General quality management | Requires traceable non-conformance and corrective-action records against a requirement or process — maps to Requirement Evidence and Trace Link; no non-conformance-specific type is designed here (`Quality` category already has one cross-cutting entry, `FCR-0033` Verification, per `Capability Categories.md`) | Generic capability present; no ISO-9001-specific behaviour implemented or planned |
| **ISO 15288** (Systems and Software Engineering — System Life Cycle Processes) | Systems engineering process | Requires requirement allocation across a system hierarchy and full lifecycle traceability from stakeholder need to verified requirement — maps directly to Requirement Group (hierarchy), Requirement Allocation, and Requirement Trace Link | Generic capability present; this is the standard this Platform's own domain model most closely mirrors in shape, without implementing any of its specific process requirements |
| **IEC 61508** (Functional Safety) | Safety-critical systems | Requires independent verification, a documented safety case (an evidence aggregation), and — for higher integrity levels — approval/sign-off records | Traceability and evidence aggregation present; **electronic approval is a named Future Capability** (`WP7.2B Security Architecture.md`), not yet built |
| **DO-178** (Software Considerations in Airborne Systems) | Aviation software assurance | Requires bidirectional traceability between requirements, design, code, and test/verification evidence, plus configuration management of baselines | Traceability, allocation, and baseline (Requirement Collection) capabilities present at the architectural level; no DO-178-specific artifact or process is implemented |
| **Medical Device standards** (e.g., IEC 62304-shaped processes) | Regulated medical software/device engineering | Requires traceability from a user need through a requirement to verification and risk-control measures, plus tamper-evident record retention | Traceability and evidence present; **tamper resistance remains Technical Debt** (`WP7.2B Security Architecture.md`) — a real, disclosed gap this architecture does not resolve |
| **Defence standards** | Defence-sector engineering programmes | Requires classification/security-marking of requirements and export-controlled data handling, alongside traceability | Traceability present; classification/marking maps to `FCR-0026` (Defence-Sector/Regulated-Environment Compliance) — explicitly **not** designed here, gated on a real, named defence-sector opportunity per that entry's own existing disposition |
| **Nuclear standards** | Nuclear-sector engineering programmes | Similar profile to Defence — rigorous traceability, independent verification, and long-term, tamper-resistant record retention | Traceability and independent verification present; tamper resistance remains Technical Debt, classification remains `FCR-0026`'s own gated future scope |

## What This Mapping Confirms

**Every one of the seven illustrative standard families draws on the
same small set of generic capabilities** (traceability, baseline
management, independent verification, evidence retention) — none
requires a standard-specific mechanism this architecture would need to
design differently per standard. This is itself the architectural
justification for building one discipline-neutral Systems Engineering
Foundation rather than a standard-specific compliance module: the
generic capability, built once, already covers the shared need every
named family expresses, exactly as `WP7.2A Recommended Programme.md`'s
own "Engineering Workflow, not Engineering Disciplines" recommendation
anticipated.

**Two capabilities recur as gaps across the higher-assurance families
specifically** (IEC 61508, Medical Device, Defence, Nuclear): electronic
approval/sign-off and tamper resistance. Both are already named as
Future Capabilities or Technical Debt in `WP7.2B Security
Architecture.md` — this mapping does not add new findings, it confirms
those two items are the correct ones to prioritise **if and when** a
real opportunity in one of these higher-assurance sectors materialises,
consistent with Security Principle 7's own "do not build ahead of
demonstrated need."

## What This Document Deliberately Does Not Do

- Does not implement, or design implementation for, any clause of any
  named standard.
- Does not commit this Platform to certification against any named
  standard.
- Does not invent a compliance workflow, checklist, or audit process
  specific to any named standard.
- Does not treat the seven named families as an exhaustive or
  prioritised list — they are the Work Package's own illustrative
  examples, reviewed as given, not expanded or ranked.

## Related Documents

`WP7.2B Requirements Domain Model.md`; `WP7.2B Security Architecture.md`;
`docs/governance/Future Capability Register.md` (`FCR-0026`);
`docs/governance/Capability Categories.md` (`Quality` category);
`docs/security/Threat Model.md` (assumption 10).
