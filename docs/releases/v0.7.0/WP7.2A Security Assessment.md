# WP 7.2A — Security Assessment

## Purpose

Assesses each of the seven candidate programmes from a security
perspective — likely trust boundaries, likely threat surfaces, expected
security complexity, and likely future hardening work — informed
directly by `Threat Model.md`, `Security Roadmap.md`, and both dedicated
Engineering Foundation Security Reviews. Also carries forward this Work
Package's own architectural and governance risk findings that bear on
security posture specifically, per this Work Package's own controlling
instruction.

## Per-Programme Security Assessment

### Programme A — Requirements & Verification Platform

| Dimension | Finding |
|---|---|
| **Likely trust boundaries** | None new. A Requirements Engine, built the same way every Engineering Foundation framework was built, remains trusted, first-party, in-process code — the identical trust boundary `Threat Model.md`'s own "Trust Boundaries" section already names as this platform's only real internal boundary (Discovery/Registration/Lifecycle machinery, unreachable from any module). |
| **Likely threat surfaces** | A new **asset class**, not a new attack surface: requirement and traceability data. `Threat Model.md` assumption 1 already names "requirements" directly as engineering IP this platform will eventually manage — this programme is the first to actually store it. No new network-facing surface, no new plugin surface. |
| **Expected security complexity** | Low-Medium. No new authentication, authorization mechanism, or trust boundary is required — `IPermissionEvaluator` and `IAuditRecorder`, already built, are expected to be composed at the calling layer exactly as every `v0.6.0` sample module already demonstrates. |
| **Likely future hardening work** | If `FCR-0026` (Defence-Sector/Regulated-Environment Compliance) ever becomes a real, named opportunity, a Requirements Engine handling classified or export-controlled requirements would be a direct consumer of that future hardening — disclosed as a plausible future relationship, not a current requirement. |

### Programme F — Platform Hardening

| Dimension | Finding |
|---|---|
| **Likely trust boundaries** | **Directly changes an existing one.** This is the only programme whose entire purpose is altering a trust boundary — closing the gap between "a loaded plugin" and "a first-party module" (`TD-09`), and between an unauthenticated REST caller and an authenticated one (`TD-13`). |
| **Likely threat surfaces** | Reduces existing surfaces rather than adding new ones — the plugin-loading surface (`Threat Scenario 1`, `Threat Model.md`) and the REST API's own unauthenticated-caller surface (`TD-13`) both shrink. |
| **Expected security complexity** | **Highest of any candidate programme.** `Security Roadmap.md` item 1 itself calls for "a dedicated Architecture Work Package" to evaluate a genuine isolation model (a separate `AssemblyLoadContext`, a manifest-declared capability scope, code-signing) — real, non-trivial security engineering, not a routine application of an existing mechanism for two of its three components (`FCR-0001`'s own plugin isolation decision; `FCR-0003`'s own authentication mechanism choice). |
| **Likely future hardening work** | This programme *is* the future hardening work every other programme's own security assessment defers to. Once complete, it directly unlocks `FCR-0002` (Third-Party Plugin Ecosystem) and materially de-risks `FCR-0021` (Multi-User/Tenant Isolation), should either become real. |

### Programme G — AI & Engineering Intelligence

| Dimension | Finding |
|---|---|
| **Likely trust boundaries** | Unknown — entirely dependent on what "an AI/automation caller" ends up meaning, which no document in this repository yet specifies. If such a caller runs in-process (mirroring `FCR-0024`'s own described shape, an ordinary `ICommandRegistry` consumer), no new trust boundary results. If it runs out-of-process or autonomously, it would introduce a genuinely new actor category `Threat Model.md`'s own "Actors" table does not yet name. |
| **Likely threat surfaces** | Unknown for the same reason — cannot be assessed without a concrete design, which this programme's own register entry confirms does not exist. |
| **Expected security complexity** | Unknown, potentially significant if an autonomous or semi-autonomous caller is ever proposed — recommend this be threat-modelled on its own terms the moment a concrete consumer is named, per `Threat Model.md`'s own "What This Model Deliberately Does Not Cover" discipline (do not threat-model a capability that does not exist yet in any form). |
| **Likely future hardening work** | Cannot be named without a concrete design — disclosed honestly as unknown, not speculated. |

### Programmes B, C, D, E — Mechanical, Building Services/HVAC, Structural, Electrical

| Dimension | Finding |
|---|---|
| **Likely trust boundaries** | None anticipated beyond what the Engineering Core already establishes — each would plausibly be trusted, first-party, in-process code, identical to every framework certified so far. |
| **Likely threat surfaces** | Cannot be assessed meaningfully — no defined capability exists within any of the four to threat-model against. Any statement here would be speculation, which `Threat Model.md`'s own governing discipline explicitly declines to produce. |
| **Expected security complexity** | Presumed Low, by analogy to the already-certified Engineering Core frameworks (Materials, Calculation), but this is an analogy, not a finding — no real design exists to verify it against. |
| **Likely future hardening work** | Cannot be named. |

## Cross-Programme Security Observations

**Programmes A and Programmes B–E share an identical trust-boundary
profile** — none introduces a new one. This is a direct, structural
consequence of `ADR-0023`'s own four-layer model: every Engineering
Module, regardless of discipline, runs inside the same Runtime Host
every Platform Service already does (`VISION.md`'s own Architectural
Philosophy section). **Programme F is the singular exception** — it is
the only candidate whose entire purpose is altering an existing trust
boundary, and correspondingly carries the highest security engineering
complexity of any candidate, a genuine cost this assessment does not
minimise even though it recommends sequencing that cost second, not
first (`WP7.2A Recommended Programme.md`).

## Risk: Architectural and Governance Risks Bearing on Security Posture

Per this Work Package's own Risk Assessment requirement, two risks
specifically security-adjacent are carried forward from
`WP7.2A Strategic Roadmap Review.md`:

- **`VISION.md`'s own "readiness" objective is not fully met by this
  recommendation.** Proceeding with Programme A before Programme F
  means `FCR-0001`/`FCR-0003`/`FCR-0004` remain open through at least
  one further release. **Mitigation:** each item's own named trigger
  (a real third-party plugin; a concrete networked deployment scenario)
  is monitored, not ignored — `WP7.2A Recommended Programme.md` commits
  to bringing Programme F forward immediately if either trigger fires
  before `v0.9.0`'s own currently-recommended sequencing.
- **A Requirements Engine handling classified/export-controlled data
  (per `Threat Model.md` assumption 1, `FCR-0026`) without a dedicated
  threat-model addendum would repeat `FS-1`'s own disclosed pattern**
  (the dormant `ProjectModel`'s own unencrypted classification fields).
  **Mitigation:** `WP7.2A Candidate Work Package Catalogue.md`'s own
  Candidate K explicitly recommends a threat-model addendum as part of
  the Requirements Engine's own architecture phase, before
  implementation, not after.

## Verdict

No candidate programme carries a Release-Blocking security concern.
Programme F carries the highest security engineering complexity but the
most directly positive security impact; Programme A carries the lowest
incremental security complexity of any programme with real engineering
value, introducing a new asset class but no new trust boundary. This
assessment supports `WP7.2A Recommended Programme.md`'s own conclusion
without finding any security-specific reason to override it.

## Related Documents

`docs/security/Threat Model.md`; `docs/security/Security Roadmap.md`;
`docs/security/Security Principles.md`; `WP7.1D Security Review
Report.md`; `WP7.1E Security Review Report.md`; `WP7.1F Security Review
Summary.md`; `WP7.2A Recommended Programme.md`; `WP7.2A Candidate Work
Package Catalogue.md`.
