# WP 7.2A — Strategic Roadmap Review

## Purpose

Reviews every existing repository input bearing on "what should TempestOS
build next" — `Future Capability Register.md`, `Product Roadmap.md`,
the Engineering Core and Platform Core certifications, `Technical Debt
Register.md`, both dedicated Security Reviews, the Academy, architecture
documents, release retrospectives, and `VISION.md` — and states, with
evidence, what those inputs actually support and where they genuinely
disagree. This document is the evidentiary foundation `WP7.2A Recommended
Programme.md` draws its conclusion from; it does not itself recommend —
that is a separate deliverable, per this Work Package's own controlling
instruction.

## 1. Where TempestOS Stands Today

Both certifiable layers of the platform are now certified:

- **Platform Core (`v0.6.0`)** — `CERTIFIED WITH ACCEPTED TECHNICAL
  DEBT` (`WP6.8 Platform Certification Report.md`). Eleven Platform
  Services, zero Release Blocking findings, sixteen tracked debt items,
  thirteen disclosed trade-offs.
- **Engineering Core (`v0.7.0`, `WP 7.1A`–`WP 7.1F`)** —
  `ENGINEERING CORE CERTIFIED WITH ACCEPTED TECHNICAL DEBT`
  (`WP7.1F Engineering Core Certification Report.md`). Five frameworks,
  zero Release Blocking findings, eight tracked debt items, four
  disclosed trade-offs.

**Zero Engineering Modules exist.** Every capability shipped through
both certifications is infrastructure — `VISION.md`'s own words: "the
platform an engineering-domain product will eventually be built on, not
yet that product itself." This Work Package exists specifically because
that gap is now the single largest fact about where TempestOS stands.

## 2. What `Future Capability Register.md` Actually Supports

36 entries (`FCR-0001`–`FCR-0036`), reviewed in full against the seven
candidate programmes this Work Package must evaluate:

| Category | Entries | Status |
|---|---|---|
| Platform | `FCR-0001`–`FCR-0020` (19 of 20 platform-adjacent) | Mostly Identified, several disclosed as debt-driven, not strategic |
| Infrastructure | `FCR-0021`–`FCR-0023` | Identified, explicitly gated on real multi-user/cloud/mobile need not yet present |
| AI | `FCR-0024` | Identified — **the framework already supports this caller shape**; no design work is actually required |
| Commercial | `FCR-0025`, `FCR-0026` | Identified, explicitly gated on a real customer/opportunity not yet present |
| Engineering Foundation (cross-cutting) | `FCR-0029`–`FCR-0033` (Implemented), `FCR-0034`–`FCR-0036` (Deferred) | Complete — the Engineering Core just certified |
| Systems Engineering | `FCR-0027` (Requirements Engine) | Identified — aspirational, but the **only** discipline with a named platform-level hook (`ADR-0013`) and a completed foundation to build on |
| Project Management | `FCR-0028` (Project Engine) | Identified — aspirational; only trace is dormant, pre-Claude-era code; heavier, unresolved security-design burden (`Security Roadmap.md` item 4) |
| Mechanical / Structural / Electrical / Building Services-HVAC / Manufacturing | **Zero entries each** | Not identified — `WP7.0B Engineering Discipline Assessment.md` found this "cannot be sequenced from existing evidence," full stop |

**The register itself already answers most of this Work Package's own
question before any new analysis is performed**: five of the seven
candidate programmes this Work Package is asked to evaluate
(Mechanical, HVAC, Structural, Electrical, and — more weakly — the AI
programme) correspond to register categories the register's own
Coverage Note discloses as empty or already-solved. This is not this
Work Package's own new finding — it is `WP 7.0B`'s finding, re-confirmed
here by re-reading the same register a second time, six Work Packages
later, and finding it unchanged in this respect: no Work Package between
`WP 7.0B` and this one added a Mechanical, Structural, Electrical,
HVAC, or Manufacturing capability, because none was positioned to.

## 3. What `Product Roadmap.md` Actually Supports

`Product Roadmap.md` (`WP 7.0A`) named eight sequential phases. Phase 4
(Engineering Foundation) is now complete — but not in the shape that
document's own "working premise" anticipated. Re-reading it directly:

> "This phase's own working premise... close the platform-level gaps
> `Future Capability Register.md` identifies under the **Platform** and
> **Infrastructure** categories before building outward into Engineering
> Modules."

**This premise was not followed.** `WP 7.0B`'s own Architecture Review
(`WP7.0B Engineering Foundation Architecture.md`) chose instead to build
the five Engineering Foundation frameworks (`FCR-0029`–`FCR-0033`) —
Engineering Data Model, Units & Quantities, Materials, Calculation,
Verification — none of which are Platform or Infrastructure candidates
in `Product Roadmap.md`'s own sense. This is disclosed here, explicitly,
as a genuine, real tension this review found, not smoothed over: the
project's own stated Phase 4 premise called for Platform Hardening
first; the project's own actual practice built Engineering Foundation
frameworks instead, and — per `WP7.1F Engineering Core Certification
Report.md` — that choice is now certified as sound, with zero Release
Blocking consequence. Whatever this Work Package recommends next should
account for this precedent directly, not pretend `Product Roadmap.md`'s
original premise still governs unmodified.

Phase 5 (Engineering Modules) is `Product Roadmap.md`'s own next phase
after Engineering Foundation. It names exactly two disciplines with a
concrete candidate — Systems Engineering (`FCR-0027`) and Project
Management (`FCR-0028`) — and states directly: "six of the nine
discipline categories have no identified candidate yet... this phase's
own scope cannot be written today [for those six] — it depends on a
dedicated capability-identification exercise engaging real
engineering-domain stakeholders."

## 4. What `VISION.md` Actually Supports — Including a Real, Disclosed Tension

`VISION.md`'s own Long-Term Objective 2 states a "ready" bar for
shipping the first Engineering Module: "authentication and transport
security resolved (`FCR-0003`/`FCR-0004`), the plugin/registration trust
boundary closed (`FCR-0001`), and governance tooling mature enough that
a platform-level gap does not go unnoticed for nine Work Packages again
(`FCR-0005`)." **None of these three has been resolved.** Read literally,
this objective would argue against beginning any Engineering Module —
including a Requirements Engine — until Platform Hardening lands first.

This review does not resolve that tension by ignoring it; it resolves it
by checking each named trigger against `Security Roadmap.md` and
`Security Principles.md` Principle 7 directly:

- `FCR-0001`'s own trigger is "a real third-party plugin." `AT-06`
  confirms `src/Plugins/` remains empty — the trigger has not arrived.
- `FCR-0003`/`FCR-0004`'s own trigger is "a concrete deployment scenario
  beyond a trusted local network." No such scenario exists — every
  Engineering Foundation framework runs in-process, first-party, exactly
  the same trust boundary the certified Platform Core already operates
  under.
- `FCR-0005`'s own trigger is procedural (a governance tool), not a
  precondition any specific future capability depends on functionally.

**Building any of the three now, with no real trigger present, would
itself violate Security Principle 7** — the same "do not build ahead of
demonstrated need" discipline `VISION.md`'s own Product Principle 3
restates for product capability generally. `VISION.md`'s "ready" language
is therefore read here as aspirational sequencing guidance, not a
literal gate that blocks every Engineering Module until three
speculative items are pre-built — a reading consistent with `WP 7.0B`'s
own actual practice (which also proceeded past this same "readiness"
language, to certified success). This finding is carried forward
explicitly into `WP7.2A Recommended Programme.md`'s own risk disclosure,
not silently assumed.

## 5. What the Technical Debt Register Actually Supports

24 tracked debt items, 17 disclosed trade-offs — reviewed for whether
any is now urgent enough to force a specific next programme. **None is.**
Every open item carries a named, concrete revisit trigger, and this
review confirms none of those triggers has fired since either
certification: no real third-party plugin exists (`TD-09`/`TD-10`/
`TD-11`), no concrete networked deployment scenario exists (`TD-13`/
`TD-14`), and every Engineering Core debt item (`TD-17`–`TD-24`) is
proportionate to a foundation whose callers remain trusted, first-party,
in-process code. The register argues for *readiness to act once a
trigger fires*, not for treating any current item as forcing this
Work Package's own recommendation.

## 6. What Both Security Reviews Actually Support

`WP 7.1D` and `WP 7.1E`'s own dedicated Security Reviews, and
`WP7.1F Security Review Summary.md`'s own cross-framework review, found
zero Release Blocking findings and confirmed the Engineering Core's own
trust boundary is identical to the Platform Core's: trusted, first-party,
in-process callers only. **No security finding anywhere in this
repository names an urgent, unaddressed risk that should override an
otherwise evidence-backed programme choice.** This is itself informative
for the candidate evaluation: a programme that keeps the same trust
boundary (an Engineering Module built entirely of first-party,
in-process code) introduces materially less new security surface than
one that would open a new one (third-party plugins, a network-facing
surface for an unauthenticated caller).

## 7. What the Academy Actually Supports

99 Academy files, 53 Work Package retrospectives, 15 concept guides, and
`docs/engineering/Engineering Principles.md`'s own 28 principles — all
now written from real, certified, working code across two full
programmes (Platform Core, Engineering Core). The Academy has never
covered a discipline-specific Engineering Module, because none exists.
Whichever programme is recommended next will be the Academy's own first
opportunity to teach a genuinely domain-facing (not infrastructure-facing)
capability — a qualitatively new kind of content this repository's own
Academy has not produced before.

## 8. What the Architecture Documents Actually Support

`docs/architecture/` (20 documents) describes Platform Core structure
exclusively — Navigation, the Shell, the Command Framework, Diagnostics,
Plugin Architecture, Platform Layering. None describes an Engineering
Module's own architecture, because `ADR-0013`'s own classification
question ("is this a platform service or a module?") has never been
asked of a real Engineering Module candidate — `FCR-0027` and `FCR-0028`
are both explicitly, currently **unclassified**. Whichever programme is
recommended next will need this classification decided as its own first
architectural act, exactly as `VISION.md`'s own "Definition of Platform
vs. Engineering Modules" section anticipates.

## 9. What the Release Retrospectives Actually Support

Every retrospective from `WP 6.8` onward that names a recommendation for
"what comes next" points in the same direction, cross-checked directly:

- `WP6.8-platform-services-integration-review.md` §6 named Platform
  Hardening items (now `FCR-0001`, `FCR-0003`–`FCR-0006`) as
  recommendations, but explicitly as candidates, not commitments.
- `WP7.0B Candidate Work Package Catalogue.md` sequenced Candidate I
  (Requirements Engine Architecture) directly after the Engineering
  Foundation frameworks (Candidates D–H), with Candidate D (Data Model)
  as its only dependency — now satisfied.
- `WP7.1E Future Capability Recommendations.md` Recommendation 1 states
  directly: "When `FCR-0027` (Requirements Engine) is eventually
  designed, recording that a requirement has been verified should call
  `IVerificationService.RecordAsync`... directly."
- `WP7.1F Lessons Learned.md` and `ENGINEERING_CORE_COMPLETION_REPORT.md`
  both name three genuinely open paths without recommending one:
  a real Engineering Module, Platform Hardening, or Requirements Engine
  design work — the same three-way choice this Work Package now must
  resolve.

**No retrospective names Mechanical, Structural, Electrical, Building
Services/HVAC, or Manufacturing as a next-programme candidate at any
point in this repository's history.** This is not an oversight this
review is correcting — it is the consistent, repeated, and correct
output of a project that has never had evidence to name one.

## 10. Summary Finding

Repository evidence overwhelmingly supports evaluating this Work
Package's seven named candidate programmes as falling into three tiers,
not seven equally-weighted options:

- **Tier 1 (evidence-backed, real foundation exists):** Programme A
  (Requirements & Verification Platform) and Programme F (Platform
  Hardening) — both traceable to specific, real `FCR` entries, both
  named repeatedly across multiple retrospectives, both with a completed
  dependency satisfied.
- **Tier 2 (identified but explicitly not yet actionable):** Programme G
  (AI & Engineering Intelligence) — the underlying capability
  (`FCR-0024`) already works structurally; there is no real design gap
  to close, only a hypothetical future consumer.
- **Tier 3 (no capability identified, cannot be sequenced from evidence):**
  Programmes B, C, D, E (Mechanical, HVAC, Structural, Electrical) —
  `WP7.0B Engineering Discipline Assessment.md`'s own finding, re-confirmed
  unchanged by this review six Work Packages later.

`WP7.2A Programme Comparison Matrix.md` scores all seven formally, using
this tiering as a starting classification, not a substitute for scoring
each individually against all eleven required criteria.

## Related Documents

`docs/governance/Future Capability Register.md`; `docs/governance/
Product Roadmap.md`; `VISION.md`; `docs/governance/Capability
Categories.md`; `docs/governance/Quality/Technical Debt Register.md`;
`docs/security/Security Roadmap.md`; `docs/security/Threat Model.md`;
`ENGINEERING_CORE_COMPLETION_REPORT.md`; `WP6.8 Platform Certification
Report.md`; `WP7.1F Engineering Core Certification Report.md`;
`WP7.0B Engineering Discipline Assessment.md`; `WP7.0B Candidate Work
Package Catalogue.md`; `WP7.2A Programme Comparison Matrix.md`;
`WP7.2A Recommended Programme.md`.
