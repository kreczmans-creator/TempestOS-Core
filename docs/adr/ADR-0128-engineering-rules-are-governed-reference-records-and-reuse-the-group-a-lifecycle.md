# ADR-0128: Engineering Rules Are Governed Reference Records, and Reuse the Group A Lifecycle

## Status

Accepted — `Group B` (P02 Engineering Intelligence), 2026-09-06.

## Context

`P02` introduces four new kinds of authored engineering content: rules,
decision trees, review definitions and trade-study definitions.

The obvious reading is that these are *code* — logic, belonging in
source, changed by a developer, deployed with a release. That reading is
wrong, and following it would have cost the platform most of what makes
`Group A` trustworthy.

A design rule has every property a reference-data record has. It came
from somewhere (a standard, a handbook, a company practice, an
engineer's judgement) and that source must be recorded. Somebody wrote
it, somebody else should check it, and until they have, work must not
rely on it. It changes over time, and an assessment made last quarter
must remain reproducible against the rule as it then stood. It can be
withdrawn and replaced. It can be wrong.

`Group A` already has all of that: `ReferenceProvenance`, the
Draft → Checked → Validated → Released → Superseded lifecycle with
provenance gates, `ReferenceDataCatalog<TDefinition>` over
`IEngineeringDocumentStore` with typed and secondary indexes and
per-record write locks, revision history, supersession that links through
the existing `supersedes` relationship, and a validation-service shape
(`ADR-0126`).

Building a second lifecycle for rules would duplicate infrastructure the
platform's charter prohibits, and would guarantee the two diverged —
almost certainly in the direction of the rule side being weaker, because
"it is only a rule" is exactly the argument that gets a release gate
skipped.

## Decision

A rule definition, a decision tree, a review definition and a trade-study
definition are each a governed reference-data record, held in a
`ReferenceDataCatalog<TDefinition>` of their own, under their own
`IEngineeringDocument.Kind`:

| Content | Catalogue | Document Kind | Library name |
|---|---|---|---|
| Design rules | `RuleCatalog` | `EngineeringRule` | `EngineeringRules` |
| Decision trees | `DecisionTreeCatalog` | `EngineeringDecisionTree` | `DecisionTrees` |
| Review definitions | `ReviewDefinitionCatalog` | `EngineeringReviewDefinition` | `EngineeringReviews` |
| Trade studies | `TradeStudyCatalog` | `EngineeringTradeStudy` | `EngineeringTradeStudies` |

Everything follows from that:

- **Only released content governs work.** Every reasoning service reads
  released records and nothing else, with no opt-out.
  `UnreleasedDecisionTreeException`, `UnreleasedReviewDefinitionException`
  and `UnreleasedTradeStudyException` refuse rather than proceed. An
  applicable-but-unreleased *rule* is counted and reported instead, so
  the assessment says what it did not run.
- **Released content is immutable.** The shared lifecycle already refuses
  `ReviseAsync` on a Released record. Changing a released rule means
  registering a corrected one and superseding, and `P02` inherits that
  without a line of its own.
- **Revisions are pinned, not referenced.** Every evaluation, walk,
  finding and judgement records a `ReferencePin(Library, RecordId,
  RevisionNumber)` for the rule and for the subject. A result is
  reproducible against exactly what it read.
- **Validation is per-content-kind, on the shared base.**
  `TEMPEST-EIR-001..016` for rules, `TEMPEST-EID-001..014` for trees,
  `TEMPEST-EIV-001..009` for reviews and `TEMPEST-EIT-001..017` for trade
  studies extend `ReferenceValidationService<TDefinition>`, which already
  checks provenance and resolves standard references.

`ReferenceDataCatalog<TDefinition>` was not modified to accommodate any
of this, and `Group A` was not modified. `P02` reads `P01`; nothing in
`P01` knows `P02` exists.

## Consequences

**A rule cannot be shipped by a developer alone.** It is registered,
checked, validated and released through the same gate as a material
property. That is slower than editing a source file, and it is the
correct speed: guidance that no engineer has reviewed must not silently
start failing designs.

**Rule content is data, so it can be imported, exported, diffed and
audited** by the same machinery that handles reference data, and appears
in the same revision history and governance reports.

**A per-library pin resolver was deliberately not built.** Each result
pins revisions through the catalogue that produced them. A generic
"resolve any pin" service would have made `P02` depend on all seven `P01`
libraries at once, for the sake of a convenience nothing needs.

**The rule expression algebra is data too.** `RuleExpression` is a
polymorphic, serialisable record hierarchy (`AllOf`, `AnyOf`, `Not`,
quantity comparison, text match, property-recorded, evidence-required,
stated), evaluated by a pure function. Rules are not compiled, not
scripted, and not executed as code — which is what allows a rule to be
stored, revisioned and reviewed as a document at all.

## Alternatives considered

**Rules in source, as C# predicates.** Rejected: no provenance, no
review gate, no revision pinning, and an assessment from six months ago
could never be reproduced once the code changed.

**One shared `EngineeringContent` catalogue for all four kinds.**
Rejected: it would need a discriminator and a nullable field set per
kind, and searching for "rules in the Tolerances domain" would have to
filter a mixed collection. Four typed catalogues over one shared base is
the same amount of shared machinery with none of that.

**A rules DSL, parsed at load.** Rejected for now: it adds a grammar, a
parser and a class of syntax errors, to express what eight record types
already express, and the records serialise and diff better than text
would.
