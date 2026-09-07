# ADR-0129: Business Governance Records Reuse the Shared Reference-Data Lifecycle, and Keep Domain Status Separate

## Status

Accepted — `Group C` (P07 Business Governance & Scale), 2026-09-06.

## Context

`P07` introduces eleven kinds of governed business record: contract
templates, issued contracts, business risks, insurance policies, IP
assets, data assets, rate cards, financial assumptions, financial
scenarios, opportunities and operating models.

Every one of them has the same governance shape. It was authored by
somebody. It came from somewhere, and where it came from matters. Somebody
else should check it before work relies on it. It changes over time, and
a decision taken last quarter must remain readable against the version
that was current when it was taken. It can be withdrawn and replaced. It
can be wrong.

That is exactly the shape `ADR-0126` extracted for `Group A` and
`ADR-0128` reused for `Group B`. Building a third lifecycle for business
records would duplicate infrastructure this platform's charter prohibits,
and would guarantee the three diverged — most likely in the direction of
the business side being weaker, because "it is only a rate card" is
precisely the argument that gets a release gate skipped.

There is, however, a genuine difference from `Group A` and `Group B`. A
material has no state beyond its governance state: a material record is
Draft, Checked, Validated, Released or Superseded, and there is nothing
else to say about it. A contract has two states at once. The *record* can
be Released — accurate, checked, complete — while the *contract* is still
in negotiation. A rate card can be a Released record and an unapproved
price. A policy record can be Released and the policy expired.

Collapsing those two axes is how a system comes to report a draft as a
signed contract.

## Decision

**Governance is shared; domain status is not.**

Every `P07` library is a `ReferenceDataCatalog<TDefinition>` over the
Engineering Data Model, with its own `IEngineeringDocument.Kind` and its
own secondary uniqueness index:

| Content | Library | Document Kind |
|---|---|---|
| Contract templates | `BusinessContractTemplates` | `BusinessContractTemplate` |
| Issued contracts | `BusinessContracts` | `BusinessContract` |
| Business risks | `BusinessRisks` | `BusinessRisk` |
| Insurance policies | `BusinessInsurancePolicies` | `BusinessInsurancePolicy` |
| IP assets | `BusinessIPAssets` | `BusinessIPAsset` |
| Data assets | `BusinessDataAssets` | `BusinessDataAsset` |
| Rate cards | `BusinessRateCards` | `BusinessRateCard` |
| Financial assumptions | `BusinessFinancialAssumptions` | `BusinessFinancialAssumption` |
| Financial scenarios | `BusinessFinancialScenarios` | `BusinessFinancialScenario` |
| Opportunities | `BusinessOpportunities` | `BusinessOpportunity` |
| Operating models | `BusinessOperatingModels` | `BusinessOperatingModel` |

From that follow, without a line of new infrastructure: provenance on
every record; a released record that cannot be edited in place, only
superseded; every revision permanently readable; `ReferencePin`
traceability; and per-library validation services on the shared base.

**Domain status is a separate axis on the definition.**
`ContractStatus`, `PolicyStatus`, `PipelineStage` and
`RevenueReality` are properties of the business object, orthogonal to
`ReferenceValidationState`. A caller asking "is this contract signed?"
reads `Status`; a caller asking "may I rely on this record?" reads the
record's validation state. Neither answers the other.

**Governance facts common to all eleven are composed, not inherited.**
`BusinessGovernanceFacts` — owner, classification, review schedule,
authorisations, outstanding authorities, evidence — is a property on each
definition. A base class would have forced eleven unrelated domains
through one schema with a nullable field for each of their differences;
`ADR-0126` rejected the same thing for `Group A`, for the same reason.

**Shared validation runs once.** `BusinessGovernanceValidator` checks the
shared facts (`TEMPEST-BGV-001`–`010`) and each package's own service
calls it, rather than eleven copies of "does this record name an owner?".

## Consequences

**A rate card cannot be published by editing a file.** It is registered,
checked, validated, released and separately approved. That is slower, and
it is the correct speed for a number that binds the organisation to
whoever it is shown to.

**Business records gain revision traceability for free**, which is what
lets an issued contract resolve to the template revision it was drawn
from, and a two-year-old quotation reproduce the price it gave.

**Two states must be read together to understand a record**, and a caller
that reads only one will be wrong. This is a real cost of the decision.
It is mitigated by naming: `ReferenceValidationState` is on the record,
`Status` is on the definition, and no property is named in a way that
invites confusion between them.

**`P07` does not depend on `P01` or `P02`.** It reads the document store,
persistence and identity, and nothing else. The three programmes are
independent, and the container's registration order reflects that.

## Alternatives considered

**A `BusinessObject` base class carrying identity, status, revision,
owner and approval.** Rejected: an insurance policy and an opportunity
share governance and share nothing else, and one schema for both means a
schema that describes neither.

**One `BusinessRecord` catalogue with a discriminator.** Rejected for the
same reason `ADR-0128` rejected it for `P02`: searching for "rate cards
applying in June" would mean filtering a mixed collection, and the typed
catalogues cost nothing over the shared base.

**Reusing `ReferenceValidationState` as the contract status.** Rejected,
and the most tempting of the three. Released would have to mean
"executed", which makes an accurate record of a draft contract
impossible to express, and makes every accurate record of an unsigned
contract look like a signed one.
