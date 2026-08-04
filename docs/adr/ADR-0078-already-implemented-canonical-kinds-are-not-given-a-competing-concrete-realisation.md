# ADR-0078: The Five Already-Implemented Canonical Kinds Are Not Given a Competing Concrete Realisation in the Engineering Domain Implementation

## Status

Accepted — `v0.8.0` "Engineering Workspace", `WP 8.2C` (Engineering Domain Implementation), 2026-08-04.

## Context

`WP8.2A Canonical Object Catalogue.md` already reconciled five of the ~49 canonical objects as `Implemented`, not `Conceptual`: `Requirement`, `RequirementCollection`/`RequirementGroup`, `VerificationRecord`, `CalculationRecord`, and `MaterialSpecification` — each realised today by its own, already-shipped framework (`Tempest.Core.Requirements`/`Verification`/`Calculations`/`Materials`). `WP8.2B Interface Catalogue.md` nonetheless proposes Domain-level facet-composed interfaces for the equivalent concepts (`IRequirement`, `IRequirementSet`, `IVerificationResult`, `ICalculationResult`, `IMaterial`), explicitly disclosing that these "describe the same shape" as the real, shipped types "in a deliberately loose reconciliation, not a literal match."

`WP 8.2C`'s own controlling instruction requires implementing "every canonical Engineering Object class" and "every frozen contract exactly," while simultaneously forbidding "Requirements logic," "Calculations logic," and "Verification logic," and stating "those modules shall consume this implementation rather than duplicate it." Giving `IRequirement`/`IVerificationResult`/`ICalculationResult`/`IMaterial` their own new concrete classes, each independently calling `IEngineeringDocumentStore.CreateAsync` under the same `Kind` string the real frameworks already use (`"Requirement"`, `"VerificationRecord"`, `"CalculationRecord"`, `"MaterialSpecification"`), would create two structurally different, independently-evolving writers for one `Kind` — a genuine risk of confusion (and, if ever pointed at the same store, corruption) that no other Engineering Core framework has ever introduced.

## Decision

**The Domain-level interfaces for these five concepts (`IRequirement`, `IRequirementSet`, `IVerificationResult`, `ICalculationResult`, `IMaterial`) are compiled exactly as `WP 8.2B` specified them — every frozen contract exists as real C#, satisfying "implement every frozen contract exactly" literally. None of the five is given a new concrete class in `Tempest.Core.EngineeringDomain`.** Concrete realisation of these five Kinds remains exactly where `WP8.2A Canonical Object Catalogue.md` already placed it: owned by `Tempest.Core.Requirements`/`Verification`/`Calculations`/`Materials`, unchanged by this Work Package. The remaining 39 canonical object interfaces — every one of the catalogue's `Conceptual` entries this Work Package's own family files touch — do receive full concrete classes and generic-factory wiring.

## Consequences

**Positive:**

- Directly honours the controlling instruction's own explicit prohibition on Requirements/Verification/Calculations logic — building a second writer for an already-owned `Kind` would itself have been exactly that kind of logic, however thin.
- Zero risk of two incompatible concrete shapes ever contending for the same `Kind` string, in this store or any future one — the "one `Kind`, one owner" property `WP8.2B Dependency Rules.md` §4 already states ("a canonical object interface is owned by whichever framework implements it") is upheld exactly, not merely in spirit.
- `WP8.2B`'s own already-disclosed "deliberately loose reconciliation" gap is not silently deepened by a second, competing implementation attempt — it remains exactly as open, and exactly as visible, as `WP 8.2B` left it, for a future Work Package to close deliberately (most plausibly by extending the real `Requirement`/`VerificationRecord`/`CalculationRecord`/`MaterialSpecification` classes to additionally implement their own Domain facet interfaces, once a real consumer needs that).

**Negative:**

- A caller holding an `IRequirement` (Domain) reference today has no way to actually obtain one from this Work Package's own framework — the interface compiles, but nothing in `Tempest.Core.EngineeringDomain` ever constructs an instance of it. This is disclosed, not fixed, here.
- The Engineering Domain's own representative sample module (`EngineeringDomainSampleModule`) therefore cannot demonstrate these five Kinds through the generic factory machinery the way it demonstrates the other 39 — it instead demonstrates genuine cross-framework linkage by registering a real `IMaterialSpecification` through `IMaterialCatalog` directly and referencing it from a Domain-level `Part`, which is the honest, available alternative.

## Alternatives Considered

**Giving all five their own new concrete classes under the same `Kind` strings** — considered and rejected; see Context, above. Directly risks corrupting or shadowing the real frameworks' own already-shipped data.

**Giving all five new concrete classes under new, Domain-prefixed `Kind` strings (e.g. `"DomainRequirement"`)** — considered and rejected. Avoids the collision risk but creates two entirely separate, permanently-diverging representations of "a Requirement" with no reconciliation path named anywhere — a worse outcome than the status quo `WP 8.2B` already disclosed, not a better one.

**Retrofitting the real `Requirement`/`VerificationRecord`/`CalculationRecord`/`MaterialSpecification` classes to additionally implement their own Domain facet interfaces** — considered and rejected for this Work Package specifically, since it requires touching `Tempest.Core.Requirements`/`Verification`/`Calculations`/`Materials` directly, which this Work Package's own controlling instruction explicitly forbids. Named here as the most plausible future path, not attempted.

## Related Documents

`WP8.2A Canonical Object Catalogue.md` §3 Cross-Reference Check; `WP8.2B Interface Catalogue.md` §4/§6 (the "deliberately loose reconciliation" disclosure); `WP8.2B Dependency Rules.md` §4 (Ownership Rules); `src/Tempest.Core/EngineeringDomain/Contracts/RequirementsVerification.cs`; `src/Tempest.Core/EngineeringDomain/Contracts/Calculations.cs`; `src/Tempest.Core/EngineeringDomain/Contracts/Materials.cs`.
