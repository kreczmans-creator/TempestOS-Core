# WP 7.0B — Engineering Discipline Assessment

## Status

Complete. Assesses every Engineering Discipline category in
`docs/governance/Capability Categories.md` for maturity, completeness,
gaps, and recommended sequencing. **No discipline-specific capability
is invented here** — this document assesses what exists (nothing, for
seven of nine) and what would be needed to change that (a real
stakeholder engagement, not further documentation review), not what
that capability should be.

## Assessment

| Discipline | Maturity | Completeness | Gap | Recommended Sequencing |
|---|---|---|---|---|
| **Systems Engineering** | Aspirational only (`FCR-0027`) | 0% — no design work of any kind | No architecture, no classification under `ADR-0013`, no scoped Work Package | First among the nine, once Engineering Foundation (Programme 5) lands — the only discipline with a named platform-level hook (`ADR-0013`'s own Future Considerations) |
| **Project Management** | Aspirational only (`FCR-0028`) | 0% — only dormant, pre-Claude-era code (`ProjectModel`) traces toward it | No architecture, no classification under `ADR-0013`, security design (`Security Roadmap.md` item 4) not yet started | Second, alongside or immediately after Systems Engineering — the only other discipline with a named platform-level hook |
| **Materials** | Foundation only (`FCR-0031`, identified this Work Package) | 0% discipline-specific; one cross-cutting foundation capability identified | No material-specific capability identified at all; `FCR-0031` itself is unscoped | Third or later — depends on `FCR-0029`/`FCR-0030` (Engineering Foundation) landing first, and on a real stakeholder need being identified |
| **Quality** | Foundation only (`FCR-0033`, identified this Work Package) | 0% discipline-specific; one cross-cutting foundation capability identified | No quality-specific capability (inspection, non-conformance workflow) identified at all | Alongside Materials — `FCR-0033` depends on `FCR-0027` landing first |
| **Mechanical Engineering** | Not started | 0% — zero identified capabilities of any kind | No document reviewed through `WP 7.0B` names a single Mechanical Engineering capability | **Cannot be sequenced from existing evidence** — see "Why Five Disciplines Cannot Be Sequenced," below |
| **Structural Engineering** | Not started | 0% | Same as Mechanical | Cannot be sequenced from existing evidence |
| **Electrical Engineering** | Not started | 0% | Same as Mechanical | Cannot be sequenced from existing evidence |
| **Building Services / HVAC** | Not started | 0% | Same as Mechanical | Cannot be sequenced from existing evidence |
| **Manufacturing** | Not started | 0% | Same as Mechanical; would consume `FCR-0031` (Materials Framework) once it exists | Cannot be sequenced from existing evidence, but structurally depends on `FCR-0031` more directly than the other four |

## Why Five Disciplines Cannot Be Sequenced From Existing Evidence

Mechanical, Structural, Electrical, Building Services/HVAC, and
Manufacturing each have **zero** identified capabilities in `Future
Capability Register.md` — not merely low-priority ones. Recommending
that, say, Structural should precede Electrical would require a
business or technical justification this repository does not contain.
Inventing one (e.g., "Structural is more foundational because loads
inform everything else") would be exactly the kind of speculative,
non-evidence-based claim this project's own standing discipline forbids
(`Future Work Package Guidelines.md` §8, "prefer evidence over
speculation"). **The honest answer is that no sequencing recommendation
among these five is possible today** — the correct next step is a real
engineering-domain stakeholder engagement or a concrete customer
scenario naming one of them first, not a documentation-derived
guess dressed up as a recommendation.

Manufacturing is the partial exception: it structurally depends on
`FCR-0031` (Materials Framework) more directly than the other four,
since manufacturing process planning is meaningless without material
specification data to plan against — a real, if weak, sequencing signal
the other four lack entirely.

## Cross-Discipline Observations

- **Two disciplines (Systems Engineering, Project Management) are ahead
  of the other seven for a specific, identifiable reason**: `ADR-0013`
  named them directly as examples requiring classification, years before
  this Work Package existed. This is not evidence they are more
  valuable — only that they are the only two this repository's own
  history happens to have already touched.
- **The Engineering Foundation Programme (`FCR-0029`–`FCR-0033`)
  benefits every discipline simultaneously**, but was derived from only
  two disciplines' own aspirational descriptions (Systems Engineering,
  Project Management) plus `Capability Categories.md`'s own generic
  category definitions — not validated against a real Mechanical,
  Structural, Electrical, HVAC, or Manufacturing requirement, since none
  exists to validate against yet. This is a real risk, disclosed in
  `WP7.0B Roadmap Risk Register.md`.

## Related Documents

`docs/governance/Capability Categories.md`; `docs/governance/Future
Capability Register.md`; `WP7.0B Engineering Foundation
Architecture.md`; `WP7.0B Roadmap Risk Register.md`.
