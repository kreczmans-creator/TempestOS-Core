# WP 7.1D — Engineering Calculation Framework — Engineering Core Impact Assessment

## Purpose

Assesses what completing the Engineering Calculation Framework
(`FCR-0032`, Candidate `F`) changes for the remaining Engineering
Foundation Programme (`FCR-0033`, Candidate `H`, `WP7.0B Candidate Work
Package Catalogue.md`), now that four of the five Engineering
Foundation frameworks are real, tested implementations.

## Candidate-by-Candidate Impact

### Candidate H — Verification & Validation Framework Architecture

**No direct dependency**, unchanged by this Work Package. `WP7.0C
Engineering Foundation Contracts.md` confirms Verification depends only
on `Tempest.Core.EngineeringData`, not on Calculation. Candidate H
remains sequenced behind Candidate I (Requirements Engine), exactly as
`WP7.0B`/`WP7.1A`/`WP7.1B`/`WP7.1C` each recommended in turn. A plausible
future integration — a verification recording that a specific
calculation record satisfied a requirement — remains available once
Verification is designed, but is not a dependency this Work Package
introduces.

## What Remains Unchanged

- `FCR-0027` (Requirements Engine) and `FCR-0028` (Project Engine)
  remain **not yet classified** under `ADR-0013` — unchanged by this
  Work Package's own scope.
- The five still-empty Engineering Discipline categories (Mechanical,
  Structural, Electrical, Building Services/HVAC, Manufacturing) remain
  empty — this Work Package introduces no discipline-specific
  calculation, only cross-cutting dispatch infrastructure.
- `FCR-0034` (Affine Unit Conversion / Temperature) is unaffected —
  Calculation's own by-convention relationship with Units & Quantities
  does not require Temperature; a future temperature-dependent
  calculation would need `FCR-0034` resolved first.

## What Changes

**Four of the five Engineering Foundation frameworks are now
Implemented** (`FCR-0029` Data Model, `FCR-0030` Units & Quantities,
`FCR-0031` Materials, `FCR-0032` Calculation) — only `FCR-0033`
(Verification & Validation, Candidate H) remains unimplemented, itself
sequenced behind an unrelated capability (Requirements Engine,
Candidate I) rather than behind any of the four already complete. This
Work Package's own `DoubleLengthCalculationDefinition` demonstrates, for
the first time, all three implemented frameworks used together in one
place (Units & Quantities for `TInput`/`TResult`, the Data Model for
durable recording, an open reference toward Materials) — a genuine,
concrete demonstration that the Engineering Foundation's own frameworks
compose correctly, not merely that each works in isolation.

## Recommendation

**No further Engineering Foundation implementation Work Package is
strictly required before a real discipline module could begin** — the
four frameworks a Mechanical/Structural/Electrical/HVAC capability would
need (data, units, materials, calculation dispatch) are all real and
proven. Candidate H (Verification) remains available once Candidate I
(Requirements Engine) is designed, but is not a blocker for a
discipline-specific Engineering Module to begin, should Product Approval
choose that direction instead of continuing the Engineering Foundation
programme strictly in sequence.

## Related Documents

`docs/governance/Future Capability Register.md`; `docs/releases/v0.7.0/
WP7.0B Capability Dependency Report.md`; `WP7.0C Cross-Framework
Dependency Report.md`; `WP7.0B Candidate Work Package Catalogue.md`;
`WP7.1D Implementation Report.md`.
