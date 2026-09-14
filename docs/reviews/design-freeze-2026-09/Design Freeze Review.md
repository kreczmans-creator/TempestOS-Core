# TempestOS Design Freeze Review — 8 September 2026

**Prepared by:** the chief engineer of record for the `v0.17.0` line (Claude, acting under the Product Owner's instruction of 8 September 2026)
**For:** the Product Owner, as the client of this programme
**Decision requested:** continue the current product to `v1.0.0` on the remediation programme, or re-implement from the design freeze on the same architectural columns with a different layout.
**Status of this document:** a briefing, not a certification. Every figure in it is reproducible from the repository at commit `31bba97` on `release/v0.17.0`; the commands are in the evidence appendices under `evidence/`.

---

## 1. The decision in one page

**Recommendation: do not pivot. Continue the current product to `v1.0.0` on the remediation programme, with five amendments, and spend ten developer-days on a shadow build that would make a later pivot a decision on evidence rather than on estimates.**

**What the evidence says about what you have.** TempestOS was built as a platform first and became a product in its fifth phase. Of its 147 recorded architectural decisions, 30 are structural columns that any implementation must keep, and every one of them is realised in code; the four newest, the transactional SQLite store, the dimension-vector units, the frozen platform layers and the session principal, were verified complete to the letter. The substrates that would be most expensive to rebuild (persistence, units, reference governance, commands) are finished, proven by fault-injection and property tests, and reusable verbatim. The two things genuinely wrong beneath the surface, a missing permission gate on reference release and three coexisting verification models, cost the same to fix whichever option is chosen.

**What the evidence says about what you saw.** Every defect in the first Windows session was on the surface, and all six were fixed in one day with the full gate green. The surface audit found the same class of defect elsewhere, in places no user had reached yet: a Requirement created from the ribbon has no node in its own tree; the default "Create Manufacturing Object" always fails; half the rail is a "not yet implemented" card; 18 of 74 commands are listed and unusable; there is no way to search for an object by name. These are not signs that the codebase is garbage. They are the signature of a shell composed from platform primitives rather than from the user's questions, and the programme already contains the Work Packages that change that.

**What the two options cost.** Keeping and remediating: 106 to 109 developer-days remaining, 17 to 18 calendar weeks, three reviewable releases along the way. Starting again on the same columns with a fresh layout: 113 developer-days, 19 weeks, a seven-week period in which there is nothing to review, and a codebase of about 60,000 lines instead of 117,000 at the end. The difference is four to seven days. The difference in risk is not symmetrical: this programme's weakness has been assumptions that were not put in front of a user, and Option 1 puts a release in front of one three more times.

**What to decide now.** Fix the two high substrate hazards before verifying `v0.17.0` (one day). Add a Findability Work Package to `v0.18.0` and pull the archive of the six unreachable subsystems forward (§5.2). Decide whether to run the two-week shadow build in parallel with `v0.18.0`; if the firm intends to build on this codebase for years, run it. And hold the programme to the kill-switch in §8: if the `v0.18.0` review finds three or more "I made it, where is it" defects, or the async read surface overruns by half, pivot to the fresh layout as `v0.19.0`, on the substrate that will by then be even better proven.

---

## 2. What was reviewed, and how

- **The design as decided:** all 147 Architecture Decision Records in `docs/adr/`, each classified as a structural column, keep, keep-but-simplify, revisit or drop, with the code that implements it cited (`evidence/adr-review-*.md`).
- **The code as built:** line counts, project graph, largest files, churn and commit history (`evidence/metrics.md`); the substrates read for soundness and reusability with a hazard register (`evidence/substrate-audit.md`); the user-facing surfaces mapped against what the shell claims (`evidence/surface-audit.md`).
- **The debt, the tests and the plan:** `BACKLOG.md`, the test suite's shape and fragility history, CI, and the `v1.0.0` Work Package programme re-costed against what `v0.17.0` has actually delivered (`evidence/debt-tests-plan.md`).
- **The product as experienced:** the Product Owner's first Windows session on `v0.17.0` (8 September 2026), its recorded video and screenshots, and the two hotfix rounds that answered it (`WP 17.9.1`, `WP 17.9.2`), used here as case studies because they are the only end-user evidence the programme has.

What this review does not do: it does not re-run the test suites (the gate figures are those recorded at each commit), and it does not benchmark. It does not weigh anyone's effort to date; sunk cost is not evidence.

---

## 3. Where the product came from, and why that matters

The repository is eight weeks old. Its first commit is dated 15 July 2026; at the freeze it holds 528 commits, 94 in July, 117 in August and 317 in the first eight days of September (`evidence/metrics.md` §8). That acceleration is the arrival of agent-driven development at scale, and it is the single most important fact for the decision in front of you: **the constraint on this programme has never been the rate at which code can be produced.** Forty thousand lines were added and nineteen thousand deleted in the last hundred commits alone.

**It was built as a platform first.** `VISION.md` records the intent at `v0.6.0` in its own words: "eleven verified platform services ... and zero Engineering Modules. Every capability shipped so far is infrastructure." The roadmap ran Platform Foundation, Developer Experience, Platform Services and Engineering Foundation before the first discipline shipped (`docs/governance/Product Roadmap.md`, Phases 1–4). The engineering constitution (`docs/releases/FOUNDATION.md`) was written for that platform: four layers that never invert (Modules → Platform APIs → Platform Services → Runtime Host), a custom dependency-injection container (ADR-0005), module discovery by reflection, plugin failure taxonomies, a REST API as a hosted service, a licence gate at start-up. Every one of those was designed, implemented, tested and documented before there was a product to put on it.

**The product arrived in phase 5 and was redefined in phase 5.5.** Six engineering disciplines were added between `v0.7.0` and `v0.10.0` on a Workspace layer that proved genuinely rendering-agnostic: the presentation moved from a terminal UI (ADR-0066) to an Avalonia desktop (ADR-0092) with no change to any Workspace contract, which the ADR review counts as one of the strongest structural validations in the whole register. Then, in the first week of September, the `v1.0.0` Release Candidate Audit and the Work Package programme that replaced it restated what the product is: **"a single-user, locally-trusted desktop system of record for a small engineering consultancy"** in which an engineer opens a client project, authors and runs a cited calculation sheet, has it independently checked and issued as a PDF, records time, raises an invoice request to the accounting system, and reads utilisation and margin on the cockpit (`docs/releases/v1.0.0/WorkPackages.md`, "What v1.0.0 is").

**`v0.17.0` is the first release built for that definition.** In one release the persistence backend was replaced with a transactional SQLite store (ADR-0144, ADR-0145), the units became a runtime dimension vector (ADR-0147), plugin trust, the REST API and licensing were frozen outside the build (ADR-0146), identity collapsed to one OS-derived session principal, 807 governance files were archived, and the test suite was cut from about 5,650 to about 5,450 tests that run in a few minutes. The ADR review verified all four substrate decisions as complete in code, with the specific pragmas, transaction ordering and frozen-folder contents matching the records (`evidence/adr-review-0101-0147.md`, "v0.17.0 substrate ADRs — detail").

**What the architecture still carries from the platform era** is the subject of §4 and §6. Three things are worth naming now because they explain the Product Owner's experience on 8 September better than any single defect:

1. **Between 30,000 and 31,000 lines of `Tempest.Core` are fully built, unit-tested and unreachable from any screen.** Engineering Intelligence (7,622 lines), Business Governance (7,632), Commercial Intelligence (5,892), Engineering Assets (3,658), Business Operations (3,005) and Knowledge (2,958) have, with the exception of Engineering Assets, zero references from `Tempest.Workspace` or `Tempest.Desktop` (`evidence/metrics.md` §4; `evidence/adr-review-0101-0147.md`, patterns). They are scheduled for archiving by `WP 19.0B`. They are not the reason the shell feels empty, but they are the reason the codebase is 117,000 lines for a product whose user can reach perhaps a third of it.
2. **The object model implements every facet on every Kind.** ADR-0075 decided that objects compose small facet interfaces; the base class implements all of them unconditionally, so a Risk has a bill-of-materials quantity and a Decision can be moved under an assembly (`evidence/adr-review-0051-0100.md`, patterns). This is why the first Windows session saw Quantity and Find Number on a Project and on a Calculation.
3. **The shell was composed from platform primitives, not from user journeys.** Six discipline modules each register a node provider, a view factory, a facet provider, command bindings and a workspace registration through six Kind-keyed provider categories on one manager interface (ADR-0067, 0082, 0096, 0097). Every screen is generic across Kinds because the platform made that cheap. What the platform did not make cheap is the question a user actually asks: "I just made a thing; where is it?" That is the question both hotfix rounds of 8 September answered.

The governance record tells the same story from the other side. `PROJECT_STATUS.md` has been changed 144 times in eight weeks, more than any source file; the seven most-churned files in the repository are all governance registers (`evidence/metrics.md` §8). Seventy of 528 commit subjects mention a review, board or remediation. The documentation, at 39,823 non-blank lines, is one third the size of the live source. None of that is wasted, but it was the cost of building a platform to a constitution, and the constitution was written for a product that is not the one being shipped.

---

## 4. What we have: the architecture as built

### 4.1 The structural columns

The three ADR reviews classified all 147 decisions. Totals: **30 structural columns, 75 keep, 15 keep-but-simplify, 12 revisit, 15 drop.** The fifteen drops are the REST API, licensing and plugin-trust decisions already frozen outside the build, the two superseded presentation paradigms (terminal UI, flat-list thread), and two shell decisions whose code was deleted without their records being marked (ADR-0033, ADR-0035). The full tables, one code citation per row, are Appendix A. The thirty structural verdicts consolidate into eight columns:

| # | Column | Decisions | Realised in |
|---|---|---|---|
| C1 | **One durable store, one abstraction, one transaction.** Every canonical object is a Kind over the single document store; object state, revisions, relationships, attachment bytes and the audit row commit atomically; in-memory repositories are caches populated only after commit; attachment bytes are hashed and verified on read; durable state carries a schema version and migrates on read. | ADR-0041, 0053, 0072, 0113, 0114, 0120, 0144, 0145 | `src/Tempest.Core/Persistence/SqlitePersistenceStore.cs`, `EngineeringDomain/Implementation/EngineeringDomainContext.cs` (`ExecuteWriteAsync`), `AttachmentContentStore.cs` |
| C2 | **One authorisation point, durable queryable audit, one OS-derived session principal.** | ADR-0044, 0045, 0146 | `Identity/IPermissionEvaluator.cs`, `Audit/AuditRecorder.cs`, `Identity/SessionPrincipal.cs` |
| C3 | **Reads are direct; every mutation is a command.** The command registry with descriptors is the one dispatch path both the ribbon and the palette use. | ADR-0036, 0037, 0063 | `Commands/CommandRegistry.cs`, `Workspace/Composition/EngineeringWorkspaceComposer.cs` |
| C4 | **Units are a runtime dimension vector behind a typed facade, with affine offsets, and the platform refuses to guess** across any boundary that is not equivalent: units, currencies, working days against elapsed days. | ADR-0054, 0125, 0147, 0130 (the `Money` half), 0133 | `UnitsAndQuantities/Dimension.cs`, `QuantityVector.cs`, `Unit.cs`, `BusinessGovernance/Money.cs` |
| C5 | **Reference data is governed.** One shared catalogue layer with provenance and a Draft → Verified → Released lifecycle; a calculation result carries the exact reference revision it stood on; a review is attributed to the signed-in principal and cannot be forged. | ADR-0126, 0143, 0055 (classification) | `ReferenceData/ReferenceDataCatalog.cs`, `ReferenceData/Review/ReferenceReviewService.cs`, `ReferenceData/ReferencePin.cs` |
| C6 | **The object model's shape.** Kinds compose small facet interfaces; one generic relationship type with an open, descriptive kind string; one canonical lifecycle vocabulary specialised per family; Kind-keyed registration is how a discipline joins the workspace. | ADR-0067, 0073, 0074, 0075, 0076 | `EngineeringDomain/Contracts/Facets.cs`, `Relationships.cs`, `Lifecycle.cs`, `Workspace/WorkspaceManager.cs` |
| C7 | **One shipped surface: an Avalonia desktop with the cockpit as its landing screen.** The console is an internal harness, never a second product. | ADR-0069, 0092, 0094, 0101 | `src/Tempest.Desktop/`, `Views/CockpitView.cs` |
| C8 | **The tool reports; a person decides.** No service returns a chosen recommendation, no type can approve, award, sign off or merge on its own; estimate, quote, quotation and outcome are four types; a template use pins the revision it used; assets reference calculations and requirements by identity, never by copy. | ADR-0127, 0131, 0134, 0136, 0137 | `EngineeringIntelligence/AssessmentOutcome.cs`, `CommercialIntelligence/Suppliers/SupplierIdentityService.cs`, `EngineeringAssets/Templates/EngineeringTemplate.cs` |

Two observations about the columns matter for both options.

**They are almost entirely substrate.** Seven of the eight columns live in `Tempest.Core`, and the eighth (C7) is a framework choice. No column constrains how the shell is laid out, how a discipline's screens are composed, or how many provider categories a manager interface has. The Product Owner's instruction for this review, "architectural structural columns to remain the same, but general layout can be updated within reason", is therefore not a tight constraint on a fresh implementation. It leaves the whole of the surface, and much of the Workspace layer, open.

**They are all realised, and the four newest are realised completely.** The reviews found the persistence, transaction, units and identity decisions of `v0.17.0` implemented to the letter (`evidence/adr-review-0101-0147.md`, detail section). That is what makes Option 2 a re-layout rather than a rewrite: the parts that would be most expensive and most dangerous to rebuild are the parts that are finished.

What the ADR reviews say should **not** be carried into any implementation, fresh or continued, is also consistent across the three batches: the custom DI container (ADR-0005, "the weakest cost/benefit case among the early infrastructure decisions", reaffirmed once on inertia); the plugin marketplace, REST API and licensing (already frozen); the macro system and the external-controller abstraction, each built for one real implementation and one test stub (ADR-0099, 0100); the six-discipline Engineering Readiness Review ceremony (ADR-0106) and the vocabulary register (ADR-0105), neither of which exists in the live tree; the `BusinessAuthorisation` ceremony (ADR-0130's other half, "deliberately awkward" by its own admission, in a product whose user is the firm's sole authority); the Knowledge layer with no execution path and no screen (ADR-0139, 0140); and, above all, the implementation of the facet model on the base class, which the interface-level decision (ADR-0075) never asked for.

### 4.2 The substrates

The substrate audit (`evidence/substrate-audit.md`) read each non-UI layer for what exists, what proves it, what could hurt, and whether a fresh build could take it verbatim. Its verdicts:

| Substrate | Source / test lines | Verdict | The evidence that matters |
|---|---|---|---|
| Persistence (SQLite, transactions, lock) | 1,982 / 2,810 | **Reusable as is** | Fault-injection tests fail a commit after the body ran and read the raw collections to prove nothing landed; two hundred concurrent move races never form a parent cycle; a second store over the same root is refused by name; ten thousand keys list and read inside budget. The file backend's non-atomicity is asserted by a test, not merely documented. |
| Engineering object model | 6,766 / 8,977 | **Reusable with trim** | Thirty-one Kinds each override exactly one method; every mutation goes through one project-commit-apply path under one lock; the largest test file in the suite (1,384 lines) exists to attack mutator preconditions. The trim is the facet set (§4.1) and a parent index. |
| Units and quantities | 2,352 / 1,152 | **Reusable as is** | "The most self-contained and best-tested substrate reviewed": a property test per dimension, twenty-six of them, over every unit pair. |
| Reference data governance | 7,148 / 2,283 | **Reusable as is** | Five-state lifecycle with a transition table; reaching Released requires verified provenance; the reviewer and the date come from the session and the clock, never from a caller; audit rows on verify and release. One addition needed, below. |
| Calculations | 4,078 / 2,279 | **Reusable with trim** | Append-only execution records; property tests; the governed bracket check refuses to run on an unreleased material and pins the revision it used. The trim: that governance covers one of six definitions today. |
| Identity, configuration, logging, host | 5,188 / 6,426 | **Reusable with trim** | Identity, configuration and logging are small and already bridge to the Microsoft libraries where it matters. The host is a 1,335-line composition root for the whole platform. |
| Commands and events | 2,035 / 3,868 | **Reusable as is** | Already right-sized when the plugin-trust apparatus was frozen; tests are nearly double the source. |
| Frozen platform | 3,120 / 12,603 | Reference design only | No project file, not in the solution, and no live type references any frozen type (verified name by name, because the namespace is shared). |
| Verification models | 3,091 plus two Kinds | **Consolidate** | Three models are live and independently registered. `WP 17.1B` stated it would collapse them; the transactional half of that Work Package shipped and this half did not. It is `TD-171` on `WP 18.2B`. |

Fourteen hazards were registered (`evidence/substrate-audit.md`, "Hazard register"). Four are rated high and each has a cheap fix:

- **H8/H13 — any signed-in principal can release reference data.** The review service checks that someone is signed in, not who. The permission evaluator that gates audit queries, reporting and verification reads is not called here. Fix: one `RequirePermission` call in `RequireReviewer`, mirroring the existing pattern. Half a day. This is the one finding in the whole review that touches what the product promises about governance, and it should be fixed before `v0.17.0` is verified.
- **H4/H5 — children are found by scanning every object**, once per rendered node, and the delete guard does the same scan while holding the write lock. Correct, and quadratic. Fix: a by-parent index in the 37-line in-memory repository. Half a day. `WP 18.1A` is where a durable query belongs.
- **H10/H11 — reference pinning covers one calculation of six**, and a second, weaker provenance breadcrumb (`ReferenceMaterial`, no revision, no state check) coexists with it. `WP 18.0A` turns the six definitions into built-in functions whose inputs are cited cells; the trim is to retire the breadcrumb when it does.
- **H14 — three verification models.** Owned by `WP 18.2B`; the audit's recommendation is to keep the governed `VerificationArtefact` shape and fold the other two into it.

The reading of this table for the decision is direct. **Every substrate a fresh build would have to rebuild is a substrate it does not need to rebuild.** The persistence, units, reference and command layers can be lifted as they stand, with their tests, into any solution. The object model and the host need trimming in either option. The two things that are genuinely wrong, the release gate and the three verification models, are wrong in the same way for both options and cost the same to fix in either.

### 4.3 The surfaces

The surface audit (`evidence/surface-audit.md`) mapped the shell from source, every claim against every screen. The Product Owner's phrase, "doesn't actually contain half of the things it claims to have", turns out to be arithmetically exact.

**What the rail claims and what it delivers.** Ten rail entries; five (Tasks, Commercial, Resources, Knowledge, Administration) render a "not yet implemented" card. The card is honest, badged and tracked, and the rail marks it with a dot, but half the rail is not a surface. Inside a project, two of nine tabs (Reports, Settings) are the same card. Home renders the identical control as Engineering with the cockpit as its first tab; there is no surface unique to Home (`surface-audit.md` §1).

**What the ribbon and palette claim and what they deliver.** Seventy-four commands are registered across the six disciplines. Eighteen of them, 24%, carry an "unavailable" binding: every discipline's Move and Copy (twelve commands, because no destination picker exists anywhere in the Desktop), calculation execute and recalculate (no input form), document attach (no file picker), and requirement link, move-group and add-to-collection (no object picker). They are listed, described, disabled and explained, which is the designed behaviour (ADR-0070). It means a user can see forty-odd verbs and find that the ones that move, link or attach anything do not work (`surface-audit.md` §2).

**Where a created object goes, discipline by discipline.** This is the class of defect the Windows session found in Mechanical, and the audit checked all six:

| Discipline | On "Create" | Result |
|---|---|---|
| Mechanical | Parent from selection or open project (`WP 17.9.2`) | Appears where the user is looking |
| Verification | Subject from selection (designed that way) | Appears |
| Documents, Calculations, Manufacturing | No parent is ever supplied | Visible in a module-wide category tree, never a member of any project |
| Manufacturing, default Kind | The handler requires a `PartId` the binding never collects | **Always fails** with an exception message |
| Requirements | No group, no collection; the tree roots only on groups and collections | **No node anywhere in its own tree.** The object exists; nothing can reach it by clicking |

The Requirements case is strictly worse than the Mechanical one the Product Owner reported, and it was found by reading, not by a user. Both are now `TD-172`.

**What the object screens show.** Every facet provider re-derives the same fourteen-facet base independently, six times. Every Kind shows its own `Id` and its `Parent` as raw GUID strings while, one row away, "Last Revised By" is resolved to a name (`surface-audit.md` §5). The editor shows Name and Content on Kinds that can neither be renamed nor revised, disabled and empty rather than absent (§4). The Property Inspector's validation section is real for every Kind except Requirement, while the editor's own comment still says it is a placeholder (§5).

**Two calculation models.** The "Engineering Calculations" rail entry drives one of the six registered calculation definitions end to end, through a governed flow that pins the reference revision and parents the named result on the open project; that path is real and is the strongest surface in the product (§6, and the journey test that walks it). The "Calculations" discipline tab, reached through the Engineering workspace, is a second model: a `Calculation` object Kind with five template entries and fourteen commands, of which execute and recalculate are unavailable. The two converge only at the data layer. A user exploring the ribbon's `calculations.*` verbs has no way to discover that the working flow is on a different rail entry (§6).

**The verification gap.** A verification result recorded through the shipped command links its evidence to the Activity, never to the Requirement the Activity names as its subject; the Requirement's "Verification Coverage" facet counts only edges from its own id and therefore always reads "Not Verified" (§8, journey e). That is now `TD-173`.

**No object search.** Ctrl+K and the header search open the command palette, which searches commands. Ctrl+F filters the currently open discipline tree. There is no way to type "bracket" and find the bracket (§8, journey f).

**The seven journeys.** Of the seven user goals the audit walked, three are reachable end to end (create a project and put a part under it; run a governed calculation and attach it to the project; see what changed recently), one is partial (find an object), and three are blocked (record a requirement and link it to a part; add a document revision to a part; record a verification result against a requirement) (§8).

Set against §4.2, the picture is consistent: the substrates are sound and the surfaces do not yet answer the user's questions. The cockpit is the exception that proves it. Its numbers are real read models over live state, with two self-disclosed placeholders (`ReviewStatus`, Materials), because it was built from a question, "what needs my attention", rather than from a Kind (§7).

### 4.4 The tests, the debt and the governance weight

**Debt.** `BACKLOG.md` holds 28 live rows after this review (27 at the freeze plus one added below), 43 rows already owned by a programme Work Package, and 16 archived with the frozen layers (`evidence/debt-tests-plan.md` §1). The register marks nothing as release-blocking, because `CONTRIBUTING.md` defines that term narrowly as "data loss a user can reproduce from the running UI" and the one such row, `TD-147`, was closed by the transactional store. The audit applied a wider test, "would a consultancy notice", and judged six rows blocking for `v1.0`: no project context in the running application (`TD-76`), no compare-and-swap on requirements (`TD-25`), no business-identifier uniqueness (`TD-38`), an untested post-commit failure window (`TD-150`), a superseded-source warning that can never fire (`TD-157`), and a data-loss fix not pinned on its production path (`TD-63`). All six are already owned by a planned Work Package. Two register-hygiene findings were made and one was corrected during the review: the identifier `TD-171` was assigned to two unrelated defects (the verification-model collapse and the palette-placement defect from `WP 17.9.1`); the second is now `TD-172` with its own row. The ADR Register's own narrative still says "144 ADRs" against 147 rows, and six ADRs (0016, 0033, 0035, 0043, 0116, 0124) describe mechanisms that later work deleted or replaced without their status being amended.

**Tests.** At the freeze the suites run 4,949 Core and 501 Desktop test cases (3,698 and 467 test methods by attribute; theories expand). A 40-file sample classified 85% behavioural, 15% structural and none self-referential, and a repository-wide scan for tautological assertions found zero (`evidence/debt-tests-plan.md` §2.2). That is a materially healthier suite than the one the `v1.0.0` audit described on 4 September. What it still lacks is stated plainly by its own configuration: mutation testing is scoped to two folders, `Calculations` and `UnitsAndQuantities`, and has produced no score yet; coverage is collected and published as a diagnostic, never enforced; and the only test that launches the real windowed application is an advisory Linux job. Every defect the Product Owner found on 8 September was on a surface the headless suite had passed.

**Fragility.** The last three hundred commits contain seven distinct classes of intermittent failure, each traced to a root cause and each fixed structurally rather than by retry: shared console streams, arbitrary sleeps, a process-wide native pool clear, a first-use native initialisation race, disposal racing a UI continuation, a write-then-index race in three reconciliation sweeps, and assertions too weak to fail (`evidence/debt-tests-plan.md` §3.3). Four of the seven were found and fixed on the day the `v1.0.0` plan was written. This is the strongest single piece of evidence that the substrate is now understood: every one of these has a named cause, a named fix and a test that would catch its return.

**CI.** Five jobs on Windows 2022; the `CI Gate` check is required, strict, and depends only on the build-and-test matrix and the governance health check. Warnings are promoted to errors on the command line, not in `Directory.Build.props`, which is why a local build can pass an analyser warning that CI refuses; this bit the first Windows build of `WP 17.9.1` and is now a recorded working rule. Mutation testing runs on a Monday schedule only. The release workflow rebuilds and retests at the tag and runs the health check as a hard gate.

**Governance.** The reset of `WP 17.0B` moved 809 Markdown files into `archive/docs-2026-09/`, leaving 266 live files under `docs/` of which 147 are ADRs, and cut the health check from sixteen register cross-checks to five checks derived from source and git. `CONTRIBUTING.md` is 70 lines. The Markdown budget (a PR may not add more documentation lines than code lines) is enforced. The weight is now proportionate; the residue is the staleness noted above, which is bookkeeping, not architecture.

### 4.5 Case studies: the first Windows session

The first Windows session on `v0.17.0` is the only end-user evidence the programme has, so it is worth reading carefully. The Product Owner's words were: "the engineering workspace doesn't open properly in any windows, it's a really hard to navigate place that doesn't actually contain half of the things it claims to have"; then, on the screenshot: "no ability to add material in the calculation page"; and "create mechanical object tells me that something has been made, but it doesn't open, I can't find it, can't even search for it anywhere." Six defects were traced. In every case the question was the same: did the substrate do the wrong thing, or did the surface fail to show the right thing?

| Defect | Root cause | Layer | Fix | Size |
|---|---|---|---|---|
| Engineering opened with no Project Explorer and no Properties panel | Not reproduced headlessly; a saved layout or earlier close had removed both panels and nothing restored them on entry | Surface (docking) | Entering Engineering guarantees both panels are present | 1 method, 1 call, 1 journey test |
| A Project and a Calculation offered Quantity, Find Number and Reference Designator | The base class implements the BOM facet on every Kind (§4.1, C6 implementation) | Object model as exposed by the surface | Editor gates the section to the five mechanical Kinds | 1 set, 1 condition |
| A Calculation's editor led with "Execute" and a raw JSON box | A developer seam (`TD-159` class) shipped as the first thing on the object | Surface | Section retired; a pointer to the calculation workspace | 1 section |
| "Last Revised By" showed a Windows SID | The identity id is stored, correctly; nothing translated it for display | Surface (presentation of a correct substrate value) | A principal directory over the current principal and the OS | 1 class, 1 enum member, 8 provider lines |
| "Create Mechanical Object" made an object nobody could find | The Ribbon and Palette binding never supplied a parent and the handler never consulted the open project; the explorer roots on Projects and walks parent links; the filter searches only the loaded tree. The object was written correctly. | Surface (command context) | The context carries the open project; a parent policy; a "Not in any project" node so nothing is unreachable | 1 property on the context, 1 policy class, 1 category node |
| No way to add a material; "Select a governed material" with nothing to select | The only write path into the library was the seed corpus; the picker was in the left column, the error in the right | Surface plus one missing workbench method | `AddMaterialAsync` (validating source, sign and duplicates); a picker beside the inputs; an add-material form | 1 method, 1 form, 1 picker |

Two hotfix rounds, `WP 17.9.1` (22 files, +489/−99) and `WP 17.9.2` (20 files, +927/−38), fixed all six in one working day with the full gate green in both configurations. Not one of the six required a change to persistence, transactions, units, identity, reference governance or the calculation engine. Two of them exposed real information the substrate had been holding correctly all along: the orphaned Part existed in SQLite with a valid state record, and the sample module turns out to seed three structural objects that had never been visible in any tree.

The lesson is not that the surface is careless. It is that **the surface was composed from the platform's primitives rather than from the user's questions.** A Kind-keyed explorer over parent links is a correct primitive. "Where is the thing I just made?" is the user's question, and no primitive answers it unless something is written to. That something is a few hundred lines per question. It was, on 8 September. It will be again at each Windows review until the surface is built from the journeys in "What v1.0.0 is", which is precisely what `WP 18.2A`, `19.2A` and `19.2B` are scoped to do.

---

## 5. Option 1 — keep it, and make it work

### 5.1 Where the programme stands

The `v1.0.0` programme was costed on 8 September at 133 developer-days across four releases, 22 calendar weeks at about 1.2 parallel agent streams, 27 weeks executed serially (`docs/releases/v1.0.0/WorkPackages.md`, "Programme total"). Its first release, `v0.17.0`, is engineering-complete on `release/v0.17.0`: every one of its nine Work Packages is delivered and verified against the plan's own scope text (`evidence/debt-tests-plan.md` §5.3), the gate is green in both configurations, and two unplanned hotfix rounds have been added on top. It is not yet merged, tagged or published; that waits on the Product Owner's Windows verification.

| Release | Planned dev-days | Status |
|---|---|---|
| `v0.17.0` Reset and Substrates | 41 | Done, plus `WP 17.9.1` and `WP 17.9.2` outside the plan |
| `v0.18.0` Calculation as Document | 42 | Not started |
| `v0.19.0` Consultancy Seam and Desktop | 36 | Not started |
| `v1.0.0` Release Candidate | 14 | Not started |
| **Remaining** | **92** | |

The stated critical path (`17.1A → 17.1B → 18.0A → 18.2A → 18.2B → 18.3A → 19.0A → 19.1A → RC.0A → RC.0E`) sums to 63 developer-days, of which 14 are done. Forty-nine critical-path days remain; the other 43 remaining days are schedulable beside them.

### 5.2 What this review changes in the plan

The evidence supports the plan's sequencing rule, substrates before surfaces, and its two largest bets: the calc-sheet model with a frozen grammar (`WP 18.0A`, 12 days, high risk by the audit's rating) and the outbound invoicing connector (`WP 19.1A`, 9 days, high risk). Neither is changed here. Four amendments are recommended, all small, all following from what the Windows session and the audits showed:

| Change | Why | Effort |
|---|---|---|
| **Add `WP 18.1B` Findability** to `v0.18.0`, after `18.1A`: one global search over titles and identifiers in the store (SQLite full-text), applied to the explorer filter and the palette; a single creation-placement rule for all six disciplines (the `TD-172` class, of which `WP 17.9.2` fixed the Mechanical instance); and a "recently changed" list on the cockpit. | The three questions the first Windows session could not answer were "where is it", "how do I find it" and "what changed". None is on the plan by name; `19.2B` would meet them late. | 5 days |
| **Pull `WP 19.0B` (archive P02–P07) to the start of `v0.18.0`.** | It removes about 31,000 unreachable lines from the build and the review surface before the two largest Work Packages are written on top of them. It is proven at this scale by `WP 17.2A` and rated low risk. | 2 days, unchanged, moved |
| **Widen `WP 18.2A` to include the object-page composition rule:** an object's editor and inspector show only the facets its own interface composes, driven by one declaration per Kind, so `WP 17.9.1`'s per-section gating does not have to be repeated Kind by Kind. | The facet-on-base-class implementation (§4.1) is the mechanism behind the "claims to have half of what it shows" experience. A declaration per Kind is cheaper than retro-fitting each screen, and `18.2A` is already the Work Package that replaces the calculation surface. | 3 days |
| **Replace the custom DI container with `Microsoft.Extensions.DependencyInjection` inside `WP 19.2A`.** | ADR-0005 is the weakest decision in the register and `19.2A` already rebuilds the composition root that constructs everything. Optional; it can also wait until after `v1.0`. | 3 days, or 0 if deferred |
| **Two substrate fixes before `v0.17.0` is verified:** a permission gate on reference release (H8) and a by-parent index in the in-memory repository (H4). | The first is the only finding in the review that touches a governance promise; the second removes a quadratic scan from every tree and from the delete guard's time under the write lock. | 1 day |
| **Add `WP 18.1C` What a Part is** (Product Owner, 9 September, after the object opened right up): a Part carries what a calc sheet cites and a title block shows: a material assignment pinned to a released reference revision, a standard-versus-custom designation (today a Component is the de-facto standard part and nothing says so), a part number distinct from its name, and a mass with its unit; an Assembly authors its bill of materials (child, quantity, find number, item number, reference designator) and shows it as a table; a Part has **no BOM input at all**, only a read-only **Where used** readout from the assembly it sits in and that assembly's chain. **This is not PLM and not ERP, by the Product Owner's instruction of 9 September:** the single-parent tree stays, there is no part-occurrence model, no multi-assembly usage, no change control on lines, no procurement, supplier, cost or stock fields. The declaration-per-Kind rule in `18.2A` then renders these. | The first thing the Product Owner saw once an object opened was that it held none of the data an engineer would put on it. `IPart.MaterialId` exists as a bare string nothing sets; the material catalogue, reference pins and dimensioned quantities already exist, so this is model and page work on finished substrate, not new substrate. `TD-174`, `TD-175`. | 5 days |

Adjusted remaining effort: **109 developer-days** with all six, **106** if the container swap is deferred. At the plan's parallelism that is 17 to 18 calendar weeks; serially, 20 to 21. A Release Candidate in January 2027 against the plan's own December, for a product whose user can, at each of the three intermediate releases, do something they could not do before.

### 5.3 What Option 1 does not fix

Honesty requires listing what a `v1.0.0` on this line will still carry, because it is what a fresh build would spend its first weeks removing:

- **Six provider categories on one manager interface** (`RegisterView`, `RegisterExplorerArea`, `RegisterFacetProvider`, `RegisterRenameFactory`, `RegisterDeleteFactory`, `RegisterReviseFactory`), each added additively to a frozen contract (`evidence/adr-review-0051-0100.md`, patterns). Every new Kind touches all six. It works; it is the reason adding a discipline costs a day and adding a user journey costs a week.
- **Two object models.** Requirements are immutable snapshots reconstructed on every read and are deliberately not on the shared base class (ADR-0084); every other Kind is. The editor cannot resolve a real Requirement and falls back to a generic body (`TD-41`).
- **The base class implements every facet.** Even after `18.2A` hides the results, a Risk will still have a `SetBomLineAsync`.
- **A platform-era host** of 1,162 lines (`TempestHost.cs`, the largest file in the repository), module discovery by reflection, hosted-service orchestration and a custom container, all serving one composition root that constructs everything it needs directly.
- **Children are found by scanning every object.** `GetLiveChildrenAsync` lists the whole repository and filters in memory (`src/Tempest.Workspace/Workspace/Mechanical/MechanicalProductStructureNodeProvider.cs`); the transactional store made the repository a cache, so this is correct and fast at hundreds of objects and will need a query at tens of thousands. `WP 18.1A` is where that query belongs.

None of these is a correctness defect. All of them are drag. They are why Option 2 exists as a question.

---

## 6. Option 2 — start again from the implementation phase, on the same columns

### 6.1 What "start again" would actually mean

The instruction was: the architectural columns stay, the general layout may change within reason. Section 4.1 showed the columns are substrate, and §4.2 shows the substrate is the part of the tree that is finished and sound. A fresh start on those terms is therefore not a rewrite of TempestOS. It is a **new solution that takes about 22,000 lines of `Tempest.Core` verbatim with their tests, archives 31,000 lines that nothing reaches, replaces 8,500 lines of platform-era host with a framework, re-cuts the 6,000-line object model, and builds the application and desktop layers again from the user's five journeys instead of from Kind-keyed primitives.** Roughly 60,000 lines where there are 117,000 today.

### 6.2 What is kept verbatim

| Substrate | Lines | Carried as |
|---|---|---|
| Persistence: SQLite store, transactions, lock, queryable interface | 1,777 | As is, with its tests (crash-consistency, contract tests over both backends until the file backend is deleted) |
| Units and quantities: dimension vector, typed facade, affine units, property tests | 1,974 | As is |
| Reference data: catalogue layer, provenance, review lifecycle, seeding, pins | 5,349 | As is |
| Reference libraries: materials, fasteners, bearings, standards, constants | 5,680 | As is; `WP 18.0B`'s structured citation lands on them either way |
| Calculations: engine, definitions, governed bracket check, records | 1,824 | As is; `WP 18.0A` builds beside it either way |
| Identity, permissions, audit | 1,343 | As is |
| Commands: registry, descriptors, bindings, invocation tracking | 1,577 | As is; the six discipline registrations that use it are rewritten |
| Settings, configuration, logging, events, concurrency, versioning | 2,572 | As is |
| **Total kept** | **≈ 22,100** | About 30% of `Tempest.Core`, and the 30% every column in §4.1 lives in |

Also kept from the Desktop: docking (1,123 lines), theming (1,244), the document viewer (963) and the Digital Thread graph (1,310), about 4,600 lines that are framework-shaped and journey-independent.

### 6.3 What changes

| Today | Fresh layout | Why |
|---|---|---|
| Four projects (`Core`, `Workspace`, `Desktop`, `Harness`) plus Samples, Validation, Templates, Frozen | Three: `Core` (substrates and domain), `Desktop`, `Tests`. The Workspace layer exists so that "a second presentation layer can compose" (ADR-0062); there is none and ADR-0101 says there never will be. Its 19,000 lines fold into an `Application` namespace in Core. | One fewer layer to carry every change through |
| Custom host, container, module discovery, hosted-service orchestration, plugin manifests, notifications, reporting, export, macros, input bindings (≈ 8,500 lines) | `Microsoft.Extensions.Hosting` and `DependencyInjection`; one composition root of a few hundred lines that constructs what it needs. The engineering constitution's rules on disposal, cancellation and failure isolation survive as principles applied to the composition root, not as a platform. | ADR-0005 is the register's weakest decision; the module system discovers one composition root |
| A base class that implements every facet on every Kind; Requirements on a separate immutable model; three verification models | Kinds declared once, as records composing only the facets they mean; one transactional context (kept); one verification model; Requirements on the same model | The mechanism behind "shows things that mean nothing here" (§4.5); `TD-171`; `TD-41` |
| Six provider categories per discipline (node provider, view factory, facet provider, rename, delete, revise factories) registered on one manager | **One declaration per Kind** (its fields, its facets, its parent rule, its lifecycle family, its verbs) from which the explorer, the object page, the inspector, the palette and the search are all generated | Adding a journey costs a declaration, not six registrations; creation placement becomes one rule (`TD-172`) |
| Explorer rooted per discipline (Mechanical on projects; the other five on module-wide categories); parent lookups by scanning every object | One project tree with every Kind under it, backed by a SQL query; full-text search over the store; a change feed carrying the store sequence number (the `WP 18.1A` design, built in from day one) | "Where is it", "find it", "what changed" become properties of the store, not of each screen |
| Ten rail entries, five of them cards; a generic Engineering workspace with six discipline tabs | The `WP 19.2B` rail from the start: Home, Projects, Engineering, Calculations, Timesheets, Invoicing, Reports, Settings; inside a project, list-and-detail per Kind, generated from the declarations | The rail says what the product does |
| Two calculation models | One: the governed workbench path (the one that works) generalised by `WP 18.0A` into the calc sheet | §4.3 |
| 147 ADRs, 30 of them columns | The 30 columns re-affirmed by reference; the 15 drops and 12 revisits closed with one supersession record each; new ADRs only for the declaration model and the shell | The record stays whole; the ceremony shrinks |

### 6.4 Sequencing and cost

Effort in the same units as the plan: developer-days for one developer working with agents, at the same 1.2-stream parallelism.

| Phase | Scope | Days | User-visible at the end |
|---|---|---|---|
| F0 Carve-out | New solution; substrates and their tests moved verbatim; `Microsoft.Extensions` host; composition root; CI green | 3 | Nothing |
| F1 Object model re-cut | Kinds as declarations; facets by composition; one verification model; Requirements on the model; state migrations from the `v0.17.0` schema | 10 | Nothing |
| F2 Application | Declarations drive read models; SQL-backed project tree; full-text search; change feed with store sequence; creation placement; project scope | 10 | Nothing |
| F3 Desktop shell | Rail; project workspace; generated list-and-detail per Kind; object page; cockpit; explorer; docking, theming and viewer reused; the bracket journey reproduced | 20 | **Parity with `v0.17.0` plus findability**, seven weeks in |
| F4 Calculation as document | `WP 18.0A`, `18.0B`, `18.2A` (cheaper on generated pages), `18.2B`, `18.3A` | 31 | The calc-sheet product |
| F5 Consultancy seam | `WP 19.0A`, `19.1A`, `19.1B` | 19 | Time, invoices, KPIs |
| F6 Release candidate | `RC.0A`–`RC.0F`, `19.3A` | 17 | Installer, golden examples, security posture, clean-machine review |
| F7 Record | ADR supersessions; the declaration-model and shell ADRs; `PHYSICAL_REVIEW` rewritten for the new shell | 3 | |
| **Total** | | **113** | |

Nineteen calendar weeks at the plan's parallelism; twenty-three serially. Against Option 1's 106 to 109 days (the Part-model amendment applies to both options; Option 2's F1 absorbs it), the fresh layout costs **four to seven days more**, and the first seven weeks produce nothing the Product Owner can open. That is the whole of the cost difference, and it is smaller than intuition suggests for one reason: the expensive substrates are reused, not rebuilt. (The two substrate fixes in §5.2 apply to both options and are included in both totals.)

### 6.5 What Option 2 buys, and what it risks

It buys the removal of every item in §5.3. After F3 a new journey costs a declaration and a screen; "where is it" is answered by the store; the object model means what it says; the composition root is a few hundred lines on a framework everyone knows. The evidence that these are real gains is the two hotfix rounds: three of the six defects (§4.5) could not exist in a layout where placement is one rule and pages are generated from a Kind's own facets.

It risks four things, and they are not symmetrical with Option 1's:

1. **A seven-week dark period.** F0–F3 produce nothing a user can verify. Every Windows review so far has found defects the headless suite passed; seven weeks without one is seven weeks of unreviewed assumptions. Option 1 puts a reviewable release in the Product Owner's hands every six to seven weeks.
2. **Second-system effect.** The fresh layout is designed by the same people, on the same day, with the same blind spots that produced the current one. The specific protection is that the columns are fixed and the surface is generated from declarations, which removes the largest source of per-screen judgement; it does not remove judgement from the declarations.
3. **Migration.** `v0.17.0` data is test data by the Product Owner's ruling, so there is nothing to migrate today; there will be by the time F3 lands if `v0.17.0` is used in anger. The state schema versioning (ADR-0120) makes this tractable, not free.
4. **Loss of the behavioural test estate.** About 4,900 Core tests move with their substrates. The 500 Desktop journey tests and the Workspace tests, roughly 25,000 lines, are written against the surfaces being replaced. F3 must reproduce the journeys, not the tests, and that is where the estimate is most likely to be wrong.

There is one way to buy down risks 1 and 2 cheaply: **run F0–F2 as a two-week shadow build in a worktree, in parallel with `v0.18.0`, using the agent capacity the plan already assumes.** At the end of two weeks either the generated-page approach demonstrably reproduces the Mechanical and calculation journeys on the re-cut model, or it does not. That is a 10-day spend for a decision that otherwise rests on this document's estimates.

---

## 7. Side by side

| | **Option 1 — keep it and make it work** | **Option 2 — same columns, fresh layout** |
|---|---|---|
| **What it is** | The `v1.0.0` programme as planned, with five amendments (§5.2) | A new solution that lifts 22,000 lines of substrate verbatim, archives 31,000, replaces the platform host, re-cuts the object model, and generates the surface from one declaration per Kind (§6) |
| **Remaining effort** | 106–109 developer-days | 113 developer-days |
| **Calendar at 1.2 streams** | 17–18 weeks | 19 weeks |
| **Calendar serially** | 20–21 weeks | 23 weeks |
| **First thing the Product Owner can verify** | `v0.17.0` now; `v0.18.0` in about 7 weeks | Parity with `v0.17.0` plus findability in about 7 weeks; `v0.18.0` scope about 6 weeks after that |
| **Windows reviews before `v1.0`** | Three (`v0.18.0`, `v0.19.0`, RC) | Two (F3, then RC), unless F4 is also reviewed |
| **Substrate risk** | None new; the four high hazards are fixed in both | None new; the same four |
| **Surface risk** | The `TD-172` class recurs at each review until `18.1B`/`18.2A`/`19.2B` land; each recurrence has cost about a day to fix | Placement, search and page composition are properties of the store and the declarations, so the class cannot recur; the risk moves to the declarations being wrong |
| **Schedule risk** | Concentrated in `18.0A` (grammar first) and `19.1A` (two OAuth connectors); both unchanged in either option | The same two, plus F1–F3's estimate (43 days) resting on this document rather than on a plan that has already delivered one release to figure |
| **Test estate** | 4,949 Core and 501 Desktop tests carried; behavioural journeys extended | About 4,900 Core tests carried with their substrates; roughly 25,000 lines of Workspace and Desktop tests replaced by journeys against the new surface |
| **What `v1.0` still carries** | Six provider categories, two object models, facets on the base class, the platform host, per-node scans until `18.1A` (§5.3) | None of those; a smaller solution of about 60,000 lines |
| **Cost of a later pivot** | Option 2 remains available at any release boundary; the substrates keep improving in the meantime | Reverting to Option 1 after F3 means abandoning F1–F3 |
| **Governance record** | 147 ADRs, 30 columns, 15 drops and 12 revisits to close with supersession notes | The same, plus two new ADRs (declaration model, shell) |

**The decision criteria that separate the options** are not cost, which is within twelve days, and not correctness, which is the same substrate either way. They are:

1. **How much the Product Owner values a reviewable release every six to seven weeks against a seven-week dark period.** Every Windows review so far has found something the headless suite passed. That argues for more reviews, not fewer.
2. **How many more times the `TD-172` class is acceptable.** Each instance has cost about a day, and each has been found by a person. Option 1 closes the class at `18.1B`; Option 2 cannot produce it.
3. **Whether the firm will build on this codebase after `v1.0`.** Over one release the drag in §5.3 is a rounding error; over five it is not. If `v1.0` is the last release before a rewrite anyway, Option 1 is plainly right. If the product is meant to grow for years, the twelve days buy a smaller, plainer codebase, and that is when Option 2 earns its dark period.

---

## 8. Recommendation

**Continue on Option 1, with the five amendments in §5.2, and buy the option to pivot with a two-week shadow build.**

The reasoning, in order of weight:

1. **The substrate is finished and sound, and it is the part that is hardest to build.** All four `v0.17.0` substrate decisions are complete in code to the letter; the persistence layer has fault-injection proof of its central claim; the units have a property test per dimension; the seven classes of intermittent failure each have a root cause and a guard. A fresh start would lift all of this unchanged. That removes the strongest argument for starting again, which is normally "the foundations are wrong".
2. **Every defect the Product Owner found on 8 September was a surface defect, and every one was fixed in a day.** Twelve hundred lines across two hotfix rounds answered six defects with the gate green. The remaining known defects of the same class (`TD-172`, `TD-173`) are the same shape. This is drag, not rot, and the plan already contains the Work Packages that remove the drag (`18.1A`, `18.2A`, `19.2A`, `19.2B`), now with `18.1B` added to remove it earlier.
3. **The cost difference is within twelve days and the risk difference is not symmetrical.** Option 2's extra cost is small because it reuses the substrate, but its seven-week dark period sits exactly where this programme has been weakest: assumptions that were not put in front of a user. Option 1 puts a release in front of the Product Owner three more times before `v1.0`.
4. **Option 2 stays available.** Nothing in Option 1 forecloses the fresh layout. The substrates keep improving, the archive of P02–P07 happens in either case, and the declaration-per-Kind idea is adopted inside `18.2A` where it is cheapest to try.

**Conditions on the recommendation.** Three things should be done regardless, and one should be decided now:

- Fix H8 (the release permission gate) and H4 (the parent index) before the `v0.17.0` Windows verification; they are a day of work and one of them is a governance promise.
- Adopt `18.1B` Findability and pull `19.0B` forward, as in §5.2. Findability is the Product Owner's own three questions; archiving is cheap and proven.
- Record the ADR housekeeping this review found: mark ADR-0016, 0033, 0035, 0043, 0116 and 0124 amended or superseded; update the ADR Register narrative to 147; close the 15 drops with one supersession note each. Half a day, and it stops the register lying about itself.
- **Decide now whether to run the two-week shadow build** (§6.5). It costs ten developer-days of agent time in a worktree, in parallel with `v0.18.0`, and it converts §6.4's estimate from this document's judgement into evidence. If the Product Owner expects to build on this codebase for years, spend the ten days.

**Kill switch: the conditions under which this recommendation should be reversed, stated before the fact so they cannot be argued after it.**

| Trigger | Measured at | Action |
|---|---|---|
| The `v0.18.0` Windows review finds **three or more** defects of the `TD-172` class (an object made, shown or linked somewhere the user is not looking) | `WP 18.9.0` physical review | Stop `v0.19.0`'s surface Work Packages; run F1–F3 as `v0.19.0` on the substrate as it stands |
| `WP 18.1A` (async read surface) exceeds **150%** of its 6-day estimate, or lands and the explorer and inspector still require per-screen reload calls | `WP 18.1A` PR | Same as above: the Workspace layer is resisting the change it exists to make easy |
| The shadow build, if run, reproduces the Mechanical and calculation journeys on generated pages **within its ten days** | End of the shadow build | Re-open this decision with evidence; the case for F3 becomes an engineering fact rather than an estimate |
| The `v0.19.0` review finds the rail still carrying a "not yet implemented" card | `WP 19.9.0` physical review | Not a pivot trigger; a `19.2B` failure to be fixed before tagging, as its own acceptance bar says |

If none of the triggers fires, `v1.0.0` ships on this line in January 2027 with a smaller, plainer codebase than it has today, and the fresh layout becomes what `v1.1` is built on if the firm decides it wants it.

**Postscript, the same night.** On the Product Owner's instruction the two substrate conditions were met before the Windows verification, as `WP 17.9.3`: the release permission gate (H8) and the by-parent index (H4), plus the surface defects this review found (`TD-172`'s placement half across Documents, Calculations, Requirements and Manufacturing; `TD-173`; the Parent GUID; the editor's empty Content box) and the six stale ADR statuses. `TD-172` was then closed in full by `WP 17.9.4` (the shell switches to the object's area, reveals it and opens it after every Create), after the second smoke test found the created Part placed correctly but never shown. The first thing the Product Owner saw once it opened was that a Part holds none of the data a calc sheet cites: no material, no standard-versus-custom, a "Bill of Materials" section that is really the part's own line in its parent. That is recorded as `TD-174` and `TD-175` and as the sixth amendment in §5.2, `WP 18.1C`, with an explicit guard the Product Owner gave the same day: TempestOS is not to become an ERP or a PLM system. The Part gets what a calculation and a drawing need from it, and nothing that exists to manage procurement, stock, occurrences or change. It is the clearest confirmation the review could have asked for of §4.5's lesson: the surfaces were composed from the platform's primitives, and the objects those primitives expose were never specified from the engineer's side. Everything in `18.1B` remains with the programme.

**Second postscript, 2026-09-09.** The Product Owner then asked the question this review stopped short of: whether Tempest is the place for doing calculations at all, or a client project system of record that evidence is tagged to. The answer is recorded as `D-028`: calculations are done wherever the engineer does them and recorded in Tempest as cited, checked, issued evidence; nothing new computes in Tempest in `v1.0`; the in-app calculation surfaces stay in place unextended (the Product Owner chose, later the same day, to keep the capability rather than rebuild it later); the product is deliberately not an ERP and not a PLM. `v0.18.0` becomes *Evidence and Check* (37 days, down from 42), `WP 18.1C` is dissolved into `18.0A` and `18.2A`, and the frozen-grammar hazard this review ranked highest disappears with the Work Package that carried it. The recommendation of §8 stands: keep the substrates; what changes is what is built on them. `v0.17.0` was accepted by the Product Owner's smoke tests the same day.

---

## Appendices

- A. ADR verdicts (`evidence/adr-review-0001-0050.md`, `evidence/adr-review-0051-0100.md`, `evidence/adr-review-0101-0147.md`)
- B. Metrics (`evidence/metrics.md`)
- C. Substrate audit and hazard register (`evidence/substrate-audit.md`)
- D. Surface audit (`evidence/surface-audit.md`)
- E. Debt, tests and plan (`evidence/debt-tests-plan.md`)
