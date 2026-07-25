# TempestOS v0.4.0 — Risk Register

## Purpose

Release-level risks that span more than one work package, or that concern
the release as a whole rather than a single package's own execution. Each
work package's own entry in `WorkPackages.md` also names risks specific to
itself; this document does not repeat those, only the ones bigger than any
one package.

## How to Use This Document

Update it as the release proceeds — a risk register that isn't revisited
is worse than none, because it will be trusted anyway. When a risk is
retired (mitigated, or the underlying decision is made), it is marked
retired with the date and the decision that retired it — **rows are never
deleted**, per this document's own standing rule.

This register has now been revised twice, following two rounds of planning
review. Numbering (`R1`–`R10`) is stable across both revisions; rows are
marked retired rather than removed, per this document's own standing rule.

---

## Register

| # | Risk | Affects | Likelihood | Impact | Mitigation | Status |
|---|---|---|---|---|---|---|
| R1 | **Background Services (WP 4.5) requires touching the Runtime Host's frozen startup/shutdown sequence.** The single riskiest touch-point in the release — every other work package builds *alongside* the Host; this one builds *into* it. **Update, 2026-07-23**: the parenthetical concern about WP 4.2 touching the same table is retired — ADR-0026 (WP 4.2C) has already extended `Host Lifecycle.md`'s phase table once, by precedent (decimal sub-numbering, e.g. `3.1`/`3.2`), establishing the pattern WP 4.5 should follow rather than inventing its own. WP 4.5's own touch remains fully open. | WP 4.5 | Medium | High | Review any change to `TempestHost`/`Host Lifecycle.md`/`Runtime State Machine.md` with the same weight WP 2.7A/B gave the original design — an ADR, not a quiet extension. WP 4.5 should follow ADR-0026's decimal sub-numbering precedent unless it has a specific reason not to. | Open |
| R2 | **Navigation Framework had no existing architectural grounding.** | WP 4.6A/4.6B | High → reduced further | High → reduced | **Mitigated in two stages**: (1) split into an architecture-only phase (`WP 4.6A`) before implementation (`WP 4.6B`), mirroring WP 2.7A; (2) its harder dependency question (relationship to Command Framework) is now resolved by ADR-0022 rather than left for `WP 4.6A` to untangle from scratch. Residual risk is now scheduling discipline and the single remaining open question (does Navigation belong in `Tempest.Core` at all), not an absence of a plan. | Open, reduced |
| R3 | **Event Bus (WP 4.4) and Command Framework (WP 4.7) risk overlapping if their relationship isn't decided explicitly.** | WP 4.4, WP 4.7 | Medium → shifted → reduced further | Medium | Event Bus's own placement is now decided (ADR-0020). The two work packages are also now further apart in the release sequence (4.4 vs. 4.7) than originally planned, which reduces the risk of them being designed in a rush and blurring together — but increases the risk that nobody circles back to cross-reference them explicitly once separated by other work. `WP 4.7`'s own deliverables now make that cross-reference mandatory. **Update, 2026-07-25 — ADR-0028**: the distinction is now documented explicitly (event = zero-or-more subscribers, no expected result; command = exactly one handler, expected result) as part of `WP 4.4`'s own architecture phase, ahead of `WP 4.7` rather than left for it to discover. Residual risk is now only that `WP 4.7` actually cross-references it, not that the distinction itself is undecided. | Open, re-scoped, reduced |
| R4 | ~~Plugin Manifest (WP 4.2) and Background Services (WP 4.5) both propose extending `Host Lifecycle.md`'s phase table, which WP 2.7A/B treated as complete and frozen.~~ **Plugin Manifest half retired, 2026-07-23 — ADR-0026 (WP 4.2C)**: Plugin Manifest's own extension is now fully decided (two new decimal-numbered phases, `3.1`/`3.2`, inserted without renumbering the existing thirteen — see RD-0013 for why renumbering was rejected). Background Services' own extension (WP 4.5) is a separate, still-open matter — see R1. | WP 4.5 | Medium → reduced | Medium | Sequence explicitly (`ReleasePlan.md`'s proposed order keeps them apart) rather than developing both in parallel against the same frozen document. WP 4.5 now has a worked precedent (ADR-0026) to follow rather than reasoning about phase-table extension from scratch. | Open, reduced |
| R5 | **Scope creep into the legacy `LoggingService`/bootstrap code migration** (WP 4.8) becoming larger than this release can absorb alongside ten other work packages. | WP 4.8 | Medium | Low–Medium | `WP 4.8` explicitly scopes this as a *decision* (migrate now vs. re-scope forward again), not a commitment to migrate within this release. | Open |
| R6 | ~~Sample Module (WP 4.3) starting before its dependencies stabilise, producing throwaway work.~~ **Retired and superseded, 2026-07-23**, by the planning revision that moved the Sample Module from last (originally WP 3.8) to early (`WP 4.3`) deliberately — per the reviewed philosophy: prove the platform with a real module before extending it, rather than after. See R9 for the risk this change itself introduces. | — | — | — | Superseded by R9 | Retired |
| R7 | **This release adds several genuinely new architectural surfaces at once** (Event Bus, Command Framework, hosted services, plugin manifests), unlike the Runtime Foundation, which mostly *discovered* an architecture six existing services had already constrained. | Whole release | Medium → reduced further | Medium | Three of the originally-named "likely new decisions" are now resolved (ADR-0020, ADR-0021, ADR-0022) before implementation; a fourth, general safeguard now exists as well — ADR-0023's platform-layering rule gives every remaining and future decision one checkable review question, rather than each new surface needing its own boundary reasoned from scratch. **Update, 2026-07-23**: Plugin Manifest placement is now also resolved (ADR-0025, ADR-0026) — the only remaining residual exposure named here is Navigation's `Tempest.Core` question — see `Architecture.md`. | Open, reduced further |
| R8 | **Governance discipline (ADRs, Academy retrospectives, the 13-section template) is more expensive to sustain across eleven work packages than it was across seven.** | Whole release | Medium | High | Treat Academy/ADR updates as part of each work package's own Definition of Done, never a follow-up pass. Count is now 11 work packages (`4.0`–`4.9`, with `4.6` split), up from 9 — the discipline this risk names must scale with that count, not stay fixed to the original estimate. | Open |
| R9 | **The Sample Module (WP 4.3) is now built early, deliberately, so later work packages can validate against a real consumer instead of a hypothetical one — but this benefit is entirely lost if those later work packages don't actually come back and extend it.** A module built once and never revisited is no better than one built at the end. | WP 4.4, WP 4.5, WP 4.6B, WP 4.7 | Medium | Medium | Every one of `WP 4.4` through `WP 4.7`'s own Acceptance Criteria now names "extends the WP 4.3 sample module" explicitly, as a checked requirement — not an aspiration left to good intentions. | Open — new, from planning revision |
| R10 | ~~Navigation's dependency on Command Framework had inverted from the original planning pass, unresolved.~~ **Retired, 2026-07-23 — ADR-0022**: Navigation and Command Framework are decided to be orthogonal platform services; neither depends on the other. Application logic wires intent (commands) to execution (navigation, or an event another service reacts to) explicitly. The dependency-direction question this risk named no longer exists — there is no dependency between the two to point in either direction. | — | — | — | Retired by ADR-0022 | Retired |

---

## Risks Considered and Not Included

- **Third-party dependency risk** — not applicable; the Runtime Foundation's
  own custom-container philosophy (ADR-0005) and this release's scope do
  not currently propose adopting new external packages. Revisit if any
  work package's planning changes this.
- **Performance risk** — not currently assessed; no work package has a
  stated performance requirement beyond the existing Build and Test Gates
  continuing to pass. Revisit if Event Bus or Background Services planning
  surfaces a genuine throughput concern.
