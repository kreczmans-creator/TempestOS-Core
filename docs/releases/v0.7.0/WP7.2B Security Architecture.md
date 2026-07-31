# WP 7.2B — Security Architecture

## Status

Architecture only. **This document captures architectural security
requirements; it does not design any security mechanism**, per this
Work Package's own explicit instruction.

## Purpose

Reviews the nine security dimensions this Work Package's own controlling
instruction names for the Requirements & Verification Platform, classifying
each as **Implemented** (inherited, working today), **Future Capability**
(a real, named gap requiring future design), **Technical Debt** (a real,
disclosed limitation, not urgent), or **Not Applicable** — mirroring the
same four-way discipline `WP7.1D`/`WP7.1E`'s own Security Reviews
established for the Engineering Core.

## Classification

| Dimension | Classification | Rationale |
|---|---|---|
| **Requirement integrity** | **Implemented (inherited)** | A requirement's own revision history is immutable once written — inherited directly from `IDocumentRevision`'s own existing guarantee (Principle 4). No requirement-specific integrity mechanism beyond this is architecturally required; none is designed here. |
| **Authorisation** | **Implemented (inherited)** | `IPermissionEvaluator`, composed at the calling layer, exactly as every Engineering Core sibling already does. No new authorization mechanism is required or proposed. |
| **Auditability** | **Implemented (inherited)** | `IAuditRecorder`, composed at the calling layer, exactly as every existing sample module already demonstrates. |
| **Traceability integrity** | **Implemented (inherited), with a disclosed limitation** | `LinkAsync`'s own append-only design (no "unlink" operation exists in the approved contract) means a trace link, once created, cannot be silently altered or removed — a real, structural integrity guarantee. **Disclosed limitation, not a defect:** no framework-level check prevents a caller from recording a contradictory or duplicate trace link (e.g., two mutually exclusive `"derivesFrom"` relationships) — this is the identical class of gap `TD-18` already discloses for `LinkAsync` generally, not a new one this Platform introduces. |
| **Concurrent editing** | **Technical Debt (new, disclosed here)** | `ReviseAsync`'s own per-document lock (Principle 6) guarantees no two concurrent revisions of the *same* document ever collide on revision number — but it provides no compare-and-swap or "expected prior revision" check. Two authors editing the same requirement concurrently could each successfully call `ReviseAsync`; the second call's own content silently becomes current, with no conflict signalled to either author. This is a genuine, real gap this architecture discloses rather than papers over — recommended as a new Technical Debt Register entry once implementation begins (`WP7.2B Required ADR Catalogue.md`). |
| **Tamper resistance** | **Technical Debt (inherited, platform-wide)** | No cryptographic signing of any stored document exists anywhere in this platform (mirrors `TD-16`'s identical disclosure for license files). Not a Requirements-specific gap — the same, already-accepted, disclosed platform-wide trust posture. |
| **Identity ownership** | **Implemented (inherited)** | Authorship attribution (`CreatedByPrincipalId`/equivalent) is inherited directly from `EngineeringDocumentStore`'s own existing pattern, proven by `MaterialCatalog`/`CalculationEngine`/`VerificationService` alike. |
| **Future electronic approval** | **Future Capability** | No mechanism for multi-party sign-off or approval workflow exists anywhere in this platform today. A genuine, real future need for regulated engineering practice (`WP7.2B Standards Mapping.md`), not designed here — no approval mechanism is proposed by this architecture. |
| **Future digital signatures** | **Future Capability** | No cryptographic signature mechanism exists anywhere in this platform (mirrors `FCR-0017`'s identical, still-unresolved future capability for license files). A Requirements-specific signature need would be a specific instance of this same, already-identified, still-unbuilt platform-wide gap — not a new one. |

## Summary

**Zero Release Blocking findings — this is an architecture review, not
an implementation, so no finding can be Release Blocking in the sense
either Engineering Foundation Security Review used.** Two genuinely new
disclosures: **Concurrent editing** (a real Technical Debt item, to be
formally registered once implementation begins) and the **Traceability
integrity** limitation (a narrower, already-partially-disclosed
extension of `TD-18`). Every other dimension is either fully inherited
from already-proven Engineering Core/Platform Core mechanisms, or is a
genuine Future Capability this architecture explicitly declines to
design ahead of real need, consistent with Security Principle 7.

## What This Document Deliberately Does Not Do

- **Does not design a concurrency-conflict-detection mechanism** for
  `ReviseAsync` — the gap is disclosed; resolving it (an
  optimistic-concurrency check, a locking UI convention, or an accepted
  "last write wins" policy formally documented as a trade-off) is the
  owning implementation Work Package's own decision.
- **Does not design an approval workflow or a digital-signature
  mechanism** — both are named as real Future Capabilities, consistent
  with `Security Principles.md` Principle 7's own "do not build ahead of
  demonstrated need."
- **Does not propose a threat model addendum's own content** — that
  remains `WP7.2B Required ADR Catalogue.md`'s own recommended future
  action, consistent with `WP7.2A Security Assessment.md`'s own earlier
  recommendation for this exact Work Package.

## Related Documents

`docs/security/Threat Model.md`; `docs/security/Security Principles.md`
(Principle 7); `docs/governance/Quality/Technical Debt Register.md`
(`TD-16`, `TD-18`); `docs/governance/Future Capability Register.md`
(`FCR-0017`); `WP7.1D Security Review Report.md`; `WP7.1E Security
Review Report.md`; `WP7.2A Security Assessment.md`; `WP7.2B Required ADR
Catalogue.md`.
