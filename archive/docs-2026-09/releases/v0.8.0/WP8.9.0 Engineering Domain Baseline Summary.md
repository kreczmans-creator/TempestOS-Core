# WP 8.9.0 — Release Preparation & Product Baseline — Engineering Domain Baseline Summary

## Purpose

A snapshot of the Engineering Domain exactly as it ships in `v0.8.0` —
what compiles, what runs, what is deliberately still contract-only —
verified directly against the compiled source and the real test suite.
New deliverable type for this release; no `v0.7.0` precedent, since the
Engineering Domain did not exist before `WP 8.2A`.

## What Ships

`Tempest.Core.EngineeringDomain` is a real, compiled, tested namespace
of 83 contract types (`WP 8.2B`) and 38 concrete object classes
(`WP 8.2C`) — a shared canonical vocabulary sitting between the
Engineering Data Model and every discipline framework, consumed by
nothing yet (no discipline module has been asked to build on it), but
proven working end to end by its own representative sample module.

| Layer | Status | Count | Originating Work Package |
|---|---|---|---|
| Canonical Engineering Object catalogue | Documented, not code | ~49 objects, 13 families | `WP 8.2A` |
| Compiled contracts (`Tempest.Core.EngineeringDomain`) | Real, compiled | 83 interfaces/enums/records | `WP 8.2B`, compiled `WP 8.2C` |
| Concrete object classes | Real, tested | 38 | `WP 8.2C` |
| Already-Implemented Kinds (owned elsewhere, not duplicated) | Real, but not via this namespace | 5 | `WP 8.2A` (reconciled), `ADR-0078` (WP 8.2C, not duplicated) |
| Shared services (repository, lifecycle table, validation, digital thread) | Real, DI-registered | 9 interfaces, 10 DI registrations | `WP 8.2C` |
| Representative sample graph | Real, running | 16 objects, 8 families | `WP 8.2C` (`EngineeringDomainSampleModule`) |

## Verified Against Compiled Code and the Real Test Suite

- 39 dedicated framework unit tests (`EngineeringDomainFrameworkTests.cs`)
  exercise factory construction, lifecycle transitions, revisions,
  relationships (including self-reference rejection), validation,
  evidence composition, attachments, dependency traversal, and impact
  analysis — all against real, non-mocked collaborators.
- 10 host-registration tests confirm every shared service resolves as a
  singleton through the real, unmodified `TempestHost`, and that
  `EngineeringDomainContext` genuinely shares the same
  `IEngineeringDocumentStore` instance every other Engineering Core
  framework resolves — not a second, isolated store.
- 7 sample-module integration tests, including one running the full
  sixteen-object graph build through the real Host end to end, confirm
  the framework is not merely unit-testable in isolation but genuinely
  wired into the platform's own composition root.
- A real cross-framework link is demonstrated, not merely asserted: the
  sample graph registers an actual `IMaterialSpecification` through
  `Tempest.Core.Materials.IMaterialCatalog` and references it from a
  Domain-level `Part` — proving the new layer and an existing Engineering
  Core framework genuinely interoperate through the one shared document
  store.

## Known Limitations (Disclosed, Not Blocking)

1. **Only 38 of ~49 canonical objects have a concrete class** — 5 are
   deliberately not duplicated (already owned by `Requirements`/
   `Verification`/`Materials`/`Calculations`, `ADR-0078`); the remaining
   ~6 (Reference, Tag, Classification, Custom Object extension mechanism,
   Verification/Verification Activity's own further specialisation) are
   realised as relationships, metadata fields, or an extensibility
   mechanism rather than objects, by design, per `WP8.2A Canonical
   Object Catalogue.md` §12/§13.
2. **No discipline framework consumes `Tempest.Core.EngineeringDomain`
   yet** — the layer exists, compiles, and is tested, but its own
   Definition of Done ("consumed by every future engineering
   discipline") is necessarily still forward-looking; no discipline
   module has been authorised to build on it as of this release.
3. **The in-memory repository does not rebuild itself from the real
   store on Host restart** (`ADR-0077`'s own disclosed gap) — the
   underlying documents themselves survive a restart; the by-Kind index
   over them does not, yet.
4. **`IValidationRuleSet` enforces zero Kind-specific rules by design**
   — only `StructuralValidationRules.NoSelfReference` is structurally
   enforced today, exactly as `WP8.2B Validation Contract
   Specification.md` specified.
5. **`WP8.2B`'s own `IRelease : IBaseline : IConfiguration` interface
   chain is three levels of specialisation deep**, contradicting that
   same document's own Dependency Rules §6 — compiled exactly as frozen,
   disclosed, not corrected (see Release Readiness Report Finding 3).

None of the five rises to release-blocking — each is a named, disclosed
scope boundary, not a defect found during this review.

## Verdict

The Engineering Domain, as shipped, matches every claim `WP 8.2C`'s own
Implementation Report made about it, with one correction (38, not 39,
concrete classes — see Release Readiness Report Finding 2), verified
directly against the compiled source and the real, passing test suite.
Ready for Product Approval as the platform's own first compiled,
running, genuinely shared engineering vocabulary layer.

## Related Documents

`docs/releases/v0.8.0/WP8.2A Engineering Domain Architecture.md` and
companions; `docs/releases/v0.8.0/WP8.2B Engineering Domain
Contracts.md` and companions; `docs/releases/v0.8.0/WP8.2C Engineering
Domain Implementation Report.md`; `ADR-0072`–`ADR-0079`;
`docs/academy/02 Runtime Architecture/18-engineering-domain-architecture.md`.
