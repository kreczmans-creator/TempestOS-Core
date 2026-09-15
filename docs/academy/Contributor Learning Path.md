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
in. `docs/academy/Academy Index.md` is the Academy's own table of
contents, one line per document — read this document first.

> **Status note, September 2026.** `WP 17.0B` (2026-09-08) archived much
> of what this path used to cite — the register suite under
> `docs/governance/`, several `docs/architecture/` documents, the Academy's
> `01 Engineering Principles` and `03 Work Packages` folders. Every one of
> them was moved, not deleted, to `archive/docs-2026-09/` at the same
> relative path; this document now points there where that is the case.
> The current process is `CONTRIBUTING.md` at the repository root, and the
> current programme is `docs/releases/v1.0.0/WorkPackages.md`.

### 0. If you are not a software engineer

Read, in this order, before anything below:

- **[`docs/academy/00 Introduction/01-how-tempestos-gets-built.md`](00%20Introduction/01-how-tempestos-gets-built.md)**
  — the stages of building software, explained with this repository's
  own artefacts, and one Work Package followed end to end.
- **[`docs/academy/00 Introduction/02-plain-language-glossary.md`](00%20Introduction/02-plain-language-glossary.md)**
  — keep it open beside every chapter.
- Then the chapters in `02 Runtime Architecture` from `59-evidence.md`
  onward (the product as it is today), each of which opens with an
  **In plain terms** paragraph, and then backwards through `50`–`58`
  (the ground it was built on) and `42`–`49` (how the application was
  made honest and durable). Steps 1–7 below are written for engineers;
  the chapters are written for both.

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
- **`CONTRIBUTING.md`** (repository root) — what a Work Package must
  produce today: a branch, a pull request whose description is the
  retrospective, one review round, the Markdown budget, the Definition
  of Done. Where it differs from Engineering Governance, it governs.
- **`archive/docs-2026-09/governance/Governance Philosophy.md`** — *why*
  the register suite existed, and the "Unknown is preferable to invented
  data" discipline that still applies to every document you write from
  here on. The registers themselves (`Governance Index.md` and the rest)
  are archived beside it; `BACKLOG.md` and `docs/adr/` are the live
  answers to what still needs doing and what was decided.

### 4. How the platform works

- **[`docs/academy/00 Introduction/00-welcome-to-the-academy.md`](00%20Introduction/00-welcome-to-the-academy.md)**
  — what the Academy is and how it's organised.
- **`archive/docs-2026-09/academy/01 Engineering Principles/`** — the
  vocabulary the older Academy chapters assume you already have (SOLID,
  Immutability, Dependency Injection, Deterministic Systems, State
  Machines, the Atomic Phase Principle, and the rest). Archived by
  `WP 17.0B`, still worth the hour.
- **[`docs/academy/02 Runtime Architecture/`](02%20Runtime%20Architecture/)**
  — how the whole platform fits together: the Module Pipeline, the
  Startup Sequence, Working with the TempestOS Host, Platform Layering,
  Plugin Architecture, Failure Isolation Across TempestOS.
- **`docs/architecture/`** — the deeper reference documents these concept
  guides summarise: `Runtime Host Architecture.md`, `Host Lifecycle.md`,
  `Runtime State Machine.md`, `Failure Behaviour.md` are still live there;
  `Ownership Matrix.md`, `Platform Service Map.md` and `Engineering
  Glossary.md` are at `archive/docs-2026-09/architecture/`.

### 5. Why specific decisions were made this way

- **`docs/adr/`** — the full Architecture Decision Record catalogue (149
  as of `v0.18.0`; `docs/governance/Architecture/ADR Register.md` lists
  them). You do not need to read them all now — read ADR-0013
  (platform-service vs. module failure), ADR-0017 (Host-owned
  collaborators), and ADR-0023 (the four-layer platform model) first; they
  are cited by nearly everything else. Then the four `v0.17.0` substrate
  decisions, ADR-0144 to ADR-0147, and the two `v0.18.0` ones, ADR-0148
  and ADR-0149 — they are the ground the product now stands on.
- **`archive/docs-2026-09/architecture/Rejected Designs.md`** — designs
  seriously considered and declined. Read this alongside the ADRs it
  accompanies — knowing what TempestOS chose *not* to do is often as
  informative as knowing what it did.
- **`docs/governance/Architecture/ADR Register.md`** — live; the Rejected
  Designs, Architecture Document and Decision registers are archived
  under `archive/docs-2026-09/governance/Architecture/`.

### 6. A real module and a real hosted service, end to end

- **[`docs/academy/02 Runtime Architecture/03-building-a-module.md`](02%20Runtime%20Architecture/03-building-a-module.md)**
  and **[`04-building-an-event-driven-module.md`](02%20Runtime%20Architecture/04-building-an-event-driven-module.md)**
  — practical, module-author-facing guides.
- **`src/Samples/Tempest.Samples/`** — the real reference modules:
  `ClockModule`/`ClockLifecycleObserverModule` (the original pair every
  later Work Package validated against), the navigation, command and
  diagnostics samples from `v0.5.0`, and the six Engineering Discipline
  sample modules. The Module Register that listed them is archived at
  `archive/docs-2026-09/governance/Engineering/Module Register.md`; read
  the source directly alongside `archive/docs-2026-09/architecture/Sample
  Module Architecture.md`.
- **Don't hand-copy a sample module to start your own.** As of `WP 5.3`,
  `dotnet new tempest-module` scaffolds a correctly-shaped module
  directly — see `src/Templates/README.md`.
- **`src/Tempest.Core/BackgroundServices/`** — `HostedServiceDiscoveryService`
  and `HostedServiceManager`, alongside the archived `Background Services
  Architecture.md` and the `WP 4.5` implementation retrospective (both
  under `archive/docs-2026-09/`). The test fixtures under
  `tests/Tempest.Core.Tests/BackgroundServices/HostedServiceFixtures.cs`
  are the closest worked examples available.

### 6a. What `v0.5.0` added on top of the Platform Foundation

Four more platform services now exist beyond the six the Runtime
Foundation established and the four `v0.4.0` added — read each concept
guide, in this order, once steps 1–6 above make sense:

- **[`09-navigation-architecture.md`](02%20Runtime%20Architecture/09-navigation-architecture.md)**
  — `INavigationProvider`/`NavigationService`, a DI-public registry of
  navigable destinations.
- **[`10-shell-and-application-composition.md`](02%20Runtime%20Architecture/10-shell-and-application-composition.md)**
  — `TempestShell`, `Tempest.App`'s own composition root at `v0.5.0`, and
  the first time this platform actually ran as a real, interactive
  application. Read as history, not current state: `TempestShell` was
  superseded as `Tempest.App`'s own default entry point by `WorkspaceShell`
  at `v0.8.0` (`ADR-0068`) and retired entirely at `v0.11.0` (`ADR-0101`,
  `WP 11.3B`) once found to have been unreachable for three releases. The
  composition-root *pattern* this guide teaches is what matters going
  forward; the specific class no longer exists. TempestOS's shipped
  application is `Tempest.Desktop`; `Tempest.App`/`WorkspaceShell` is now
  TempestOS's Internal Engineering Harness — see `ADR-0101` for the full,
  current picture and `README.md` for how to run either.
- **[`11-command-framework.md`](02%20Runtime%20Architecture/11-command-framework.md)**
  — `ICommandDispatcher`/`ICommandRegistry`, invoking application logic
  uniformly from a typed caller or a string Id.
- **[`12-diagnostics-and-composite-logging.md`](02%20Runtime%20Architecture/12-diagnostics-and-composite-logging.md)**
  — `IDiagnosticsProvider`, a read-only projection over the Host's own
  lifecycle state.
- **`docs/security/Threat Model.md`** and **`Security Principles.md`** —
  the v0.5.0 Security Baseline every Work Package's Definition of Done is
  now checked against.

### 6b. The product as it stands at `v0.18.0`

Steps 4–6a describe the platform. The product built on it — a client
project system of record for an engineering consultancy, in which
calculations done elsewhere are recorded as cited, checked, issued
evidence — is chapters `33`–`64` of `02 Runtime Architecture`. Read in
this order if your Work Package touches the product rather than the
runtime:

- **`53-sqlite-persistence.md`**, **`54-one-transaction-per-engineering-change.md`**,
  **`61-the-screen-follows-the-store.md`** — how anything is saved and
  how the screen learns about it. Everything else assumes these.
- **`51-engineering-reference-data.md`**, **`60-source-citations-and-supersession.md`**,
  **`52-units-as-a-runtime-dimension-vector.md`** — the governed data
  evidence cites, and the quantities it declares.
- **`59-evidence.md`**, **`63-the-evidence-workspace.md`**,
  **`64-independent-check-and-the-issue-sheet.md`** — the product's own
  record, its screen, and its check-and-issue workflow.
- **`43-one-way-to-run-a-command.md`**, **`44-invariants-that-fail-the-build.md`**,
  **`57-workspace-and-harness.md`** — the rules a change must respect,
  and the tests that enforce them.
- **`56-frozen-layers.md`** and **`05 Case Studies/07-the-design-freeze-review.md`**
  — what is deliberately out of the build, and why.

### 7. How to actually contribute

- **Contribution workflow.** Every Work Package is a branch and a pull
  request (`CONTRIBUTING.md`): investigate against the real repository
  before assuming a premise (see `WP 4.4C`'s archived retrospective for
  what happens when this is skipped), design before implementing
  anything non-trivial, record a decision that constrains future code
  as an ADR, implement, test, and write the pull request description as
  the retrospective — what changed, why, and the evidence. The Definition
  of Done is the list in `CONTRIBUTING.md`; `06 Engineering Standards/09-the-governance-reset-and-how-a-release-is-now-run.md`
  explains how this replaced the older process.
- **Testing philosophy.** `docs/academy/06 Engineering Standards/
  02-testing-strategy.md` — prefer real implementations over mocks; the
  one recurring exception is a level-recording `ILogger`, used only to
  observe log output. The internal-test-seam pattern (an `internal`
  overload accepting explicit input, alongside the public,
  ambient-scanning one) is used consistently for Module Discovery, Plugin
  Discovery, and Hosted Service Discovery alike — see
  `docs/academy/04 Design Patterns/04-reflection-based-discovery.md`.
- **Documentation expectations.** A pull request may not add more
  Markdown lines than code lines (`CONTRIBUTING.md`; enforced by
  `scripts/governance-healthcheck.ps1`). Update an ADR or architecture
  document your change makes untrue in the same pull request; file
  anything you find but do not fix as a row in `BACKLOG.md`. The Academy
  is no longer updated per Work Package — see the September 2026 note in
  `00 Introduction/00-welcome-to-the-academy.md` for how it is maintained
  now.
- **Engineering governance.** Re-read
  `docs/academy/06 Engineering Standards/Engineering Governance.md` once
  you have the platform context from steps 4–6 — its Review Gates,
  Definition of Done, and ADR Creation Rules will make considerably more
  sense with real examples already in hand.

## After This Path

You should now be able to open `docs/releases/v1.0.0/WorkPackages.md`
(the current programme — every earlier release's own `WorkPackages.md`
is archived under `archive/docs-2026-09/releases/`, retained for
history, not where new work is scoped), find the Work Package you are
about to work on, read the Academy chapters that cover its ground, read
its Execution Plan if the release has one (`docs/releases/v0.18.0/Execution
Plan.md` is the model), and proceed under `CONTRIBUTING.md`. **A note on
drift:** this section has pointed at the wrong release plan twice before
(`WP 5.4` and `WP 16.2B` each corrected it). If the programme document
moves again, this is the line to fix.

## Related Documents

`docs/academy/Academy Index.md` (the Academy's own table of contents);
`docs/releases/FOUNDATION.md`; `CONTRIBUTING.md`; `BACKLOG.md`;
`docs/releases/v1.0.0/WorkPackages.md`; `docs/academy/06 Engineering
Standards/Engineering Lifecycle.md`; the archived
`archive/docs-2026-09/governance/Future Work Package Guidelines.md`.
