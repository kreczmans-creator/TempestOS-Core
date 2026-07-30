# ADR-0057: Verification Framework — Relationship to Audit and Method Vocabulary

## Status

Accepted — `WP 7.1E` (Verification Framework), 2026-07-30.

## Context

`WP7.0C Required ADR Catalogue.md` reserved two questions for this Work
Package: whether Verification and Audit remain separate, complementary
mechanisms rather than either being merged into the other; and whether
`method` should become a closed enumeration once a real standard names
one, or remain open indefinitely.

This Work Package's own controlling instruction introduced substantially
more scope than `WP7.0C Engineering Foundation Contracts.md`'s own
illustrative code block showed — explicit verification criteria,
structured evidence, linked engineering documents, linked calculations,
material references, and revision-capable identity — the same
"engineering evidence, not merely [an outcome]" requirement `WP 7.1D`
(Calculation) already established a precedent for resolving. This Work
Package resolves both reserved questions and designs the additional
structure this evidentiary requirement demands, plus one genuine
implementation finding neither prior Work Package anticipated: how
verification history is queried without a new index.

## Decision

**1. Verification and Audit remain separate, complementary mechanisms —
confirmed.** No dependency on `Tempest.Core.Audit` was introduced.
`IVerificationRecord` answers "has this engineering claim been
demonstrated?"; `IAuditRecorder` answers "who did what, when." Merging
either into the other was rejected, exactly as
`WP7.0C Required ADR Catalogue.md`'s own reasoning anticipated:
overloading `IAuditRecorder.RecordAsync` with verification-specific
semantics would violate the same one-reason-to-change principle
`ADR-0045` already settled for Audit generally.

**2. `method` remains an open string — confirmed.** No real engineering
standard was identified during this Work Package naming a fixed
verification-method vocabulary (inspection, test, analysis,
demonstration remain the informally-expected values, never enforced).
Closing this vocabulary now would encode assumptions no real discipline
requirement yet validates, mirroring `Materials.MaterialProperty`'s own
identical "do not invent a taxonomy" precedent (`ADR-0055`).

**3. Verification history is queried through the Engineering Data
Model's own existing reference mechanism — a genuine implementation
finding, not anticipated by the reserved-ADR catalogue.** Each
verification is its own `IEngineeringDocument` of
`Kind = "VerificationRecord"`, created via `IEngineeringDocumentStore.
CreateAsync`. `RecordAsync` then links the subject document to it
(`VerifiedByRelationshipKind = "verifiedBy"`) via
`IEngineeringDocumentStore.LinkAsync`.
`GetVerificationHistoryAsync` reads exactly this back via
`IEngineeringDocumentStore.GetReferencesAsync`, filtered to
`"verifiedBy"` references, sorted by `VerifiedAt`. No new index, and no
direct `Persistence.IPersistenceStore` dependency, is needed at all —
a stronger resolution than `Materials.MaterialCatalog`'s own direct
Persistence dependency (`ADR-0055`), since Verification never looks
anything up by an arbitrary caller-chosen string key, only by
`IEngineeringDocument` Id, exactly the shape `GetReferencesAsync`
already provides.

**4. `RecordAsync`'s own signature changes from accepting a bare
`evidence: string?` to a `VerificationContext` — an additive extension
to the shown contract, not a change to `subjectDocumentId`, `outcome`,
or `method`, all three of which remain exactly as approved.**
`VerificationContext` mirrors `Calculations.CalculationContext`'s own
shape (a caller-populated recorder, not framework-dispatched — the
causality differs, since nothing here is a framework-invoked pure
function, but the ergonomic benefit of one aggregating, incrementally-
built object is the same), letting a caller record explicit criteria,
evidence, linked documents, linked calculation records, and referenced
materials before a single `RecordAsync` call.

**5. Linked documents and linked calculation records are validated;
referenced materials are not.** `VerificationContext.LinkDocument`/
`LinkCalculationRecord` both resolve to real `IEngineeringDocumentStore.
LinkAsync` calls — a non-existent Id throws
`EngineeringDocumentNotFoundException`, since Verification already
depends directly on the Data Model. `ReferenceMaterial` remains an open,
unvalidated string, mirroring `Calculations.CalculationContext.
ReferenceMaterial`'s own identical, disclosed precedent (`ADR-0056`
Decision 6) — Verification has no dependency on `Tempest.Core.Materials`,
consistent with the approved contract naming Materials only as "where
appropriate," never mandatory.

**6. No new exception type — `EngineeringDocumentNotFoundException` is
reused directly**, exactly as the approved contract specified and
`WP7.1A Future Capability Recommendations.md` Recommendation 2
anticipated. `Tempest.Core.Verification` introduces zero exception
types of its own.

## Consequences

**Positive:**

- Verification history requires no new storage mechanism and no direct
  Persistence dependency — the simplest dependency shape of any
  Engineering Foundation framework so far.
- Linked documents and calculation records are genuinely validated
  (real `EngineeringDocumentNotFoundException` on a bad Id), a stronger
  traceability guarantee than Materials' or Calculation's own
  material-reference handling, since Verification's own hard dependency
  on the Data Model makes validation free to provide.
- Zero new exception types keeps this framework's own public surface
  minimal, and directly fulfils a two-Work-Packages-old recommendation.

**Negative:**

- `RecordAsync`'s own linking of extra documents/calculation records is
  not transactional — a failure partway through (e.g., the second of
  two linked documents does not exist) leaves the verification record
  created and linked to its subject, but not to every intended
  additional link (see Technical Debt Assessment, `TD-23`).
- `GetVerificationHistoryAsync` returns an empty list, rather than
  throwing, for a non-existent `subjectDocumentId` — a deliberate,
  disclosed choice for consistency with `GetReferencesAsync`'s own
  identical behaviour, but a caller expecting symmetry with
  `RecordAsync`'s own throwing behaviour should note the asymmetry.

## Alternatives Considered

**Merging Verification into Audit** — considered and rejected; see
Decision 1.

**A closed `VerificationMethod` enumeration** — considered and rejected;
see Decision 2.

**A dedicated `materialId`-to-verification or `subjectDocumentId`-to-
verification index via direct `IPersistenceStore` access**, mirroring
`MaterialCatalog`'s own design — considered and rejected once the
existing `LinkAsync`/`GetReferencesAsync` mechanism was found to satisfy
the identical need without any new dependency at all.

**A hard dependency on `Tempest.Core.Calculations` to validate linked
calculation record Ids against a real, registered calculation** —
considered and rejected. A `CalculationRecord<TResult>.Id` is simply a
`Guid` that happens to also be an `IEngineeringDocument` Id;
`LinkAsync`'s own existing existence check is sufficient without
requiring Verification to reference the Calculation Framework's own
assembly at all.

## Related Documents

`ADR-0053` (the Engineering Data Model's own reference mechanism this
decision reuses); `ADR-0055`/`ADR-0056` (the "thin, no duplicated
storage" precedent this decision extends furthest); `ADR-0045` (Audit's
own orthogonality this decision confirms, not revisits);
`docs/releases/v0.7.0/WP7.0C Engineering Foundation Contracts.md`;
`docs/releases/v0.7.0/WP7.0C Required ADR Catalogue.md`;
`docs/releases/v0.7.0/WP7.1A Future Capability Recommendations.md`
(Recommendation 2); `docs/releases/v0.7.0/WP7.1E Implementation
Report.md`.
