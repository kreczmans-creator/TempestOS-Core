# Frozen layers

The plugin trust platform (`Tempest.Core.Plugins` — signing, the trust
store, trust tiers, capability enforcement, component principals, the
denied-type registry and assembly loading), the inbound REST API
(`Tempest.Core.Api`) and Licensing (`Tempest.Core.Licensing`) are frozen
out of the `v1.0` build by `ADR-0146`: there is no project file here, no
folder here is referenced by `src/TempestOS.slnx`, and nothing here
compiles, is tested, or is shipped. The code stands as it was at the
commit it was frozen at, compiles only against the `Tempest.Core` of that
commit, and is not maintained — every later change to `Tempest.Core` will
drift further from it, and that is expected rather than a defect. It is
kept, rather than deleted, because deleting it would throw away working
design that a paying client may one day ask for; it returns to the build
when one does — when a client asks for third-party plugins, for an
inbound API, or for licence enforcement — by being brought back as a real
project, re-pointed at the `Tempest.Core` of that day, and re-reviewed
against whatever the platform has become in the meantime. Plugin
*manifest discovery* is deliberately not here: it stays live, in place,
in `src/Tempest.Core/Plugins`, so the host keeps discovering and
recording what is in the plugin drop folder without loading, signing,
verifying, scoping or enforcing any of it. The matching tests are frozen
alongside, at `tests/Frozen/`.

## P02-P07: the engineering-reasoning and business programmes (`WP 18.0C`)

`ADR-0146`'s freeze test — "unreachable from any shipped surface" —
applies just as directly to six more namespaces, frozen by `WP 18.0C`
(`D-028`, 2026-09-09): `D-028` re-scoped `v0.18.0` from building a
calculation engine of TempestOS's own to recording evidence of
calculations the engineer already does elsewhere, and none of these
namespaces is read by any surface that ships. Each is frozen to
`src/Frozen/Tempest.Core.<Namespace>`, files keeping their relative
path under `src/Tempest.Core/` (a namespace-owned seed dataset under
`ReferenceData/Seeding/Datasets/` moves with its namespace, not left
behind); matching tests move to `tests/Frozen/Tempest.Core.Tests/`.
Three namespaces are frozen in full; three keep a part still read by
the dormant calculation workbench (`BracketEngineeringRecordService`,
`EngineeringTraceRegister`, `Desktop/WorkspaceHost.cs`) or by another
kept kind's own definition — in each of those three, the namespace's
own root governance/validation files turned out not to be optional to
what stayed, the same shape recurring three times independently.

| Namespace (`P`) | Moved | Stays live | ADRs | Lines moved (source / tests) |
|---|---|---|---|---|
| `EngineeringIntelligence` (P02) | Everything (56 files) + its seed (`RuleSeed.cs`) | Nothing | `ADR-0127`, `ADR-0128` — Frozen | 9,100 / 2,140 |
| `BusinessOperations` (P04) | `Interaction` (Crm), `FinancialEntry`/`BudgetValidationService`/`BudgetPositionService` (Finance), all of Purchasing, Quality, Records | `Organisation`, `Contact`, `Budget`, `OrganisationCatalog`, `ContactCatalog`, `BudgetCatalog`, `OrganisationValidationService`, and the namespace-root `OperationalGovernance.cs`/`OperationalValidation.cs` they depend on directly | `ADR-0142` — amended, subject partly kept | 2,505 / 804 |
| `CommercialIntelligence` (P03) | Everything (29 files) + its seed (`CommercialSeed.cs`) | Nothing | `ADR-0131`-`ADR-0135` — Frozen | 7,461 / 1,478 |
| `Knowledge` (P06) | Everything (13 files) + its seed (`KnowledgeSeed.cs`) | Nothing | `ADR-0139`-`ADR-0141` — Frozen | 3,966 / 1,155 |
| `BusinessGovernance` (P07) | Contracts, Risk, Assets (IP/data), Finance (Assumption/Scenario/Control), Development (Opportunity/Pipeline), Operating, `Pricing.PricingService`, `DeterminationState.cs` | `Money`, `CurrencyCode`, `EffectivePeriod`, `RateCard`, `RateCardCatalog`, and the namespace-root `BusinessAuthority.cs`/`Confidentiality.cs`/`BusinessEvidence.cs`/`BusinessStewardship.cs`/`BusinessGovernanceValidation.cs` they depend on directly | `ADR-0129` — amended, subject partly kept; `ADR-0130` — kept, subject whole | 7,527 / 2,383 |
| `EngineeringAssets` (P05) | `DesignReviews`, `TechnicalDocumentation` | `CalculationPacks`, `Templates`, `Verification`, and the namespace-root `AssetApplicability.cs`/`AssetGovernance.cs`/`AssetValidation.cs`/`EngineeringEvidence.cs` they depend on directly; `Seeding/Datasets/EngineeringAssetSeed.cs` stays too, trimmed to the three kept kinds | `ADR-0136`, `ADR-0137` — amended, subject partly kept; `ADR-0138` — kept, subject whole | 1,755 / 477 |

**32,308 lines left `src/Tempest.Core`** (85,151 before, 52,843 after).
A further 4,610 lines of `Runtime/*HostRegistrationTests.cs` and 1,572
lines of an `Integration`/`Population` test cluster (`BracketScenarioTests`,
`RefusalTests`, `ScenarioHarness`, `ScenarioRecords`,
`CrossDomainReferenceTests`, `ScenarioReadinessTests`, `SeedDatasetTests`)
moved with them — not namespace-named, but irreparably tied to the
frozen reasoning chain they proved, via a `SeedHarness` that seeded
every P01-P06 library at once. `tests/Tempest.Core.Tests/Population/SeedHarness.cs`
itself stays live, trimmed to the kept P01 libraries only:
`tests/Tempest.Core.Tests/Calculations/BracketEngineeringDemonstrationTests.cs`
subclasses it and sits outside this Work Package's own files.

Five files were split rather than moved whole, because they mixed a
kept kind's definition with an archived one's: `BusinessOperations/Crm/CrmCatalogs.cs`,
`BusinessOperations/Crm/Organisation.cs`, `BusinessOperations/Finance/FinanceCatalogs.cs`,
and `ReferenceData/Seeding/Datasets/EngineeringAssetSeed.cs` (source);
their test-side counterparts (`OperationsFixtures.cs`, `BusinessGovernanceFixtures.cs`,
`AssetFixtures.cs`, `PricingTests.cs`, and the `*HostRegistrationTests.cs`
files for every namespace but `CommercialIntelligence`, which had none)
split the same way. One diagnostic code retired outright rather than
moved: `CrmValidationRules.SupplierRecordMustResolve` (`TEMPEST-BOC-004`),
because the kept `OrganisationValidationService`'s optional cross-check
against `CommercialIntelligence.Suppliers` cannot mean anything once
that namespace is frozen; `Organisation.SupplierRecordId` itself, a
plain string, is unaffected.
