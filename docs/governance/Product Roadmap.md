# Product Roadmap

## Register Metadata

| Field | Value |
|---|---|
| **Register Name** | Product Roadmap |
| **Purpose** | The high-level, phase-based sequencing of TempestOS's own future — how the Platform phases already shipped relate to the Engineering Modules and commercial capability `VISION.md` describes, and which `Future Capability Register.md` entries plausibly belong to which phase. |
| **Scope** | Every release phase from Platform Foundation (`v0.3.0`–`v0.4.0`) through Future Expansion. |
| **Owner** | Project Maintainer. |
| **Source of Truth** | `docs/governance/Delivery/Release Register.md` (what has actually shipped); `Future Capability Register.md` (what is identified but not yet scheduled); `VISION.md` (why the sequencing below is the sequencing chosen). |
| **Review Frequency** | Reviewed at every release boundary, and whenever a new phase's own scope is approved. |
| **Last Reviewed** | 2026-07-30 (`WP 7.0A`, Future Capability Register & Product Vision — established). |
| **Related Documents** | `Future Capability Register.md`; `Capability Categories.md`; `VISION.md`; `docs/governance/Delivery/Release Register.md`; `docs/releases/v0.7.0/WorkPackages.md`. |
| **Related ADRs** | ADR-0013. |
| **Related Academy Articles** | None yet. |
| **Coverage Status** | Complete for phases already shipped or actively planned (Platform Foundation, Engineering Foundation); **sequencing only, not scoped**, for every phase beyond that — see each phase's own note. |

---

## Purpose of This Document

This document answers one question: **in what order should TempestOS's
own future be attempted?** It does not scope any Work Package — that
remains `docs/releases/vX.Y.0/WorkPackages.md`'s own job, produced by
its own dedicated Architecture, Planning, and Contract Review phase, per
this project's standing discipline (`FOUNDATION.md` §1). This document
instead shows the likely sequencing of *phases*, so that phase-level
sequencing decisions (build Engineering Modules before or after
Enterprise Features, for example) are made deliberately, once, rather
than implicitly re-derived by whichever Work Package happens to propose
next.

**No release number below is committed** except where a release has
already shipped or a branch already exists — per this Work Package's own
instruction not to commit to specific release numbers unless already
approved.

## Phase Sequence

### Phase 1 — Platform Foundation (Complete)

**Shipped as:** `v0.1.0` through `v0.4.0`.

The Runtime Host, Dependency Injection, Configuration, Logging,
Discovery, Registration, Lifecycle, the Module Framework, the Plugin
Manifest, the Event Bus, and Background Services — the infrastructure
every later capability, whether Platform or Engineering Module, runs on
top of. See `docs/releases/Platform Foundation Completion Report.md`.

### Phase 2 — Developer Experience (Complete)

**Shipped as:** `v0.5.0`.

Navigation, the Shell & Composition Framework, the Command Framework,
Diagnostics improvements, and developer tooling (`dotnet new
tempest-module`) — everything the Platform Foundation needed to become
an actual, usable application, plus the platform's first comprehensive
security audit (`WP 5.0S`).

### Phase 3 — Platform Services (Complete)

**Shipped as:** `v0.6.0`, `CERTIFIED WITH ACCEPTED TECHNICAL DEBT`.

Reporting, Permissions & Identity, Notifications, the REST API,
Settings, Audit, Licensing, and Export/Import — the first release to add
genuinely new, domain-facing capability rather than infrastructure,
proving the platform can support real cross-service integration (eleven
platform services, each with a verified consumer).

### Phase 4 — Engineering Foundation (Current, Not Yet Scoped)

**Branch:** `feature/v0.7.0-engineering-foundation`, cut from `main` at
the `v0.6.0` tag. **This Work Package (`WP 7.0A`) is this phase's own
first activity** — establishing the governance artefacts (this
document, `Future Capability Register.md`, `Capability Categories.md`,
`VISION.md`) the phase's own subsequent Work Packages will be selected
against, before any implementation begins.

This phase's own working premise (subject to its own Architecture,
Planning, and Contract Review, not yet held): close the platform-level
gaps `Future Capability Register.md` identifies under the **Platform**
and **Infrastructure** categories before building outward into
Engineering Modules — a platform with known, disclosed gaps in its own
authentication, governance tooling, and trust-boundary enforcement is a
weaker foundation for a first Engineering Module than one without them.
See `docs/releases/v0.7.0/WorkPackages.md` for the four concrete
candidate items already identified (`FCR-0006`, `FCR-0005`, `FCR-0001`,
`FCR-0003`/`FCR-0004`).

### Phase 5 — Engineering Modules (Not Yet Scoped)

The phase `VISION.md` names as TempestOS's own reason for existing: real
capability for the Engineering Discipline categories `Capability
Categories.md` establishes (Systems Engineering, Project Management,
Mechanical, Structural, Electrical, Building Services/HVAC, Materials,
Manufacturing, Quality). `Future Capability Register.md` currently holds
concrete candidates only for Systems Engineering (`FCR-0027`,
Requirements Engine) and Project Management (`FCR-0028`, Project Engine)
— six of the nine discipline categories have no identified candidate yet
(see that register's own Coverage Note). **This phase's own scope
cannot be written today** — it depends on a dedicated
capability-identification exercise engaging real engineering-domain
stakeholders, not a documentation-only exercise like this one.

### Phase 6 — Professional Features (Not Yet Scoped)

Capability that makes an individual Engineering Module genuinely usable
by a professional engineering practice day-to-day, once at least one
Engineering Module exists to make professional: likely candidates
include `FCR-0012` (Reporting delivery/history — professional
deliverables), `FCR-0014` (advanced Settings — per-user configuration),
and `FCR-0018` (REST request-parameter binding, if a professional
integration need arises). Named here only as plausible sequencing, not
approved scope.

### Phase 7 — Enterprise Features (Not Yet Scoped)

Capability that scales TempestOS from a single professional practice to
a larger organisation: `FCR-0021` (Multi-User/Tenant Isolation),
`FCR-0022` (Cloud Synchronisation), `FCR-0025` (Commercial Licensing
Model — floating/seat-based licensing implies more than one concurrent
user), and `FCR-0026` (Defence-Sector/Regulated-Environment Compliance,
if a real opportunity in that sector materialises). Sequenced after
Engineering Modules and Professional Features because enterprise-scale
capability without a real Engineering Module for an enterprise customer
to actually run would be built ahead of demonstrated need, contradicting
`Security Principles.md` Principle 7.

### Phase 8 — Future Expansion (Not Yet Scoped)

Everything else `Future Capability Register.md` names without a clear
phase yet: `FCR-0002` (Third-Party Plugin Ecosystem), `FCR-0020`
(Secrets Redaction, gated on Phase 4/7's own authentication or cloud
work), `FCR-0023` (Offline/Mobile), `FCR-0024` (AI/Automation Command
Invocation), and any capability a future capability-identification
exercise adds to the six currently-empty Engineering Discipline
categories.

## How This Roadmap Relates to `v0.7.0`'s Own Scope

`docs/releases/v0.7.0/WorkPackages.md` already names four candidate
items or item-pairs (`C1`–`C4`), each traced to a specific `FCR` entry
above (`FCR-0006`, `FCR-0005`, `FCR-0001`, `FCR-0003`/`FCR-0004`
respectively) — all four sit within Phase 4 (Engineering Foundation) as
this document defines it. This Work Package's own Recommended v0.7
Candidate Work Packages deliverable (`docs/releases/v0.7.0/WP7.0A
Recommended v0.7 Candidate Work Packages.md`) assesses each against
Strategic Value, Technical Complexity, and Platform Readiness explicitly.

## Non-Commitments

This document deliberately does **not**:

- Commit to a `v0.7`, `v0.8`, `v1.0`, etc. release number for any phase
  beyond Phase 4, which already has an approved branch name (not a
  version-numbered scope).
- Scope any Work Package within Phase 5 (Engineering Modules) onward —
  each requires its own Architecture, Planning, and Contract Review
  phase before a Work Package number is assigned.
- Assume Phase 5 must be entirely one Work Package, or that the nine
  Engineering Discipline categories are built in the order listed in
  `Capability Categories.md` — that order is alphabetical/structural,
  not a sequencing recommendation.

## Related Documents

`Future Capability Register.md`; `Capability Categories.md`; `VISION.md`;
`docs/governance/Delivery/Release Register.md`; `docs/releases/v0.7.0/
WorkPackages.md`.
