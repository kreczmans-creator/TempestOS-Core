# TempestOS Academy — Index

**Purpose.** One table of contents for the whole Academy, one line per
document, so a reader can find a chapter by topic without knowing which
folder it lives in. Written in September 2026, after `v0.18.0`, replacing
the archived index (`archive/docs-2026-09/academy/Academy Index.md`) that
`WP 17.0B` retired along with the register suite. It is deliberately
short: a line per document and nothing else, so that keeping it current
costs one line per new chapter.

**If you are not a software engineer,** start with the three documents under
*Introduction*, then read *Runtime Architecture* from chapter `59` onward
(the product as it is today) and work backwards. Every chapter from `42`
opens with an **In plain terms** paragraph and closes with **What to take
away**.

**A note on release status.** Chapters `01`–`64` describe work that has
been merged to `main` and released (`v0.18.0`, tagged 2026-09-14, is the
newest release). Chapters `65`–`76`, case study `08` and standards `10`–`11`
describe the `v0.19.0`, `v0.19.1` and `v0.20.0` **release candidates** —
branches `release/v0.19.0`, `release/v0.19.1` and `release/v0.20.0`, cut
2026-09-10 to 2026-09-15 and still under the Product Owner's manual test
when this index was written — and, at the time chapter `76` was written,
the `v0.21.0` plan, which then had no code at all. **By the end of
2026-09-15, `release/v0.21.0` was itself a release candidate with real,
merged code** — seventeen of the plan's twenty-two Work Packages, per
`docs/releases/v0.21.0/Release Notes.md` — not covered by a chapter of
its own yet. Each of chapters `65`–`76` says so in its own header.
Nothing they describe is released until its Release Notes say it is.

**A note on paths.** A chapter written before 2026-09-08 may cite a
`docs/architecture/…` or `docs/governance/…` document that has since moved
to `archive/docs-2026-09/` at the same relative path. The archived
`01 Engineering Principles` and `03 Work Packages` folders are listed at
the end.

## Introduction

Orientation. Read these first, in order.

- [Welcome to the TempestOS Academy](00%20Introduction/00-welcome-to-the-academy.md) — what the Academy is, who it is for (the Product Owner and any engineer joining), how it is organised, what was archived, and the note on honesty. Read this page first.
- [How TempestOS Gets Built: the Stages, in Plain Terms](00%20Introduction/01-how-tempestos-gets-built.md) — the stages of building software — deciding, planning, recording decisions, writing, proving, reviewing, releasing, living with it — explained with this repository's own artefacts, and one Work Package followed end to end. Start here if you are not a software engineer.
- [Plain-Language Glossary](00%20Introduction/02-plain-language-glossary.md) — every term the Academy uses, in plain words, in three groups: general software terms, TempestOS's own vocabulary, and the programme names. Keep it open beside every chapter.

## Runtime Architecture — the platform, then the product, in build order

Seventy-six chapters. The folder's name is historical: from chapter `13` onward the subject is the engineering product as much as the runtime beneath it.


### The runtime (`v0.3.0`–`v0.5.0`)

- [The Module Pipeline: Discovery → Registration → Lifecycle → Dependency Injection](02%20Runtime%20Architecture/01-the-module-pipeline.md) — Discovery → Registration → Lifecycle → Dependency Injection, as one connected system.
- [The Startup Sequence](02%20Runtime%20Architecture/02-the-startup-sequence.md) — why configuration (and later, logging) must exist before dependency injection begins, and the ordering this forces.
- [Building a Module](02%20Runtime%20Architecture/03-building-a-module.md) — the practical, module-author-facing guide, including the parameterless-constructor constraint and its attribute-based lift.
- [Building an Event-Driven Module](02%20Runtime%20Architecture/04-building-an-event-driven-module.md) — the same guide, extended for a module that publishes or subscribes to events.
- [Working with the TempestOS Host](02%20Runtime%20Architecture/05-the-runtime-host.md) — a first-read guide to `TempestHost`, synthesising the six reference documents below into one narrative.
- [Platform Layering: Designing a Platform Service](02%20Runtime%20Architecture/06-platform-layering.md) — the four-layer model (Modules → Platform APIs → Platform Services → Runtime Host, ADR-0023) and how to classify a new capability against it.
- [Plugin Architecture](02%20Runtime%20Architecture/07-plugin-architecture.md) — the concept guide: what a plugin manifest is, why it's a pre-discovery artifact, and how loading one requires zero change to Module Discovery.
- [Failure Isolation Across TempestOS](02%20Runtime%20Architecture/08-failure-isolation.md) — the recurring platform-service/module/plugin/subscriber isolation question, all four worked examples side by side.
- [Navigation Architecture](02%20Runtime%20Architecture/09-navigation-architecture.md) — the concept guide: why a UI-adjacent concept can still be architecturally UI-agnostic, the platform/application rendering boundary, the contribution model, and common mistakes.
- [Shell & Application Composition](02%20Runtime%20Architecture/10-shell-and-application-composition.md) — the concept guide: why "the thing that runs the app" is not the same component as "the thing the app runs," the composition-root relationship to the Runtime Host, and common mistakes.
- [Command Framework](02%20Runtime%20Architecture/11-command-framework.md) — the concept guide: why commands exist, the Command/Mediator pattern, why TempestOS didn't adopt CQRS, and how `ICommandDispatcher`/`ICommandRegistry` answer two genuinely different callers' needs.
- [Diagnostics & Composite Logging](02%20Runtime%20Architecture/12-diagnostics-and-composite-logging.md) — the concept guide: why composite logging and read-only lifecycle-state visibility are the same underlying need, the `Func<T>` lazy-accessor pattern, and common mistakes.

### The engineering foundation and workspace (`v0.7.0`–`v0.9.0`)

- [Calculation Framework](02%20Runtime%20Architecture/13-calculation-framework.md) — the concept guide: why a calculation is not a command, the purity guarantee that makes concurrent execution safe, and common mistakes.
- [Verification Framework](02%20Runtime%20Architecture/14-verification-framework.md) — the concept guide: distinguishing Verification from Audit and from a Calculation Record, three structurally similar "record what happened" types with genuinely different semantics.
- [The Engineering Data Model](02%20Runtime%20Architecture/15-engineering-data-model.md) — the concept guide: the document/revision/reference pattern, why it is a layer above `IPersistenceStore` rather than a replacement for it, and common mistakes. Written by `WP 7.1F`, four Work Packages later than `WP7.0C Academy Plan.md` originally called for — a disclosed documentation-drift finding, not a silent gap.
- [Requirements Engine](02%20Runtime%20Architecture/16-requirements-engine.md) — the concept guide: the three-layer Requirement-as-Document pattern, and the relationship-kind/traceability vocabulary.
- [Engineering Workspace](02%20Runtime%20Architecture/17-engineering-workspace.md) — the concept guide: why the Workspace is a graphical evolution of the same composition root `TempestShell` already occupies, why its View layer is forbidden from calling a mutating service directly, and how the architecture/contracts/UX-specification/shell/navigation/cockpit sequence fits together.
- [Engineering Domain Architecture](02%20Runtime%20Architecture/18-engineering-domain-architecture.md) — the concept guide: why four separately-designed frameworks converging on the same shape became binding platform architecture, and how the architecture/contracts/implementation phases relate.

### The desktop application (`v0.10.0`–`v0.13.x`)

- [User Experience & Desktop Application](02%20Runtime%20Architecture/19-user-experience-and-desktop-application.md) — the concept guide, written at the architecture stage: why the presentation paradigm changes a second time (console → terminal, `v0.8.0`; terminal → graphical, `v0.10.0`), and what stays exactly the same underneath both changes.
- [Desktop Application Framework](02%20Runtime%20Architecture/20-desktop-application-framework.md) — the concept guide, written at implementation stage: how `EngineeringWorkspaceComposer` lets two presentation layers load the identical six-discipline Workspace without copying `Program.cs`'s own composition sequence.
- [Engineering Cockpit — Graphical Implementation](02%20Runtime%20Architecture/21-engineering-cockpit-graphical-implementation.md) — the Cockpit rendered graphically in `Tempest.Desktop`, and why auditing the placeholders that already claimed to exist was the Work Package's central act (`WP 10.1A`).
- [Runtime Host — Restart Stability & Readiness Signalling](02%20Runtime%20Architecture/22-runtime-host-restart-stability.md) — two symptoms — stale reads after a restart and sample modules occasionally failing to initialise — traced to one readiness-signalling gap in the Host (`WP 10.1B`).
- [Workspace Modernisation — Real Dispatch Behind a Modern Shell](02%20Runtime%20Architecture/23-workspace-modernisation.md) — modernising the shell required a small, real Workspace contract extension (`ADR-0096`) rather than pure presentation work, and why that was the smaller risk (`WP 10.2A`).
- [Docking & Workspace Layouts](02%20Runtime%20Architecture/24-docking-and-workspace-layouts.md) — docking and saved layouts added additively at the Desktop layer, never by reopening a frozen contract (`WP 10.2B`).
- [Engineering Object Editors](02%20Runtime%20Architecture/25-engineering-object-editors.md) — one editor engine for six disciplines, because every engineering object already composes the same facet set (`ADR-0075`, `WP 10.3A`).
- [Ribbon & Command Experience](02%20Runtime%20Architecture/26-ribbon-and-command-experience.md) — the Ribbon reuses the three Kind-keyed dispatch verbs rather than adding a command framework of its own (`ADR-0096`/`ADR-0097`, `WP 10.3B`).
- [Digital Thread Visualisation](02%20Runtime%20Architecture/27-digital-thread-visualisation.md) — the relationship graph as a presentation over the existing Digital Thread, and how expand/collapse and selection reuse the Object Editor's own reads (`WP 10.4A`).
- [Workspace Visual Polish & Engineering User Experience](02%20Runtime%20Architecture/28-workspace-visual-polish.md) — `ThemeReactiveBrush` and the visual polish pass, with the no-ADR decision re-verified rather than assumed (`WP 10.5A`).
- [Desktop Workflow & Professional Interaction](02%20Runtime%20Architecture/29-desktop-workflow-and-professional-interaction.md) — four dialogs with no shared base class still count as a framework, and one discipline's create flow wired end to end rather than eight shallow ones (`WP 10.5B`).
- [Command Execution & Productivity Experience](02%20Runtime%20Architecture/30-command-execution-and-productivity-experience.md) — Undo/Redo as a plain delegate stack, a Macro as an ordinary registered command, the Command Palette and external input bindings (`WP 10.6A`).
- [Commercial User Experience & Application Completion](02%20Runtime%20Architecture/31-commercial-user-experience-and-application-completion.md) — launch the application and audit it first: how a GUI audit was done honestly with no way to render, and what it changed (`WP 10.5C`).
- [Responsive Workspace & Ribbon Minimisation](02%20Runtime%20Architecture/32-responsive-workspace-and-ribbon-minimisation.md) — the Product Compliance Audit's own remediation: a width-driven responsive rule over the workspace and a genuinely minimisable Ribbon; closes `TD-70`/`TD-71`.

### Product Convergence & Recovery (`v0.14.0`, August 2026)

- [The Product Spine](02%20Runtime%20Architecture/33-the-product-spine.md) — `IProjectDirectory`/`IProjectContext`/`IShellNavigator`: the project-centric backbone the product was missing, established without rewriting the engineering platform beneath it.
- [Engineering Object Rehydration](02%20Runtime%20Architecture/34-engineering-object-rehydration.md) — `ADR-0113`: durable `EngineeringObjectState` plus factory-driven rehydration (`IRehydratable<TSelf>`), so a persisted object survives a process restart with its identity, lifecycle, revisions, relationships and provenance intact; removes `Projects.Index` rather than leaving two competing persistence mechanisms. Closes `TD-85`; opens `TD-86`/`TD-87`/`TD-88`.
- [Project-Centric Convergence](02%20Runtime%20Architecture/35-project-centric-convergence.md) — transitive `ProjectMembership`, `IEngineeringScope`, and the `ShellAreas`/`ProjectAreas` descriptor tables that make a not-yet-implemented module say so honestly instead of pretending. Standalone calculation sets remain a first-class, project-free workflow. Closes `TD-89` for the spine.
- [Workspace Layout & Docking](02%20Runtime%20Architecture/36-workspace-layout-and-docking.md) — `ADR-0095`: the immutable `WorkspaceLayoutTree` (splits, tab groups, floating windows) that replaces the fixed 5x3 `DockingGrid`, with drag-to-dock, collapse/auto-hide, resize, persistence and restoration. Closes `TD-72`; opens `TD-90`/`TD-91`.
- [Attachment Content Storage](02%20Runtime%20Architecture/37-attachment-content-storage.md) — `ADR-0114`: an attached file becomes a file this platform holds. The byte shape of the store it already had, with metadata and content deliberately separated so rehydrating a whole object graph loads no files, and reads that report `Available`/`Missing`/`Corrupt` rather than handing back damaged bytes. Closes `TD-31` and implements `FCR-0054`; opens `TD-95`/`TD-96`/`TD-97`.
- [Document & Drawing Viewer](02%20Runtime%20Architecture/38-document-and-drawing-viewer.md) — `ADR-0115`: a real viewer, not a metadata display. PDFium rasterises pages on demand at the current zoom, because a text-extraction viewer serves a specification and fails at the vector drawings mock-ups 2 and 3 are about. The viewport is a pure immutable value; the viewer is an ordinary `TD-72` panel. Closes `TD-80` for the scope delivered and visually accepts it against the mock-ups; opens `TD-98`–`TD-101`. Includes what a 287-green headless suite could not see: four user-visible defects — one of them a missing Open button that made the whole viewer unreachable — found the first time anyone rendered the window.
- [Project Documents & Requirements](02%20Runtime%20Architecture/39-project-documents-and-requirements.md) — `TD-102`: the two project areas that were marked **Implemented** and drew a declared-capability card with no badge. Documents join through `ProjectMembership` transitively; requirements join through the allocation link the platform already records, because a requirement is not an engineering object and a `ProjectId` field would be a second answer. Declared status and recorded verification are shown side by side because they disagree. Opens `TD-103` (the desktop shell establishes no principal).
- [Production Rehydration & the Principal Boundary](02%20Runtime%20Architecture/40-production-rehydration-and-the-principal-boundary.md) — `ADR-0116`: two defects with one shape — the product worked because the sample harness happened to ship. Twelve engineering Kinds rehydrated only because `Tempest.Samples` registered them, and nine more were registered nowhere at all and were silently discarded on every restart, found by reflecting over the domain rather than reading the registration list. The lesson worth carrying: when proving an absence, assert the dependency, not the symptom — a behavioural test passes either way when the assembly is loaded in the test process. Resolves `TD-103` and `TD-104`; closes `TD-75`'s rehydration half and says plainly which half is left. Includes the bug the fix introduced: publishing only a non-null principal left a sample's principal standing in a session that should have had none.
- [Project Tasks & Delivery Workflow](02%20Runtime%20Architecture/41-project-tasks-and-delivery-workflow.md) — `ADR-0117`: the task model already existed and nothing used it. Its central decision is a refusal — a task's status is not `LifecycleState`, because the canonical table forbids Released → Draft (correctly) while a finished task must reopen. Rather than weaken a rule protecting released engineering data, the task family implements `IFamilySpecificState`, a contract the platform had declared and never used: when a rule is in your way, check whether the codebase already predicted the exception. Also records the mutation that survived — a state change that never persisted, invisible because every assertion read the object it had just changed — and two test fragilities: a stale reference across `ReviseAsync`, and `Single()` failing on a shown window. Partially resolves `TD-81`, for Tasks only.
- [Project Timeline, Risks, Issues & Decisions](02%20Runtime%20Architecture/42-project-timeline-risks-issues-and-decisions.md) — the governance families (risks, issues, decisions) and the timeline (milestones, deliverables) get a workflow and a surface — with a per-family status vocabulary and membership by the parent chain, and nothing the model cannot actually know.

### Remediation and hygiene (`v0.14.0`–`v0.16.0`)

- [One Way to Run a Command: the Binding Contract](02%20Runtime%20Architecture/43-one-way-to-run-a-command.md) — four ways to run a command become one canonical `Evaluate` then `InvokeAsync` path for every surface, the Macro Manager bug that ran every step against no context, and the build-time guard against a fifth mechanism.
- [Invariants That Fail the Build](02%20Runtime%20Architecture/44-invariants-that-fail-the-build.md) — Kind eligibility as two mechanisms held by one invariant (`ADR-0118`), and the five architectural rules `WP-H` turned from prose into tests that fail the build — rewritten again when the project graph changed.
- [Deleting Dead Architecture and Consolidating Duplicates](02%20Runtime%20Architecture/45-deleting-dead-architecture-and-consolidating-duplicates.md) — the retired v0.1 architecture deleted with proof, seven refresh tails and nine settings stores made one each, and nineteen CRUD methods moved out of `MainWindow` verbatim — why removal is riskier work than addition.
- [The UI Thread and Blocking Calls](02%20Runtime%20Architecture/46-the-ui-thread-and-blocking-calls.md) — what a thread and a blocking call are, the Cockpit that re-read itself eight times, the Undo toolbar that refreshed off the UI thread for five releases (`ADR-0119`), and the Windows startup crash of the same shape.
- [Desktop Productisation and Brand Recovery](02%20Runtime%20Architecture/47-desktop-productisation-and-brand-recovery.md) — the brand recovered, the dead-end chips, lost selections and invisible scrollbars found by driving the real application, and the Avalonia upgrade that made the desktop launch on Linux.
- [Durable State Schema Versioning](02%20Runtime%20Architecture/48-durable-state-schema-versioning.md) — every saved record carries a schema version, migrations apply only on read, enums serialise as strings, a golden corpus keeps proving old records still load, and the downgrade that was verified to be a one-way door (`ADR-0120`, `ADR-0121`, `ADR-0123`).
- [The Accessibility Baseline](02%20Runtime%20Architecture/49-accessibility-baseline.md) — modal dialogs that trap focus, names a screen reader can speak, a keyboard for the Digital Thread graph — and the focus ring that shipped invisible because its test could not fail.

### `v0.17.0` Reset and Substrates

- [The Engineering Calculation Workspace](02%20Runtime%20Architecture/50-the-engineering-calculation-workspace.md) — a real Desktop workflow over the governed calculation the platform already had, the catalogue registered in the harness but not the product, `ADR-0143`'s pinned reference revision, and why `D-028` left the surface in place and stopped investing in it.
- [Engineering Reference Data: Seven Libraries, One Catalogue Layer](02%20Runtime%20Architecture/51-engineering-reference-data.md) — seven reference libraries on one shared catalogue layer — provenance, a Draft-to-Released lifecycle, `ReferencePin` — and the permission gate on release the design-freeze review found missing (`ADR-0124`, `ADR-0126`).
- [Units as a Runtime Dimension Vector](02%20Runtime%20Architecture/52-units-as-a-runtime-dimension-vector.md) — why the phantom-type units could not multiply a force by a length, the seven-exponent dimension vector that can, automatic same-dimension conversion, affine temperatures, and thirty-seven property-based tests (`ADR-0147`).
- [SQLite Persistence](02%20Runtime%20Architecture/53-sqlite-persistence.md) — from a folder of files that was crash-safe but not power-loss durable to one SQLite database in WAL mode with full synchronous writes, an instance lock, and the intermittent that had been misdiagnosed as lock contention (`ADR-0144`).
- [One Transaction per Engineering Change](02%20Runtime%20Architecture/54-one-transaction-per-engineering-change.md) — object state, revisions, relationships, attachment bytes and the audit row commit as one transaction under one lock; memory is a cache applied after commit; 1,802 lines of compensation deleted (`ADR-0145`).
- [Configuration, Logging and the Session Principal](02%20Runtime%20Architecture/55-configuration-logging-and-the-session-principal.md) — `appsettings.json`, environment variables and a daily log file on Microsoft.Extensions — the first packages `Tempest.Core` ever took — and a session principal whose identity is the operating system's, not configuration's (`ADR-0146`).
- [Frozen Layers: Plugin Trust, REST, Licensing and P02–P07](02%20Runtime%20Architecture/56-frozen-layers.md) — what freezing code out of the build means, why plugin trust, REST, licensing and the six P02–P07 programmes were frozen rather than deleted, what stayed live, and what it would take to bring a layer back (`ADR-0146`, `D-028`).
- [Workspace and Harness: Splitting Tempest.App](02%20Runtime%20Architecture/57-workspace-and-harness.md) — `Tempest.App` becomes `Tempest.Workspace` (a library) and `Tempest.Harness` (a console), the shipped product stops depending on its own diagnostic tool, and `InternalsVisibleTo` gives way to a public contract (`ADR-0101` amended).
- [Where Things Land and Open: the First Windows Reviews](02%20Runtime%20Architecture/58-where-things-land-and-open.md) — the Product Owner's first Windows runs of `v0.17.0`: a Part that hung from nothing, panels that did not appear, a stale executable with nothing on screen to say so — and the four hotfix rounds that turned each finding into a guarantee.

### `v0.18.0` Evidence and Check

- [Evidence: the Record of a Calculation Done Elsewhere](02%20Runtime%20Architecture/59-evidence.md) — the product's central record: a calculation done elsewhere, kept as files, subject, citations pinned to released reference data, declared figures, check and issue — refusing rather than throwing, and not an ERP or a PLM (`ADR-0148`, `D-028`).
- [Source Citations and Supersession](02%20Runtime%20Architecture/60-source-citations-and-supersession.md) — a structured citation on every reference record, carried forward on revise; supersession proved by a property test over all five libraries; the plan that said 79 corrected to 41; and the warning that cannot yet fire (`ADR-0149`).
- [The Screen Follows the Store: Sequence, Change Feed and Snapshot](02%20Runtime%20Architecture/61-the-screen-follows-the-store.md) — a store sequence, a change feed raised once per commit and a coherent snapshot read, so every panel follows the data with no manual refresh and no blocking UI call — the close of the story chapter 46 opened.
- [Findability: Full-Text Search and Recently Changed](02%20Runtime%20Architecture/62-findability.md) — a full-text search index written in the same transaction as the object, the Command Palette's Objects section where the latest query wins, and a Recently changed card read from the audit trail.
- [The Evidence Workspace: File Picker, Editor Declarations and Libraries](02%20Runtime%20Architecture/63-the-evidence-workspace.md) — the Evidence rail area, a file picker behind an interface so a test can attach a real file, editors declared per Kind rather than branched, the Libraries tab — and the tab that never loaded because every test had refreshed it by hand.
- [Independent Check and the Issue Sheet](02%20Runtime%20Architecture/64-independent-check-and-the-issue-sheet.md) — a check stored verbatim with an independence rule built in but switched off by default, an issue sheet rendered as a PDF and attached to the record, supersession that keeps the issued revision immutable, and the three-transaction issue disclosed as a warning.

### `v0.19.0` Consultancy Seam and Desktop (release candidate)

- [The Project Commercial Core: Client, Rate Card, Time and Deliverables](02%20Runtime%20Architecture/65-the-project-commercial-core.md) — `WP 19.0A`, `ADR-0150`: a project gains a client, PO reference, budget and pinned Released rate card; a timesheet entry freezes its billing and cost rate on the day; a deliverable completes once; extended on `v0.20.0` by frozen payment terms, calculation-as-task and commercial identity at New Project.
- [Outbound Invoicing and the Connector Seam](02%20Runtime%20Architecture/66-outbound-invoicing-and-the-connector-seam.md) — `WP 19.1A` (parts 1–3), `19.1A-R1`, `19.9.0`, `19.10D`, `ADR-0151`: one connector seam to Xero and QuickBooks Online — a network call is a result state never a retry loop, the request's own id is the idempotency key, Paid is read never set, tokens live outside the database, and the double-draft defect the Desktop journey found one run in two.
- [Read Models: KPIs, Project Status, Tasks and Accounts](02%20Runtime%20Architecture/67-read-models-kpis-status-tasks-and-accounts.md) — `WP 19.1B`, `19.5C`, `19.7B`, `19.8B`, `20.1B`: the five `ADR-0150` KPI equations, six project statuses with a reason, eight task buckets, and a read-only accounts reading from the accounting package — every figure recomputed from the records, none stored.
- [The Layout Walk and the Composer](02%20Runtime%20Architecture/68-the-layout-walk-and-the-composer.md) — `WP 19.3A`, `19.3A-R1`, `19.2A`: every screen rendered headlessly at two window sizes and checked for overlap and overflow — with the tab-strip defect the walk could not see until it was asked — and the 830-line window constructor split into four named phases.
- [The Honest Rail and the Structure Tab](02%20Runtime%20Architecture/69-the-honest-rail-and-the-structure-tab.md) — `WP 19.2B`, `19.4A`: five declared-but-empty rail modules removed rather than dimmed, Engineering folded into a project's Structure tab, the rail-surface contract and automation-name tests, and the overlap and menu-bar fixes the Product Owner's first pass demanded.

### `v0.19.1` the Product Owner's first pass (release candidate)

- [Quotations, Change Orders and the Project Lifecycle](02%20Runtime%20Architecture/70-quotations-change-orders-and-the-project-lifecycle.md) — `WP 19.5A`–`D`, `19.10H`, `19.10R`, `20.10E`, `ADR-0152`: accepting a quotation creates the project's milestones, deliverables and requirements (disclosed as non-atomic), Hold/Resume/Sign off/Reopen with a ninety-day archive, one archived-project guard at the point availability is decided, and the change order finding D18 demanded.
- [The Shell as Sketched: Five Areas, Three Trees, Four Dashboards](02%20Runtime%20Architecture/71-the-shell-as-sketched.md) — `WP 19.7A`, `19.7B`, `19.10O`, `19.10Q`, `19.10C`, `19.10D`: the Product Owner's nine paper sketches become the rail (Home, Projects, Tasks, Engineering, Business), three trees, four dashboards and collapsible columns, with the five defects re-navigating nineteen suites found.
- [Real Files and Real Records: Attachments, the Library Editor and the Viewer](02%20Runtime%20Architecture/72-real-files-and-real-records.md) — `WP 19.4B`, `19.6A`, `19.10P`, `20.1C1`, `20.2B`: a real drop zone on every attachable Kind, a generic record editor across the eight libraries, content-addressed attachment storage with streamed reads, DWG opened externally, and the SVG kill switch invoked honestly.
- [The Technical-Debt Rationalisation and the Overnight Tranche](02%20Runtime%20Architecture/73-the-technical-debt-rationalisation-and-the-overnight-tranche.md) — `WP 19.10F`, `19.10G`, `19.10B`–`19.10R`: every backlog row re-verified against the code at one commit and scored P1–P4 — the register found wrong in both directions — then sixteen rows closed overnight with a test each.

### `v0.20.0` the debt tranche (release candidate) and the `v0.21.0` plan

- [The Debt Tranche: Writes on the Bus, Unique Identifiers, Index-First Rehydration](02%20Runtime%20Architecture/74-the-debt-tranche.md) — `WP 20.1A1`, `20.1A2`, `20.1C2`, `20.3A`, `20.3B`: Requirements writes on the change bus, business identifiers unique within a project, index-first rehydration with the kill switch invoked, evidence never falsely Issued, and seven small closures including a defect that was not there.
- [The Object Picker, Move and Copy, and Macros That Replay](02%20Runtime%20Architecture/75-the-object-picker-move-copy-and-macros.md) — `WP 20.2A`, `20.2C`: one object-picker dialog unblocks fifteen commands across two backlog rows, the Palette becomes contextual, the absence test retires itself, and macros record real values while still never running past a person's yes.
- [Tear-Out and Dock Everywhere: a Panel Can Never Be Lost](02%20Runtime%20Architecture/76-tear-out-and-dock-everywhere.md) — `WP 20.0A` (`ADR-0153`, Proposed), `WP 20.10D`, the `v0.21.0` plan: a panel can never be lost, as five tested invariants; why the cross-monitor docking design was written and costed (twenty days, not six) before any of it was built; and what `v0.21.0` plans.

## Design Patterns

Recurring structural patterns TempestOS actually uses, explained in terms of the real code that uses them.

- [The Registry Pattern](04%20Design%20Patterns/01-the-registry-pattern.md) — one place a system can ask "what do we know about X?" without the asker knowing how or where it is stored.
- [Descriptor and Snapshot Types](04%20Design%20Patterns/02-descriptor-and-snapshot-types.md) — information flows between pipeline stages as safe, immutable values no later stage can corrupt.
- [Minimal Interface, Extension-Method Sugar](04%20Design%20Patterns/03-minimal-interface-with-extension-sugar.md) — keep an interface's implementable surface to one real method while giving callers a full, convenient API through extension methods.
- [Reflection-Based Discovery](04%20Design%20Patterns/04-reflection-based-discovery.md) — the technique behind Module, Plugin and Hosted Service discovery, and the four disciplines that make it safe.
- [Phantom-Type Dimension Safety](04%20Design%20Patterns/05-phantom-type-dimension-safety.md) — the compile-time-safety pattern this framework introduces to TempestOS for the first time.

## Case Studies

Narrative deep-dives into one decision or one defect each.

- [Case Study: Why RuntimeModule Is Immutable](05%20Case%20Studies/01-why-runtimemodule-is-immutable.md) — an immutability decision that required a second, structurally duplicated type, and why it was still right.
- [Case Study: Why Lifecycle State Lives Externally](05%20Case%20Studies/02-why-lifecycle-state-lives-externally.md) — why a module's lifecycle state is held by the platform rather than by the module.
- [Case Study: Why Dispose Is Always Legal](05%20Case%20Studies/03-why-dispose-is-always-legal.md) — permissive disposal, defended under challenge, and how it generalised to the Host's own fault-recovery path.
- [Case Study: Why Discovery Is Isolated](05%20Case%20Studies/04-why-discovery-is-isolated.md) — why Discovery runs before, and independently of, dependency injection.
- [Case Study: Why Isn't Configuration Mutable?](05%20Case%20Studies/05-why-isnt-configuration-mutable.md) — why the configuration provider is immutable, and what Settings exists for instead.
- [The Attachment Sweep That Could Delete Live Content](05%20Case%20Studies/06-the-attachment-sweep-that-could-delete-live-content.md) — a data-loss race that shipped with its debt row already marked Resolved, the write-intent marker whose first fix had the ordering wrong, the lost update inside the fix for the first, and the transaction that finally made the whole class impossible.
- [Case Study: The Design-Freeze Review — Keep the Substrate, Remediate the Surface, Buy the Option to Pivot](05%20Case%20Studies/07-the-design-freeze-review.md) — continue or re-implement: the review that answered with reproducible figures, named its hazards, recommended keeping the substrate and remediating the surface, and led to `D-028` the next day.
- [The View That Stopped Listening](05%20Case%20Studies/08-the-view-that-stopped-listening.md) — `WP 19.7C`: thirteen screens whose change-feed subscription died on the first navigate-away, hidden for a release by the on-entry re-read; the fix tied to the control's own lifetime, and the difference between a test that passes on both versions and one that proves the fix.

## Engineering Standards

The conventions TempestOS holds itself to, and how they changed.

- [Engineering Standard: Exception Design](06%20Engineering%20Standards/01-exception-design.md) — the exception hierarchy convention every platform capability follows, and which deliberately introduce none.
- [Engineering Standard: Testing Strategy](06%20Engineering%20Standards/02-testing-strategy.md) — prefer real implementations over mocks, the internal-test-seam pattern, and the conventions every test project follows (see `07` for what the suite learned later).
- [Working with TempestOS's Governance Registers](06%20Engineering%20Standards/03-governance-registers.md) — why the governance register suite exists, how to maintain one, common mistakes.
- [Engineering Standard: Continuous Integration](06%20Engineering%20Standards/04-continuous-integration.md) — CI philosophy, the `.github/workflows/ci.yml` build pipeline, release verification, and the engineering workflow around it (`WP 11.1A`).
- [Engineering Standard: Release Engineering](06%20Engineering%20Standards/05-release-engineering.md) — branching strategy, pull request workflow, release process, versioning policy, and the emergency hotfix process (`WP 11.1B`).
- [Engineering Standard: Governance Automation](06%20Engineering%20Standards/06-governance-automation.md) — the automated Governance Health-Check Tool (`FCR-0005`), what it validates, and what it deliberately does not fix (`WP 11.2A`).
- [Test Determinism and Suite Hygiene](06%20Engineering%20Standards/07-test-determinism-and-suite-hygiene.md) — fixed delays replaced by real joins, tests that could not fail, console capture that serialised the suite, one host fixture for ninety-three copies, property assertions, mutation testing — and the rules a contributor follows now.
- [The Physical Review and the Release Gate](06%20Engineering%20Standards/08-the-physical-review-and-the-release-gate.md) — `PHYSICAL_REVIEW.md`, the CI Gate that requires the health check, the release path that was weaker than the merge path, and the gate as it is actually run on a release-candidate head.
- [The Governance Reset, and How a Release Is Now Run](06%20Engineering%20Standards/09-the-governance-reset-and-how-a-release-is-now-run.md) — why 807 governance files were archived, what `CONTRIBUTING.md` and `BACKLOG.md` replaced, the Markdown budget, and how a release is now run from an Execution Plan in waves with a gate at every merge.
- [The Product Owner's Test as the Source of Record](06%20Engineering%20Standards/10-the-product-owners-test-as-the-source-of-record.md) — How the Product Owner's manual test drives a release: comments transcribed verbatim as the source of record, a rehearsal of the review script before the reviewer sees it, decisions taken in the Product Owner's absence disclosed as defaults, and the `v0.20.0` findings closed the same day.
- [CI in Shards, and the Gate at Scale](06%20Engineering%20Standards/11-ci-in-shards-and-the-gate-at-scale.md) — `WP 19.9.1`, `19.10M`, `20.3D`, `20.9.0`: the Desktop suite outgrows its CI ceiling, the build-script bugs found while buying time back, the suite sharded three ways by namespace, and the race a faster gate exposed; what `v0.21.0` still leaves advisory.
- [TempestOS Engineering Governance](06%20Engineering%20Standards/Engineering%20Governance.md) — the project's constitution from `WP 2.1` to `v0.16.0`: Work Package lifecycle, review gates, Definition of Done, ADR rules, decision authority. Read with its September 2026 status note; `CONTRIBUTING.md` governs today.
- [Engineering Lifecycle](06%20Engineering%20Standards/Engineering%20Lifecycle.md) — the Idea → Investigation → Architecture → ADR → Implementation → Testing → Review → Release pipeline, elaborating Governance §1; three stages changed at `WP 17.0B`.

## Repository-wide

- [Contributor Learning Path](Contributor%20Learning%20Path.md) — the ordered reading sequence across `README.md`, `docs/releases/`, this Academy, `docs/architecture/`, `docs/adr/` and `CONTRIBUTING.md`, with a step 0 for readers who are not software engineers.

## Archived Academy material (`WP 17.0B`, 2026-09-08)

- `archive/docs-2026-09/academy/01 Engineering Principles/` — SOLID, Separation of Concerns, Immutability, Composition over Inheritance, Dependency Injection, Fail Fast, Deterministic Systems, State Machines, Defensive Programming, Single Responsibility, the Atomic Phase Principle. The vocabulary the older chapters assume.
- `archive/docs-2026-09/academy/03 Work Packages/` — one thirteen-section retrospective per Work Package from `WP 2.1` to `WP 16.5B`, about 240 documents; the deepest record of *why* for everything up to `v0.16.0`.
- `archive/docs-2026-09/academy/Academy Index.md`, `Academy Audit Report.md`, `Academy Masterclass Roadmap.md` — the previous index, the `WP 4.4F` audit, and the masterclass candidates (none written).

## Maintaining this index

One line per document. A Work Package that adds an Academy document adds its line here in the same pull request; nothing else about this file needs to change.
