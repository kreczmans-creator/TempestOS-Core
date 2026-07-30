# WP 7.1F — Future Capability Register Review

## Purpose

Confirm `FCR-0029`–`FCR-0033` (the five Engineering Foundation
frameworks) are Implemented, review the three capabilities found during
implementation and Security Review (`FCR-0034`, `FCR-0035`, `FCR-0036`),
and determine whether each is Accepted (a real, scheduled priority),
Scheduled (named for a specific future release), or Deferred (real, not
yet scheduled) — per this Work Package's own controlling instruction.

## 1. Confirmation: `FCR-0029`–`FCR-0033` Are All Implemented

| FCR | Framework | Status | Verified By |
|---|---|---|---|
| FCR-0029 | Engineering Data Model | **Implemented** | `Tempest.Core.EngineeringData` exists, compiles, is registered in `TempestHost.cs`, has a real sample-module consumer, and is consumed by all four sibling frameworks (`WP7.1F Engineering Core Consumption Matrix.md`) |
| FCR-0030 | Units & Quantities | **Implemented** | `Tempest.Core.UnitsAndQuantities` exists, compiles, has zero Platform Service dependency and no DI registration by design, and is consumed directly by `Materials` |
| FCR-0031 | Materials | **Implemented** | `Tempest.Core.Materials` exists, compiles, is registered, has a real sample-module consumer |
| FCR-0032 | Calculation | **Implemented** | `Tempest.Core.Calculations` exists, compiles, is registered, has a real sample-module consumer, and was the first Engineering Foundation framework to receive a dedicated Security Review |
| FCR-0033 | Verification | **Implemented** | `Tempest.Core.Verification` exists, compiles, is registered, has a real sample-module consumer, and was the second Engineering Foundation framework to receive a dedicated Security Review |

**Confirmed: this completes the entire Engineering Foundation programme**
— every capability `WP 7.0B`'s own Capability Dependency Analysis
identified as architecturally necessary before any discipline-specific
Engineering Module can begin is now real, tested, shipped code, not a
proposed contract.

## 2. Review of `FCR-0034`, `FCR-0035`, `FCR-0036`

| FCR | Description | Disposition | Rationale |
|---|---|---|---|
| FCR-0034 | Affine Unit Conversion (Temperature and similar dimensions) | **Deferred** | Priority becomes High "the moment one [discipline module] does" need Temperature — none does yet. Not scheduled for any named release; correctly gated on a real discipline-module requirement, per Security Principle 7's own "do not build ahead of real need" discipline, applied here to product sequencing rather than security specifically. |
| FCR-0035 | Calculation Execution Timeout & Cancellation Support | **Deferred** | No current calculation definition is long-running; calculation definitions remain trusted, first-party, in-process code. Not scheduled; revisit trigger (a real, demonstrated need) has not occurred. |
| FCR-0036 | Transactional Multi-Document Operations for the Engineering Data Model | **Deferred** | `WP7.1E Future Capability Recommendations.md` itself recommends this be resolved only "against a real, demonstrated multi-consumer need, not Verification alone" — Verification remains the only real consumer of this shape today. Not scheduled. This Work Package's own Security Review Summary additionally recommends reassessing `TD-18` alongside this entry, since both concern `LinkAsync`'s own growing multi-consumer load — a reason to track the two together when `FCR-0036` is eventually scheduled, not a reason to schedule it now. |

**None of the three is Accepted (a confirmed, real, scheduled
priority) or Scheduled (named for a specific future release).** All
three remain genuinely gated on a real, demonstrated need that has not
yet materialised — consistent with every prior Engineering Foundation
Work Package's own identical judgement, independently re-confirmed here
rather than merely carried forward.

## 3. `FCR-0005` (Governance Register Health-Check Tooling) — Priority Raised

`FCR-0005` was raised by `WP 6.8` specifically to catch the class of
drift `WP7.1F Engineering Core Architecture Conformance Report.md` §7
found: `Interface Register.md`, `Dependency Injection Register.md`, and
`Module Register.md` going stale, undetected, across an entire release
phase. That exact pattern has now recurred once (across the five
Engineering Foundation Work Packages) since `FCR-0005` was identified,
and it was never built. **This Work Package raises `FCR-0005`'s own
priority from Medium to High** — it is no longer a single observed
instance plus a theoretical risk (`WP7.0B Roadmap Risk Register.md`'s own
`GR-1`), it is a confirmed, second, independent occurrence of the
identical failure mode, now closed manually a second time rather than
prevented. See `docs/governance/Future Capability Register.md`'s own
updated `FCR-0005` entry.

## 4. No New Future Capability Identified Beyond What `WP 7.1D`/`WP 7.1E` Already Raised

This Work Package's own cross-framework review (`WP7.1F Security Review
Summary.md`) found no Engineering-Core-wide security or architectural
gap that was not already captured by an existing `FCR`/`TD` entry. The
one cross-cutting observation it did produce — `TD-18`'s own growing
relevance — is folded into the existing `FCR-0036` entry's own future
disposition (§2, above), not registered as a new, separate capability.

## Coverage Note

**36 capabilities remain in the register** (`FCR-0001`–`FCR-0036`) — this
Work Package added none, since its own review produced no capability not
already tracked. `FCR-0029`–`FCR-0033` are now uniformly Implemented;
`FCR-0034`–`FCR-0036` remain uniformly Deferred, each with an unmet,
named, concrete revisit trigger — no entry was invented, deferred
without a stated reason, or silently dropped.

## Related Documents

`docs/governance/Future Capability Register.md`; `WP7.1F Security Review
Summary.md`; `WP7.1F Technical Debt Disposition.md`; `WP7.1F Engineering
Core Architecture Conformance Report.md`; `WP7.0B Roadmap Risk
Register.md` (`GR-1`); `WP7.1F Engineering Core Certification Report.md`.
