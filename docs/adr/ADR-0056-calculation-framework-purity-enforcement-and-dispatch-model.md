# ADR-0056: Calculation Framework — Purity Enforcement and Dispatch Model

## Status

Accepted — `WP 7.1D` (Engineering Calculation Framework), 2026-07-30.

## Context

`WP7.0C Required ADR Catalogue.md` reserved two questions for this Work
Package: whether `ICalculationDefinition<TInput, TResult>.Calculate`'s
purity requirement should remain convention-only (documented, verified
by test) or be enforced by a stronger mechanism (a dedicated analyzer, a
restricted execution context); and whether `CalculationRecord<TResult>`
should optionally integrate with the Engineering Data Model or remain a
standalone record type.

This Work Package's own controlling instruction introduced substantially
more scope than `WP7.0C Engineering Foundation Contracts.md`'s own
illustrative code block showed: calculation metadata, explicit
assumptions, explicit constraints, a validation model, an intermediate-
result model, a calculation execution context, material references, and
mandatory revision-capable identity — all requirements a calculation
"represents engineering evidence, not merely a numerical answer." This
Work Package resolves both reserved questions and designs the additional
structure this evidentiary requirement demands.

## Decision

**1. Purity enforcement: convention-only, confirmed.** No compiler- or
analyzer-enforced purity mechanism is introduced.
`ICalculationDefinition<TInput, TResult>.Calculate` remains a documented
requirement, verified by
`ExecuteAsync_ConcurrentDifferentInputs_SamePureCalculation_
AllProduceCorrectResults` — the same test category
`WP7.0C Testing Strategy.md` itself named. A sandboxed or restricted
execution context was considered and rejected, exactly as
`WP7.0C Required ADR Catalogue.md` itself anticipated: no C#/.NET
mechanism exists for this without disproportionate custom
infrastructure, absent a demonstrated, real problem with convention-only
enforcement.

**2. `CalculationRecord<TResult>` integration with the Engineering Data
Model: mandatory, not merely plausible.** Every execution is durably
recorded as an `EngineeringData.IEngineeringDocument` of
`Kind = "CalculationRecord"` — resolving the reserved question in the
direction this Work Package's own "Calculation identity" and
"Calculation revision support" requirements demand. `CalculationRecord<TResult>.Id`
is the underlying document's own Id, usable directly with
`IEngineeringDocumentStore` for revision history — mirroring
`Materials.MaterialCatalog`'s own "thin index, no duplicated API"
resolution: `ICalculationEngine` adds no `ReviseAsync`/`FindAsync` of its
own. Each execution always creates a fresh document — an append-only
evidentiary event, never looked up later by a caller-chosen key — so,
unlike `MaterialCatalog`, no direct `Persistence.IPersistenceStore`
dependency is needed.

**3. `Calculate`'s own signature changes from `Calculate(TInput)` to
`Calculate(TInput, CalculationContext)`.** The approved contract's own
illustrative `Calculate(TInput input)` cannot express intermediate
results, per-execution constraint checks, or material references — none
of which existed as a concept before this Work Package. `CalculationContext`
is a fresh, non-shared, non-ambient recorder the engine constructs once
per execution and discards after reading it back — it does not
compromise purity in any sense that matters (no I/O, no state shared
across executions, no hidden side channel a caller cannot see, since
every value recorded appears directly in the resulting
`CalculationRecord<TResult>`).

**4. `CalculationMetadata` (Name, Description, Category, Assumptions,
Constraints) is fixed per definition, copied into every
`CalculationRecord<TResult>` at execution time.** This makes "every
assumption is explicit" a structural property of the record itself —
never requiring a live lookup of the original definition (which may not
even still be registered) to know what governed a past execution.

**5. Validation outcome (`Valid`/`Conditional`) is derived automatically
from recorded constraint checks, never asserted by the engine itself.**
A constraint violation severe enough to invalidate the result is
expected to be reported by throwing `CalculationInputInvalidException`
directly — no record is created for that case, exactly as the approved
contract's own Error Handling section specifies. `Conditional` exists
for the softer case: a definition that returns a real result while
still disclosing that an advisory constraint was not met.

**6. "Material references" are open, unvalidated strings — no dependency
on `Tempest.Core.Materials`.** `CalculationContext.ReferenceMaterial(string
materialId)` records a reference without this framework resolving or
validating it, mirroring `EngineeringData.DocumentReference.RelationshipKind`'s
own open-string precedent. The approved contract does not define a hard
Materials dependency for Calculation (only the reverse: Materials'
own contract names Calculation as a plausible *consumer* of Materials'
own properties) — this Work Package does not introduce one.

## Consequences

**Positive:**

- Every `CalculationRecord<TResult>` is genuinely self-contained
  evidence — assumptions, intermediate results, validation outcome, and
  material references travel with the result, not merely cross-
  referenced by Id.
- No new dependency (Materials, a sandboxing mechanism, an analyzer) was
  introduced beyond what the approved contract and this Work Package's
  own requirements strictly demand.
- The append-only, no-lookup-by-key design means `CalculationEngine`
  needs no `IPersistenceStore` dependency of its own, a simpler shape
  than `MaterialCatalog`'s.

**Negative:**

- `Calculate`'s own signature is not what the approved contract's
  illustrative code showed — a disclosed, authorised change (this
  Work Package's own explicit "Calculation context" requirement left no
  alternative), not an unauthorised deviation.
- Purity remains unenforced by the compiler — a definition that
  violates it (performs I/O, mutates shared state) will not be caught
  except by code review or the concurrency test's own, necessarily
  partial, empirical evidence.
- `CalculationIntermediateResult.Value` (a boxed `object`, like
  `Materials.MaterialProperty.Value`) is not guaranteed to deserialize
  back to its exact original CLR type if read back from storage later —
  it is fully inspectable from the in-memory record `ExecuteAsync`
  returns immediately, which is this Work Package's own primary use
  case, but not guaranteed to round-trip through the durable document
  for every possible CLR type a future definition might choose (see
  Technical Debt Assessment).

## Alternatives Considered

**A dedicated Roslyn analyzer enforcing `Calculate`'s own purity at
compile time** — considered and rejected, mirroring
`WP7.0C Required ADR Catalogue.md`'s own reasoning: no demonstrated,
real problem with convention-only enforcement exists yet to justify the
infrastructure cost.

**Leaving `CalculationRecord<TResult>` as a standalone record, with no
Data Model integration** — considered and rejected, since this Work
Package's own explicit "Calculation identity" and "Calculation revision
support" requirements cannot be satisfied by an in-memory-only record
with no durable, revisable storage.

**A hard dependency on `Tempest.Core.Materials` for validated material
references** — considered and rejected. The approved contract does not
require it, and an open string reference is sufficient for "Material
references where applicable" without coupling Calculation's own
timeline to Materials or requiring every calculation consumer to also
depend on Materials even when it never touches one.

## Related Documents

`ADR-0053` (the Engineering Data Model integration precedent this
decision extends); `ADR-0055` (the "thin index, no duplicated API"
precedent this decision's own `CalculationRecord.Id`-based traceability
mirrors); `docs/releases/v0.7.0/WP7.0C Engineering Foundation
Contracts.md`; `docs/releases/v0.7.0/WP7.0C Required ADR Catalogue.md`;
`docs/releases/v0.7.0/WP7.1D Implementation Report.md`.
