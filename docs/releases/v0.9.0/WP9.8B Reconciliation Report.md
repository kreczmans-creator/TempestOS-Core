# WP 9.8B — Platform Service Register Reconciliation — Reconciliation Report

## Purpose

Eliminate the outstanding Platform Service Register inconsistency
carried through the last three release reviews (`WP 7.4.0`, `WP 8.9.0`,
`WP 9.9.0`): the four Engineering Foundation frameworks implemented by
`WP 7.1A`–`WP 7.1E` (Engineering Data Model, Materials, Engineering
Calculations, Verification) have never had rows in `docs/governance/
Engineering/Platform Services Register.md` or entries in `docs/architecture/
Platform Service Map.md`, despite being correctly tracked as Implemented
everywhere else. Verification and documentation only — no implementation
change, no architectural redesign, no contract change, no service
invented, no service redesigned.

## Disclosed Sequencing Note

This Work Package's own number, `9.8B`, sits inside the `WP 9.6A`–
`WP 9.8A` range `WP 9.5A`'s own controlling instruction explicitly
skipped, and is commissioned **after** `WP 9.9.0` (Release Preparation &
Product Baseline) despite carrying an earlier number — the identical
shape of disclosed numbering irregularity this project's own governance
discipline already has precedent for (`WP 9.3A` completing after
`WP 9.4A`, `v0.9.0`). This is not a defect: `WP 9.9.0` recommended
`v0.9.0` **APPROVED** but the Product Owner has not yet merged, tagged,
or pushed the release — `WP 9.8B` is a direct, disclosed response to
`WP9.9.0 Product Approval Report.md`'s own top standing recommendation
("Make a firm decision about the four-Engineering-Foundation-framework
Platform Service Map/Register gap — this time, actually decide"),
performed before the release is finalised rather than after. Recorded
here plainly, per this project's own "disclose all inconsistencies… do
not silently modify historical records" discipline.

## What Was Reviewed

Per this Work Package's own controlling instruction, five governance
documents:

1. `docs/governance/Engineering/Platform Services Register.md`
2. `docs/architecture/Platform Service Map.md`
3. `docs/governance/Engineering/Dependency Injection Register.md`
4. `docs/governance/Engineering/Module Register.md`
5. `docs/governance/Engineering/Interface Register.md`

Against the real, running implementation of all four Engineering
Foundation frameworks: `Tempest.Core.EngineeringData` (`IEngineeringDocumentStore`/
`EngineeringDocumentStore`, `WP 7.1A`), `Tempest.Core.Materials`
(`IMaterialCatalog`/`MaterialCatalog`, `WP 7.1C`), `Tempest.Core.Calculations`
(`ICalculationEngine`/`CalculationEngine`, `WP 7.1D`), `Tempest.Core
.Verification` (`IVerificationService`/`VerificationService`, `WP 7.1E`).

## Finding 1 — The Gap Was Real, Confirmed by Direct Inspection, Confined to Exactly Two Documents

Direct `grep` of all five reviewed documents for each of the four
frameworks' own key type names confirmed:

| Document | Engineering Data Model | Materials | Engineering Calculations | Verification |
|---|---|---|---|---|
| Platform Services Register | **Missing** | **Missing** | **Missing** | **Missing** |
| Platform Service Map | **Missing** | **Missing** | **Missing** | **Missing** |
| Dependency Injection Register | Present, correct | Present, correct | Present, correct | Present, correct |
| Module Register | Present, correct | Present, correct | Present, correct | Present, correct |
| Interface Register | Present, correct | Present, correct | Present, correct | Present, correct |

The Dependency Injection Register already correctly documents all four
services' own exact registration line, lifetime, and dependency
rationale (backfilled by `WP 7.1F`, `2026-07-30`, and never subsequently
drifted). The Module Register and Interface Register likewise already
correctly list each framework's own sample module and public interface.
**The disclosed gap was real, but narrower than a first reading of "four
frameworks missing governance coverage" might suggest** — it was
confined to exactly the two documents `WP 7.3A` originally named
(Platform Services Register, Platform Service Map), never the other
three. Confirmed by direct inspection, not assumed from the prior
disclosure's own wording.

## Finding 2 — A Second, Distinct Arithmetic Drift, Found During This Reconciliation

While re-deriving the Platform Services Register's own row count
directly (rather than trusting its stated "27 entries" total), a direct
count of the table found **26** rows — not 27 — before this Work
Package's own four additions. The register's own stated bucket
breakdown ("24 Implemented, 1 planned..., 1 developer-convenience
layer") sums to 26, internally consistent with the true row count; only
the headline "27" figure itself was wrong. This is a genuine, distinct
finding from the four-row omission — an arithmetic slip somewhere in
this register's own prior editing history, not previously disclosed by
any Work Package's own review. Corrected directly (26 → 30, after this
Work Package's own four additions), not silently.

## Finding 3 — Two Further Stale "Depended on By" Entries, Found During Cross-Consistency Verification

Per this Work Package's own explicit instruction to cross-check
"Dependencies"/"Consumers" for consistency, the Identity & Permissions
and Persistence rows in `Platform Service Map.md`'s own "At a Glance"
table were checked against their own real consumers, not merely their
own already-written text. Both rows' own "Depended on by" column named
only pre-`v0.7.0` or contemporaneous consumers, never updated when the
Engineering Foundation programme began consuming either service in
`v0.7.0`:

- **Identity & Permissions** — real consumer since `WP 7.1A`: Engineering
  Data Model (`ICurrentPrincipalAccessor`, revision attribution); since
  `WP 7.1D`: Engineering Calculations (`ICurrentPrincipalAccessor`,
  record attribution); since `WP 7.1E`: Verification
  (`ICurrentPrincipalAccessor`/`IPermissionEvaluator`, record
  attribution and query gating). None of the three was ever added to
  this row.
- **Persistence** — real consumer since `WP 7.1A`: Engineering Data
  Model (its own durable storage); since `WP 7.1C`: Materials (its own
  `materialId` index, mirroring Settings'/Audit's own already-documented
  reuse). Neither was ever added to this row.

Both corrected directly in `Platform Service Map.md`, disclosed inline
at each correction, not silently.

## Finding 4 — The Related ADRs Field Was Also Stale

`Platform Services Register.md`'s own `Related ADRs` metadata field
read "ADR-0005 through ADR-0052" — a range that never included
`ADR-0053`/`ADR-0055`–`ADR-0061`, despite the register's own
pre-existing Requirements Engine row (`WP 7.3A`) already citing
`ADR-0058`–`ADR-0061`, outside that stated range, before this Work
Package began. A genuine, pre-existing metadata staleness, not
introduced by this Work Package, found while updating the field to
include the four newly-added rows' own ADRs. Corrected directly,
disclosed inline.

## Backfill Performed

**Zero services invented. Zero services redesigned.** Every fact added
below was verified directly against the real, running implementation —
`TempestHost.cs`'s own registration code, each framework's own real
constructor signature, and a direct, repository-wide search for real
(non-test) consumers — never assumed from a prior Work Package's own
retrospective claim alone (though every fact found was, in the end,
consistent with what those retrospectives already said).

### Platform Services Register — four rows added

| Service | Status | Originating Work Package | Key ADRs |
|---|---|---|---|
| Engineering Data Model | Implemented | WP 7.1A | ADR-0053 |
| Materials | Implemented | WP 7.1C | ADR-0055 |
| Engineering Calculations | Implemented | WP 7.1D | ADR-0056 |
| Verification | Implemented | WP 7.1E | ADR-0057 |

Plus four new "Verification of 'Implemented' Status" paragraphs, mirroring
the existing paragraph shape every other Implemented service already
has (source location, registration point, test count, real consumers).

### Platform Service Map — four complete sections added

Each following the identical Responsibility/Key types/Dependencies/
Consumers/Lifecycle/ADR references/Academy references shape every
existing entry in this document already uses — no new section shape
invented:

- **Engineering Data Model** — depends on Persistence, Identity &
  Permissions; consumed by Materials, Engineering Calculations,
  Verification, Requirements Engine, and the Engineering Domain
  (`EngineeringDomainContext`, `WP 8.2C`); 25 tests.
- **Materials** — depends on Engineering Data Model, Persistence;
  consumed by `MaterialsSampleModule` and the base
  `EngineeringDomainSampleModule` (`WP 8.2C`); 55 tests.
- **Engineering Calculations** — depends on Engineering Data Model,
  Identity & Permissions; consumed by `CalculationSampleModule` and
  `Tempest.App.Workspace.Calculations` (`WP 9.2A`); 52 tests.
- **Verification** — depends on Engineering Data Model, Identity &
  Permissions; consumed by `VerificationSampleModule`, Requirements
  Engine (`GetEvidenceAsync`), `Tempest.App.Workspace.Verification`
  (`WP 9.3A`), and `.Manufacturing` (`WP 9.5A`); 49 tests.

Each new section carries an explicit `**Disclosed, WP 9.8B.**` closing
note naming this Work Package as the reason the section now exists,
rather than presenting the backfill as though it had always been
current.

## Cross-Check Results (Service Ownership / Lifetime / Registration / Dependencies / Consumers)

| Dimension | Engineering Data Model | Materials | Engineering Calculations | Verification |
|---|---|---|---|---|
| **Ownership** | `Tempest.Core`-owned, DI-public | `Tempest.Core`-owned, DI-public | `Tempest.Core`-owned, DI-public | `Tempest.Core`-owned, DI-public |
| **Lifetime** | Singleton (container-constructed) | Singleton (container-constructed) | Singleton (container-constructed) | Singleton (container-constructed) |
| **Registration** | `TempestHost.cs` Phase 6, after Persistence + Identity & Permissions | `TempestHost.cs` Phase 6, immediately after Engineering Data Model | `TempestHost.cs` Phase 6, immediately after Materials | `TempestHost.cs` Phase 6, immediately after Engineering Calculations |
| **Dependencies (real, from constructor)** | `IPersistenceStore`, `ICurrentPrincipalAccessor` | `IEngineeringDocumentStore`, `IPersistenceStore` | `IEngineeringDocumentStore`, `ICurrentPrincipalAccessor` | `IEngineeringDocumentStore`, `ICurrentPrincipalAccessor`, `IPermissionEvaluator` |
| **Consumers (real, non-test)** | Materials, Engineering Calculations, Verification, Requirements Engine, `EngineeringDomainContext` | `MaterialsSampleModule`, `EngineeringDomainSampleModule` | `CalculationSampleModule`, `Tempest.App.Workspace.Calculations` | `VerificationSampleModule`, Requirements Engine, `Tempest.App.Workspace.Verification`/`.Manufacturing` |

Every cell above independently re-derived from source — `TempestHost.cs`'s
own registration order and comments, each service's own real
constructor signature, and a repository-wide `grep` for real
(non-test) constructor usage — cross-checked against, and found fully
consistent with, `Dependency Injection Register.md`'s own already-correct
entries for the same four services.

## Consistency Verdict

**Full consistency confirmed across all five reviewed governance
documents, after this Work Package's own backfill.** Zero contradictions
found between any two documents' own account of any of the four
frameworks' own ownership, lifetime, registration, dependencies, or
consumers. `Interface Register.md`, `Dependency Injection Register.md`,
and `Module Register.md` required zero changes — each was already
correct and remains correct. `Platform Services Register.md` and
`Platform Service Map.md` required the backfill described above, plus
two disclosed, unrelated stale-metadata corrections (Findings 2 and 4)
found along the way.

## Definition of Done — Verified

**"Platform Service documentation accurately reflects the implemented
platform."** Confirmed: every one of the four Engineering Foundation
frameworks now has a complete, accurate governance record across all
five reviewed documents, each independently re-derived from the real,
running implementation, not carried forward from a prior claim.

**"No outstanding Platform Service governance inconsistencies
remain."** Confirmed by this Work Package's own direct, five-document
cross-check — see Consistency Verdict, above. The one remaining,
disclosed Platform Service-adjacent gap this project's governance
tracks (`FCR-0005`, Governance Register Health-Check Tooling — the
*absence of tooling* to catch this class of drift automatically, as
distinct from the drift itself) is **not** a Platform Service
documentation inconsistency and is therefore outside this Work
Package's own Definition of Done; recorded as a standing recommendation
in `WP9.8B Lessons Learned.md`.

## Related Documents

`docs/governance/Engineering/Platform Services Register.md`;
`docs/architecture/Platform Service Map.md`; `docs/governance/
Engineering/Dependency Injection Register.md`; `docs/governance/
Engineering/Module Register.md`; `docs/governance/Engineering/Interface
Register.md`; `WP9.8B Engineering Review.md`; `WP9.8B Security
Review.md`; `WP9.8B Systems Engineering Review.md`; `WP9.8B Lessons
Learned.md`; `WP9.9.0 Product Approval Report.md`;
`docs/governance/Future Capability Register.md` (`FCR-0005`).
