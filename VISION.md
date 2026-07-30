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
phase (`v0.7.0`). It is the first place TempestOS's engineering-domain
ambition is stated as a coherent vision rather than left as scattered
signal — a `Threat Model.md` assumption here, a dormant `ProjectModel`
field there, one aspirational sentence in `PROJECT_STATUS.md`. Every
claim below that describes an existing fact is cited to where that fact
already lives; every claim that describes an ambition is stated as an
ambition, not asserted as already decided.

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

## Vision Beyond v1.0

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
Principles.md`; `ADR-0013`; `PROJECT_STATUS.md`.
