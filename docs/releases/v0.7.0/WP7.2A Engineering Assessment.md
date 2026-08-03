# WP 7.2A — Engineering Assessment

## Purpose

Assesses each candidate programme's own engineering value, architectural
leverage, and technical readiness — informed directly by the Engineering
Core Certification, the Platform Core Certification, and both
frameworks' own architecture documentation — and carries forward the
architectural risks bearing on this Work Package's own recommendation,
per its Risk Assessment requirement.

## Engineering Readiness Per Programme

### Programme A — Requirements & Verification Platform

**Technically ready.** Both of its prerequisites — `Tempest.Core.
EngineeringData` (`FCR-0029`) and `Tempest.Core.Verification`
(`FCR-0033`) — are Implemented and certified
(`WP7.1F Engineering Core Certification Report.md`). No further
Engineering Foundation work is required before this programme's own
architecture phase can begin. `WP7.0C Cross-Framework Dependency
Report.md`'s own structural design decision — `Verification` depends on
`EngineeringData`'s generic document concept, never on a concrete
Requirements type — was made specifically to avoid a circular dependency
once a Requirements Engine exists; this assessment confirms that decision
remains sound and unmodified through five further Work Packages of
implementation and one certification review.

**Architectural leverage:** highest of any candidate. A Requirements
Engine would be the first framework to consume `IVerificationService`
as a real, non-sample-module client — the exact relationship `WP7.1E
Future Capability Recommendations.md` Recommendation 1 anticipated
directly. It would also be the first Engineering Foundation-adjacent
capability requiring its own `ADR-0013` classification decision (module
vs. platform service) — a genuinely new kind of architectural decision,
not a repetition of one already made five times.

### Programme F — Platform Hardening

**Technically ready**, and the lowest-risk option evaluated —
`IPermissionEvaluator` (`ADR-0044`) already exists; every one of its
three components applies an existing mechanism rather than designing a
new one. **Architectural leverage:** high within the Platform Core, zero
within the Engineering Core — this programme does not touch, extend, or
depend on any Engineering Foundation framework.

### Programme G — AI & Engineering Intelligence

**Not technically actionable in its current state.** `ICommandRegistry`/
`ICommandDispatcher` already support the caller shape `FCR-0024`
describes; there is no missing infrastructure to design. This programme
cannot be given an engineering-readiness assessment beyond "the
prerequisite already exists," because no further engineering work has
been identified within it.

### Programmes B, C, D, E — Mechanical, Building Services/HVAC, Structural, Electrical

**Not technically scopeable.** Each would plausibly draw on
`Tempest.Core.UnitsAndQuantities`, `Tempest.Core.Materials`, and
`Tempest.Core.Calculations` — all Implemented and certified — but
readiness of a *foundation* is not the same as readiness of a
*capability built on it*. No document names what any of the four would
actually compute, specify, or verify. An engineering-value assessment
requires a defined capability to assess; none exists.

## Architectural Leverage Comparison

Re-derived directly against the real Engineering Core dependency graph
(`WP7.1F Engineering Core Architecture Conformance Report.md`):

| Programme | Consumes Engineering Data Model | Consumes Units & Quantities | Consumes Materials | Consumes Calculation | Consumes Verification |
|---|---|---|---|---|---|
| A | Yes (documents) | No | No | No | Yes (direct) |
| F | No | No | No | No | No |
| G | No | No | No | No | No |
| B/C/D/E (plausible, unconfirmed) | Plausible | Plausible | Plausible | Plausible | Unclear |

**Programme A is the only candidate with a confirmed, non-speculative
consumption relationship to any Engineering Core framework** — its
consumption of `Tempest.Core.Verification` is not merely plausible, it
is the literal relationship `WP7.1E Future Capability Recommendations.md`
already named as the framework's own intended first real consumer.
Programmes B–E's own plausible consumption is exactly that — plausible,
not confirmed, since no defined capability exists to confirm it against.

## Architectural Risks and Mitigations

Per this Work Package's own Risk Assessment requirement:

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| The Engineering Foundation was validated against only two disciplines' own aspirational descriptions (Systems Engineering, Project Management) — `WP7.0B Roadmap Risk Register.md`'s own `RR-1`/`AR-1` — and Programme A is itself one of those two, meaning its own foundation may still prove the wrong shape once a real discipline requirement is identified | Medium | Medium-High | Treat the Engineering Foundation as provisional until validated against a second, real discipline — unchanged guidance from `RR-1`; Programme A's own architecture phase (Candidate K) should explicitly note this residual risk, not treat the foundation as beyond question. |
| `ADR-0013`'s own classification question (platform service vs. module) has never actually been decided for a real capability — `FCR-0027` and `FCR-0028` are both explicitly unclassified today | Medium | Medium | Candidate K's own expected output explicitly requires this decision as its first architectural act, per `VISION.md`'s own "Definition of Platform vs. Engineering Modules" section — not deferred to implementation. |
| A Requirements Engine could be designed to depend on a future, not-yet-built Requirements-specific type inside `Tempest.Core.Verification`, reintroducing exactly the circular-dependency risk `WP7.0C Cross-Framework Dependency Report.md` deliberately avoided | Low | High if it occurred | Explicitly re-confirmed in this assessment: `Verification`'s own design already depends only on `EngineeringData`'s generic document concept — Candidate K's own architecture phase should preserve this, not weaken it by requesting a concrete `Verification`-side Requirements type. |

## Long-Term Maintainability Comparison

Programme A scores highest (`WP7.2A Programme Comparison Matrix.md`,
4/5) because it would reuse three already-proven, certified mechanisms
(document identity/revisioning, verification recording, calling-layer
permission/audit composition) rather than introducing new
infrastructure — the same "reuse over reinvention" discipline
`ENGINEERING_CORE_COMPLETION_REPORT.md` names as the Engineering
Foundation's own central architectural lesson. Programmes B–E score
lowest specifically because a module built on invented, unvalidated
requirements carries a real risk of needing substantial rework once real
requirements are identified — the same risk `RR-1` already names for the
foundation itself, compounded rather than resolved by building further
on top of it without validation.

## Verdict

Programme A is both the most technically ready and the most
architecturally leveraged candidate evaluated — its only two
prerequisites are complete and certified, its consumption relationship
to the Engineering Core is confirmed rather than speculative, and its
residual risk (foundation validated against only two disciplines) is
already disclosed and actively tracked, not a new finding this
assessment introduces. This supports `WP7.2A Recommended Programme.md`'s
own conclusion directly.

## Related Documents

`WP7.1F Engineering Core Certification Report.md`; `WP7.1F Engineering
Core Architecture Conformance Report.md`; `WP7.0C Cross-Framework
Dependency Report.md`; `WP7.0B Roadmap Risk Register.md`; `WP7.1E
Future Capability Recommendations.md`; `VISION.md`; `ADR-0013`;
`WP7.2A Recommended Programme.md`.
