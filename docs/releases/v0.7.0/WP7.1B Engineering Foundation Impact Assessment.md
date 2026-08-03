# WP 7.1B — Units & Quantities Framework — Engineering Foundation Impact Assessment

## Purpose

Assesses what completing the Units & Quantities Framework (`FCR-0030`,
Candidate `E`) changes for the remaining Engineering Foundation
Programme (`FCR-0031`–`FCR-0033`, Candidates `F`–`H`, `WP7.0B Candidate
Work Package Catalogue.md`), now that a real, tested implementation
exists to build against.

## Candidate-by-Candidate Impact

### Candidate F — Engineering Calculation Framework Architecture

**Direct dependency, now unblocked.** `WP7.0C Engineering Foundation
Contracts.md` named Units & Quantities as a by-convention dependency for
the Calculation Framework (`TInput`/`TResult` "expected... to be
`Quantity<TDimension>`-based where the calculation is dimensioned").
That dependency is now real: a future calculation definition can accept
and return `Quantity<TDimension>` values, convert between units safely,
and rely on the same-`Unit`-only arithmetic rule proven by this Work
Package's own test suite. Candidate F's own architecture phase can now
design against working code, including the seven concrete dimensions
this Work Package established, rather than an abstract "dimensioned
value" concept.

### Candidate G — Materials Framework Architecture

**Direct dependency, now unblocked.** `FCR-0031` depends on both
`FCR-0029` (implemented, `WP 7.1A`) and `FCR-0030` (implemented, this
Work Package) — both of Materials' own upstream dependencies are now
real and proven. `IMaterialSpecification.Properties`'s own proposed
shape (`IReadOnlyDictionary<string, object>`, boxing `Quantity<TDimension>`
values of differing dimensions) can now be prototyped against a real
`Quantity<TDimension>` implementation, including confirming which of
this Work Package's own seven dimensions (Mass, Force, Pressure, and
Area/Volume for geometric properties) cover Materials' own most likely
first properties (density, yield strength).

### Candidate H — Verification & Validation Framework Architecture

**No direct dependency.** `WP7.0C Engineering Foundation Contracts.md`
confirms Verification depends only on `Tempest.Core.EngineeringData`,
not on Units & Quantities — unchanged by this Work Package. Candidate H
remains sequenced behind Candidate I (Requirements Engine), exactly as
`WP7.0B` recommended.

## What Remains Unchanged

- `FCR-0027` (Requirements Engine) and `FCR-0028` (Project Engine)
  remain **not yet classified** under `ADR-0013` — unchanged by this
  Work Package's own scope.
- The five still-empty Engineering Discipline categories (Mechanical,
  Structural, Electrical, Building Services/HVAC, Manufacturing) remain
  empty — this Work Package introduces no discipline-specific concept,
  only cross-cutting, discipline-neutral dimensions.
- One new capability was identified during implementation, not planning
  — `FCR-0034` (Affine Unit Conversion / Temperature), disclosed in
  `ADR-0054` and tracked as `TD-19`.

## Recommendation

**Candidate G (Materials) is now the strongest next candidate** — both
of its own upstream dependencies (`FCR-0029`, `FCR-0030`) are complete
and proven, not merely approved on paper. **Candidate F (Calculation)
is the second-strongest** — its own by-convention dependency on Units &
Quantities is now real, though Calculation's own design carries more
open questions (`ADR-0056`'s own purity-enforcement question) than
Materials does. Candidate H (Verification) remains correctly sequenced
behind Candidate I (Requirements Engine), unchanged.

## Related Documents

`docs/governance/Future Capability Register.md`; `docs/releases/v0.7.0/
WP7.0B Capability Dependency Report.md`; `WP7.0C Cross-Framework
Dependency Report.md`; `WP7.0B Candidate Work Package Catalogue.md`;
`WP7.1B Implementation Report.md`.
