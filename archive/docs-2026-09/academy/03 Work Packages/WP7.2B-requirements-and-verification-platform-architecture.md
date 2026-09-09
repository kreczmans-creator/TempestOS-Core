# WP 7.2B — Requirements & Verification Platform Architecture

## What This Document Is

Like `WP 7.0A`, `WP 7.0B`, `WP 7.0C`, and `WP 7.2A` before it, this is
not a standard 13-section implementation retrospective — `WP 7.2B`
shipped no production code, no test, and no compiled interface. It
mirrors the same whole-review shape (What Was Achieved, Architectural
Lessons, Implementation Lessons, Repository Maturity, Recommendations,
Key Takeaways), because this Work Package, like those four, is an
architecture and planning milestone rather than a feature implementation.

## Introduction

`WP 7.2A` recommended, and Product Approval accepted, Programme A
(Requirements & Verification Platform) as the Engineering Foundation
programme's own continuation into Systems Engineering. `WP 7.2B`'s own
controlling instruction asked this Work Package to design the complete
architecture for that Platform — every domain concept, every
integration point, every security and standards implication — while
introducing zero discipline-specific engineering behaviour and zero
production code, mirroring `WP7.0C Engineering Foundation Contracts.md`'s
own identical role for the original five Engineering Foundation
frameworks.

## What Was Achieved

Eleven completion deliverables produced under `docs/releases/v0.7.0/`,
prefixed `WP7.2B`: a Requirements Platform Architecture (the overall
design and `ADR-0013` classification), a Systems Engineering Architecture
(the three-layer Engineering Core → Systems Engineering Foundation →
Engineering Discipline Modules model), a Digital Thread Architecture
(illustrating engineering information flow, disclosing which links are
proven today versus architecturally correct but not yet built), a
Requirements Domain Model (twelve concepts, each an architectural
responsibility reusing an existing Engineering Core mechanism), a
Platform Integration Report (Verification, Reporting, REST API
integration points), a Dependency Analysis (eleven Platform Core/
Engineering Core services reviewed for direction, ownership, lifetime,
and layering), a Security Architecture (nine dimensions classified,
finding one genuinely new Technical Debt item), a Standards Mapping
(seven illustrative standard families, kept industry-neutral throughout),
a Required ADR Catalogue (three reserved decisions, `ADR-0058`–
`ADR-0060`), an Academy Plan (finding no Engineering Principles
extension is warranted yet), and this retrospective.

## Architectural Lessons

**The single hardest decision in this architecture — how a Requirement
Allocation target is represented — was resolved by declining to make it
concrete.** Modelling an allocation target as either a reference to any
`IEngineeringDocument` or an open, unvalidated string when none exists
yet looks like an unresolved question; it is in fact the discipline-
neutrality requirement's own correct answer, mirroring
`CalculationContext.ReferenceMaterial`'s and `VerificationContext.
ReferenceMaterial`'s own identical open-reference precedent one layer
down. The lesson generalises past this Work Package: discipline
neutrality is sometimes enforced by refusing to specialise a type, not
by adding an abstraction layer over a specialisation that was made
anyway.

**Reviewing eleven Platform Core/Engineering Core services together, for
dependency direction and layering, confirmed something the Engineering
Core itself already proved**: this Platform requires zero new platform
capability to integrate cleanly. Every integration point it needs
(`IEngineeringDocumentStore`, `IVerificationService`, calling-layer
`IPermissionEvaluator`/`IAuditRecorder` composition) already exists,
proven, unmodified — a strong, direct confirmation that
`WP7.2A Recommended Programme.md`'s own "completed technical foundation"
finding was correct, not merely plausible.

## Implementation Lessons

**An architecture-only Work Package can still find a genuine new
security gap, simply by asking what a new kind of consumer stresses that
no prior consumer did.** `WP7.2B Security Architecture.md`'s own
"Concurrent editing" finding — no compare-and-swap check exists on
`ReviseAsync` — was not previously disclosed anywhere in the Engineering
Foundation programme, because no prior consumer (Materials, Calculations,
Verification) had a genuinely multi-author, collaborative-editing usage
profile. A Requirements Engine's own target users (a systems engineering
team, not a single calculation author) does. The lesson: a new
consumer's own usage shape is itself worth a fresh security pass, even
against an already twice-reviewed foundation.

**Reviewing seven illustrative standards together, rather than
one at a time, surfaced the shared generalisable capability
(traceability, baseline management, independent verification, evidence
retention) almost immediately** — the same efficiency `WP7.2A Strategic
Roadmap Review.md` found reviewing seven candidate programmes together.
This is now the second consecutive Work Package in this project's
history to find that reviewing a set of options side by side, rather
than sequentially, surfaces a shared pattern faster than incremental
analysis does.

**"No principle extension is warranted" is itself a disciplined,
evidence-based finding, not a gap.** `docs/engineering/Engineering
Principles.md`'s own governing rule — derive from shipped code, never
assert in advance — meant this Work Package, having produced no
implementation, correctly declined to add a Systems Engineering
principle. Stating this explicitly in `WP7.2B Academy Plan.md`, rather
than silently omitting the section, keeps this Work Package's own
review checklist honest.

## Repository Maturity

TempestOS now has a complete, evidence-grounded architecture for its
own first Engineering Discipline capability — twelve domain concepts,
three reserved ADRs, and eleven completion deliverables, produced with
zero production code and zero new dependencies on anything not already
certified. This is the fourth consecutive architecture-only Work
Package in this project's history to advance the project materially
without shipping code (`WP 7.0A`, `WP 7.0B`, `WP 7.0C`, `WP 7.2A`, now
`WP 7.2B`) — a pattern this project's own governance continues to treat
as legitimate progress, provided each produces a genuinely decision-
useful, evidence-backed artefact.

## Recommendations for the Next Work Package

- **Resolve `WP7.2B Required ADR Catalogue.md`'s own three reserved
  decisions (`ADR-0058`–`ADR-0060`) before writing any production code**
  — mirroring every Engineering Foundation implementation Work Package's
  own identical discipline of confirming its own reserved ADR before
  implementation begins.
- **Do not pre-empt `ADR-0060`'s own concurrent-editing question with a
  speculative mechanism** — resolve it against real evidence of the
  Requirements Platform's own actual usage pattern, once real usage
  exists to observe.
- **Write the recommended new concept guide only once real, tested code
  exists to derive its own worked examples from** — consistent with
  every existing Academy concept guide's own origin, and with this
  Work Package's own explicit "no principle invented ahead of evidence"
  discipline applied one document further.

## Key Takeaways

1. A cross-cutting foundation's own design discipline (reuse over
   reinvention, calling-layer composition, open references for soft
   dependencies) generalises to a second architectural layer without
   needing to be re-derived — it was proven once, at the Engineering
   Core, and applied again here unchanged.
2. Discipline neutrality is sometimes best enforced by deliberately
   declining to specialise a type, with the reasoning stated explicitly,
   rather than by adding abstraction over a specialisation made anyway.
3. A new consumer of an already-certified foundation is itself a fresh
   security-review trigger — this Work Package found a genuine, real gap
   no prior Engineering Foundation review had reason to look for.

## Related Documents

`WP7.2B Requirements Platform Architecture.md`; `WP7.2B Systems
Engineering Architecture.md`; `WP7.2B Digital Thread Architecture.md`;
`WP7.2B Requirements Domain Model.md`; `WP7.2B Platform Integration
Report.md`; `WP7.2B Dependency Analysis.md`; `WP7.2B Security
Architecture.md`; `WP7.2B Standards Mapping.md`; `WP7.2B Required ADR
Catalogue.md`; `WP7.2B Academy Plan.md`; `WP7.2B Lessons Learned.md`;
`WP7.0C Engineering Foundation Contracts.md` (the format precedent this
document follows); `WP7.2A Recommended Programme.md`.
