# WP 7.2C — Security Review

## Status

Contract review only. **This review classifies each finding; it does
not design any security mechanism**, per this Work Package's own
explicit instruction.

## Purpose

Reviews the Requirements Platform's own proposed contracts (`WP7.2C
Requirements Platform Contracts.md`) against the nine security
dimensions this Work Package's own controlling instruction names,
classifying each as **Already Addressed**, **Future Capability**,
**Technical Debt**, or **Release Blocking** — a superset of `WP7.2B
Security Architecture.md`'s own four-way classification, now including
"Release Blocking" since a contract review, unlike a pure architecture
review, can meaningfully identify a contract-level defect serious enough
to block implementation.

## Classification

| Dimension | Classification | Rationale |
|---|---|---|
| **Requirement ownership** | **Already Addressed** | `CreatedByPrincipalId` (`WP7.2C Requirements Platform Contracts.md` §2) is inherited directly from `IEngineeringDocumentStore`'s own existing attribution pattern — proven correct by `MaterialCatalog`/`CalculationEngine`/`VerificationService` alike. |
| **Authorisation boundaries** | **Already Addressed, with one open contract-level question** | The calling-layer pattern (`IPermissionEvaluator`, composed externally) is confirmed at the contract level (`WP7.2C Platform Integration Matrix.md`). **Open question, not Release Blocking:** whether `IRequirementsService` should additionally gate any of its own methods internally (mirroring `IVerificationService.GetVerificationHistoryAsync`'s own internal gate) is explicitly left to the owning implementation Work Package — disclosed, not silently decided, and not urgent enough to block this contract's own approval. |
| **Integrity** | **Already Addressed** | Inherited directly from `IDocumentRevision`'s own immutability guarantee (Principle 4) — no requirement-specific integrity mechanism is proposed or required beyond it. |
| **Revision control** | **Already Addressed** | Inherited directly from `IEngineeringDocumentStore.ReviseAsync`/`IRequirementsService.ReviseAsync` — every statement change produces a new, retained revision. |
| **Auditability** | **Already Addressed** | Calling-layer `IAuditRecorder` composition, confirmed at the contract level (`WP7.2C Platform Integration Matrix.md`) — identical to every existing Engineering Core sibling. |
| **Concurrent modification** | **Technical Debt (confirmed, carried forward from `WP 7.2B`)** | `WP7.2B Security Architecture.md`'s own finding — no compare-and-swap/expected-revision-number check exists on `ReviseAsync` — is re-confirmed unchanged at the contract level: `IRequirementsService.ReviseAsync`'s own proposed signature (`WP7.2C Requirements Platform Contracts.md` §1) carries no expected-prior-revision parameter. **Not Release Blocking** — no real, demonstrated multi-author collaborative-editing incident exists yet; `ADR-0060` remains reserved, not answered, for the owning implementation Work Package. |
| **Traceability tampering** | **Already Addressed, with a disclosed limitation** | `LinkAsync`'s own append-only design (Principle-equivalent to Principle 4) means a relationship, once recorded, cannot be silently altered — re-confirmed unchanged from `WP7.2B Security Architecture.md`. No framework-level check prevents a caller from recording a contradictory or duplicate relationship — the same disclosed, non-blocking limitation `TD-18` already carries. |
| **Future electronic approval** | **Future Capability** | No mechanism for multi-party sign-off exists anywhere in this platform. Confirmed unchanged from `WP7.2B Security Architecture.md` — no contract proposed in this Work Package introduces one, consistent with Security Principle 7. |
| **Future digital signatures** | **Future Capability** | No cryptographic signature mechanism exists anywhere in this platform (mirrors `FCR-0017`'s own still-unresolved future capability). Confirmed unchanged. |

## Zero Release Blocking Findings

**No dimension reviewed rises to Release Blocking.** Every disclosed
gap (concurrent modification, traceability-tampering's own duplicate/
contradiction detection) is a real, named limitation with a reserved
ADR (`ADR-0060`) or an inherited, already-accepted platform-wide
disclosure (`TD-18`) — none represents an undisclosed defect in the
proposed contracts themselves. This finding is consistent with, and
extends, `WP7.2B Security Architecture.md`'s own identical conclusion
one review stage earlier.

## What Changed Since `WP 7.2B`'s Own Security Architecture Review

**Nothing new was found.** This is itself a meaningful, positive
confirmation, not an oversight: `WP7.2C Requirements Platform
Contracts.md`'s own proposed method signatures introduce no new
security-relevant surface beyond what `WP7.2B Requirements Domain
Model.md` already anticipated at the architecture level — the contract
review's own job (turning architectural responsibilities into concrete
signatures) did not itself surface a new risk, because every signature
proposed is a direct, disciplined translation of an already-reviewed
responsibility, not a new capability invented during this pass.

## Related Documents

`WP7.2B Security Architecture.md` (the precedent this review confirms
and extends); `docs/governance/Quality/Technical Debt Register.md`
(`TD-16`, `TD-18`); `docs/governance/Future Capability Register.md`
(`FCR-0017`); `WP7.2C Requirements Platform Contracts.md`; `WP7.2C
Required ADR Catalogue.md` (`ADR-0060`); `WP7.2C Platform Integration
Matrix.md`.
