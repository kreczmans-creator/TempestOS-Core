# WP 7.1F — Technical Debt Disposition

## Purpose

Classify every Engineering Core Technical Debt item and disclosed
trade-off in `docs/governance/Quality/Technical Debt Register.md` into
exactly one of four dispositions, mirroring `WP6.8 Technical Debt
Disposition.md`'s own role for `v0.6.0`: **Resolved** (fixed, no longer
applicable), **Accepted** (a deliberate, disclosed limitation this
programme ships with), **Deferred** (a real, open item, not urgent, with
a named revisit trigger), or **Release Blocking** (must be fixed before
the Engineering Core can certify).

## Engineering Core Technical Debt (`TD-17`–`TD-24`)

| # | Item | Disposition | Rationale |
|---|---|---|---|
| TD-17 | `IDocumentRevision.Content` is a plain, opaque `string` — no structured/typed payload support | **Accepted** | An approved-contract scope decision (`WP7.0C Engineering Foundation Contracts.md`'s own disclosed Extension Point), not an oversight. No concrete consumer with a structured-content requirement exists yet. Revisit trigger: a real, demonstrated need. |
| TD-18 | `LinkAsync`'s own concurrency behaviour under many simultaneous calls against the same source document is not tested at the same depth as `ReviseAsync`'s own atomicity | **Deferred** | No atomicity concern is believed to exist (each call writes an independent, randomly-keyed entry), but this Work Package's own Security Review Summary confirms `LinkAsync` is now load-bearing for four consumers (`Materials`, `Calculations`, `Verification`, and `Verification`'s own multi-link `RecordAsync` sequence most heavily) — real, not urgent, revisit alongside `FCR-0036`. |
| TD-19 | No affine/offset unit conversion — Temperature deliberately deferred | **Accepted** | An approved-contract-compatible scope decision (`ADR-0054`), not a defect. Revisit trigger: a real discipline module naming a Temperature requirement — none exists yet. |
| TD-20 | `MaterialCatalog` reads a material's own full revision history to reconstruct current state | **Deferred** | No measured performance problem exists at current scale; mirrors `TD-12`'s own identical "no measured problem yet" discipline. |
| TD-21 | No cancellation reaches into `Calculate` once execution has started | **Deferred** | Calculation definitions remain trusted, first-party, in-process code — the same trust boundary the Command Framework already operates under without cancellation reaching a handler either. Revisit trigger: a real, demonstrated need (`FCR-0035`). |
| TD-22 | `CalculationContext` imposes no bound on recorded data volume or type fidelity | **Deferred** | No current consumer needs either capability; disclosed, not urgent. |
| TD-23 | `RecordAsync`'s own multi-link sequence is not transactional | **Deferred** | `IEngineeringDocumentStore` itself offers no transactional multi-write primitive to build on; no real, demonstrated failure from the non-transactional sequence has occurred. Revisit trigger: `FCR-0036`. |
| TD-24 | `VerificationContext` imposes no bound on recorded data volume; `GetVerificationHistoryAsync` scales with total reference count | **Deferred** | No current consumer needs bounded recording; mirrors `TD-12`/`TD-20`/`TD-22`'s own identical disclosure discipline. |

**Disposition summary (Engineering Core items only): 0 Resolved, 2
Accepted, 6 Deferred, 0 Release Blocking.**

## Engineering Core Disclosed, Accepted Trade-offs (`AT-14`–`AT-17`)

All four are Accepted by construction, per the Technical Debt Register's
own Governing Distinction — none requires action absent a real,
demonstrated need:

| # | Item | Disposition |
|---|---|---|
| AT-14 | Compile-time dimension-safety guarantee verified by direct inspection, not an automated compiler-error test | Accepted |
| AT-15 | `IMaterialCatalog` performs no permission-gating of its own — calling-layer enforcement expected | Accepted |
| AT-16 | `CalculationContext.ReferenceMaterial` accepts any string, unvalidated against `Tempest.Core.Materials` | Accepted |
| AT-17 | `VerificationContext.ReferenceMaterial` accepts any string, unvalidated against `Tempest.Core.Materials` | Accepted |

**Disposition summary: 0 Resolved, 4 Accepted, 0 Deferred, 0 Release
Blocking.**

## Release-Blocking Assessment

**Zero items across either table are classified Release Blocking.**
Every open (Deferred or Accepted) item was disclosed at the time its
owning Work Package shipped, approved by the same governance process
that approved that Work Package's own scope, and carries a named,
concrete revisit trigger. None represents an unannounced defect, a
silently-abandoned requirement, or a correctness gap in any of the five
approved Engineering Foundation contracts. `TD-18`'s own upgraded
disposition (Deferred, not simply "still Open" as previously stated) is
the one re-assessment this Work Package's own cross-framework review
produced — reflecting genuinely increased relevance (four real
consumers now depend on `LinkAsync`, not zero), not a newly-discovered
defect.

## Comparison to the Rest of the Technical Debt Register

The Engineering Core's own eight tracked debt items and four trade-offs
sit alongside 16 pre-existing `v0.6.0`-and-earlier debt items and 13
pre-existing trade-offs (`Technical Debt Register.md`'s own full total:
24 tracked items, 17 disclosed trade-offs). None of the Engineering
Core's own items worsens, resolves, or otherwise touches any pre-existing
item — confirmed directly: no Engineering Foundation Work Package's own
Technical Debt Assessment references `TD-01` through `TD-16`, and no
`v0.6.0`-era retrospective references `TD-17` through `TD-24`.

## Verdict

**The Engineering Core carries zero Release Blocking technical debt.**
Every disclosed item is either a deliberate, approved-contract-compatible
scope decision (Accepted) or a real but non-urgent limitation with a
named revisit trigger (Deferred). The Engineering Core's own debt
profile — eight items across five frameworks, two of which (`TD-21`
through `TD-24`) were found by dedicated Security Reviews rather than
discovered as defects — is proportionate to a foundation layer whose
callers remain trusted, first-party, in-process code throughout.

## Related Documents

`docs/governance/Quality/Technical Debt Register.md` (the complete,
authoritative source this disposition classifies); `WP7.1F Security
Review Summary.md`; `WP7.1F Future Capability Register Review.md`;
`WP7.1F Engineering Core Certification Report.md`.
