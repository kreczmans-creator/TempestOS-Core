# WP 7.0C — Engineering Foundation Required ADR Catalogue

## Status

**A catalogue of anticipated ADRs, not finished ADR documents.** None of
the five entries below is written as an `Accepted`-status file under
`docs/adr/` — each is deferred to its own owning Work Package's
dedicated architecture phase, mirroring `docs/releases/v0.6.0/Required
ADRs.md`'s own role for that release. Numbering begins at `ADR-0053` —
the highest existing ADR is `ADR-0052-rest-api-identity-resolution-
never-touches-the-ambient-current-principal.md`, confirmed directly
against `docs/adr/` immediately before this document was written.
**No candidate Work Package (`D`–`H`, `WP7.0B Candidate Work Package
Catalogue.md`) is yet approved** — "Originating Candidate" below names
the candidate expected to resolve each entry, not an approved Work
Package.

## The List

### ADR-0053 — Engineering Data Model's Storage Substrate and Revision/Reference Persistence Model

**Originating Candidate.** `D` (Engineering Data Model & Document
Foundation Architecture).
**Context.** `IEngineeringDocumentStore` (`WP7.0C Engineering Foundation
Contracts.md`) needs a durable substrate for documents, revisions, and
references. `IPersistenceStore` (`WP 6.4`) is a plausible, not
guaranteed, choice — its own key-value shape has no native concept of a
revision sequence or a typed reference.
**Anticipated decision.** Either (a) build directly on
`IPersistenceStore`, serializing revision/reference structure into its
own value strings, accepting `FCR-0007`'s existing query-capability gap
as a shared limitation, or (b) introduce a new, dedicated storage
abstraction purpose-built for revisioned, linked documents. This
Work Package's own proposed contract (`WP7.0C Engineering Foundation
Contracts.md`) does not presume which — both remain open.
**Alternative considered and rejected.** Building the Data Model
directly on top of a hypothetical extended `IPersistenceStore` with
native query support was considered and explicitly not assumed here,
since `FCR-0007` (that extension) is itself unscheduled and gated on a
real, measured performance problem — coupling the Data Model's own
timeline to an unrelated, unscheduled capability would be a planning
risk this catalogue declines to introduce.

### ADR-0054 — Units & Quantities: Representation, Precision, and Registration Model

**Originating Candidate.** `E` (Units & Quantities Framework
Architecture).
**Context.** `Quantity<TDimension>`/`Unit<TDimension>` (`WP7.0C
Engineering Foundation Contracts.md`) are proposed as `double`-backed,
generic value types requiring no DI registration at all — a genuine
departure from every other Engineering Foundation framework.
**Anticipated decision.** Confirm (or revise) the `double`-based
representation; confirm (or revise) the no-DI-registration design;
decide whether `IUnitConverter` is built at all, given its own proposed
triviality.
**Alternative considered and rejected.** A `decimal`-based
representation was considered, for exact decimal arithmetic some
engineering standards may eventually require, and not assumed here —
`double` is this review's own proposed default (matching this
platform's own existing numeric conventions elsewhere), but the
owning Work Package should confirm this is sufficient before committing,
since changing the underlying numeric type later would be a
breaking change to every consumer.

### ADR-0055 — Materials Framework: Property Typing and Platform-Service Classification

**Originating Candidate.** `G` (Materials Framework Architecture).
**Context.** `IMaterialSpecification.Properties` (`WP7.0C Engineering
Foundation Contracts.md`) is proposed as an open
`IReadOnlyDictionary<string, object>`, boxing `Quantity<TDimension>`
values of differing dimensions in one heterogeneous collection — a
disclosed, not fully satisfying, resolution to C#'s own difficulty
expressing a strongly-typed heterogeneous dimensioned collection.
**Anticipated decision.** Confirm the open, boxed-`object` property
shape, or design a stronger alternative (a discriminated-union-style
property value, or a fixed, extensible enum of well-known property
kinds each with its own dimension). Also: `ADR-0013` classification
(Platform Service, confirmed here as this review's own working default,
or a Module).
**Alternative considered and rejected.** A fixed, closed set of
property names (density, yield strength, and a handful of others) was
considered and rejected — closing the set now would encode assumptions
about which material properties matter without a real discipline
requirement to validate them against, repeating exactly the invention
`WP 7.0A`/`WP 7.0B` both declined for discipline-specific capability.

### ADR-0056 — Calculation Framework: Purity Enforcement and Dispatch Model

**Originating Candidate.** `F` (Engineering Calculation Framework
Architecture).
**Context.** `ICalculationDefinition<TInput, TResult>.Calculate`
(`WP7.0C Engineering Foundation Contracts.md`) is required, by
documented convention, to be a pure function — C# has no compiler-
enforced purity mechanism, so this requirement is presently
convention-only, not structurally guaranteed.
**Anticipated decision.** Confirm convention-only enforcement
(documented requirement, verified by test, per `WP7.0C Testing
Strategy.md`'s own purity/concurrency test), or design a stronger
mechanism (a dedicated analyzer, a restricted execution context). Also:
whether `CalculationRecord<TResult>` should optionally integrate with
the Engineering Data Model (recording a calculation as a document
revision) or remain a standalone record type.
**Alternative considered and rejected.** Requiring every calculation to
run inside a sandboxed or restricted execution context (preventing
impure operations at runtime) was considered and not adopted as this
review's own default, since no C#/.NET mechanism for this exists without
substantial custom infrastructure this review judges disproportionate
absent a demonstrated, real problem with convention-only enforcement.

### ADR-0057 — Verification & Validation Framework: Relationship to Audit and Method Vocabulary

**Originating Candidate.** `H` (Verification & Validation Framework
Architecture).
**Context.** `IVerificationRecord` (`WP7.0C Engineering Foundation
Contracts.md`) is deliberately distinct from `IAuditRecorder`, but both
answer adjacent "what happened" questions, and `method` is deliberately
an open `string` rather than a closed vocabulary.
**Anticipated decision.** Confirm Verification and Audit remain
separate, complementary mechanisms (this review's own proposed
default, per `WP7.0C Cross-Framework Dependency Report.md`'s own Reuse
Opportunities finding) rather than merging either into the other.
Separately: whether `method` should eventually become a closed
enumeration once a real, confirmed standard or practice names one, or
remain open indefinitely.
**Alternative considered and rejected.** Merging Verification into
Audit (adding an `Outcome`/`Method` field directly to
`IAuditRecorder.RecordAsync`) was considered and rejected — Audit's own
existing contract (`ADR-0045`) is deliberately generic ("who did what,
when"); overloading it with verification-specific semantics would
violate the same one-reason-to-change principle `WP6.5`'s own Audit
design already established, and would force every non-verification
Audit consumer to reason about fields irrelevant to it.

## Cross-Reference Check

Every entry above cites a specific `WP7.0C Engineering Foundation
Contracts.md` clause and, where applicable, an existing ADR its own
anticipated decision would extend or mirror (`ADR-0013`, `ADR-0041`).
No open question disclosed anywhere else in this Work Package's own
deliverables (`Cross-Framework Dependency Report.md`, `Engineering
Standards Mapping.md`, `Platform Integration Matrix.md`) is missing an
entry here — each of the five Engineering Foundation frameworks has
exactly one catalogued ADR, consistent with this review's own
one-Work-Package-per-framework structure (`WP7.0B Candidate Work
Package Catalogue.md`, Candidates `D`–`H`).

## Related Documents

`docs/releases/v0.6.0/Required ADRs.md` (the precedent this catalogue's
own structure follows); `WP7.0C Engineering Foundation Contracts.md`;
`WP7.0B Candidate Work Package Catalogue.md`; `docs/adr/` (`ADR-0001`–
`ADR-0052`, the existing sequence this catalogue extends).
