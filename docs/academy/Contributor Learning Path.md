# Contributor Learning Path

## Purpose

An ordered reading sequence for a capable software engineer joining
TempestOS with no prior knowledge of it. Follow this path top to bottom,
in order — each step assumes the ones before it. By the end, you should
be able to pick up a Work Package and contribute without asking anyone
else where to look first.

This document is repository-wide — it points into `README.md`,
`docs/releases/`, `docs/academy/`, `docs/architecture/`, `docs/adr/`, and
`docs/governance/` in the order a new contributor actually needs them,
rather than assuming you already know which of those five trees to start
in. `docs/academy/Academy Index.md`'s own "Learning Path" section is the
*Academy-internal* continuation of step 4, below — read this document
first.

## The Path

### 1. Orientation — what is this repository

- **[`README.md`](../../README.md)** — what TempestOS is, how the
  solution is organised, how to build and run it. Five minutes; gives you
  the map before you need to read it in detail.
- **[`PROJECT_STATUS.md`](../../PROJECT_STATUS.md)** — where the project
  stands *right now*: current branch, current release, current and next
  Work Package, repository metrics, known unknowns. This tells you what's
  actually true today, as opposed to what any single document written
  earlier still claims.

### 2. The non-negotiables — what must never change

- **[`docs/releases/FOUNDATION.md`](../releases/FOUNDATION.md)** — why
  TempestOS is built this way, and the specific architectural principles
  every future contributor is expected to preserve. Read this in full,
  not skimmed — it is the one document in this repository explicitly
  written to be read again, not just once.

### 3. How the project is governed

- **[`docs/academy/06 Engineering Standards/Engineering Governance.md`](06%20Engineering%20Standards/Engineering%20Governance.md)**
  — the project's constitution: Work Package lifecycle, review gates,
  Definition of Done, when an ADR is required, Academy maintenance,
  release approval, decision authority.
- **[`docs/academy/06 Engineering Standards/Engineering Lifecycle.md`](06%20Engineering%20Standards/Engineering%20Lifecycle.md)**
  — the concrete Idea → Investigation → Architecture → ADR → Rejected
  Designs → Implementation → Testing → Architecture Review → Academy →
  Governance → Release → Maintenance pipeline every Work Package follows.
- **[`docs/governance/Governance Index.md`](../governance/Governance%20Index.md)**
  and **[`Governance Philosophy.md`](../governance/Governance%20Philosophy.md)**
  — the governance register suite, and *why* it exists (read the
  Philosophy document even if you only skim the registers themselves —
  the "Unknown is preferable to invented data" discipline it describes
  applies to every document you write from here on).

### 4. How the platform works

- **[`docs/academy/00 Introduction/00-welcome-to-the-academy.md`](00%20Introduction/00-welcome-to-the-academy.md)**
  — what the Academy is and how it's organised.
- **[`docs/academy/01 Engineering Principles/`](01%20Engineering%20Principles/)**
  — the vocabulary the rest of the Academy assumes you already have
  (SOLID, Immutability, Dependency Injection, Deterministic Systems, State
  Machines, the Atomic Phase Principle, and the rest).
- **[`docs/academy/02 Runtime Architecture/`](02%20Runtime%20Architecture/)**
  — how the whole platform fits together: the Module Pipeline, the
  Startup Sequence, Working with the TempestOS Host, Platform Layering,
  Plugin Architecture, Failure Isolation Across TempestOS.
- **`docs/architecture/`** — the deeper reference documents these concept
  guides summarise: `Runtime Host Architecture.md`, `Host Lifecycle.md`,
  `Runtime State Machine.md`, `Failure Behaviour.md`, `Ownership
  Matrix.md`, `Platform Service Map.md`, `Engineering Glossary.md`.

### 5. Why specific decisions were made this way

- **`docs/adr/`** — the full Architecture Decision Record catalogue (30
  as of this baseline). You do not need to read all 30 now — read
  ADR-0013 (platform-service vs. module failure), ADR-0017 (Host-owned
  collaborators), and ADR-0023 (the four-layer platform model) first;
  they are cited by nearly everything else.
- **`docs/architecture/Rejected Designs.md`** — designs seriously
  considered and declined. Read this alongside the ADRs it accompanies —
  knowing what TempestOS chose *not* to do is often as informative as
  knowing what it did.
- **`docs/governance/Architecture/`** — the ADR Register, Rejected
  Designs Register, Architecture Document Register, and Decision
  Register, if you need to find a specific decision quickly rather than
  read the catalogue front to back.

### 6. A real module and a real hosted service, end to end

- **[`docs/academy/02 Runtime Architecture/03-building-a-module.md`](02%20Runtime%20Architecture/03-building-a-module.md)**
  and **[`04-building-an-event-driven-module.md`](02%20Runtime%20Architecture/04-building-an-event-driven-module.md)**
  — practical, module-author-facing guides.
- **`src/Samples/Tempest.Samples/`** — `ClockModule` and
  `ClockLifecycleObserverModule`, the living reference modules every
  later Work Package extends. Read the source directly alongside
  `docs/architecture/Sample Module Architecture.md`.
- **`src/Tempest.Core/BackgroundServices/`** — `HostedServiceDiscoveryService`
  and `HostedServiceManager`, alongside `docs/architecture/Background
  Services Architecture.md` and the WP 4.5 implementation retrospective.
  No real hosted service ships yet (`docs/governance/Engineering/Hosted
  Services Register.md`) — the test fixtures under
  `tests/Tempest.Core.Tests/BackgroundServices/HostedServiceFixtures.cs`
  are the closest worked examples available today.

### 7. How to actually contribute

- **Contribution workflow.** Every Work Package follows the Engineering
  Lifecycle (step 3, above): investigate against the real repository
  before assuming a premise (see `WP 4.4C`'s own retrospective for what
  happens when this is skipped), design before implementing anything
  non-trivial, record a genuine alternative as an ADR or Rejected Design
  entry, implement, test, update the Academy and governance registers as
  part of the same change — never a follow-up pass — and only then is
  the Work Package considered done (Engineering Governance §3).
- **Testing philosophy.** `docs/academy/06 Engineering Standards/
  02-testing-strategy.md` — prefer real implementations over mocks; the
  one recurring exception is a level-recording `ILogger`, used only to
  observe log output. The internal-test-seam pattern (an `internal`
  overload accepting explicit input, alongside the public,
  ambient-scanning one) is used consistently for Module Discovery, Plugin
  Discovery, and Hosted Service Discovery alike — see
  `docs/academy/04 Design Patterns/04-reflection-based-discovery.md`.
- **Documentation expectations.** Every Work Package updates the Academy
  and any architecture document it touches as part of its own Definition
  of Done (Engineering Governance §6) — not a separate, later pass. A
  Work Package that changes what a governance register tracks updates
  that register too (`docs/governance/Governance Philosophy.md`, "How
  Contributors Maintain Governance").
- **Engineering governance.** Re-read
  `docs/academy/06 Engineering Standards/Engineering Governance.md` once
  you have the platform context from steps 4–6 — its Review Gates,
  Definition of Done, and ADR Creation Rules will make considerably more
  sense with real examples already in hand.

## After This Path

You should now be able to open `docs/releases/v0.4.0/WorkPackages.md`,
find the Work Package you are about to change, read its own retrospective
(if one already exists) or its own scope entry (if it's still ahead), and
proceed — following the Engineering Lifecycle, checking `docs/governance/
Future Work Package Guidelines.md` for the standing expectations every
future Work Package must meet.

## Related Documents

`docs/academy/Academy Index.md` (the Academy's own, narrower internal
navigation); `docs/releases/FOUNDATION.md`; `docs/governance/Governance
Index.md`; `docs/governance/Future Work Package Guidelines.md`;
`docs/academy/06 Engineering Standards/Engineering Lifecycle.md`.
