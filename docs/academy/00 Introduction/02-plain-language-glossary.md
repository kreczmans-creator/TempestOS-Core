# Plain-Language Glossary

Keep this open beside every other Academy chapter. One entry per term.
Where TempestOS uses a word differently from its everyday software
meaning, the general meaning comes first, then "In TempestOS:". A **bold**
word inside a definition is itself an entry.

---

## 1. Software and engineering terms

- **ADR (Architecture Decision Record).** A short document recording one
  technical decision: what was true before, what was chosen, and what it costs
  as well as what it buys. In TempestOS: one file per decision under
  `docs/adr/` (`ADR-0148`, and so on); required for "any decision that
  constrains future code." See: `01-how-tempestos-gets-built.md`.
- **API.** A defined way one piece of software can ask another to do
  something, without needing to know how it works inside. In TempestOS: an
  inbound REST API (`Tempest.Core.Api`) exists but is not part of the shipped
  product — see **Frozen layer**.
- **assembly.** In .NET, one compiled unit of code: a library (a `.dll`) or an
  executable (a `.exe`). In TempestOS: `Tempest.Core`, `Tempest.Workspace`,
  `Tempest.Harness` and `Tempest.Desktop` are the four shipped assemblies, and
  a build-breaking test checks which may depend on which. See:
  `57-workspace-and-harness.md`.
- **asynchronous.** Work started without the caller sitting and waiting for it
  to finish, so the program can keep responding meanwhile. In TempestOS:
  saving to disk is asynchronous so the screen never freezes — see **thread/UI
  thread**.
- **backlog.** A list of known work not yet done. In TempestOS: `BACKLOG.md`,
  the live technical-debt list, capped at 30 rows.
- **branch.** A private, named copy of the code, changed without affecting
  anyone else's, until merged back. In TempestOS: one branch, and one git
  **worktree**, per **Work Package**.
- **build.** Turning source code into a runnable program; also, one such
  runnable copy. In TempestOS: a build must reach zero warnings and zero
  errors, in both configurations, to be accepted — see **warning-as-error**.
- **CI (Continuous Integration).** Automatically building and testing every
  proposed change before it may merge. In TempestOS: the required "CI Gate"
  check (`.github/workflows/ci.yml`) must pass before anything reaches `main`.
- **class/type.** A blueprint, in code, for a kind of value: what data it
  holds and what it can do. In TempestOS: for example `LifecycleState`,
  `EngineeringObjectBase`.
- **commit.** One saved, named snapshot of a change, with a message explaining
  what and why. In TempestOS, commit messages are treated as a primary record
  of intent — the Academy is written largely from them.
- **compile.** Translate source code into a form the computer can run,
  checking it makes sense along the way. In TempestOS, **warning-as-error**
  means many things other projects tolerate stop the compile outright.
- **composition root.** The one place in a program where all its pieces are
  wired together at start-up. In TempestOS: `EngineeringWorkspaceComposer`,
  which both `Tempest.Desktop` and `Tempest.Harness` run at start. See:
  `10-shell-and-application-composition.md`.
- **dependency.** Code, or another project, that a piece of code needs to run.
  In TempestOS, dependencies flow one way only — checked by
  `DependencyDirectionTests`, an **invariant**.
- **dependency injection.** Giving an object the services it needs from
  outside rather than having it build them itself, so a test can swap one in.
  In TempestOS: one of `Tempest.Core`'s six **platform service**s.
- **deterministic.** Given the same start, always behaving the same way. In
  TempestOS: the **Host**'s startup and shutdown sequence is deterministic —
  modules start and stop in a fixed order.
- **event.** A signal that something happened, broadcast so any interested
  code can react without knowing the sender. In TempestOS: `Tempest.Core`'s
  Event Bus.
- **exception.** An error that stops normal execution unless something catches
  it. In TempestOS, an ordinary engineering refusal (citing an unreleased
  record, say) is deliberately *not* one: `EvidenceService` returns it as a
  result instead, so a screen shows a message, not a crash. See:
  `59-evidence.md`.
- **fixture (test fixture).** A known, fixed starting state a test runs
  against, so its result depends only on the code under test. In TempestOS,
  the persistence store's contract tests run once against a shared fixture for
  every storage backend.
- **headless.** Running a program — especially one with a screen — without an
  actual screen attached. In TempestOS, `Tempest.Desktop.Tests` render real
  windows headlessly: the real layout code runs, monitor or not.
- **immutable.** A value that cannot change once created; a "change" produces
  a new value instead. In TempestOS, `RuntimeModule` is immutable by design,
  so no caller can corrupt what the platform relies on. See: `05 Case
  Studies/01-why-runtimemodule-is-immutable.md`.
- **index.** A structure built over stored data purely to make lookup fast. In
  TempestOS, the old file-per-key store had none (a full scan for any
  whole-collection read); product search is now backed by a SQLite full-text
  index.
- **interface.** A named contract listing what code must be able to do,
  without saying how, so implementations can be swapped. In TempestOS: for
  example `IPersistenceStore`, `IWorkspace`.
- **invariant.** A rule that must always hold, whatever else happens. In
  TempestOS, five platform rules are each enforced by a test that fails the
  build the instant the rule breaks, rather than left as a sentence nobody
  re-checks. See: `44-invariants-that-fail-the-build.md`.
- **journey/acceptance test.** A test that drives the whole running product as
  a real user would, through a complete task, rather than one small piece in
  isolation. In TempestOS, one such test first revealed that engineering
  objects did not survive a restart. See: `33-the-product-spine.md`.
- **lifecycle.** The sequence of states something passes through from creation
  to retirement. See **lifecycle state** below for TempestOS's own vocabulary.
- **lock.** A mechanism stopping two processes changing the same thing at
  once. In TempestOS, `SqlitePersistenceStore` holds an exclusive lock for its
  whole run, so a second TempestOS on the same data folder is refused
  outright.
- **merge.** Combining one branch's changes into another. In TempestOS, only
  the lead merges, after the **gate** is green and one review round is done.
- **migration.** Moving data, or a system, from an old shape to a new one. In
  TempestOS, a **schema** migration runs once, only when an old record is
  actually read. See: `48-durable-state-schema-versioning.md`.
- **mock/stub/double.** A stand-in for a real dependency, used in a test so
  the test need not use the real, slow or unpredictable thing. In TempestOS,
  Desktop acceptance tests deliberately avoid mocking the screen itself,
  rendering real windows instead.
- **module.** A self-contained unit of a program with a defined boundary. In
  TempestOS, the unit discovered, registered and run through the platform's
  own Module Pipeline. See: `01-the-module-pipeline.md`.
- **mutation test.** Deliberately planting small bugs in working code and
  checking the test suite notices each one; a mutation that survives means a
  test is missing. In TempestOS, one such mutation found that a task's status
  change had stopped actually saving, unnoticed by every existing test. See:
  `41-project-tasks-and-delivery-workflow.md`.
- **namespace.** A named grouping of code that keeps names from clashing. In
  TempestOS: for example `Tempest.Core.ReferenceData`; six whole namespaces
  were removed from the shipped build — see **Frozen layer**.
- **persistence.** Saving something so it still exists after the program
  restarts. See: `53-sqlite-persistence.md`.
- **principal.** The identity a running action is carried out as. In
  TempestOS, `ICurrentPrincipalAccessor` names the signed-in identity behind
  an action, which is what lets the independent-**check** rule refuse a
  principal checking their own work.
- **property-based test.** A test stating a rule that must hold for *any*
  input, checked against many computer-generated cases rather than a handful
  chosen by hand. In TempestOS, one generates a hundred random sequences
  against a reference record and checks every **citation** still resolves
  correctly afterwards. See: `60-source-citations-and-supersession.md`.
- **pull request (PR).** A request to merge a branch, with the write-up of
  what changed and why attached. In TempestOS the PR description *is* the
  retrospective, and may not add more Markdown than code — see **Markdown
  budget**.
- **race condition.** A bug caused only by unpredictable timing between two
  things happening at once. In TempestOS, one was found in the order SQLite's
  start-up settings are applied — issuing one out of order could make a
  concurrent connection fail. See: `53-sqlite-persistence.md`.
- **refactor.** Restructuring code internally without changing what it does
  from the outside.
- **regression.** A bug where something that used to work stops working. In
  TempestOS, a "golden corpus" of seven real, pre-upgrade saved records is
  loaded on every test run so a future change can never silently break reading
  old data.
- **repository.** Two meanings appear here. (1) In git, the whole stored
  history of a project. (2) In software design, an object standing between the
  rest of the code and where data lives, so the code need not know storage
  details. In TempestOS, `IEngineeringObjectRepository` holds the in-memory
  engineering object graph; a project's own, now-retired
  `JsonProjectRepository` wrote straight to disk instead, bypassing the real
  store, audit and **lifecycle** entirely. See: `33-the-product-spine.md`.
- **schema.** The shape of a saved record: which fields it has, what each
  means. See: `48-durable-state-schema-versioning.md`.
- **serialise.** To turn an in-memory value into a storable format (commonly
  text), and back ("deserialise"). In TempestOS, enum values now serialise as
  their name rather than a bare number, so reordering the enum in code can
  never silently reinterpret an old saved value.
- **snapshot.** A fixed copy of changing information, taken at one moment,
  that does not update itself later. In TempestOS, `ModuleLifecycleStatus` is
  a fresh snapshot rebuilt on every query, distinct from `RuntimeModule`'s own
  permanent record. See: `04 Design
  Patterns/02-descriptor-and-snapshot-types.md`.
- **tag.** In git, a permanent name pinned to one commit — for example
  `v0.18.0`. TempestOS also uses "tag" for an unrelated idea: a plain,
  unchecked pointer to another record's name — see **subject (as a tag)**.
- **thread/UI thread.** A thread is one independent sequence of instructions;
  a single-threaded program does everything strictly one at a time. In
  TempestOS, the UI thread is the one thread allowed to draw the screen, and a
  build-breaking rule bars any call on it that would sit and wait for a save,
  so a slow write can never freeze the window.
- **transaction.** A unit of work guaranteed to complete in full or leave no
  trace of starting — never half-done. See:
  `54-one-transaction-per-engineering-change.md`.
- **unit test.** A test checking one small piece of behaviour in isolation —
  for example, that a disallowed status change is refused.
- **WAL (write-ahead log).** A way a database avoids leaving a half-write
  behind: a change is appended to a separate log first and folded back in
  later, so a crash mid-write leaves the original file untouched. See:
  `53-sqlite-persistence.md`.
- **warning-as-error.** A build setting turning every compiler warning into a
  build failure. In TempestOS, `TreatWarningsAsErrors` is required for every
  build, on the reasoning that a warning ignored today is a defect tomorrow.
- **worktree.** A second working folder checked out from the same git
  repository, so more than one branch can be worked on at once. In TempestOS,
  one worktree per **Work Package** lets several pieces of work proceed in
  parallel without contending for the same files.

---

## 2. TempestOS's own vocabulary

- **accepted trade-off (`AT-`).** A deliberate compromise the project lives
  with rather than fixes — for example, a small, bounded set of files allowed
  to bypass normal command dispatch because fixing it would cost more than it
  buys. See: `44-invariants-that-fail-the-build.md`.
- **archive.** Documents (not code) superseded by the `WP 17.0B` governance
  reset, moved with `git mv` so their history stays intact, under
  `archive/docs-2026-09/`.
- **attachment.** A file linked to an engineering object. Once only a
  *description* of a file, an attachment's actual bytes are now stored in the
  same durable store as everything else. See:
  `37-attachment-content-storage.md`.
- **canonical Kind.** A Kind built to the platform's own base shape (identity,
  revisions, attachments and audit inherited from one common base) rather than
  a one-off. Evidence is the newest example. See: `59-evidence.md`.
- **check.** The independent review of a piece of Evidence, recorded as a
  plain value rather than a workflow of its own; when switched on, the checker
  cannot be the same person as the author. See: `59-evidence.md`.
- **chief engineer / lead.** The chief engineer decides *how* the Product
  Owner's decisions get built, and writes a release's **Execution Plan**. "The
  lead" is the same role acting day to day — merging branches, running the
  **gate**, re-briefing work that goes off course.
- **citation / `ReferencePin`.** A citation records that work relied on a
  specific reference record. A `ReferencePin` is the exact, permanent address
  of that reliance — library, record and revision, taken from the record
  actually read, never guessed. See:
  `60-source-citations-and-supersession.md`.
- **Cockpit.** The Engineering Cockpit, the cross-project landing screen: a
  summary (status, recently changed objects), not a place work is done.
- **command / descriptor / binding.** A command is anything the product can be
  asked to do. A descriptor declares what a command needs and which Kinds it
  applies to. A binding turns a descriptor and the current selection into a
  runnable command, reporting one of three outcomes: ran, declined, or
  unavailable — never a silent failure. See: `43-one-way-to-run-a-command.md`.
- **Command Palette.** A keyboard-invoked, searchable list of every command
  the product can run, as an alternative to the **Ribbon**.
- **compiling checkpoint.** A commit made within the first hour of a Work
  Package, deliberately kept buildable, so half-finished work is never at risk
  of being lost.
- **Core.** `Tempest.Core`, the platform itself: the Host, its platform
  services, persistence, commands, and engineering objects' base types.
  Depends on nothing else in the repository.
- **decision record (`D-`).** A record of a decision that changes the *scope*
  of what is being built, as opposed to an ADR's technical decision. `D-028` —
  stopping calculation-engine work in favour of recording calculations as
  Evidence — is the model. See: `59-evidence.md`.
- **Desktop.** `Tempest.Desktop`, TempestOS's one shipped, graphical product:
  the Ribbon, the Project Explorer, the Object Editors.
- **discipline.** One self-contained engineering area of the Workspace, with
  its own Kinds and commands — Mechanical, Documents, Requirements,
  Verification, Manufacturing, Calculations, and now Evidence.
- **engineering object.** Any saved thing an engineer works with — a Part, a
  Requirement, a piece of Evidence — built on one shared base so identity,
  revisions, attachments, audit and restart behaviour all work the same way
  regardless of **Kind**.
- **Evidence.** A canonical **Kind** recording proof of engineering work done
  elsewhere: the files, what it is about, which reference records it relied
  on, its key figures, who checked it, when it was issued. See:
  `59-evidence.md`.
- **Execution Plan.** The chief engineer's plan for a release: what the
  Product Owner will get, Work Packages grouped into **wave**s, which files
  each one owns, and the manual test script the build must pass.
- **Frozen layer.** Code that once shipped but is deliberately excluded from
  the `v1.0` build — no project references it, nothing compiles or is tested —
  kept rather than deleted so it can return, re-reviewed, if a client ever
  needs it. The plugin trust platform, the REST API, Licensing, and most of
  P02–P07 are frozen this way. See: `src/Frozen/README.md`.
- **Future Capability (`FCR-`).** A capability not being built yet, numbered
  so a later decision to build it, or not, has something concrete to refer
  back to.
- **gate.** The full set of checks a change or release must pass: a **build**
  at zero warnings and errors in both configurations, every test passing, the
  architecture **invariant**s green, and — for a release — three consecutive
  green CI runs.
- **Harness.** `Tempest.Harness`, a text-only console tool over the same
  shared **Workspace** code the real product runs — a fast, scriptable way to
  exercise the platform, not a second product. See:
  `57-workspace-and-harness.md`.
- **health check.** An automated script run in CI checking the repository's
  own governance rules — for example, the **Markdown budget**.
- **Host / `TempestHost`.** The Runtime Host: discovers, registers and starts
  every **module** in a fixed, **deterministic** order, and reverses that
  order on shutdown.
- **issue sheet.** The document produced when Evidence is formally issued —
  its record, citations and figures rendered onto one printable sheet,
  attached back to the record.
- **kill switch.** A rule built into every Work Package: stop and report,
  rather than improvise, if the goal cannot be met without touching a file
  outside the list the plan assigned.
- **Kind.** The platform's own term for a named category of engineering object
  — Part, Requirement, Evidence — each with its own rules. See:
  `15-engineering-data-model.md`.
- **lifecycle state.** `LifecycleState`, the platform's own canonical,
  eight-state vocabulary for where an object stands (Draft, Released,
  Superseded, and so on). Some Kinds specialise it with their own closed set
  of states instead — Requirement and Evidence both do. See: `59-evidence.md`.
- **Markdown budget.** The rule that a pull request may not add more lines of
  Markdown than lines of code, so the project's own process can never again
  outweigh the product it protects.
- **Object Editor.** The screen that opens an engineering object; it works
  generically for any **Kind**, driven by that Kind's own declared facets
  rather than a bespoke screen per Kind.
- **physical review.** A person who did not build the release running it by
  hand, on a clean machine, following `PHYSICAL_REVIEW.md` — a
  ten-to-fifteen-minute smoke test that has repeatedly found real defects no
  automated test caught.
- **platform service.** One of six foundational capabilities every module is
  built on: Configuration, Logging, Discovery, Registration, **dependency
  injection**, and Lifecycle.
- **plugin.** Externally loaded code the platform can run beyond its built-in
  modules. The full plugin trust platform — signing, a trust store, capability
  enforcement — is not part of the shipped build (see **Frozen layer**);
  discovering what sits in the plugin folder, without trusting any of it,
  still runs live.
- **Product Owner.** The person who decides what TempestOS should be and what
  is worth building — not a software engineer, and the Academy's primary
  non-technical reader.
- **project / portfolio / programme.** The engineering hierarchy an object
  sits inside: a Project belongs to a Programme, which belongs to a Portfolio.
  Distinct from the seven business **programme**s (P01–P07) in Section 3, an
  unrelated use of the same word. See: `33-the-product-spine.md`.
- **Project Explorer.** The tree view listing a project's engineering objects,
  grouped by discipline area.
- **Property Inspector.** The panel showing the selected object's own fields —
  name, status, and so on.
- **rail / area.** The rail is the row of top-level destinations shown in the
  Desktop application; each destination is one "area" (`ShellArea`).
  Navigation is held as one single value naming the current area, so two parts
  of the screen can never disagree about where the user is. See:
  `33-the-product-spine.md`.
- **reference data / library / record.** Reference data is a fact an engineer
  relies on but did not measure personally. A library is one subject's worth
  of it (Materials, Fasteners, Bearings, Standards, Constants, and more); a
  record is one such fact, with its own provenance and **lifecycle**. See:
  `51-engineering-reference-data.md`.
- **release.** A named group of Work Packages with a theme, shipped together
  (`v0.17.0`, `v0.18.0`), not finished until it passes the **gate** and the
  **physical review**.
- **Released / Draft.** The two ends of a record's lifecycle. Draft means
  unverified — nothing may rely on it. Released means a named person has
  checked it and work may depend on it; citing an unreleased record is refused
  outright.
- **revision.** A new, separate version of an object created by revising it;
  the previous revision is never edited or lost, staying readable through the
  object's own history forever.
- **Ribbon.** The row of grouped command buttons across the top of the Desktop
  application.
- **seam.** A single point in the code every path to a particular outcome is
  made to pass through, so a rule enforced there cannot be bypassed by an
  undiscovered second route. `EvidenceService` is one: every act with a
  precondition goes through it, reporting a refusal as a result rather than
  throwing. See: `59-evidence.md`.
- **subject (as a tag).** The Part, Assembly or Requirement a piece of
  Evidence is about, held as a bare id and never checked as a real structural
  link — deliberately, so the object tree stays one strict hierarchy rather
  than a web of relationships. See: `59-evidence.md`.
- **technical debt row (`TD-`).** One numbered, honestly recorded shortcoming
  in `BACKLOG.md`, mapped to the Work Package expected to close it, or marked
  unowned.
- **wave.** A group of Work Packages in an Execution Plan that can run at once
  because they touch different files; a wave merges as a group, and the
  **gate** runs after every merge.
- **Work Package.** One numbered, scoped piece of work — goal, dependencies,
  an estimate in developer-days — that becomes one **branch** and one **pull
  request**. Named like `WP 18.0A`. See: `01-how-tempestos-gets-built.md`.
- **Workspace.** `Tempest.Workspace`, the shared engineering-domain library —
  what a project contains and what each **discipline** can do to it — used by
  both `Tempest.Desktop` and `Tempest.Harness`.

---

## 3. The programme names (P01–P07 / Group A–F)

Seven business and engineering-reasoning programmes were designed
alongside the core product; most were removed from the shipped `v1.0`
build by `WP 18.0C` once `D-028` re-scoped what the product needed —
see **Frozen layer**. Sources: `docs/architecture/Group *.md`,
`docs/architecture/P04 Business OS.md`, `src/Frozen/README.md`.

- **P01 — Engineering Reference Data (Group A).** Seven reference libraries
  (Materials, Standards, Fasteners, Bearings, Components, Constants,
  Manufacturing) sharing one provenance-and-lifecycle layer. **Not frozen** —
  the live substrate Evidence citations are built on.
- **P02 — Engineering Intelligence (Group B).** Material-selection logic,
  manufacturing decision trees, design rules, review logic and a trade-off
  framework, reasoning built on P01's facts. **Frozen in full**.
- **P03 — Commercial Intelligence (Group D).** Who could make a part, what it
  would cost, how long it would take, which supplier to use — every answer
  sourced and dated. **Frozen in full**.
- **P04 — Business OS** (no Group letter). What the business is doing: who it
  deals with, what it has committed to spend, what it ordered, what must be
  kept. **Mostly frozen** — only its Organisation, Contact and Budget
  catalogues stay live.
- **P05 — Engineering Assets (Group E).** Reusable artefacts engineering work
  produces: calculation packs, templates, verification evidence, design
  reviews, document issue control. **Mostly frozen** — Calculation Packs,
  Templates and Verification stay live.
- **P06 — AI Knowledge & Academy (Group F).** How to ask for engineering work
  well, learning paths, discussion questions, worked examples — deliberately
  not an automated tutor. **Frozen in full**.
- **P07 — Business Governance & Scale (Group C).** Contracts, the risk
  register, IP and data protection, pricing, financial forecasting, the sales
  pipeline, the operating model needed to scale. **Mostly frozen** — Money,
  currency codes, effective periods and the rate-card catalogue stay live.
