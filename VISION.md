# TempestOS Vision

## What This Document Is

`FOUNDATION.md` (`docs/releases/FOUNDATION.md`) records what must not
change about *how* TempestOS is built. This document records what
TempestOS is *for* — the product ambition every future Engineering
Module, Platform Service, and commercial decision is measured against.
Where `FOUNDATION.md` is engineering constitution, this is product
constitution. Both are permanent, cross-release documents; neither
carries a version number; neither is superseded by the next release.

This document was produced by `WP 7.0A` (Future Capability Register &
Product Vision), the first Work Package of the Engineering Foundation
phase (`v0.7.0`), on 2026-07-30. It is the first place TempestOS's
engineering-domain ambition is stated as a coherent vision rather than
left as scattered signal — a `Threat Model.md` assumption here, a
dormant `ProjectModel` field there, one aspirational sentence in
`PROJECT_STATUS.md`. Every claim below that describes an existing fact
is cited to where that fact already lives; every claim that describes an
ambition is stated as an ambition, not asserted as already decided.

**Provenance note, added `WP 21.9.1`, 2026-09-15.** The text below,
unless marked otherwise, is preserved exactly as `WP 7.0A` wrote it on
2026-07-30, describing TempestOS at the close of the Platform phase
(`v0.6.0`). It is not rewritten to look continuously current — several
of its present-tense claims about "zero Engineering Modules" describe
`v0.6.0` and have not been true for some time; the fact record is
corrected in **"Where the Product Stands at the v1.0 Release Candidate"**
below, immediately after "What TempestOS Is", rather than edited into
the original prose. Read this document in four layers: the **original
vision** (this text, 2026-07-30, the Platform phase); the **historical
platform phase** (`v0.2.0`–`v0.16.0`, summarised in `docs/governance/
Product Roadmap.md` Phases 1–5); the **current `v1.0` product**
(`v0.17.0`–`v0.21.0`, re-scoped by `D-028` on 2026-09-09 and executed
under `docs/releases/v1.0.0/WorkPackages.md`); and the **long-term
future vision**, restated unchanged in kind in "Vision Beyond v1.0"
below.

## What TempestOS Is

TempestOS is a modular runtime platform: a Host that discovers,
registers, and orchestrates modules and platform services running
inside it, built on four architectural layers that never invert
(Modules → Platform APIs → Platform Services → Runtime Host,
`ADR-0023`). As of `v0.6.0`, it is a certified platform with eleven
verified platform services — Configuration, Logging, Discovery,
Registration, Dependency Injection, Lifecycle (the Runtime Foundation);
Navigation, the Shell, the Command Framework, Diagnostics (Developer
Experience); Reporting, Permissions & Identity, Notifications, the REST
API, Settings, Audit, Licensing, Export/Import (Platform Services) —
and zero Engineering Modules. Every capability shipped so far is
infrastructure: the platform an engineering-domain product will
eventually be built on, not yet that product itself.

> **This paragraph describes `v0.6.0` (2026-07-28) and is preserved as
> `WP 7.0A` wrote it — it is no longer a description of the current
> product.** `docs/governance/Product Roadmap.md` Phase 5 (reviewed
> 2026-09-04) already records six Engineering Disciplines shipped
> `v0.9.0`–`v0.15.0` (Mechanical Product Structure, Requirements
> Management, Engineering Calculations, Documents, Verification,
> Manufacturing) plus Project Management (the Product Spine, `v0.14.0`)
> — a fact this document never carried forward. Since then the product
> was re-scoped again, deliberately, by `D-028` (2026-09-09): TempestOS
> `v1.0` is **not** built as a set of per-discipline Engineering Modules
> in this document's own sense; it is "a client project system of
> record for an engineering consultancy, that evidence is tagged to."
> See **"Where the Product Stands at the v1.0 Release Candidate"**,
> immediately below, for what is actually shipped or in candidate today.

## Where the Product Stands at the v1.0 Release Candidate (`v0.21.0`, 2026-09-15)

*(Added `WP 21.9.1`, 2026-09-15, for factual alignment. This section
states the current, verified product; it does not replace the vision
above or below it, and it is not itself the vision — it is the fact
record the vision is measured against.)*

TempestOS's product direction changed twice after `WP 7.0A` wrote the
text above, each time by an explicit, dated decision, neither silently:

1. **The historical platform phase (`v0.2.0`–`v0.16.0`).** Contrary to
   this document's "zero Engineering Modules" framing, `Product
   Roadmap.md`'s own Phase 5 record (last reviewed 2026-09-04) shows six
   Engineering Disciplines shipped end to end between `v0.9.0` and
   `v0.15.0` — each with a browsable Project Explorer area, a real
   Property Inspector, a full command set, Digital Thread links and real
   Cockpit KPIs — plus Project Management substantively as the `v0.14.0`
   Product Spine. Three of the nine Engineering Discipline categories
   named in `Capability Categories.md` (Structural, Electrical, Building
   Services/HVAC) remain genuinely undelivered, exactly as `Product
   Roadmap.md` discloses.
2. **The current `v1.0` product (`v0.17.0`–`v0.21.0`, `D-028`).** On
   2026-09-09 the Product Owner decided TempestOS `v1.0` is **"a
   single-user, locally-trusted desktop system of record for a small
   engineering consultancy"** (`docs/releases/v1.0.0/WorkPackages.md`,
   "What v1.0.0 is") — deliberately **not** an ERP and **not** a PLM, and
   not built as a set of per-discipline Engineering Modules the way this
   document's "Definition of Platform vs. Engineering Modules" section
   describes. "Nothing new computes in Tempest" was the rule (`D-028`);
   the Product Owner amended it once, in writing, on 2026-09-15, to add
   eleven engineering calculation modules "like RoyMech's online
   calculators … no code" (`docs/releases/v0.21.0/Execution Plan.md`),
   built on the calculation framework `WP 7.1D` had already implemented
   in the platform phase (`FCR-0032`).

   As of `release/v0.21.0` (candidate `b033651d`, 2026-09-15,
   `docs/releases/v0.21.0/Release Notes.md`), a release candidate for
   `v1.0.0` under the Product Owner's manual test — **not yet tagged,
   not yet `v1.0.0`** — the product demonstrably has:
   - a project/quotation/deliverable/timesheet/invoicing consultancy
     seam, with client, PO reference, budget and pinned rate card
     (`v0.19.0`/`v0.19.1`, `ADR-0150`–`ADR-0152`);
   - evidence, independent check and issue, cited to governed reference
     data pinned at a released revision (`v0.18.0`, `ADR-0148`,
     `ADR-0149`, `D-028`);
   - eleven engineering calculation modules (beam bending, bolted
     joints, bolt groups, fillet welds, lifting lugs, column buckling,
     shaft combined stress, bearing life, thick-walled cylinders,
     thermal expansion, fatigue) and the Engineering Calculators surface
     with Re-run and Compare (`WP 21.3A`, `21.7A`, `21.7B`, `21.7C`);
   - the Engineering Assets surfaces — calculation packs, templates,
     verification artefacts, and bracket verification from the Desktop
     (`WP 21.2B`);
   - documents rendered from templates — invoice, purchase order,
     timesheet, technical report, drawing register, progress report
     (`WP 21.2A`);
   - a Velopack-packaged Windows installer with in-place update, backup
     and restore (`WP 21.5A`);
   - a stated security posture and a required dependency-vulnerability
     scan in CI (`WP 21.5E`/`21.5F`, `docs/security/Security
     Posture.md`);
   - docking steps 1–2 of tear-out-and-dock-everywhere across monitors
     (`WP 21.0A`, `ADR-0153`, **Proposed** — steps 3–4 wait on the
     Product Owner's review); and
   - Undo across Create, Delete, Move, Copy, status changes and field
     edits (`WP 21.1A`).

   `v1.0.0` itself has not shipped; `docs/releases/v1.0.0/WorkPackages.md`
   is the governing programme, superseding the platform-era sequencing
   in `Product Roadmap.md` Phase 5.5 (that document's own "Superseded,
   `WP 17.0B`" note says so directly).
3. **The long-term future vision** — everything beyond whatever release
   eventually earns the `v1.0` number — is unchanged in kind by either
   re-scope and is restated, as originally written, in "Vision Beyond
   v1.0" below. Nothing in `D-028` forecloses it; `D-028` states what
   `v1.0` itself is, not what TempestOS becomes after it.

## Why TempestOS Exists

TempestOS exists to become the platform a real engineering practice —
initially, per `Threat Model.md`'s own governing assumptions (item 1,
established at `WP 5.0S`), one working with **engineering intellectual
property: CAD, requirements, analysis, and verification records** —
runs its work on, rather than a collection of disconnected tools each
solving one part of that work in isolation. The bootstrap-era,
currently-dormant `ProjectModel`/`JsonProjectRepository` code already
modelled toward this before any of TempestOS's Claude-developed history
began — `Classification`, `SecurityLevel` (defaulting to the UK's `BPSS`
baseline), `ExportControlled`, `Customer`, and `ContractNumber` fields —
a concrete signal of original intent, even though that code has been
dead and unreferenced throughout every release to date. This document
is the first to state that intent as a deliberate, current vision rather
than leave it as an artefact of code nobody has revived.

## Long-Term Objectives

1. **Prove the platform before building the product on it.** `v0.3.0`
   through `v0.6.0` did exactly this — infrastructure, developer
   experience, and cross-service platform capability, each proven by a
   real, working consumer before the next layer was attempted. No
   Engineering Module has been designed yet, deliberately: `Capability
   Categories.md`'s own nine Engineering Discipline categories
   (Systems Engineering, Project Management, Mechanical, Structural,
   Electrical, Building Services/HVAC, Materials, Manufacturing,
   Quality) remain almost entirely unpopulated in `Future Capability
   Register.md`, honestly, because no real capability within them has
   been identified yet — not because the ambition does not exist.

   > *Overtaken within the platform phase itself, `WP 21.9.1`,
   > 2026-09-15:* `Product Roadmap.md` Phase 5 (reviewed 2026-09-04)
   > records that six Engineering Disciplines — including Mechanical,
   > Requirements Management (Systems Engineering) and Manufacturing —
   > were in fact designed and shipped `v0.9.0`–`v0.15.0`, in the
   > releases immediately after this objective was written to describe
   > `v0.6.0`/`v0.7.0`; this document was never updated to say so. See
   > "Where the Product Stands at the v1.0 Release Candidate" above.
2. **Ship the first real Engineering Module once the platform is
   ready, not before.** "Ready" means: authentication and transport
   security resolved (`FCR-0003`/`FCR-0004`), the plugin/registration
   trust boundary closed (`FCR-0001`), and governance tooling mature
   enough that a platform-level gap does not go unnoticed for nine
   Work Packages again (`FCR-0005`) — the Engineering Foundation
   phase's own working premise (`Product Roadmap.md`, Phase 4).
3. **Grow outward from Systems Engineering and Project Management
   first**, the two disciplines with an existing, named platform-level
   hook (`ADR-0013`'s own Future Considerations name a "Requirements
   Engine" and a "Project Engine" directly) — then into the seven
   remaining engineering disciplines once a real capability within each
   is identified, per a dedicated future exercise, not invented ahead of
   evidence.
4. **Scale from one professional practice to an enterprise only once a
   real Engineering Module exists for an enterprise customer to run** —
   multi-user isolation (`FCR-0021`), cloud synchronisation (`FCR-0022`),
   and defence-sector compliance readiness (`FCR-0026`) are each
   deliberately sequenced after Engineering Modules and Professional
   Features in `Product Roadmap.md`, not before.

## Target Users

- **Today (v0.6.0 and earlier):** a contributor to TempestOS itself —
  this project has, to date, had exactly one human contributor, working
  with an AI agent as co-author of every commit (`docs/governance/
  Delivery/Release Register.md`).
- **Once Engineering Modules ship:** an individual engineer or a small
  professional engineering practice, working across one or more of the
  nine Engineering Discipline categories `Capability Categories.md`
  establishes — the first real, external users this platform will ever
  have.
- **Once Enterprise Features ship:** a larger engineering organisation,
  potentially including regulated or defence-sector environments
  (`Threat Model.md` assumption 10) — a target deliberately sequenced
  last, not first, per Security Principle 7 (do not build security or
  compliance machinery ahead of a real, demonstrated need).

## Engineering Philosophy

TempestOS's engineering philosophy is `FOUNDATION.md`'s own, unchanged
by this document: architecture precedes implementation for anything
non-trivial; every component has exactly one reason to change; state
has exactly one owner; a platform-service failure and a module failure
are different categories of event; cleanup is always guaranteed;
interruption is observed only at defined boundaries; every non-obvious
decision is recorded in writing, at the time it is made; no tier of
authority substitutes for another; dependencies flow downward only,
through exactly four layers. This document does not restate
`FOUNDATION.md` in full — it is cited here because every future
Engineering Module is bound by it exactly as every Platform Service has
been.

One philosophy addition specific to product vision: **evidence over
ambition, always disclosed as which one it is.** `Future Capability
Register.md` marks a capability's own source explicitly, and this
document marks every claim as either an existing, cited fact or a
stated ambition — never blurring the two, exactly as `Governance
Philosophy.md`'s own Verified/Inferred/Unknown discipline requires
throughout the rest of this governance suite.

## Architectural Philosophy

An Engineering Module is, architecturally, nothing new: it is a Module
or a Platform Service, classified per `ADR-0013`'s own test ("does the
rest of the platform need this to exist before it can function at
all?"), running inside the one Runtime Host `FOUNDATION.md` established
— never a second, parallel execution model. A Requirements Engine, a
Project Engine, a future Mechanical Engineering module: each is
classified explicitly, before design begins, exactly as `ADR-0013`'s own
Future Considerations already anticipate. This is not a new rule this
document introduces — it is `FOUNDATION.md`'s existing rule, applied
to a category of future capability that did not concretely exist when
`FOUNDATION.md` was written.

## Product Principles

1. **Capability before commercial policy.** `WP 6.6` (Licensing)
   already established this precedent explicitly: Licensing exposes
   capability; it does not implement commercial policy. Every future
   Engineering Module follows the same split — the platform capability
   (`ILicenseProvider`-gated, if licensed) is a Platform concern; the
   pricing, packaging, and activation model around it (`FCR-0025`) is a
   Commercial concern, designed separately, later, once a real
   commercial need exists.
2. **Disclose gaps; do not hide them behind optimistic status.** Every
   release to date has certified with disclosed, accepted technical
   debt rather than claiming a false "nothing outstanding" — `v0.6.0`'s
   own certification outcome, `CERTIFIED WITH ACCEPTED TECHNICAL DEBT`,
   is the standing precedent. The same standard applies to this
   document and to `Future Capability Register.md`: six Engineering
   Discipline categories are disclosed as empty, not silently populated
   with invented candidates.
3. **Do not build ahead of real, demonstrated need.** `Security
   Principles.md` Principle 7 governs security machinery; this document
   extends the same discipline to product capability generally — an
   Engineering Module is designed once a real engineering-domain need is
   identified, not speculatively, in the order `Capability
   Categories.md`'s own table happens to list categories.
4. **Every future capability is traceable to why it exists.** `Future
   Capability Register.md`'s own "Notes" field, citing a specific prior
   document for every entry, exists so that ten releases from now, no
   capability's origin is a mystery the way the bootstrap-era
   `ProjectModel` code's own original intent very nearly was.

## What TempestOS Deliberately Is Not

- **Not a general-purpose application platform.** TempestOS is not
  positioning itself against a general web/app framework; its target
  domain is engineering-practice capability, specifically.
- **Not a second execution model bolted alongside the Runtime Host.**
  Every Engineering Module runs inside the same Host every Platform
  Service already does (`FOUNDATION.md`, "What Future Contributors Must
  Preserve").
- **Not a platform that builds compliance, multi-tenancy, or security
  machinery speculatively.** Every Enterprise Feature in `Product
  Roadmap.md`'s own Phase 7 is explicitly sequenced after a real
  Engineering Module exists for it to serve, not before.
- **Not a project that claims completeness it has not verified.** Six
  of nine Engineering Discipline categories in `Capability
  Categories.md` are empty today; this document says so directly rather
  than implying otherwise.
- **Not, today, an AI product.** `FCR-0024` (AI/Automation Command
  Invocation) is one identified, unscheduled future capability the
  Command Framework's own design already anticipates as a caller — it
  is not evidence that TempestOS is currently building AI capability
  beyond that.

## Definition of Platform vs. Engineering Modules

- **Platform** is everything a rest of the platform, including every
  Engineering Module regardless of discipline, needs to exist before it
  can function at all — the `ADR-0013` test, applied at the whole-
  capability level rather than per-service. Every capability shipped
  through `v0.6.0` is Platform. `Capability Categories.md`'s own
  Platform, Infrastructure, Integrations, AI, Academy, and Commercial
  categories are all, in this sense, Platform-adjacent: cross-cutting
  capability no single engineering discipline owns exclusively.
- **Engineering Modules** are domain-facing capability a specific
  engineering discipline would recognise as its own — the nine
  categories `Capability Categories.md` names (Systems Engineering,
  Project Management, Mechanical, Structural, Electrical, Building
  Services/HVAC, Materials, Manufacturing, Quality). An Engineering
  Module is built *on* the Platform, using Platform Services exactly as
  every `v0.6.0` sample module already demonstrates the pattern
  (`IPermissionEvaluator` for authorization, `IAuditRecorder` for
  attribution, `ISettingsService` for configuration) — never a reason
  to add a new dependency path that bypasses them.
- **The boundary is decided explicitly, per capability, before design
  begins** — never assumed. `FCR-0027` (Requirements Engine) and
  `FCR-0028` (Project Engine) are both **not yet classified**; `ADR-0013`
  itself names both as open examples of a capability that "could
  plausibly be either a platform service or a set of modules."

  > *Stale, `WP 21.9.1`, 2026-09-15:* the classification question itself
  > was overtaken by delivery, not resolved by design: `Product
  > Roadmap.md` Phase 5 records `FCR-0027`'s own capability realised as
  > the Requirements Management discipline (`v0.9.0`) and `FCR-0028`'s
  > as Project Management (`Tempest.App.Projects`, `v0.9.0` onward, and
  > substantively the `v0.14.0` Product Spine) without either capability
  > ever receiving the explicit Platform-or-Module ruling this bullet
  > describes as a precondition. Recorded here as a disclosed process
  > gap, not corrected retroactively — see `Product Roadmap.md` Phase 5
  > for the full account.

## Vision Beyond v1.0

**`v1.0` is closer than when this section was written, `WP 21.9.1` notes,
2026-09-15:** `docs/releases/v1.0.0/WorkPackages.md` now names and scopes
a `v1.0.0` release with a real programme behind it, currently at the
`v0.21.0` release candidate stage (see "Where the Product Stands at the
v1.0 Release Candidate" above) — not the wholly unassigned future number
this section was written against. The sentence below is otherwise
preserved as written: TempestOS genuinely has not yet reached `v1.0`,
and this document still does not assign a release number to any phase of
the long-term vision beyond it.

TempestOS has not yet reached `v1.0` and this document does not assign
that number to any phase in `Product Roadmap.md`. Beyond whatever
release eventually earns it, the vision this document states does not
change in kind, only in scale: TempestOS becomes a platform on which
Systems Engineering, Project Management, and — once identified —
Mechanical, Structural, Electrical, Building Services/HVAC, Materials,
Manufacturing, and Quality capability all run, integrated with each
other through the same Platform Services every discipline shares
(Audit, Settings, Reporting, Export/Import, Notifications), rather than
as disconnected point solutions. Enterprise-scale deployment (multi-user,
cloud-synchronised, potentially defence-sector-compliant) follows once
real Engineering Modules exist for an enterprise to actually run —
not before, and not speculatively designed today.

## Related Documents

`docs/releases/FOUNDATION.md` (engineering constitution, unchanged by
this document); `docs/governance/Future Capability Register.md`;
`docs/governance/Capability Categories.md`; `docs/governance/Product
Roadmap.md`; `docs/security/Threat Model.md`; `docs/security/Security
Principles.md`; `ADR-0013`; `PROJECT_STATUS.md`; `docs/releases/v1.0.0/
D-028 Evidence is the product, calculation is where the engineer does
it.md` (the current product's own re-scope, 2026-09-09);
`docs/releases/v1.0.0/WorkPackages.md` (the current, governing
programme); `docs/releases/v0.21.0/Release Notes.md` (what is actually
shipped or in candidate today).
