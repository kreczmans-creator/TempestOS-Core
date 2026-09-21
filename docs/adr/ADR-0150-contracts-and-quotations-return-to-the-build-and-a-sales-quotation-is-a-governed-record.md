# ADR-0150: Contracts and Quotations Return to the Build, and a Sales Quotation Is a Governed Record

## Status

Accepted — Product Owner request, 2026-09-21.

Amends `ADR-0129` (its Status now records the return) and exercises, for
the first time, the return path `WP 18.0C` and `src/Frozen/README.md`
documented under `ADR-0146`'s freeze test.

## Context

`WP 18.0C` (`D-028`, 2026-09-09) froze `Tempest.Core.BusinessGovernance`'s
Contracts, Risk, Assets, Finance, Development, Operating and
`Pricing.PricingService` to `src/Frozen/`, because nothing that shipped
read them. The freeze was explicit about how anything comes back: "when a
client asks for [it] — by being brought back as a real project,
re-pointed at the `Tempest.Core` of that day, and re-reviewed against
whatever the platform has become in the meantime."

The Product Owner has asked. The companion Pi dashboard
(`Tempest-Dashboard`) renders contracts and quotes as its business view,
and has done so from demo data since its first commit; its
`docs/DATA-CONTRACTS.md` already states the `Contract` and `Quote`
shapes it reads, and its file fallback (`server/connectors/util.js`)
already reads a local JSON export. `WP 18.3` gave Core a Dashboard
Export (`engineering-status.json`, `programme.json`) with nothing on the
business side to export. The owner wants real figures.

> *Later note (2026-09-21).* `engineering-status.json` is now schema v2:
> additive `health`, `kpis`, `attention`, `blockedItems` and
> `overdueActions` sections, copied verbatim from the Engineering
> Cockpit's own `EngineeringCockpit` (`Health`, the per-discipline
> `*Status`/`*KpiCards`, `AttentionItemsByDiscipline`, `BlockedItems`,
> `OverdueActions`) so the Pi renders what the desktop cockpit computes.
> Every v1 key is unchanged. See `EngineeringStatusExportAdapter`'s own
> remarks for the shape.

> *Later note (2026-09-21, `ADR-0151`).* `programme.json` is now schema
> v2 too: each `projects[]` entry additively carries `health` (`overall`,
> `byDiscipline`, `score`), `blockedCount` and `overdueActionCount`, and
> `summary` carries `byHealth` — the Cockpit's own health rollup scoped
> to that Project, in the same lower-cased words. Every v1 key is unchanged.
> Later the same day, still v2 (additive, unconsumed): `projects[].tasks[]`
> (that Project's open tasks — `id`, `identifier`, `name`, `workState`,
> `priority`, `assignedTo`, `dueDate`, `isOverdue`, `contributesTo`, from
> `ProjectTaskRegister`) and `summary.tasks` (`open`, `overdue`, `blocked`).

Three facts shaped what returns:

1. **Contracts is one unit.** The intent was to thaw `IssuedContract`
   alone. `ContractService.PrepareFromTemplateAsync` issues a contract
   *from* a Released `ContractTemplate`, `IssuedContract.TemplatePin`
   names the template revision read, and `IssuedContractValidationService`
   reads the template back to check departures. Templates and their
   catalogue return as a dependency, or nothing returns at all.
2. **`DeterminationState.cs` is not optional to Contracts.**
   `ContractTemplate.LegalReviewState`, `TemplateDeparture.LegalReviewState`
   and `ContractDeliverable.AcceptanceState` are typed with it. `WP 18.0C`
   archived it from the namespace root only because nothing *kept* needed
   it; Contracts does.
3. **Core had no record of a quote sent to a client.**
   `CommercialIntelligence` records quotes *received from suppliers*
   (`SupplierQuote`, frozen, out of scope); `Pricing.RateCardQuotation` is
   the arithmetic of pricing units against a rate card, not an offer made.
   The dashboard's `Quote` is the offer made. That is new code, not a thaw.

## Decision

**1. Contracts and `PricingService` return, whole and unchanged.**
`src/Frozen/Tempest.Core.BusinessGovernance/Contracts/` (seven files),
`Pricing/PricingService.cs` and `DeterminationState.cs` move back to
`src/Tempest.Core/BusinessGovernance/` by `git mv`, history kept.
`ContractTests.cs` returns whole; the `PricingService` half of
`PricingTests.cs` and the Contracts/`PricingService` rows of
`BusinessGovernanceHostRegistrationTests.cs` (with the contract-service
reasoning test) merge back into their live files. `TempestHost` registers
`IContractTemplateCatalog`, `IContractTemplateValidationService`,
`IIssuedContractCatalog`, `IIssuedContractValidationService`,
`IContractService` and `IPricingService` exactly as `WP 18.0C` removed
them, next to `IRateCardCatalog`.

Re-pointing at today's Core found **no source drift**: every returned
source file compiled unchanged with `TreatWarningsAsErrors`. The one test
drift was `WP 18.1A`'s retirement of the file-per-key store — the returned
host-registration test's `PersistenceStore.RootPathConfigurationKey` is
now `SqlitePersistenceStore.RootPathConfigurationKey`, as the live file
already had it.

**2. A sales `Quotation` is a twelfth `P07` kind on `ADR-0129`'s own
pattern.** `Tempest.Core.BusinessGovernance.Quotations` holds
`Quotation` (reference, `ContractParty` client, title, `Money` amount,
`QuotationStatus`, `SubmittedOn`/`FollowUpOn`/`DecidedOn`, an optional
`ReferencePin` to the rate-card revision it was priced from, an optional
`IssuedContract.Reference` it became), `QuotationCatalog`
(`BusinessQuotations`/`BusinessQuotation`, reference as secondary key,
`QuotationQuery`) and `QuotationValidationService`
(`TEMPEST-BGQ-001..011`). Governance state and domain status stay on
separate axes, as everywhere in `P07`.

The status moves one way — `Draft → Submitted → Accepted | Declined` —
and `QuotationStatuses.CanMove` is the one statement of that rule. It is
enforced where `IssuedContractValidationService` enforces a contract's
rules: in validation, by reading the registered record under the same
reference and reporting an impermissible move as an error
(`TEMPEST-BGQ-001`), never by rewriting the record. The dates must agree
with the status (a draft carries no submission date; a decision carries
one, after submission), a quotation that names the contract it became
must have been accepted, and an open quotation past its follow-up date is
reported, not rejected.

**3. The Dashboard Export writes both.** `ContractsExportAdapter` and
`QuotesExportAdapter` write `contracts.json` and `quotes.json` beside the
two existing files, in the dashboard's own documented `Contract` and
`Quote` shapes (`{ contracts: [ … ] }`/`{ quotes: [ … ] }`, `yyyy-MM-dd`
dates, `source: "tempestos"`), from the current revision of every record
that is not itself Superseded. `QuotationStatus` was modelled on the
dashboard's four words, so its mapping is lossless. `ContractStatus` has
eight values to the dashboard's five, and `ContractsExportAdapter.MapStatus`
is the one place the mapping lives: `Draft → draft`;
`InNegotiation`/`AwaitingSignature → awaiting-signature`;
`Executed → active`; `Expired`/`Terminated`/`Lapsed`/`Superseded → expired`.
Nothing maps to `on-hold` — Core has no paused contract state and none is
invented. Fields Core does not hold (`sentDate`, `renewalDate`, `url`) are
written as `null`, not guessed.

**4. What stays frozen.** Risk, Assets (IP/data), Finance, Development
and Operating in `BusinessGovernance`; all of `CommercialIntelligence`
(`SupplierQuote` and `QuoteFirmness` included — supplier quotes are not
what the dashboard shows); `EngineeringIntelligence`, `Knowledge`, the
frozen halves of `BusinessOperations` and `EngineeringAssets`, and
everything `ADR-0146` froze. Nothing else under `src/Frozen/` was touched.

## Consequences

**Positive.** The dashboard's business view can carry real figures from
the same governed records the rest of `P07` uses, without a second
contract model. The freeze's return path is now demonstrated rather than
asserted: twelve days of Core drift cost one test-side rename. Every
returned kind, and the new one, is registered, host-tested and covered.

**Negative.** `src/Tempest.Core` grows by 2,047 returned lines plus the
new kind, none of it yet reachable from a Desktop screen: contracts and
quotations are authored today only through the catalogues' own API and
reach the dashboard through the export. That is the same position
`WP 18.0C` froze Contracts *for*; the difference is that the export is a
shipped surface that reads them. A Desktop surface is a later Work
Package, not this decision. The Frozen `BusinessGovernanceFixtures.cs`
still carries Contracts fixtures its remaining frozen tests do not need;
it is not compiled and was left as it stood.

## Alternatives Considered

**Thaw `IssuedContract` alone.** Rejected: it does not compile without
`ContractTemplate`, `CommercialTerms` and `DeterminationState`, and a
contract that cannot say which template it was drawn from is the record
`C1` exists to prevent.

**Model the sales quotation as a `CommercialIntelligence.SupplierQuote`
with the parties reversed.** Rejected: a supplier's quote to us and our
quote to a client are different records with different lifecycles, and
thawing `CommercialIntelligence` for one enum would drag 7,461 lines back
for a four-word vocabulary.

**Export `ContractStatus` verbatim and let the dashboard map it.**
Rejected: `docs/DATA-CONTRACTS.md` is the dashboard's contract and its
`decorateContracts` ranks on exactly five words; a sixth would rank as
unknown and sort last, silently.

## Related Documents

`ADR-0129` (P07 records on the shared lifecycle); `ADR-0130` (`Money`);
`ADR-0146` (the freeze and its return test); `WP 18.0C`; `D-028`;
`src/Frozen/README.md`; `docs/architecture/Group C Business Governance.md`;
Tempest-Dashboard `docs/DATA-CONTRACTS.md` ("Contract", "Quote").
