# WP 7.1E — Verification Framework — Security Review Report

## Purpose

This Work Package's own controlling instruction required a proportionate
security review, mirroring `WP 7.1D`'s own precedent — the second
Engineering Foundation Work Package to require one explicitly. Every
category the controlling instruction named is reviewed below against the
real, committed implementation, classified as **Not Applicable**,
**Accepted Risk**, **Technical Debt**, or **Release Blocking**.

## Review

| Category | Finding | Classification |
|---|---|---|
| **Input validation** | Every framework-owned parameter is validated (`method`, `context` on `RecordAsync`; every `VerificationContext` recording method validates its own string arguments). `subjectDocumentId` is a bare `Guid` — an empty or malformed one simply fails `FindAsync`'s own lookup, correctly surfacing as `EngineeringDocumentNotFoundException`, not a distinct failure mode. | Not Applicable — reviewed, no gap found |
| **Exception disclosure** | `EngineeringDocumentNotFoundException`'s own message embeds the failing Id — an already-reviewed, established convention (`WP 7.1A`). `PermissionDeniedException` discloses no more than `IAuditQuery`'s own identical, already-reviewed pattern. | Not Applicable — inherits already-reviewed conventions |
| **Serialization safety** | `VerificationRecordDto` is only ever serialized, then deserialized by this framework's own trusted round-trip — never deserialized from untrusted external input. | Not Applicable — no deserialization of untrusted data occurs |
| **Thread safety** | `VerificationService` holds no mutable state of its own — every guarantee is inherited from `IEngineeringDocumentStore`'s own already-reviewed thread safety (`WP 7.1A`). Confirmed by `RecordAsync_ConcurrentCallsAgainstSameSubject_AllSucceedAndAppearInHistory` (15 concurrent recordings against the same subject, all correctly linked and retrievable). | Not Applicable — reviewed, no gap found |
| **Concurrency correctness** | Same evidence as Thread Safety, above — each `RecordAsync` call creates an independent document and an independent reference entry (itself keyed by a random Id in `EngineeringDocumentStore`'s own per-source-document collection), so concurrent recordings against the same subject cannot collide. | Not Applicable — reviewed, no gap found |
| **Resource exhaustion** | `VerificationContext` imposes no upper bound on how many criteria, evidence entries, linked documents, linked calculation records, or material references a single verification may record — the identical, already-disclosed finding `WP 7.1D`'s own Security Review made for `CalculationContext` (`TD-22`). Separately, `GetVerificationHistoryAsync`'s own cost scales with the subject document's total reference count (not only verification references) plus a full revision-history read per matching record — the same linear-scan family already disclosed for `MaterialCatalog` (`TD-20`) and `IAuditQuery` (`TD-12`). | **Technical Debt** (`TD-23`, `TD-24`) — disclosed, not blocking, proportionate to a framework whose own callers remain trusted, first-party, in-process code |
| **Denial-of-service opportunities** | No network-facing surface exists for this framework in this Work Package's own scope. `RecordAsync` is not permission-gated — mirroring `IAuditRecorder.RecordAsync`'s own identical, already-reviewed asymmetry (only the read side, `GetVerificationHistoryAsync`, is gated). | Accepted Risk — matches an already-established, platform-wide precedent |
| **Data integrity** | Every verification record is stored as an immutable, append-only `IEngineeringDocument` revision — the same integrity guarantee already established for the Data Model generally. `RecordAsync`'s own multi-step linking sequence (create record, link to subject, link to each additional document/calculation record) is **not transactional** — a failure partway through (e.g. the second of two linked documents does not exist) leaves the verification record created and linked to its subject, but not to every intended additional link. | **Technical Debt** (`TD-23`) |
| **Tamper resistance** | No cryptographic signing of stored verification records, mirroring the platform's own existing, disclosed trust model (`TD-16`) — not a new gap this Work Package introduces. | Not Applicable — inherits an already-disclosed, platform-wide trust boundary |
| **Trust boundaries** | `RecordAsync` requires no permission — any caller resolving `IVerificationService` may record a verification, exactly mirroring `IAuditRecorder`'s own identical, already-reviewed design and the approved contract's own explicit "permission-gated **read** access" framing (write access was never specified as gated). | Not Applicable — matches an already-established, contract-specified precedent |
| **Unsafe assumptions** | Reference filtering in `GetVerificationHistoryAsync` uses ordinal string comparison on `RelationshipKind` — no format-string or injection risk. No unsafe casts anywhere in this framework. | Not Applicable — reviewed, no gap found |
| **Dependency risk** | No new third-party dependency — only `System.Text.Json`, already used extensively elsewhere in `Tempest.Core`. | Not Applicable |
| **Supply-chain considerations** | No new dependency, therefore no new supply-chain surface. | Not Applicable |
| **Secure defaults** | `VerifiedByPrincipalId` defaults to an honest `"unknown"` sentinel when no principal is established. `GetVerificationHistoryAsync` fails closed by default — `VerificationSampleModule`'s own registered command demonstrates exactly this: denied until a permission grant exists. | Not Applicable — reviewed, secure by construction |
| **Backwards compatibility risks** | `Tempest.Core.Verification` is a brand-new namespace with zero existing consumers — the `RecordAsync` signature change relative to `WP7.0C`'s own illustrative proposal (never compiled or shipped code) carries no backward-compatibility impact. | Not Applicable |

## New Debt Disclosed by This Review

### TD-23 — `RecordAsync`'s Own Multi-Link Sequence Is Not Transactional

**What.** Creating a verification record, linking it to its subject, and
linking it to every additional document/calculation record are four or
more separate, sequential operations against `IEngineeringDocumentStore`
— a failure partway through leaves a partially-linked record, not a
fully-formed one and not a cleanly-absent one.

**Revisit trigger.** A real, demonstrated need for transactional
multi-document operations — see `FCR-0036`, raised directly from this
finding.

### TD-24 — `VerificationContext` Imposes No Bound on Recorded Data Volume; `GetVerificationHistoryAsync` Scales With Total Reference Count

**What.** A caller may record an unbounded number of criteria, evidence
entries, or links in one `VerificationContext`; separately,
`GetVerificationHistoryAsync` reads every reference from a subject
document (not only verification references) and a full revision history
per matching record, scaling linearly with both.

**Revisit trigger.** A real, measured performance problem, or a real
need for bounded recording.

## New Accepted Trade-off Disclosed by This Review

### AT-17 — No Dependency on Materials for Material-Reference Validation

**What.** `VerificationContext.ReferenceMaterial` accepts any string,
unverified against `Tempest.Core.Materials` — identical to
`Calculations.CalculationContext.ReferenceMaterial`'s own precedent
(`AT-16`).

**Revisit trigger.** A real, demonstrated need for framework-internal
reference validation.

## Future Capability Register Entry Raised

`FCR-0036` (Transactional Multi-Document Operations for the Engineering
Data Model) — raised directly from `TD-23`, above; see `WP7.1E Future
Capability Recommendations.md`.

## Verdict

**No Release Blocking finding.** Two new, disclosed Technical Debt items
(`TD-23`, `TD-24`) and one new, disclosed Accepted Trade-off (`AT-17`),
all proportionate to a framework whose own registering callers remain
first-party, trusted, in-process code. No speculative security feature
was implemented.

## Related Documents

`WP7.1E Implementation Report.md`; `ADR-0057`; `docs/governance/Quality/
Technical Debt Register.md` (`TD-23`, `TD-24`, `AT-17`);
`docs/governance/Future Capability Register.md` (`FCR-0036`).
