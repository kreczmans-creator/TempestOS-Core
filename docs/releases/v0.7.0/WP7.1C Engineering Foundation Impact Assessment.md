# WP 7.1C — Materials Framework — Engineering Foundation Impact Assessment

## Purpose

Assesses what completing the Materials Framework (`FCR-0031`, Candidate
`G`) changes for the remaining Engineering Foundation Programme
(`FCR-0032`–`FCR-0033`, Candidates `F`, `H`, `WP7.0B Candidate Work
Package Catalogue.md`), now that a real, tested implementation exists to
build against.

## Candidate-by-Candidate Impact

### Candidate F — Engineering Calculation Framework Architecture

**No direct dependency**, but one now-available integration
opportunity. `WP7.0C Engineering Foundation Contracts.md` named
Materials as a plausible future consumer relationship for the
Calculation Framework ("a calculation consumes a material's own
properties as input"). That integration is now concretely possible
against a real `IMaterialCatalog.FindAsync` call and a real
`MaterialProperty.Value` (a boxed `Quantity<TDimension>`), not merely a
proposed interface — Candidate F's own architecture phase can now
prototype a calculation that reads a material's own yield strength or
similar property directly.

### Candidate H — Verification & Validation Framework Architecture

**No direct dependency**, unchanged by this Work Package. `WP7.0C`'s own
contract-level design already confirmed Verification depends only on
`Tempest.Core.EngineeringData`'s generic document concept — a material
specification could, in principle, be a verification subject (it is
itself an `IEngineeringDocument`), but this is not a new dependency
Materials itself introduces.

## What Remains Unchanged

- `FCR-0027` (Requirements Engine) and `FCR-0028` (Project Engine)
  remain **not yet classified** under `ADR-0013` — unchanged by this
  Work Package's own scope.
- The five still-empty Engineering Discipline categories (Mechanical,
  Structural, Electrical, Building Services/HVAC, Manufacturing) remain
  empty — this Work Package introduces no discipline-specific concept,
  only a cross-cutting, discipline-neutral material representation.
- `FCR-0034` (Affine Unit Conversion / Temperature) is unaffected —
  Materials' own property values remain bounded to the same seven
  dimensions Units & Quantities already defines; a future Materials
  property genuinely requiring Temperature would need `FCR-0034`
  resolved first.

## Recommendation

**Candidate F (Calculation) is now the strongest next candidate** — both
of its own by-convention dependencies (`FCR-0030`, and now `FCR-0031` as
a plausible input source) are real and proven, though Candidate F's own
design still carries the open purity-enforcement question `ADR-0056`
reserves. Candidate H (Verification) remains correctly sequenced behind
Candidate I (Requirements Engine), unchanged. With `FCR-0029`,
`FCR-0030`, and `FCR-0031` now all Implemented, three of the five
Engineering Foundation frameworks are complete — Calculation and
Verification & Validation are the two that remain.

## Related Documents

`docs/governance/Future Capability Register.md`; `docs/releases/v0.7.0/
WP7.0B Capability Dependency Report.md`; `WP7.0C Cross-Framework
Dependency Report.md`; `WP7.0B Candidate Work Package Catalogue.md`;
`WP7.1C Implementation Report.md`.
