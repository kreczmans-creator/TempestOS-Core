# Decision Register

## Register Metadata

| Field | Value |
|---|---|
| **Register Name** | Decision Register |
| **Purpose** | Records significant project decisions that shaped TempestOS but do **not** meet Engineering Governance §5's ADR criteria (no genuine rejected alternative, or a process/sequencing decision rather than an architectural one) — so these decisions are not lost simply because no ADR captures them. Complements, and explicitly does not duplicate, the ADR Register. |
| **Scope** | Governance-process decisions (adopting a new discipline or document type), release-sequencing decisions (reordering or splitting Work Packages), and — from `D-021` (`WP 16.0A`, 2026-09-05) — **product decisions that set a scope or support boundary** (what a release contains, what is deferred, what the platform matrix claims). The first two are found in `WorkPackages.md`, `CHANGELOG.md`, and Work Package retrospectives; the third originates in a scope-decision Work Package and is entered only on Product Owner ratification, never on drafting. Architectural decisions with a genuine rejected alternative belong in the ADR Register, not here — see "Relationship to the ADR Register," below. |
| **Owner** | Project Maintainer. |
| **Source of Truth** | `docs/releases/v0.4.0/WorkPackages.md`, `docs/releases/v0.4.0/CHANGELOG.md`, and the individual Work Package retrospectives named in each entry below. |
| **Review Frequency** | Updated whenever a Work Package makes a significant process or sequencing decision that is not itself ADR-eligible. |
| **Last Reviewed** | 2026-09-05 (v0.16.0 Product Owner gate 1) — **`D-021`–`D-026` ratified by the Product Owner** and recorded, each row carrying both its ratification date and the fact that it was proposed 2026-09-04; approval evidence `docs/releases/v0.16.0/WP16.0A Product Owner Ratification — D-021 to D-026.md`. Total 20 → 26 entries. A third Type value, `Product`, enters the register with `D-021` (see "Common Pattern"). The `Common Pattern` paragraph was found stale in the same pass — it claimed "thirteen of the eighteen entries" and five sequencing decisions against a register already holding twenty rows and six — and is corrected by direct recount, disclosed rather than quietly amended. Ratification is Product Owner gate 1 of 4 and confers no merge, tag, publish or release-verdict authority. Previously reviewed 2026-07-28 (WP 5.2, Diagnostics Improvements). |
| **Related Documents** | `ADR Register.md`; `Rejected Designs Register.md`; `Governance Register.md`; `Engineering Evolution Register.md`. |
| **Related ADRs** | None directly — by definition, every entry below was judged *not* to require one. Where a decision later hardened into an ADR-eligible one, this is noted per entry. |
| **Related Academy Articles** | `docs/academy/06 Engineering Standards/Engineering Governance.md` (§5, the ADR/non-ADR boundary this register exists to respect). |
| **Coverage Status** | Partial — this register captures the significant, easily-identifiable process and sequencing decisions found via direct review of `WorkPackages.md`/`CHANGELOG.md`. Smaller, in-the-moment decisions (e.g., a specific variable name, a specific test helper's shape) are not tracked here; that granularity is Inferred to be neither expected nor useful, consistent with §5's own "routine code... does not need one merely because a decision, in the broadest sense, was technically made." |

---

## Relationship to the ADR Register

An ADR records a decision with a genuine rejected alternative and lasting
architectural consequence (§5). This register instead records decisions
that were significant enough to shape the project's trajectory but were
correctly judged, at the time, not to meet that bar — most commonly,
**decisions about how the team works**, not **decisions about how the
software is built**. Every entry below is **Verified** directly from the
source document cited in its own row.

## Entries

| # | Decision | When | Source | Type |
|---|---|---|---|---|
| D-001 | Archive the Python prototype; C# becomes the canonical implementation | 2026-07-21 (commit `337c9cd`) | Git history | Process — technology baseline |
| D-002 | Adopt the Academy (`docs/academy/`) as a maintained documentation asset | 2026-07-22 (commit `b45f544`, "Academy foundation documentation") | Git history; Engineering Governance §6 | Process — documentation discipline |
| D-003 | Adopt Engineering Governance as the project's constitution (work package lifecycle, review gates, Definition of Done, ADR rules, Academy maintenance, release approval, decision authority) | 2026-07-22 (commit `c8f7175`) | `docs/academy/06 Engineering Standards/Engineering Governance.md` | Process — governance framework |
| D-004 | Introduce the Atomic Phase Principle as a named Engineering Principle, distinguishing "lifecycle phase" from "atomic operation" | 2026-07-22 (commits `a18edad`, `e834fea`) | `docs/academy/01 Engineering Principles/11-atomic-phase-principle.md`; formalised as ADR-0018's Terminology section | Process — vocabulary, later informed ADR-0018 |
| D-005 | Introduce the Rejected Designs Log as a permanent engineering rule (Engineering Governance §10) | 2026-07-23 (commit `466334c`) | `docs/architecture/Rejected Designs.md` | Process — governance framework |
| D-006 | Move the Sample Module from last in sequence (originally `WP 3.8`) to early (`WP 4.3`), becoming a living reference module every later Work Package extends | v0.4.0 planning revision, 2026-07-23 | `WorkPackages.md` (How to Read This Document, item 3); `Risks.md` R6/R9 | Sequencing — planning revision |
| D-007 | Split Navigation into an architecture-only phase (`WP 4.6A`) and an implementation phase (`WP 4.6B`) | v0.4.0 planning | `WorkPackages.md` (How to Read This Document); `Risks.md` R2 | Sequencing — risk mitigation |
| D-008 | Spawn `WP 4.2A` (Runtime Platform Version), `WP 4.2B` (ADR-0025), and `WP 4.2C` (ADR-0026) as separately tracked prerequisites discovered during `WP 4.2`'s own design phase, before implementation could proceed | 2026-07-23 | `WorkPackages.md`, `WP 4.2` Status note; `docs/academy/03 Work Packages/WP4.2-plugin-manifest-architecture.md` | Sequencing — scope discovery mid-work-package |
| D-009 | Stop `WP 4.4C` without implementation once investigation showed no `IEventBus` existed yet (a task assumed a false premise); redirect into `WP 4.4`'s own architecture phase instead | 2026-07-25 | `CHANGELOG.md`, `WP 4.4` entry; `docs/academy/03 Work Packages/WP4.4-event-bus-architecture.md` | Process — premise verification before implementation |
| D-010 | Conduct `WP 4.2D` and `WP 4.4F` as dedicated, formal milestone review/audit Work Packages (Platform Services Architecture Review; Academy & Documentation Baseline Audit) rather than folding review into the next feature Work Package | 2026-07-24/25 | `WorkPackages.md`; `docs/academy/03 Work Packages/WP4.2D-platform-services-architecture-review.md`, `Academy Audit Report.md` | Process — periodic governance review cadence |
| D-011 | Implement the WP 4.5 hosted service discovery service under the name `HostedServiceDiscoveryService`, a cosmetic rename from the design phase's working name `ReflectionHostedServiceDiscoveryService` | 2026-07-25 | `docs/academy/03 Work Packages/WP4.5-background-services-implementation.md`, Section 6 | Process — naming reconciliation, no behavioural change |
| D-012 | Establish a Governance Register Baseline (`WP 4.5A`) as its own dedicated, documentation-only Work Package rather than folding governance-register creation into a feature Work Package | 2026-07-25 | This Work Package's own brief | Process — governance milestone |
| D-013 | Formally close the Foundation phase as its own dedicated Work Package (`WP 4.5B`), producing `PROJECT_STATUS.md`, a Foundation Completion Report, a Contributor Learning Path, an Engineering Lifecycle document, and standing Future Work Package Guidelines, rather than letting the Foundation phase's end be implicit | 2026-07-25 | This Work Package's own brief | Process — milestone closeout |
| D-014 | Extend `Engineering Governance.md` with two new sections (§11 Repository Organisation, §12 Naming Conventions) rather than create separate new standard documents, since both codify patterns already applied consistently since `WP 2.1` | 2026-07-25 | `Engineering Governance.md` §11/§12 | Process — standards consolidation, avoiding duplication |
| D-015 | Renumber the Developer Experience phase's four remaining Work Packages (`WP 4.6A`→`WP 5.0A`, `WP 4.6B`→`WP 5.0B`, `WP 4.7`→`WP 5.1`, `WP 4.8`→`WP 5.2`, `WP 4.9`→`WP 5.3`) to reflect that they now belong to the `v0.5.0` release, not `v0.4.0` — the old `v0.4.0/WorkPackages.md` entries are retained, each carrying a redirect note, per this project's own "never delete, mark superseded" convention | 2026-07-27 | `docs/releases/v0.5.0/ReleasePlan.md`'s "A Note on Renumbering" | Process — release-sequencing, no scope or objective change |
| D-016 | Insert a new `WP 5.0C`/`WP 5.0D` pair (Shell & Composition Framework Architecture/Implementation) into the `v0.5.0` sequence, between `WP 5.0B` and `WP 5.1`, without renumbering `WP 5.1`–`WP 5.3` — grown beyond this release's original scope once `WP 5.0C`'s own Repository Investigation confirmed `Tempest.App` still had no composition root consuming the platform | 2026-07-27 | `docs/releases/v0.5.0/ReleasePlan.md`'s "Scope" section; `docs/academy/03 Work Packages/WP5.0C-shell-and-composition-framework-architecture.md` | Sequencing — mid-release scope growth, no renumbering of unrelated Work Packages |
| D-017 | Insert `WP 5.0S` (Platform Security Baseline Audit) into the `v0.5.0` sequence as a dedicated, formal engineering audit — not a feature Work Package — establishing `docs/security/` as a new top-level documentation tree and the "v0.5.0 Security Baseline" as a standing Definition-of-Done check for every subsequent Work Package | 2026-07-28 | This Work Package's own brief; `docs/security/Platform Security Review v0.5.0.md` (Security Baseline Statement) | Process — governance/security discipline, mirrors D-012's "dedicated milestone Work Package" pattern |
| D-018 | Split `WP 5.1` (Command Framework) into an architecture-only phase (`WP 5.1A`) and an implementation phase (`WP 5.1B`), mirroring the `WP 5.0A`/`WP 5.0B` (Navigation) and `WP 5.0C`/`WP 5.0D` (Shell) precedent exactly, rather than designing and implementing in one combined Work Package | 2026-07-28 | This Work Package's own brief; `docs/releases/v0.5.0/WorkPackages.md`'s original single `WP 5.1` entry, now superseded by the split | Sequencing — planning revision, consistent with this release's own established design-then-implementation pattern |
| D-019 | Redirect a brief written as "Event Framework Implementation" (against a non-existent "Event Framework Architecture.md" and an already-fully-implemented Event Bus) into the real, current `WP 5.2` (Diagnostics Improvements) per `docs/releases/v0.5.0/WorkPackages.md`, following investigation and explicit user confirmation — mirrors `D-009`'s "premise verification before implementation" pattern exactly | 2026-07-28 | This Work Package's own brief; `docs/releases/v0.5.0/WorkPackages.md`'s `WP 5.2` entry (Diagnostics Improvements); `docs/academy/03 Work Packages/WP5.2-diagnostics-improvements.md` | Process — premise verification before implementation |
| D-020 | Re-scope `TD-01` (legacy `LoggingService` migration) forward again rather than migrating it, since `Program.cs` has not called this code since `WP 5.0D` — migrating dead code carries no behavioural benefit and only risk | 2026-07-28 | This Work Package's own brief and Repository Investigation; `Technical Debt Register.md` (`TD-01`); `docs/academy/03 Work Packages/WP5.2-diagnostics-improvements.md` | Sequencing — deliberate non-action on a named debt item, not a scope reduction |

| D-021 | `WP11.0B Architecture Roadmap.md` §1 (Definition 2) is the governing scope of `v1.0.0`; the 2026-08-28 project-centric decision (`TD-84`) becomes the v1.x roadmap; `Product Roadmap.md`'s Phase 5.5 formulation is retained as this decision's downstream expression | **Ratified 2026-09-05** (Product Owner); proposed 2026-09-04 | `docs/releases/v0.16.0/WP16.0A Product Owner Ratification — D-021 to D-026.md` (approval evidence); `docs/releases/v0.16.0/WP16.0A v0.16.0 Scope Decision.md` (the proposal); `v1.0.0 Release Candidate Audit.md` §2 | Product — scope boundary |
| D-022 | The Companion mobile application (`WP 14.0A`–`14.2.1`, branch `claude/tempestos-companion-mobile-ubznt3`) is formally deferred to v1.1; the branch is retained, neither merged nor deleted; `TD-82` becomes Deferred by decision; `FCR-0092` remains Companion-scoped | **Ratified 2026-09-05** (Product Owner); proposed 2026-09-04 | `docs/releases/v0.16.0/WP16.0A Product Owner Ratification — D-021 to D-026.md` (approval evidence); `docs/releases/v0.16.0/WP16.0A v0.16.0 Scope Decision.md` (the proposal); `Technical Debt Register.md` (`TD-82`) | Product — scope boundary |
| D-023 | No third-party plugin support is included in `v1.0.0`; `src/Plugins/` remains empty by design; `TD-49`/`50`/`56`/`61`/`64` retain their "before enablement" grading and are not v1.0 blockers | **Ratified 2026-09-05** (Product Owner); proposed 2026-09-04 | `docs/releases/v0.16.0/WP16.0A Product Owner Ratification — D-021 to D-026.md` (approval evidence); `docs/releases/v0.16.0/WP16.0A v0.16.0 Scope Decision.md` (the proposal); `FCR-0002`; Security Principle 7 | Product — scope boundary |
| D-024 | REST remains loopback-only and its listener is disabled by default, enabled explicitly through the established configuration mechanism (`Runtime:RestApi:Enabled`, default `false`, implemented by `WP 16.4B`); `TD-13`/`14`/`16` and `FCR-0003`/`0004` remain declared debt for v1.0 | **Ratified 2026-09-05** (Product Owner); proposed 2026-09-04 | `docs/releases/v0.16.0/WP16.0A Product Owner Ratification — D-021 to D-026.md` (approval evidence); `docs/releases/v0.16.0/WP16.0A v0.16.0 Scope Decision.md` (the proposal); `src/Tempest.Core/Api/RestApiHostedService.cs`; `AT-10` | Product — scope boundary |
| D-025 | v1.0 platform support boundary: **Windows** supported and CI-verified; **macOS** supported by design but **not** CI-verified; **Linux** launches successfully under Avalonia 11.3.20, on advisory smoke evidence and its determinism track record, but is **not CI-verified in the same sense as Windows**. Linux is not to be recorded as "supported" without qualification, is not to be claimed CI-verified, and the distinction between launch evidence and formal CI verification is to be retained | **Ratified 2026-09-05** (Product Owner); proposed 2026-09-04 | `docs/releases/v0.16.0/WP16.0A Product Owner Ratification — D-021 to D-026.md` (approval evidence); `docs/releases/v0.16.0/WP16.0A v0.16.0 Scope Decision.md` (the proposal); `docs/releases/v0.16.0/WP16.5B Linux Launch Spike Report.md` §8; `TD-116` | Product — support boundary |
| D-026 | `WP 15.2A` is carried into `v0.16.0`; `docs/releases/v0.15.1/` remains deleted; no `v0.15.1` release exists. The folder disposition had already been performed and is irreversible; it was disclosed as such at the gate and is accepted and ratified in that knowledge | **Ratified 2026-09-05** (Product Owner); proposed 2026-09-04 | `docs/releases/v0.16.0/WP16.0A Product Owner Ratification — D-021 to D-026.md` (approval evidence); `docs/releases/v0.16.0/WP16.0A v0.16.0 Scope Decision.md` (the proposal); `WP16.0B Integration Report.md` | Sequencing — release folder disposition |

**Total: 26 entries.**

> **`D-021`–`D-026` were Proposed before they were ratified, and the
> register says so in each row's own "When" cell rather than showing only
> the ratified state.** They were reserved by number on 2026-09-04 by
> `WP 16.0A` with their approval dates deliberately blank, and the whole
> of v0.16.0 — thirteen Work Packages, two review boards and the
> Engineering Readiness Review — was executed while they were still
> unratified, disclosed at each step rather than assumed. The Product
> Owner ratified all six on 2026-09-05. No earlier document is amended to
> imply approval came sooner. Full status history and the ratification
> instruction: `docs/releases/v0.16.0/WP16.0A Product Owner Ratification —
> D-021 to D-026.md`.
>
> Ratification of these six is **Product Owner gate 1 of 4** for v0.16.0.
> It carries no authority to merge, tag, publish, or record a release
> verdict; those are gates 2, 3 and 4, held separately.

## Common Pattern

Re-derived directly from the rows rather than carried forward: of the 26
entries above, **14 are process decisions** (how the project works), **7
are sequencing decisions** (D-006, D-007, D-008, D-016, D-018, D-020,
D-026), and **5 are product decisions** (D-021–D-025). None is an
architectural decision under §5, since none introduced a genuine,
seriously-considered-and-rejected technical alternative of its own.

The sequencing entries are downstream consequences of risk assessment or
investigation findings — reordering or extending *when* something is
built, not deciding *how*.

**`Product` is a third category, new with `D-021`.** It was introduced by
`WP 16.0A` for decisions that set a scope or support boundary: what is in
v1.0, what is deferred, what the platform matrix claims. Those are neither
process nor sequencing — they do not say how the project works or when
work happens, they say what the product is — and forcing them into either
existing category would have misfiled them. They remain correctly outside
the ADR Register for the §5 reason above: a scope boundary is a product
judgement, not a rejected technical alternative.

This paragraph previously read "thirteen of the eighteen entries" with
five sequencing decisions named, against a register that already held
twenty rows with six sequencing entries. Both figures were stale — the
prose had not been re-derived when D-019 and D-020 were added. Corrected
here by direct recount, and noted rather than silently fixed, because
this register's own `Coverage Status` claims its entries are Verified from
source and a summary that drifts undermines that claim.

## Cross-Reference Check

Every entry is traceable to a specific commit, retrospective, or planning
document cited in its own row — no entry here was reconstructed from
memory or inferred without a direct source. No entry duplicates an ADR or
Rejected Designs entry; D-011 is the one boundary case (a naming choice
made during an implementation Work Package) and is recorded here
specifically because it is a **process** clarification (reconciling two
names for the same, unchanged design) rather than a technical alternative
with consequences.
