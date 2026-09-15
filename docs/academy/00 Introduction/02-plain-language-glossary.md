# Plain-Language Glossary

Keep this open beside every other Academy chapter. One entry per term.
Where TempestOS uses a word differently from its everyday software
meaning, the general meaning comes first, then "In TempestOS:". A **bold**
word inside a definition is itself an entry. Some entries below describe
work that exists only on an unreleased release candidate; where that
matters, the entry says so rather than implying it has shipped.

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
- **content-addressed storage.** Storing a piece of data under a name derived
  from its own contents (typically a hash), rather than an arbitrary id, so
  two copies of the same bytes are recognised as identical and stored once.
  In TempestOS: `AttachmentContentStore` keys attachment bytes by their
  SHA-256 hash with a reference count, so two attachments of the same file
  share one stored copy and deleting one leaves the other's content intact
  (`WP 20.1C1`, `TD-95`).
- **dashboard.** A screen showing a summary of many things at a glance —
  tiles, lists and charts — built entirely from what already exists, never a
  place work is done. In TempestOS: Home, Projects, Engineering and Business
  each have one, reading the platform's own **read model**s rather than
  computing anything themselves; an empty panel always says why rather than
  showing a blank space. See: `67-read-models-kpis-status-tasks-and-accounts.md`.
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
- **floating / torn-out window.** Pulling a panel free of its normal docked
  position so it becomes its own free-standing window, movable anywhere on
  screen or onto another monitor. In TempestOS: every panel in
  `WorkspaceLayoutTree` can already float this way; the unreleased `ADR-0153`
  (Proposed) widens "torn out" to a project's own tabs and rail area panes
  too, across monitors, not one window alone.
- **headless.** Running a program — especially one with a screen — without an
  actual screen attached. In TempestOS, `Tempest.Desktop.Tests` render real
  windows headlessly: the real layout code runs, monitor or not.
- **idempotency (idempotency key).** A property of an operation that gives the
  same result no matter how many times it is repeated with the same input, so
  a message that arrives twice is still acted on once; an idempotency key is
  the value that tells the receiving system "this is that same request
  again." In TempestOS: sending an invoice passes the request's own id as the
  idempotency key, so a retry after a lost response can never create a second
  invoice for the same request. See: `66-outbound-invoicing-and-the-connector-seam.md`.
- **immutable.** A value that cannot change once created; a "change" produces
  a new value instead. In TempestOS, `RuntimeModule` is immutable by design,
  so no caller can corrupt what the platform relies on. See: `05 Case
  Studies/01-why-runtimemodule-is-immutable.md`.
- **index.** A structure built over stored data purely to make lookup fast. In
  TempestOS, the old file-per-key store had none (a full scan for any
  whole-collection read); product search is now backed by a SQLite full-text
  index.
- **information architecture.** How a product's screens and navigation are
  organised — what sits under what, and what a person expects to find where —
  decided before the screens themselves are built. In TempestOS: the Product
  Owner sketched the shell's own information architecture by hand (Home,
  Projects, Tasks, Engineering, Business, each with its own dashboard), and
  the rail was rebuilt to match it. See: `docs/releases/v0.19.1/Product Owner
  Comments.md`.
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
- **KPI (Key Performance Indicator).** A single figure meant to say, at a
  glance, how well something important is going. In TempestOS: five KPIs are
  defined as exact equations in `ADR-0150`, computed fresh each time rather
  than stored — **utilisation** (billable hours divided by available hours,
  per principal); **margin** (billable value plus fixed-price value, minus
  cost, per project); **work in progress** (billable value not yet
  invoiced); **days sales outstanding** (the average days an invoice
  takes to be paid, read from the connector — "unavailable," never a false
  zero, when none is authorised); and **calc throughput** (calculations
  issued as evidence in the period).
- **layout walk.** An automated check that opens every screen of an
  application in turn and inspects its layout — nothing overlapping, nothing
  spilling outside its own space — so a broken screen is caught by a machine
  before a person notices it. In TempestOS: `LayoutWalkTests` drives the real
  `MainWindow` headlessly through every rail entry and project tab at two
  window sizes, screenshotting each one as a CI artefact, and has found and
  fixed real overlap and overflow defects. See:
  `68-the-layout-walk-and-the-composer.md`.
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
- **mutation score.** A percentage from mutation testing: the share of small,
  deliberately planted bugs ("mutants") that the test suite actually notices,
  out of every mutant planted. A low score means tests exist without really
  checking the behaviour they claim to. In TempestOS, Stryker.NET runs against
  `Tempest.Core`'s Calculations and Units-and-Quantities code against a 70%
  break threshold; the unreleased `v0.21.0` plan records 67.58%, below that
  line, with a Work Package to raise it.
- **mutation test.** Deliberately planting small bugs in working code and
  checking the test suite notices each one; a mutation that survives means a
  test is missing. In TempestOS, one such mutation found that a task's status
  change had stopped actually saving, unnoticed by every existing test. See:
  `41-project-tasks-and-delivery-workflow.md`.
- **namespace.** A named grouping of code that keeps names from clashing. In
  TempestOS: for example `Tempest.Core.ReferenceData`; six whole namespaces
  were removed from the shipped build — see **Frozen layer**.
- **OAuth / PKCE / loopback listener.** OAuth is a standard way of letting a
  person sign in to their own account on another service without ever giving
  that password to the requesting application; PKCE is an extra proof step
  that stops a stolen authorisation code being reused elsewhere; a loopback
  listener is a small, temporary web address on the same machine
  (`127.0.0.1`) that catches the one-time reply once the person has approved
  access in their browser. In TempestOS: connecting Xero or QuickBooks Online
  opens the operator's own browser to sign in, and `OAuthAuthoriser` catches
  the reply on a fixed local port (`49301`), registered ahead of time with
  the provider. See: `66-outbound-invoicing-and-the-connector-seam.md`.
- **offensive security audit / dependency scan.** An offensive security audit
  is a deliberate attempt to break a system's own defences with real
  proof-of-concept attacks, rather than merely reading the code for
  weaknesses; a dependency scan automatically checks every third-party
  library a product uses against known published vulnerabilities. In
  TempestOS: the security posture statement makes a dependency scan a
  required CI check; the unreleased `v0.21.0` plan adds a full offensive
  audit of every network-facing or file-parsing surface (connectors, the
  OAuth loopback listener, Open externally, macros, import) with findings
  closed in the same tranche, not merely filed.
- **persistence.** Saving something so it still exists after the program
  restarts. See: `53-sqlite-persistence.md`.
- **poller / hosted service.** A hosted service is background work the
  platform starts and stops itself, alongside the rest of the application,
  rather than something a user opens; a poller is a hosted service that wakes
  on a fixed interval, checks something, and goes back to sleep. In
  TempestOS: `InvoiceReconciliationService` is the platform's own
  `IHostedService` contract's first real consumer — it polls every
  connector-authorised invoice request every fifteen minutes by default, so a
  paid invoice is noticed without anyone clicking Reconcile. See:
  `66-outbound-invoicing-and-the-connector-seam.md`.
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
- **read model.** A number or list computed fresh from the stored records at
  the moment a screen asks for it, and discarded the instant the screen moves
  on — never written down as a fact in its own right. In TempestOS: the five
  KPIs, a project's status, the task buckets and the cash-flow figures are
  all read models, so none of them can go stale the way a stored count could.
  See: `67-read-models-kpis-status-tasks-and-accounts.md`.
- **refactor.** Restructuring code internally without changing what it does
  from the outside.
- **regression.** A bug where something that used to work stops working. In
  TempestOS, a "golden corpus" of seven real, pre-upgrade saved records is
  loaded on every test run so a future change can never silently break reading
  old data.
- **rehearsal (dress rehearsal).** A full run-through of a review before the
  real thing, so problems in the review itself — not only the product — are
  found first. In TempestOS: `WP 19.10A`'s dress rehearsal walked
  `PHYSICAL_REVIEW.md`'s own nineteen steps in a headless test host ahead of
  the Product Owner's manual test, finding and fixing defects in the review
  document's own wording as well as real product defects.
- **release candidate / fallback candidate.** A release candidate is a build
  offered for final testing, not yet the released product; a fallback
  candidate is the most recent release candidate that has already passed its
  own full test gate, kept ready in case a newer candidate fails. In
  TempestOS: `v0.19.0`, `v0.19.1` and `v0.20.0` are all release candidates,
  none merged to `main`; `v0.19.1` is the fallback candidate while `v0.20.0`
  is under the Product Owner's manual test.
- **repository.** Two meanings appear here. (1) In git, the whole stored
  history of a project. (2) In software design, an object standing between the
  rest of the code and where data lives, so the code need not know storage
  details. In TempestOS, `IEngineeringObjectRepository` holds the in-memory
  engineering object graph; a project's own, now-retired
  `JsonProjectRepository` wrote straight to disk instead, bypassing the real
  store, audit and **lifecycle** entirely. See: `33-the-product-spine.md`.
- **schema.** The shape of a saved record: which fields it has, what each
  means. See: `48-durable-state-schema-versioning.md`.
- **secret store (DPAPI).** A place credentials are kept deliberately separate
  from a product's ordinary saved data, so a backup or export of the data
  never also hands out the credentials; DPAPI (Data Protection API) is a
  Windows feature that encrypts a file so only the same signed-in Windows user
  can decrypt it again. In TempestOS: `ISecretStore` keeps a connector's own
  tokens under `<persistence root>/secrets/`, never inside `tempest.db`;
  `WindowsDpapiSecretStore` encrypts each one with DPAPI, and a disclosed,
  unencrypted file-based fallback exists for every other operating system.
  See: `66-outbound-invoicing-and-the-connector-seam.md`.
- **serialise.** To turn an in-memory value into a storable format (commonly
  text), and back ("deserialise"). In TempestOS, enum values now serialise as
  their name rather than a bare number, so reordering the enum in code can
  never silently reinterpret an old saved value.
- **shard (CI).** Splitting one long automated test run into several smaller
  ones that run at the same time, so the whole check finishes sooner without
  testing anything less. In TempestOS: `WP 20.3D` split the Desktop test
  suite into three CI shards by namespace, bringing the Build & Test ceiling
  back down from 90 minutes to 45.
- **snapshot.** A fixed copy of changing information, taken at one moment,
  that does not update itself later. In TempestOS, `ModuleLifecycleStatus` is
  a fresh snapshot rebuilt on every query, distinct from `RuntimeModule`'s own
  permanent record. See: `04 Design
  Patterns/02-descriptor-and-snapshot-types.md`.
- **streaming read.** Reading a large stored file a piece at a time as it is
  needed, rather than loading the whole thing into memory before anything can
  be shown. In TempestOS: `IBinaryPersistenceStore.OpenReadAsync` opens a
  real, seekable stream over a stored attachment's bytes through SQLite's own
  incremental blob I/O, so the Document Viewer can open a large drawing
  without materialising it whole first (`WP 20.1C1`, `TD-96`).
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
- **business identifier.** `IEngineeringObject.BusinessIdentifier`, a
  read-only projection of the name a person actually recognises an object
  by — a Part's or Calculation's own name, a Document's number if it has
  one — never a new stored field. In TempestOS: a business identifier must be
  unique within its project (a duplicate Part name is refused, naming the
  clash), enforced only for the Kinds a person creates by hand: Part,
  Calculation, Document, Manufacturing, Verification and Evidence. See:
  `74-the-debt-tranche.md`.
- **canonical Kind.** A Kind built to the platform's own base shape (identity,
  revisions, attachments and audit inherited from one common base) rather than
  a one-off. Evidence is the newest example. See: `59-evidence.md`.
- **change bus / change feed.** The platform's own name for the one place
  every engineering write is announced once it has actually committed, so any
  open screen watching for it can update itself with no manual refresh. In
  TempestOS: every write through `EngineeringDomainContext.ExecuteWriteAsync`
  publishes a `WorkspaceChange`; Requirements writes reached it only from
  `WP 20.1A1` onward (`TD-28`), before which a docked Requirements screen went
  stale until it was closed and reopened. See:
  `61-the-screen-follows-the-store.md`.
- **change order.** A second quotation raised against work already agreed,
  carrying an existing deliverable rather than creating a new one, used when
  a project's scope grows after the original quote was accepted. In
  TempestOS: a `Quotation` whose `QuotationKind` is `ChangeOrder` generates
  its own `CO-<yyyy>-<nnn>` reference and lets **sign off** proceed on an
  open deliverable it names, where an ordinary open deliverable with no
  change order blocks sign off outright. See:
  `70-quotations-change-orders-and-the-project-lifecycle.md`.
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
- **connector / connector seam (invoicing and accounts).** A connector is the
  one piece of code that speaks to a given outside service, so that service's
  own failure modes are handled in exactly one place rather than scattered
  through the product; a connector seam is the boundary around it, so nothing
  else in the platform needs to know how the outside call actually works. In
  TempestOS: `IInvoicingConnector` and its read-only sibling
  `IAccountsConnector` are implemented by a scriptable Fake used throughout
  testing, and by real Xero and QuickBooks Online connectors, all answering
  with one of a fixed set of outcomes rather than throwing an error. See:
  `66-outbound-invoicing-and-the-connector-seam.md`.
- **Core.** `Tempest.Core`, the platform itself: the Host, its platform
  services, persistence, commands, and engineering objects' base types.
  Depends on nothing else in the repository.
- **decision record (`D-`).** A record of a decision that changes the *scope*
  of what is being built, as opposed to an ADR's technical decision. `D-028` —
  stopping calculation-engine work in favour of recording calculations as
  Evidence — is the model. See: `59-evidence.md`.
- **deliverable completion.** The record that a promised piece of work was
  actually delivered — when, by whom, against which evidence — kept as its
  own fact separate from the deliverable itself, which stays a promise. In
  TempestOS: a `DeliverableCompletion` can happen only once per deliverable; a
  second attempt is refused and returns the first, so nothing downstream can
  bill the same work twice. See: `65-the-project-commercial-core.md`.
- **Desktop.** `Tempest.Desktop`, TempestOS's one shipped, graphical product:
  the Ribbon, the Project Explorer, the Object Editors.
- **Details tab.** The first tab in a project's own workspace, added so a
  project's client, rate card, purchase-order reference and other commercial
  facts are reachable directly rather than only through the general-purpose
  object editor. In TempestOS: `WP 20.10A` added it after the Product Owner
  found the Commercial section unreachable ("doesnt exist at all… cannot
  navigate to it anywhere"); it is its own small view, not the generic editor
  reused, because that editor is built for one fixed object at a time. See:
  `65-the-project-commercial-core.md`.
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
- **honest rail.** TempestOS's own name for the discipline that the strip of
  buttons giving access to every part of the product (the **rail**) shows
  only destinations that genuinely work, never one that looks clickable but
  does nothing behind it. In TempestOS: five modules that had sat on the rail
  unbuilt (Tasks, Commercial, Resources, Knowledge, Administration) were
  removed outright rather than merely dimmed; Engineering moved inside a
  project as its own **Structure tab**. See:
  `69-the-honest-rail-and-the-structure-tab.md`.
- **Host / `TempestHost`.** The Runtime Host: discovers, registers and starts
  every **module** in a fixed, **deterministic** order, and reverses that
  order on shutdown.
- **index-first rehydration.** Building a lightweight lookup index (id, Kind,
  name, parent, status) from stored records before reconstructing every
  object in full, so a platform can answer "what exists" quickly without
  first doing all the slower work of loading everything completely. In
  TempestOS: `EngineeringObjectRehydrationService` now builds this index and
  announces it before full materialisation runs; full materialisation itself
  still runs eagerly on every startup, disclosed and left open as `TD-88`
  because too many existing callers were not yet proven safe to change. See:
  `74-the-debt-tranche.md`.
- **invoice request.** TempestOS's own record of one draft or sent invoice:
  which client, which lines (time or completed deliverables, at the rate
  already frozen), its total, and every fact a connector reports back about
  it. In TempestOS: `InvoiceRequest` never computes or asserts whether it has
  been paid — that fact is read from the connector alone, never set by
  anything in the platform. See:
  `66-outbound-invoicing-and-the-connector-seam.md`.
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
- **macro step.** One recorded action inside a user-authored macro: which
  command to run, and the values a person supplied for it at the moment it
  was recorded, replayed unattended later. In TempestOS: `MacroStep` records
  a command's own parameter values through the identical prompt a live
  invocation uses, so a macro can replay a parameterised command — though a
  command needing a person's confirmation can never be a step. See:
  `docs/adr/ADR-0099-a-macro-is-realised-as-a-registered-command-runmacrocommand-over-imacromanager.md`.
- **ManualTask.** A Kind for a piece of work with no engineering object of
  its own behind it — deliberately not named "Task", since that word was
  already claimed — created and completed directly by a person. In
  TempestOS: `task.create` and `task.complete` are its two commands; it
  appears in the tasks read model alongside deliverables, milestones,
  Evidence and invoice requests. See:
  `67-read-models-kpis-status-tasks-and-accounts.md`.
- **Markdown budget.** The rule that a pull request may not add more lines of
  Markdown than lines of code, so the project's own process can never again
  outweigh the product it protects.
- **Object Editor.** The screen that opens an engineering object; it works
  generically for any **Kind**, driven by that Kind's own declared facets
  rather than a bespoke screen per Kind.
- **object picker.** A dialog for choosing an existing object as the
  destination or target of a command, listing every live object by Kind with
  a filter box, so a command that needs "which object" has one real, reusable
  way to ask. In TempestOS: `ObjectPickerDialog` (`FCR-0073`) made Move
  keyboard-reachable and Copy work at all for twelve commands across every
  engineering discipline, closing `S2-2`. See:
  `75-the-object-picker-move-copy-and-macros.md`.
- **overnight tranche.** A block of Work Packages carried out through the
  night in one continuous push, between two points the Product Owner set. In
  TempestOS: `v0.19.1`'s overnight tranche (`WP 19.10A`–`19.10R`) ran from the
  Product Owner's first-pass comments on `v0.19.0` through to the `v0.19.1`
  candidate, folding in a technical-debt audit and its own **rehearsal**
  along the way.
- **P1–P4 priority.** A four-level scale ranking technical debt by how
  urgently it needs closing: P1 is a deterministic defect that corrupts data
  or fully blocks a real journey, closed first; P2 is real and visible but
  not corrupting; P3 is hygiene work with no user-visible effect yet; P4 is
  deliberately deferred, waiting on an external decision or a trigger
  condition that has not yet happened. In TempestOS: the Technical Debt
  Rationalisation of 2026-09-14 scored every open backlog row this way, and
  `v0.20.0` — "the debt tranche" — closed everything it rated P1 to P3 that a
  gate could prove closed.
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
- **quotation / quote line.** A quotation is the document a consultancy sends
  a client before work starts, stating what will be done and for what price;
  a quote line is one priced item on it, by hours and a rate or by a single
  fixed price. In TempestOS: accepting a `Quotation` creates one Deliverable
  and one Requirement per line automatically, so the client's agreement
  becomes the project's own scope rather than being re-typed by hand. See:
  `70-quotations-change-orders-and-the-project-lifecycle.md`.
- **rail / area.** The rail is the row of top-level destinations shown in the
  Desktop application; each destination is one "area" (`ShellArea`).
  Navigation is held as one single value naming the current area, so two parts
  of the screen can never disagree about where the user is. See:
  `33-the-product-spine.md`.
- **rate card (billing rate, cost rate, grade).** A rate card is a price
  list; a grade is the category of person or role it prices (an engineer's
  seniority, say); the billing rate is what a client is charged for an hour
  of that grade, and the cost rate is what that hour actually costs the
  business to deliver. In TempestOS: only a Released rate card may be pinned
  to a project, and a timesheet entry resolves and freezes both rates at the
  moment it is recorded, so a later change to the card never rewrites a past
  period's figures. See: `65-the-project-commercial-core.md`.
- **recovery tranche.** TempestOS's own name for a block of work aimed at
  closing every weakness a formal assessment found, so the next release
  candidate is a genuine release rather than another round of recovery. In
  TempestOS: the unreleased `v0.21.0` Execution Plan is titled "the recovery
  tranche," answering the lead's own written assessment of `v0.20.0`
  (docking, undo coverage, the calculation engine, security, testing depth)
  across eighteen Work Packages.
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
- **S2-2.** One numbered finding from the `v0.19.1` technical-debt audit's own
  "Part B" section, naming a specific gap rather than a `TD-` backlog row:
  Move could not be reached by keyboard and Copy had no working path anywhere
  in the shipped Desktop, across twelve commands. In TempestOS: `S2-2` was
  closed by `WP 20.2A`'s **object picker**, alongside `TD-115` and `TD-77`.
  See: `BACKLOG.md`.
- **seam.** A single point in the code every path to a particular outcome is
  made to pass through, so a rule enforced there cannot be bypassed by an
  undiscovered second route. `EvidenceService` is one: every act with a
  precondition goes through it, reporting a refusal as a result rather than
  throwing. See: `59-evidence.md`.
- **sign off / Closed / Archive (the 90-day rule).** Sign off is the formal
  act of closing a project, recording who did it, when, and a statement;
  Closed and Archive are the two states a signed-off project then passes
  through, derived from its closed date rather than stored as a flag. In
  TempestOS: a project is Closed for ninety days, during which it can still
  be reopened, and becomes Archive after — permanently read-only, with
  Reopen itself refused. See:
  `70-quotations-change-orders-and-the-project-lifecycle.md`.
- **source of record.** The one document treated as the authoritative
  statement of what happened or what was decided, that other documents
  summarise or act on but never override. In TempestOS: `Product Owner
  Comments.md` names itself "the source of record for what `v0.19.1`
  builds"; the Execution Plan turns it into Work Packages, never the other
  way round.
- **Structure tab.** The tab inside a project's own workspace that hosts the
  whole engineering surface — the ribbon, the Project Explorer, the docking
  panels — as one embedded control, rather than a rail destination of its
  own. In TempestOS: the same control instance also serves standalone
  engineering at Home when no project is open; the unreleased `ADR-0153`
  treats it as one shell-level tear-out unit alongside a project's other
  tabs. See: `69-the-honest-rail-and-the-structure-tab.md`.
- **subject (as a tag).** The Part, Assembly or Requirement a piece of
  Evidence is about, held as a bare id and never checked as a real structural
  link — deliberately, so the object tree stays one strict hierarchy rather
  than a web of relationships. See: `59-evidence.md`.
- **task bucket.** One named group inside the tasks read model, sorting every
  outstanding piece of work by what kind of attention it needs rather than by
  which discipline created it. In TempestOS: Overdue, Due today, Due this
  week, Later, Reviews, Approvals, Finance and Calculations are the eight
  buckets; an object leaves one the moment it is completed, closed, or (for
  Calculations) cited by issued evidence. See:
  `67-read-models-kpis-status-tasks-and-accounts.md`.
- **tear-out / dock (`ADR-0153`).** Tearing out is pulling a panel or tab free
  of its current window to make it its own floating window; docking is
  placing it back into a layout, attached to an edge or grouped into a tab
  strip with others. In TempestOS: `ADR-0095` already let the four
  engineering panels do this; the unreleased `ADR-0153` (Proposed) widens it
  to every document tab, every project tab and every rail area pane, across
  monitors, at an estimated cost of about twenty developer-days. See:
  `76-tear-out-and-dock-everywhere.md`.
- **technical debt row (`TD-`).** One numbered, honestly recorded shortcoming
  in `BACKLOG.md`, mapped to the Work Package expected to close it, or marked
  unowned.
- **timesheet entry.** One person's record of hours worked on a project on a
  given day, with the billing and cost rate it was recorded at frozen onto it
  permanently. In TempestOS: `TimesheetEntry` resolves its rates once, at the
  moment it is recorded, from the project's own pinned rate card; a later
  change to that card, even a formal supersession, never reaches an entry
  already recorded. See: `65-the-project-commercial-core.md`.
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

**Which of these the `v0.19.0`–`v0.21.0` consultancy-seam work actually
touched, verified against the code: none was newly frozen or unfrozen.**
**P01**'s seven libraries gained a real, generic record editor and a
citation view reachable from Reference data (`WP 19.6A`); **P04**'s live
Organisation catalogue gained a payment-terms field (`WP 20.1B`, `TD-180`).
The "eighth library" the Reference data screen now lists alongside P01's
seven is not a new library P01 acquired — it is **P07**'s already-live
rate-card catalogue (`RateCardCatalog`, `LibraryName` `"BusinessRateCards"`),
shown on the same screen as P01's seven for the first time (`WP 19.6A`)
rather than moved or reassigned.
