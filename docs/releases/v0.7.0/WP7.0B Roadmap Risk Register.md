# WP 7.0B — Roadmap Risk Register

## Status

Complete. Distinct from `docs/releases/v0.6.0/Risk Register.md` (a
release-scoped register, closed with that release) — this register
tracks forward-looking roadmap risk, permanent until each item is
retired by a future Work Package's own evidence.

## Roadmap Risks

| # | Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|---|
| RR-1 | The Engineering Foundation Programme (`FCR-0029`–`FCR-0033`) was designed from only two disciplines' own aspirational descriptions (Systems Engineering, Project Management) and `Capability Categories.md`'s generic category definitions — not validated against a real Mechanical, Structural, Electrical, HVAC, or Manufacturing requirement, since none exists yet. It may not fit those disciplines' real needs once identified. | Medium | Medium-High — could require redesigning the foundation after the fact | Treat the Engineering Foundation Programme as provisional until validated against a second real discipline beyond Systems Engineering/Project Management; do not consider `FCR-0029`/`FCR-0030`/`FCR-0032` stable until then. |
| RR-2 | Ten candidate Work Packages (`A`–`J`) now exist across two Work Packages' own recommendations, inviting the same scope growth `v0.6.0` experienced (nine Work Packages against an original smaller plan). | Medium | Medium | Architecture/Planning for `v0.7.0` should explicitly decide real scope, not assume all ten candidates proceed — `Product Roadmap.md`'s own Non-Commitments already establish this discipline; this risk names the specific temptation. |
| RR-3 | Five Engineering Discipline categories have no sequencing basis (see `WP7.0B Engineering Discipline Assessment.md`); under schedule or business pressure, one could be picked without real justification, repeating a documented anti-pattern this project has previously named for security work (`Security Principles.md` Principle 7, applied here to product sequencing). | Low-Medium | Medium | Explicit rule, stated in this document and in the Discipline Assessment: no discipline among the five is sequenced ahead of another without a real, named stakeholder or customer scenario — not an internal preference. |

## Architectural Risks

| # | Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|---|
| AR-1 | `FCR-0029` (Engineering Data Model), if designed too narrowly around Systems Engineering/Project Management's own needs, could prove the wrong shape once a real Mechanical/Structural/Electrical capability is identified — an expensive-to-reverse architectural commitment made early. | Medium | High | Candidate D's own architecture phase should explicitly design for extensibility across disciplines, not just the two disciplines currently named, and should be validated against at least a hypothetical second discipline before being considered final. |
| AR-2 | `FCR-0021` (Multi-User/Tenant Isolation) requires a DI container redesign (no Scoped lifetime exists today) — a high-blast-radius change if deferred until forced by a crisis rather than considered proactively once Candidates I/J (Systems Engineering/Project Management architecture) begin, since either could plausibly be the first multi-user-adjacent scenario. | Low (currently gated on real need, correctly) | High if triggered under pressure | Candidates I and J should each explicitly note, in their own architecture phase, whether they anticipate needing `FCR-0021` — surfacing the question early even though the answer may still be "not yet," per Security Principle 7. |
| AR-3 | Candidate H (Verification & Validation) depends on Candidate I (Requirements Engine), itself the least architecturally grounded candidate in this catalogue (mirroring `WP 6.1`'s own disclosed risk for Identity) — building Verification on an unstable Requirements foundation could force rework. | Medium | Medium | Sequence H strictly after I's own architecture phase stabilises, per `WP7.0B Candidate Work Package Catalogue.md`'s own dependency table — never in parallel. |

## Commercial Risks

| # | Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|---|
| CR-1 | `FCR-0026` (Defence-Sector/Regulated-Environment Compliance) is marked **Inferred**, not Verified — no confirmed real customer or opportunity exists. Over-investing in defence-specific compliance design without one would be speculative engineering effort with no confirmed return. | Low | Medium-High if triggered prematurely | Per Security Principle 7 and this capability's own register entry, no design work begins on `FCR-0026` until a real, named defence-sector opportunity is confirmed by Product — not inferred from dormant code alone. |
| CR-2 | `FCR-0025` (Commercial Licensing Model) could be engineered without a named pricing/packaging strategy, producing a mechanism that does not match the actual go-to-market plan once one exists. | Low (currently gated on real need) | Medium | Require explicit Product/Commercial input before Candidate scoping begins for `FCR-0025` — this is a commercial-policy decision, not an engineering-only one, per `VISION.md`'s own Product Principle 1 (capability before commercial policy). |

## Governance Risks

| # | Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|---|
| GR-1 | `Future Capability Register.md` has now been extended twice in two consecutive Work Packages (`WP 7.0A`: 28 entries; `WP 7.0B`: +5). Without deliberate care, this register could itself become the next one to drift stale, exactly the pattern `FCR-0005` (Governance Register Health-Check Tooling) exists to prevent elsewhere. | Medium | Medium | `FCR-0005`'s own scope should explicitly extend to cover `Future Capability Register.md`, not only the pre-existing Engineering/Documentation/Quality registers — recorded here as a scope clarification for Candidate C. |
| GR-2 | Two consecutive Work Packages (`WP 7.0A`, `WP 7.0B`) have now produced architecture and planning documentation with zero implementation. A third consecutive planning-only Work Package would risk "planning drift" — extensive roadmap documentation with no shipped capability to show for it. | Medium | Medium-High (momentum/credibility risk, not technical) | Explicitly recommend that the Work Package immediately following this one's own Engineering Review be a real scoping-and-approval step that leads to implementation beginning, not a third planning-only pass — named directly in `WP7.0B Lessons Learned.md`. |
| GR-3 | Five of nine Engineering Discipline categories remain empty after two dedicated Work Packages' own review. Without an explicit trigger, "no capability identified" could become a permanent status quo rather than a genuinely open gap being actively pursued. | Medium | Medium | State explicitly (here, and in `PROJECT_STATUS.md`) that closing this gap requires real engineering-domain stakeholder engagement — an action item for Product, not something a future documentation-only Work Package can resolve by re-reading this repository again. |

## Related Documents

`docs/releases/v0.6.0/Risk Register.md` (the release-scoped precedent
this register's own format follows); `WP7.0B Engineering Foundation
Architecture.md`; `WP7.0B Engineering Discipline Assessment.md`;
`WP7.0B Candidate Work Package Catalogue.md`; `VISION.md`;
`docs/security/Security Principles.md`.
