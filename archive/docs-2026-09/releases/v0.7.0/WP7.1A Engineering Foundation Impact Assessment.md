# WP 7.1A — Engineering Data Model — Engineering Foundation Impact Assessment

## Purpose

Assesses what completing the Engineering Data Model (`FCR-0029`,
Candidate `D`) changes for the remaining Engineering Foundation
Programme (`FCR-0030`–`FCR-0033`, Candidates `E`–`H`, `WP7.0B Candidate
Work Package Catalogue.md`), now that a real implementation exists to
build against rather than only a proposed contract.

## Candidate-by-Candidate Impact

### Candidate E — Units & Quantities Framework Architecture

**No dependency on this Work Package.** `WP7.0C Cross-Framework
Dependency Report.md` already confirmed Units & Quantities has zero
dependency on the Data Model — this remains unchanged. Candidate E can
proceed independently, in parallel with any other candidate, exactly as
`WP7.0B Engineering Foundation Architecture.md` recommended.

### Candidate F — Engineering Calculation Framework Architecture

**No direct dependency**, but one now-available integration
opportunity. `WP7.0C Engineering Foundation Contracts.md` named a
plausible, not mandatory, integration: recording a `CalculationRecord`
as a document revision. That integration is now concretely possible
against a real `IEngineeringDocumentStore.ReviseAsync` call, not merely
a proposed interface — Candidate F's own architecture phase can now
prototype this integration against working code rather than a paper
contract, if it chooses to.

### Candidate G — Materials Framework Architecture

**Direct dependency, now unblocked.** `FCR-0031` depends on `FCR-0029`
directly (`WP7.0B Capability Dependency Report.md`). This Work Package
proves the dependency is real and working: `EngineeringDataSampleModule`
creates, revises, and links documents through the real store — the
exact pattern `WP7.0C Engineering Foundation Contracts.md` proposed
Materials would build on (a material specification *is* an
`IEngineeringDocument` of `Kind = "MaterialSpecification"`). Candidate G
can now begin its own architecture phase against real, tested behaviour
rather than a proposed interface alone.

### Candidate H — Verification & Validation Framework Architecture

**Direct dependency, now unblocked, and one ambiguity fully resolved.**
`WP7.0C`'s own contract-level design already clarified that Verification
depends on the Data Model's *generic* document concept, not on a
specific Requirements Engine service (`WP7.0C Cross-Framework
Dependency Report.md` §2). This Work Package confirms that generic
document concept is real and working — `EngineeringDocumentNotFoundException`
is thrown correctly for a non-existent document Id
(`EngineeringDocumentStoreTests.
GetRevisionHistoryAsync_NonExistentDocument_ThrowsEngineeringDocumentNotFoundException`),
exactly the failure mode Verification's own approved contract depends on
being reliable. Candidate H can now proceed once Candidate `I`
(Requirements Engine Architecture) reaches its own architecture phase,
per the existing sequencing (`WP7.0B Candidate Work Package
Catalogue.md`).

## What Remains Unchanged

- `FCR-0027` (Requirements Engine) and `FCR-0028` (Project Engine)
  remain **not yet classified** under `ADR-0013` — this Work Package
  does not classify either, since neither was in its own scope.
- No new capability was identified — `Future Capability Register.md`'s
  own entry count (33) is unchanged by this Work Package.
- The five still-empty Engineering Discipline categories (Mechanical,
  Structural, Electrical, Building Services/HVAC, Manufacturing) remain
  empty — this Work Package's own scope explicitly excluded introducing
  any discipline-specific concept.

## Recommendation

**Candidates E (Units & Quantities) and G (Materials) are now the two
strongest next candidates** — E because it remains fully independent
and unblocked, G because its own direct dependency (this Work Package)
is now complete and proven working, not merely approved on paper.
Candidate F (Calculation) should follow E, per its own existing
dependency. Candidate H (Verification) should follow Candidate I
(Requirements Engine), unchanged from `WP7.0B`'s own recommendation.

## Related Documents

`docs/governance/Future Capability Register.md`; `docs/releases/v0.7.0/
WP7.0B Capability Dependency Report.md`; `WP7.0C Cross-Framework
Dependency Report.md`; `WP7.0B Candidate Work Package Catalogue.md`;
`WP7.1A Implementation Report.md`.
