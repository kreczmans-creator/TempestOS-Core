# WP 7.0C — Engineering Foundation Contract Review

## What This Document Is

Like `WP 7.0A`/`WP 7.0B` before it, this is not a standard 13-section
implementation retrospective — `WP 7.0C` shipped no production code, no
test, no compiled interface. It mirrors the same whole-review shape
(What Was Achieved, Architectural Lessons, Implementation Lessons,
Repository Maturity, Recommendations, Key Takeaways), because this Work
Package, like those before it, is a contract-review milestone rather
than a feature implementation.

## Introduction

`WP 7.0B` transformed `Future Capability Register.md` into a coherent
engineering programme, including five Engineering Foundation
capabilities (`FCR-0029`–`FCR-0033`) identified as architecturally
necessary before any discipline-specific Engineering Module begins. `WP
7.0C`'s own controlling instruction asked the natural next question:
what would each of those five capabilities' own public contract
actually look like — mirroring `v0.6.0`'s own Contract Review phase
(`Platform Service Contracts.md`, `Required ADRs.md`) for a set of
frameworks instead of platform services.

## What Was Achieved

Full proposed C# interface contracts for all five Engineering Foundation
frameworks (`Tempest.Core.EngineeringData`, `UnitsAndQuantities`,
`Materials`, `Calculations`, `Verification`), each answering the same
twelve review questions this Work Package's own controlling instruction
named. Eight completion deliverables under `docs/releases/v0.7.0/`,
prefixed `WP7.0C`: the Contracts document itself, a Cross-Framework
Dependency Report (confirming no circular dependency, and identifying
one previously-ambiguous relationship — Verification's own dependency on
the Data Model's generic document concept, not on a specific Requirements
Engine service — that this Work Package's own contract-level design
resolved more precisely than `WP 7.0B`'s capability-level graph could),
an Engineering Standards Mapping (identifying architectural requirements
only, naming zero specific real-world standards since none is
confirmed), a Platform Integration Matrix, a Testing Strategy, an
Academy Plan, a Governance Confirmation, and a Required ADR Catalogue
(`ADR-0053`–`ADR-0057`, five anticipated decisions, none yet answered).

## Architectural Lessons

Designing Units & Quantities as a pure value-type library with **zero**
DI registration — the first Engineering Foundation framework, and the
first framework in this entire document's own review, with no Platform
Service dependency of any kind — was this Work Package's own clearest
architectural finding. It is a direct, disciplined application of the
"not every public type is a DI-registered service" precedent
`CommandResult`/`LicenseValidationResult` already established, extended
here to an entire framework rather than a single result type, and
confirmed independently by two separate deliverables (`Cross-Framework
Dependency Report.md`, `Platform Integration Matrix.md`) reaching the
same conclusion from different angles.

## Implementation Lessons

There is no implementation to report — by design. The closest analogue
is the discipline required to propose a genuinely reviewable interface
contract (full XML docs, named exceptions, a stated failure model)
without silently deciding every open question a real implementation
would face. Five such questions were deliberately left open and
catalogued rather than quietly resolved (`WP7.0C Required ADR
Catalogue.md`) — for example, whether `IMaterialSpecification.Properties`'
open, boxed-`object` shape is the right long-term design, or a stronger
alternative should replace it once real material data exists to
validate against.

## Repository Maturity

`v0.6.0`'s own Contract Review format (`Platform Service Contracts.md`,
`Required ADRs.md`, `Governance Confirmation.md`, `Academy Plan.md`,
`Testing Strategy.md`) transferred to a materially different kind of
subject — cross-cutting technical frameworks rather than platform
services consumed by modules — with only one structural adaptation
required: "Primary Entities" and "Dependency Rules" as explicit review
fields, absent from `v0.6.0`'s own fifteen-question format, because
these frameworks' own data-modelling nature (entities, revisions,
references) had no close analogue among `v0.6.0`'s nine, mostly
stateless-contract services. This is itself evidence the Contract
Review pattern generalises with minor, disclosed extension rather than
wholesale reinvention.

## Recommendations

- **Resolve `ADR-0053`–`ADR-0057` during each framework's own
  Architecture Work Package** (`WP7.0B Candidate Work Package
  Catalogue.md`, Candidates `D`–`H`) — none is answered here, and none
  should be assumed answered by the mere existence of a proposed
  contract.
- **Validate the proposed `Quantity<TDimension>`/`double` representation
  against a second, real discipline need before treating it as final**
  — mirroring `WP7.0B Roadmap Risk Register.md`'s own `RR-1`, now
  applied at the contract level specifically to `ADR-0054`.
- **Do not begin implementation of any of the five frameworks until
  Engineering Review of this Work Package completes** — per this Work
  Package's own explicit closing instruction.

## Key Takeaways

1. A framework with zero Platform Service dependency (Units &
   Quantities) is not a gap in this review — it is the correct
   architectural shape for a pure value-type library, and confirming
   this explicitly, twice, from different angles, is more valuable than
   assuming it without checking.
2. Contract-level design can resolve an ambiguity a capability-level
   dependency graph left open — Verification's own relationship to a
   future Requirements Engine is the clearest instance this Work Package
   produced.
3. Proposing a stable-looking interface while explicitly cataloguing
   what remains genuinely undecided (five ADRs, none answered) is more
   honest, and more useful to the next Work Package, than either
   refusing to propose anything or silently deciding everything.

## Related Documents

`docs/governance/Future Capability Register.md`; `WP7.0B Engineering
Foundation Architecture.md`; `docs/releases/v0.7.0/WP7.0C Engineering
Foundation Contracts.md` and its seven companion deliverables;
`docs/releases/v0.6.0/Platform Service Contracts.md` (the precedent this
Work Package's own format extends).
