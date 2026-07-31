# WP 7.2A — Capability Dependency Report

## Purpose

For each candidate programme, identifies prerequisite capabilities
(what it needs to already exist), enabling capabilities (what it makes
possible for the first time), downstream consumers (what would plausibly
build on it next), and reusable frameworks (what it would consume
directly) — then demonstrates, with evidence, which programme unlocks
the greatest number of future capabilities.

## Programme A — Requirements & Verification Platform

| Dimension | Finding |
|---|---|
| **Prerequisite capabilities** | `FCR-0029` (Engineering Data Model) — Implemented. `FCR-0033` (Verification Framework) — Implemented. Both certified, both satisfied; no outstanding technical dependency. |
| **Enabling capabilities (new, this programme creates)** | A classified (`ADR-0013`), architected Requirements Engine — the first real Systems Engineering capability of any kind; the first genuine consumer of `IVerificationService` beyond a sample module; the first Academy content for the Systems Engineering category. |
| **Downstream consumers** | Every future discipline module (Mechanical, Structural, Electrical, HVAC, Manufacturing), once identified, would plausibly want to trace its own domain artefacts back to a requirement and forward to a verification record — the same generic `IEngineeringDocument` reference pattern `Verification` and `Materials` already use. `FCR-0028` (Project Engine) would also plausibly reference requirements for programme-level tracking. |
| **Reusable frameworks consumed** | `Tempest.Core.EngineeringData` (document identity/revisioning), `Tempest.Core.Verification` (verification recording), `Tempest.Core.Identity` (attribution), `Tempest.Core.Audit` (action recording, composed at the calling layer per `WP7.0C Cross-Framework Dependency Report.md`'s own Reuse Opportunities finding). |
| **Reusable frameworks NOT consumed, and why** | `Tempest.Core.Calculations` — a requirement is not a calculation; no dependency expected unless a specific requirement type demands a computed value. `Tempest.Core.Materials` — no material relationship inherent to requirements management. |

## Programme F — Platform Hardening

| Dimension | Finding |
|---|---|
| **Prerequisite capabilities** | None — `IPermissionEvaluator` (`ADR-0044`) already exists; this programme applies it, it does not build a new mechanism. |
| **Enabling capabilities (new, this programme creates)** | Closes `FCR-0001` (plugin/registration trust isolation retrofit), `FCR-0003`/`FCR-0004` (REST API authentication and TLS), and — if scoped alongside — `FCR-0005` (governance register health-check tooling). Directly unlocks `FCR-0002` (Third-Party Plugin Ecosystem Enablement), which is explicitly gated on `FCR-0001` landing first or alongside. |
| **Downstream consumers** | A real third-party plugin author (once one exists — `AT-06` confirms none does today); any future network-facing deployment scenario beyond a trusted local network; `FCR-0021` (Multi-User/Tenant Isolation), which would benefit from, but does not strictly require, this programme first. |
| **Reusable frameworks consumed** | `Tempest.Core.Identity` (`IPermissionEvaluator`, already built); `Tempest.Core.Api` (`RestApiHostedService`, already built). No Engineering Core framework is consumed at all. |
| **Reusable frameworks NOT consumed, and why** | The entire Engineering Core — this programme is orthogonal to engineering-domain capability by definition; it hardens Platform Core surfaces the Engineering Core does not touch. |

## Programme G — AI & Engineering Intelligence

| Dimension | Finding |
|---|---|
| **Prerequisite capabilities** | `ICommandRegistry`/`ICommandDispatcher` (`WP 5.1A`/`WP 5.1B`) — already Implemented and already sufficient, per `FCR-0024`'s own description. |
| **Enabling capabilities** | **None identified.** `FCR-0024`'s own register entry states the framework "already supports this caller shape" — there is no capability this programme would newly create; a concrete AI/automation consumer would simply use what already exists. |
| **Downstream consumers** | Unknown — no concrete AI/automation consumer has been proposed by any Work Package to date. |
| **Reusable frameworks consumed** | `Tempest.Core.Commands` only, and only by an as-yet-hypothetical external caller, not by any TempestOS-authored code this programme would produce. |

## Programmes B, C, D, E — Mechanical, Building Services/HVAC, Structural, Electrical

| Dimension | Finding |
|---|---|
| **Prerequisite capabilities** | None specifically named — each would plausibly draw on `Tempest.Core.UnitsAndQuantities` (dimensioned quantities), `Tempest.Core.Materials` (material properties), and `Tempest.Core.Calculations` (formula execution), all Implemented, but no document in this repository specifies *which* calculation, *which* material property, or *which* requirement any of the four would actually need. |
| **Enabling capabilities** | **Cannot be stated without inventing them.** `WP7.0B Engineering Discipline Assessment.md`'s own finding stands unchanged: zero capabilities identified for any of the four. |
| **Downstream consumers** | Unknown — cannot be named without first inventing the capability itself. |
| **Reusable frameworks consumed** | Plausible (Units & Quantities, Materials, Calculation) but entirely speculative absent a real, named requirement. |

## Which Programme Unlocks the Greatest Number of Future Capabilities

**Programme A, by a clear margin, when "unlock" is measured by genuine
enabling relationships rather than raw counted `FCR` entries it directly
closes.**

- Programme A closes **zero** existing `FCR` entries directly (`FCR-0027`
  itself has no code yet to "close" — this programme *is* `FCR-0027`'s
  first real design work) but **enables every one of the five currently-
  unidentified Engineering Discipline categories** to eventually plug
  into a shared traceability mechanism, once each is identified — the
  same enabling relationship `Verification`'s own design already proved
  generalises (Materials, Calculation, and Verification all reused the
  Engineering Data Model rather than inventing their own reference
  model). This is the qualitative sense in which a foundation unlocks
  capability: not by adding rows to a register, but by giving every
  future row a mechanism to plug into.
- Programme F closes **exactly two** named `FCR` entries (`FCR-0001`,
  bundled with `FCR-0003`/`FCR-0004`) and directly unlocks **one**
  further entry (`FCR-0002`) that is explicitly gated on it. A real,
  concrete unlock — but narrower in scope than Programme A's own
  foundation-wide relationship to every future discipline.
- Programme G and Programmes B–E unlock **nothing new**: G's own
  capability already exists structurally: nothing is gained by building
  it now that isn't already available; B–E cannot demonstrate an unlock
  because no specific capability exists within any of them yet to be
  unlocked in the first place.

## Related Documents

`WP7.2A Strategic Roadmap Review.md`; `WP7.2A Programme Comparison
Matrix.md`; `WP7.2A Recommended Programme.md`; `docs/governance/Future
Capability Register.md`; `WP7.0C Cross-Framework Dependency Report.md`;
`WP7.1E Future Capability Recommendations.md`.
