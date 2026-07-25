# TempestOS Engineering Governance

## Status

This document is normative. Where any other document in this repository —
including other Academy documents — appears to conflict with it, this document
governs, unless it has been explicitly and deliberately superseded (see
§9, Decision Authority).

This is not a description of process TempestOS aspires to one day follow. Every
rule below codifies practice already exercised, consistently, across WP 2.1
through WP 2.4 and the repository stabilisation work that preceded them —
and, in every release since, through the whole of the Foundation phase (WP
4.0 through WP 4.5B at time of writing) — including the Rejected Designs
Log (§10), formalised partway through that history, and Repository
Organisation and Naming Conventions (§11, §12), formalised at the
Foundation phase's close once both had been observed consistently across
every Work Package to date. Nothing here is aspirational; it is a record
of how TempestOS has actually been engineered, made explicit so it no
longer depends on being remembered correctly.

## Purpose

To remove ambiguity about how engineering decisions on TempestOS are made,
reviewed, recorded, and approved — for a new contributor joining the project for
the first time, and equally for the project's own engineers returning to it
after enough time has passed that memory of the reasoning has faded. This
document is the answer to "how does TempestOS actually get engineered," in one
place, that does not depend on anyone's recollection of a conversation.

---

## 1. Work Package Lifecycle

A TempestOS work package moves through the following stages, in order. No stage
is skipped, and no stage is assumed complete without the evidence §3 (Definition
of Done) requires.

1. **Brief issued.** A work package begins with an explicit, written brief:
   objective, architectural principles or constraints, implementation
   requirements, non-functional requirements, an explicit list of what is *not*
   in scope, required deliverables, and validation steps. Every brief for
   WP 2.1 through WP 2.4 followed this shape; it is the expected shape for every
   future work package, not a pattern specific to those four.
2. **Scope confirmed before implementation begins.** If a brief is ambiguous
   about scope — particularly whether it requires touching a previous work
   package's code — that ambiguity is resolved *before* writing code, not
   discovered afterward. WP 2.4's explicit reasoning for leaving discovery's
   `Activator.CreateInstance` call untouched (see ADR-0008) is the model: the
   question "does this literally-worded objective actually mean touching this
   other component" was answered, in writing, before the decision was acted on.
3. **Implementation on a dedicated branch, never directly on `main`.** Every
   work package to date has been implemented on a feature branch. `main` is
   only updated by an explicit merge, per §7.
4. **Continuous validation during implementation**, not only at the end — every
   build and test run performed during this project's history checked for zero
   warnings and zero errors at each meaningful checkpoint, not merely once at
   completion.
5. **Completion report produced**, per §3 and §4.
6. **Technical Review**, where warranted — see §2 and §10.
7. **Commit(s) created** on the feature branch, structured per §1a below.
8. **Merge and release approval sought explicitly** — see §7 and §10. A work
   package being "complete" and a work package being "merged into `main`" are
   two distinct events, requiring two distinct approvals.

### 1a. Commit Structure

A work package that represents one cohesive, reviewable unit of change is
committed as a single commit, titled to match the work package
(`WP2.4 Dependency Injection`, matching the exact convention already
established). Where a work package's scope naturally decomposes into
independent, separately reviewable pieces — as the repository stabilisation
work did, split into "Complete repository stabilisation housekeeping,"
"WP 2.1 - Framework Discovery," and "WP 2.2 - Runtime Module Manager" as three
separate commits — multiple commits are appropriate, provided each one is
independently coherent and independently buildable in isolation is not required,
but each should represent one identifiable unit of reasoning.

Commits are never combined via `--amend` once pushed, and destructive git
operations (force-push, history rewriting) require explicit approval — this is
not specific to TempestOS; it is the project's baseline git discipline, applied
without exception.

---

## 2. Review Gates

Four review gates exist. A work package does not proceed past a gate without
satisfying it.

1. **Build Gate.** `dotnet build` against the full solution: zero warnings, zero
   errors. Non-negotiable, checked before every commit and before every
   completion report.
2. **Test Gate.** `dotnet test` against the full solution: every test passes,
   including every test from every prior work package — a new work package
   breaking an existing test is a Build Gate and Test Gate failure, full stop,
   regardless of whether the new work package's own tests all pass.
3. **Technical Review Gate.** Applied when a work package introduces a
   non-obvious architectural decision, an asymmetry, a deviation from an
   explicit brief requirement, or a design choice a reasonable reviewer might
   reasonably question. This gate does not require a change — it requires a
   *justification*, in writing, sufficient that a reviewer can accept or
   challenge it on its merits. WP 2.3's Dispose-precondition exchange (preserved
   in Case Study 03 and ADR-0004) is the reference example: a specific,
   falsifiable question was asked ("does Registered → Dispose make sense?"),
   answered with explicit reasoning, and the review concluded, correctly, that
   "if the reasoning is sound, I'd be inclined to accept it" — no code changed,
   but the reasoning is now permanently recorded.
4. **Merge/Release Gate.** See §7 and §10. Distinct from the previous three
   gates: passing them makes a work package *ready* for this gate, not
   automatically through it.

---

## 3. Definition of Done

A work package is Done only when **all** of the following are true:

- The Build Gate and Test Gate (§2) both pass, verified from a clean,
  fully-committed working tree — not mid-edit, not "it worked when I last ran
  it."
- No `TODO` placeholders, no dead code, no commented-out code remain in the
  changed files.
- Every public type and member touched or introduced has XML documentation.
- Every dedicated test category the brief specified has at least one
  identifiable, correctly-named test satisfying it.
- A completion report has been produced (§4).
- Any architectural decision meeting the ADR criteria in §5 has an ADR.
- Any proposed design meeting the Rejected Designs criteria in §10 that was
  considered and not built has an entry in the Rejected Designs Log.
- Relevant Academy documentation (§6) has been created or updated to reflect the
  change — a work package that changes behaviour a prior Academy document
  describes, without updating that document, is not Done.
- Any unrelated issue discovered during implementation has been documented
  (in the completion report's Observations, and in an ADR or Academy update if
  significant enough) — and fixed only if it blocks completion, per the
  Engineering Discipline convention established in WP 2.4's brief and exercised
  in practice when the lock/try-catch construction-failure bug was found and
  fixed as part of that same work package.
- The work remains on its feature branch, unmerged into `main`, until §7's
  approval is explicitly given.

## 4. Documentation Requirements

Every completion report — regardless of work package size — includes, at
minimum:

1. Summary of what was implemented.
2. Files created.
3. Files modified.
4. Architectural decisions made, and the reasoning behind each.
5. Test results (pass/fail counts, not merely "tests pass").
6. Build results (warning/error counts, not merely "it builds").
7. Assumptions made where the brief was silent or ambiguous.
8. Observations — unrelated issues discovered, documented rather than silently
   fixed unless blocking, per §3.
9. A documentation summary — which Academy documents and ADRs were created or
   updated as a direct consequence of this work package (mandatory for every
   work package from the Academy's establishment onward, per the Academy
   foundation task's closing instruction).

This list is the floor, not the ceiling — a work package with unusually
significant architectural impact (WP 2.4's dependency injection container, for
example) warrants proportionately more detail in each section, not a
perfunctory one-liner per heading.

## 5. ADR Creation Rules

An Architecture Decision Record is **required** when a decision meets any one of
the following criteria:

- The decision was not the only reasonable choice available, and at least one
  genuine alternative was seriously considered and rejected (every ADR in
  `docs/adr/` today satisfies this).
- The decision would be expensive, risky, or disruptive to reverse later —
  particularly if reversing it would require breaking a public API or
  contradicting an established convention other code now depends on.
- The decision establishes a convention future work packages are expected to
  follow (e.g., ADR-0003's constructor convention, which later work packages'
  correctness silently depends on).
- The decision resolves an explicit ambiguity or tension between two stated
  requirements (e.g., ADR-0008 resolving the tension between WP 2.4's literal
  wording and the "do not redesign discovery" constraint).
- The decision was specifically challenged and defended under Technical Review
  (§2) — the outcome of that review, whichever way it concluded, is itself
  ADR-worthy.

An ADR is **not** required for implementation details with no genuine
alternative, no future-convention implication, and no review challenge — routine
code that simply does what the brief straightforwardly asked for does not need
one merely because a decision, in the broadest sense, was technically made.

**Format**: `ADR-NNNN-kebab-case-title.md`, numbered sequentially, in
`docs/adr/`, using the template already established: **Status**, **Context**,
**Decision**, **Consequences**, **Future Considerations**. Numbers are never
reused, even for a superseded or later-reversed decision — a superseded ADR is
marked as such in its Status section and a new ADR is created referencing it,
preserving the full history rather than overwriting it.

## 6. Academy Maintenance

The Academy (`docs/academy/`) is a maintained asset, not a one-time deliverable.
Specifically:

- Every work package from this point forward produces or updates Academy
  documentation as part of its own Definition of Done (§3) — this is not a
  separate, optional follow-up task.
- A Work Package retrospective (`03 Work Packages/`) is created for every future
  work package with the same rigour as WP 2.1 through WP 2.4's, following the
  13-section template (Introduction, Purpose, Background, The Problem, The
  Design, Alternatives Considered, Why This Solution Was Chosen, Architectural
  Principles, Benefits, Trade-offs, Common Mistakes, Future Evolution, Key
  Takeaways).
- A **Future Evolution** section that predicted a change is **updated, not left
  stale**, the moment that change actually happens — if WP 2.5 or later
  addresses a gap a prior retrospective's Future Evolution section named (for
  example, singleton disposal tracking, noted in the WP 2.4 retrospective), that
  retrospective is revisited and updated to reflect what actually happened, with
  a note connecting the two, rather than left as an orphaned, unfulfilled
  prediction.
- Engineering Principles (`01 Engineering Principles/`) and Design Patterns
  (`04 Design Patterns/`) documents are updated when a new work package changes
  or extends how TempestOS applies a principle or pattern already documented —
  not duplicated into a new, competing document.
- Case Studies (`05 Case Studies/`) are reserved for decisions significant
  enough to warrant a full narrative treatment beyond what an ADR's terse
  format allows — not every ADR needs a matching case study, but every case
  study should have a matching ADR.
- The Academy's own structure (the folder hierarchy under `docs/academy/`) is
  stable and should not be reorganised casually — a new category of document
  should fit into an existing folder's stated purpose before a new folder is
  proposed.
- `docs/architecture/Platform Service Map.md` is maintained under this same
  obligation, even though it lives outside `docs/academy/`: any work package
  that adds, removes, or changes a platform service's responsibility,
  dependencies, or consumers updates that service's entry as part of the same
  work package's Definition of Done — never as a separate, later pass.

## 7. Release Approval Process

1. A release is only cut from `main`, never from a feature branch.
2. The version to be released is recorded (`VERSION`) and matched by release
   notes (`docs/releases/`) describing what changed, in terms a non-engineering
   stakeholder can follow — summary, new features, improvements, bug fixes,
   validation status, next milestone.
3. The Build Gate and Test Gate (§2) must pass on `main` itself, not merely on
   the feature branch that fed into it, immediately before tagging.
4. An annotated tag is created only after the above are satisfied, and only
   once — a tag, once created and pushed, is not moved or recreated; if a
   mistake is discovered after tagging, a new version and a new tag are cut,
   the old one is never silently altered.
5. **Pushing to the remote — the branch, and separately, any tag — requires
   explicit approval each time.** This is not a one-time authorisation: an
   approval to push on one occasion does not carry forward to the next. This
   mirrors the project's general standing rule that hard-to-reverse,
   shared-state actions are confirmed individually, not blanket-authorised.
6. Merging a feature branch into `main` is itself a release-adjacent action and
   requires the same explicit, per-occasion approval — "approved for merge" on
   one work package is not standing approval for the next.

## 8. Coding Standards Hierarchy

When guidance from more than one source could apply to a given piece of code,
precedence is resolved in this order, highest first:

1. **An explicit instruction in the current work package's brief.** A brief's
   own stated constraints (a "do not" list, an explicit naming requirement, an
   explicit exception type) override every general convention below, for the
   scope of that work package.
2. **This Governance document.**
3. **`docs/academy/06 Engineering Standards/`** — the specific, codified
   conventions (exception design, testing strategy, and any future additions to
   this folder).
4. **Established codebase convention**, even where not yet written down as a
   standard — for example, the base-plus-subtype exception hierarchy pattern,
   or the internal-test-seam pattern, both observed consistently across four
   work packages before this document existed to name them.
5. **General software engineering principle** (SOLID, and the rest of
   `01 Engineering Principles/`) — used to fill genuine gaps or to justify a
   judgment call where no more specific TempestOS convention yet exists, never
   to override an established, more specific TempestOS convention simply
   because the general principle, applied naively, would suggest something
   different.

A conflict between levels is resolved in favour of the higher level; a
perceived conflict between two items at the *same* level (for example, two
established conventions that seem to point different ways) is Technical Review
material (§2), not something to resolve unilaterally.

## 9. Decision Authority

Three tiers of authority govern engineering decisions on TempestOS. Each tier's
authority is bounded; none may act outside its own scope.

### Architecture

The engineer or agent implementing a work package holds authority over
*internal design decisions within the scope of the current brief*: how a class
is structured, which pattern is applied, what a state machine's precise
transition rules are, how an exception hierarchy is shaped. This authority is
exercised, and immediately documented (ADR, Academy update, or both, per §5 and
§6) — it is never exercised silently. Every decision at this tier remains
subject to Technical Review.

### Technical Review

Authority to examine, question, and either confirm or reject an architectural
decision, exercised by whoever is reviewing the work — historically, the
project owner, directly. Technical Review does not require a change to be
accepted; it requires a *sufficient answer*. The standing instruction that
governs this tier, demonstrated directly in WP 2.3's Dispose review, is: ask the
specific, falsifiable question; request the reasoning, not an immediate code
change; if the reasoning holds, accept it and record why; if it doesn't,
require the change. This tier may also originate new governance-level
requirements (as this very document does) — Technical Review is not limited to
reactively auditing engineering-tier decisions.

### Product Approval

Authority over whether engineering-approved, review-passed work actually ships:
merging into `main`, pushing to the remote, cutting and pushing a release. This
authority is never assumed by the engineering tier, regardless of how confident
that tier is that the work is ready — every merge and every push in this
project's history has been, and remains, gated on an explicit, per-occasion
instruction from this tier. Product Approval also owns version numbering and
release timing, which are business decisions, not engineering ones, even though
they are informed by engineering's own build/test evidence.

**No tier substitutes for another.** Architecture cannot grant itself Technical
Review's sign-off by asserting its own decision is obviously correct. Technical
Review cannot grant itself Product Approval's authority to merge or release,
even after confirming a design is sound — review and release are different
questions, and "this is well-designed" does not imply "this should ship now."

---

## 10. Rejected Designs Log

An ADR records what was decided. A **Rejected Design** records the mirror
image: an abstraction, pattern, or capability that was seriously considered
during a work package's design phase and explicitly not built. Both exist
for the same reason — so a future contributor's question already has a
citable answer instead of depending on someone's memory of a conversation.

**An entry is required** when a proposed design was a genuine candidate —
not a passing idea dismissed in one sentence, but something weighed against
real criteria — and a future contributor could plausibly propose it again
without knowing it was already considered and declined. This is deliberately
the mirror of §5's ADR criteria, not a lower bar: "we thought about X and
said no, here's why" is exactly as citable, and exactly as easy to lose to
time, as "we thought about X and said yes."

**Format.** `docs/architecture/Rejected Designs.md`, sequential `RD-NNNN`
entries, never renumbered and never deleted. Each entry states: the design
considered, why it was rejected, how expensive it would be to introduce
later (purely additive and cheap, versus a genuinely different shape that
would require unwinding something), what — if anything — should prompt
revisiting it, and which work package or retrospective it came from.

**Maintenance.** An entry whose rejection is later reversed is marked
**Superseded**, pointing at whatever ADR or retrospective reversed it —
never silently removed, exactly as §5 already requires for a superseded
ADR. A work package that rejects a genuine design candidate adds the entry
as part of its own Definition of Done (§3); it is not a separate,
optional follow-up task, any more than an ADR is.

---

## 11. Repository Organisation

**Formalised at the close of the Foundation phase (`WP 4.5B`)**, codifying
a structure every Work Package since `WP 2.1` has already followed
consistently, rather than introducing a new one.

- **`src/`** holds exactly one namespace tree per project:
  `Tempest.Core` (the platform itself — Configuration, Logging,
  Discovery, Registration, Lifecycle, Dependency Injection, Runtime,
  Events, Plugins, BackgroundServices, Commands, Versioning, and the
  pre-module-pipeline legacy code), `Tempest.App` (the thin console entry
  point), `Samples/Tempest.Samples` (the living reference module set),
  and `Plugins/` (empty by design until a real plugin ships — see
  `docs/governance/Engineering/Plugin Register.md`). A new platform
  capability gets its own namespace folder under `Tempest.Core`
  (`Tempest.Core.BackgroundServices`, most recently) rather than being
  folded into an existing, unrelated one.
- **`tests/`** mirrors `src/`'s own namespace structure directory-for-
  directory (`tests/Tempest.Core.Tests/Modules/` tests
  `src/Tempest.Core/Modules/`, and so on) — a new namespace under `src/`
  gets a matching directory under `tests/Tempest.Core.Tests/`, not tests
  scattered across unrelated existing folders.
- **`docs/`** has five top-level trees, each with one stated purpose:
  `adr/` (Architecture Decision Records only), `architecture/` (standing,
  living architecture reference documents), `academy/` (teaching
  material, organised into the seven numbered categories `Academy
  Index.md` describes), `releases/` (`FOUNDATION.md`, the permanent
  constitution, plus one subtree per release), and `governance/` (the
  register suite, `Governance Index.md`, and standing policy documents).
  A new document belongs in the existing tree whose stated purpose
  already covers it — a new category or top-level tree is proposed only
  once an existing one's purpose genuinely does not fit, mirroring §6's
  own rule that the Academy's folder hierarchy "should not be reorganised
  casually."
- **Repository root** carries only `README.md`, `PROJECT_STATUS.md`,
  `LICENSE.md`, `VERSION`, `global.json`, `Directory.Build.props`, and
  `.gitignore` — every other document lives under one of the five `docs/`
  trees above, never loose at the root. `PROJECT_STATUS.md` is the one
  addition this phase makes to that root list, specifically because it
  must be the first thing a contributor sees, matching `README.md`'s own
  visibility.
- **`archive/`** holds retired, historical code (the Python prototype)
  under its own `README.md` explaining its status — not part of the
  active codebase, receives no further development, and is never
  confused with `docs/releases/`'s own historical release notes.

## 12. Naming Conventions

**Formalised at the close of the Foundation phase (`WP 4.5B`)**, codifying
patterns already applied consistently since `WP 2.1`.

- **ADRs.** `ADR-NNNN-kebab-case-title.md`, sequential, zero-padded to
  four digits, under `docs/adr/`. Never renumbered, never reused, even
  for a superseded decision (§5).
- **Rejected Designs.** `RD-NNNN`, sequential, referenced by number from
  the Work Package retrospective and ADR that considered it — entries
  live inside `docs/architecture/Rejected Designs.md`, not as separate
  files (§10).
- **Work Package retrospectives.** `WPX.Y[-Letter]-kebab-case-title.md`
  under `docs/academy/03 Work Packages/` — matching the Work Package
  number exactly as `WorkPackages.md` itself names it (`WP4.2C-plugin-
  discovery-lifecycle-placement.md` for `WP 4.2C`, for example). A
  Work Package with distinct architecture and implementation phases gets
  two files sharing the same `WPX.Y-` prefix, distinguished by a
  `-architecture`/`-implementation` (or equivalent) suffix.
- **Governance registers.** `Title Case Register.md` (or `Catalogue.md`/
  `Matrix.md` where that noun fits the register's own content better —
  `Event Catalogue.md`, `Traceability Matrix.md`), one register per file,
  under the appropriate `docs/governance/<Category>/` folder.
- **C# types.** `PascalCase` throughout; an interface is its
  implementation's name prefixed with `I` (`IEventBus`/`EventBus`,
  `IHostedServiceManager`/`HostedServiceManager`); an exception type ends
  in `Exception` and derives from its category's own base exception where
  one exists (`PluginException` → `DuplicatePluginIdException`), per
  `docs/academy/06 Engineering Standards/01-exception-design.md`.
- **Test classes.** `<SubjectUnderTest>Tests.cs`, one class per subject,
  under the mirrored `tests/` directory §11 describes — a test-only
  fixture shared across several test classes lives in its own,
  separately-named file (`HostedServiceFixtures.cs`,
  `RecordingLevelLogger.cs`), never bundled inside a `*Tests.cs` file it
  does not itself test.

---

## Closing Note

This document exists because TempestOS has reached the point where "how we
engineer this project" is itself part of what makes the project valuable — not
merely the runtime code it produces. Treat this document with the same
discipline as the code it governs: when practice changes, update it explicitly,
through the same review it describes, rather than letting actual practice drift
silently away from what's written here.
