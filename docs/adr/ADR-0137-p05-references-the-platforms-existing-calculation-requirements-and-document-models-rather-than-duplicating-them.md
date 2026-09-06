# ADR-0137: P05 References the Platform's Existing Calculation, Requirements and Document Models Rather Than Duplicating Them

## Status

Accepted — `Group E` (P05 Engineering Assets), 2026-09-06.

## Context

Three of `P05`'s five packages sit directly on top of machinery TempestOS
already has:

| Package | Already exists | The temptation |
|---|---|---|
| `E2` Calculation packs | `Tempest.Core.Calculations` — definitions, engine, execution records | Store the result in the pack |
| `E3` Verification artefacts | `Tempest.Core.Requirements` — requirements, revisions, status; `Tempest.Core.Verification` — verification records | Copy the requirement text into the artefact |
| `E5` Technical documentation | `Tempest.Core.EngineeringData` — documents, revisions, `DocumentReference` links | Build a document-management system |

Each temptation is the same mistake wearing different clothes, and each
looks locally sensible. Copying the requirement text into the verification
artefact means the artefact reads well on its own. Storing the calculated
result in the pack means the pack is self-contained. Holding document
relationships privately means `E5` need not depend on anything.

Each also creates two sources of truth for one fact, and the two diverge
the first time either side changes. A requirement reworded after
verification leaves an artefact quoting text that no longer exists. A
result copied into a pack disagrees with the execution record the moment
the engine is corrected.

## Decision

**`P05` references by identity and never copies the model.**

### E2 — calculations

The pack records what the calculation *was*: its inputs and where each
value came from, its method, its assumptions, its outputs and what they
must satisfy. Where TempestOS performed the arithmetic, the pack names
the definition in `CalculationMethod.CalculationDefinitionId` and links
executions by `CalculationRecord` Id. It does not restate the result.

`P05` builds no calculation engine. Validation reports a pack that names
a platform calculation and links no execution of it, because the
platform's own record of the arithmetic should be findable.

### E3 — requirements

`VerifiedRequirement` carries the requirement's `Guid`, its human
identifier, and — for the historical record — the wording and revision
*at the time of verification*. The wording is a snapshot for the reader,
explicitly labelled as such; the requirement itself remains owned by
`Tempest.Core.Requirements` and is never edited from `P05`. Validation
warns where the revision was not recorded, because that is what lets a
reworded requirement appear to have been verified.

`Tempest.Core.Verification` already records single verification acts;
`E3` links them by Id and adds the governed artefact around them, plus
the three standings its `Pass`/`Fail`/`Conditional` vocabulary cannot
express.

### E5 — documents

The bytes, the revision history and the document-to-document links stay
in `EngineeringData`. `TechnicalDocument` is the *governance card*: what
the document is, what state it is in, who owns it, when it takes effect,
what it replaces. It points at content by `DocumentId` and
`TechnicalDocument.ToDocumentReferences()` expresses its relationships as
the platform's own `DocumentReference` rows rather than keeping a private
relationship model.

`IssueRevision` is a string and deliberately distinct from the record's
own `RevisionNumber`. Drawings are issued at "A", "B", "P1"; the platform
counts 1, 2, 3. Conflating them makes it impossible to say which issue
somebody is holding.

**Enforced by a reflection test** asserting that no type under
`Tempest.Core.EngineeringAssets` exposes a property typed from
`Tempest.Core.Requirements` or `Tempest.Core.Calculations`.

## Consequences

**A `P05` artefact is not self-contained**, and reading one fully needs
the libraries it points at. Accepted: a self-contained artefact is a
second copy of facts that will drift.

**Cross-library resolution is optional throughout.** Every `P05`
validation service takes its collaborators as optional constructor
parameters, so an artefact is recordable and checkable before the thing it
cites is registered. Unresolvable references are reported, never
prevented.

**The `Verification` namespace now has two layers**, which reads oddly
until the distinction is clear: `Core.Verification` records an act,
`EngineeringAssets.Verification` governs the artefact. Named and
documented on both sides rather than merged, because merging them would
mean either giving the act a lifecycle it does not need or denying the
artefact one it does.
