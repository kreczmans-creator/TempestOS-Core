# How TempestOS Gets Built: the Stages, in Plain Terms

**Written for:** a reader who is not a software engineer and wants to
understand what building this product actually involves, stage by stage,
and why each stage exists · **Reflects:** the way the repository is run
from `v0.17.0` onward (`CONTRIBUTING.md`, the `v1.0.0` programme, the
`v0.18.0` Execution Plan) · **Read next:** `02-plain-language-glossary.md`,
then the chapters this one points at.

**In plain terms.** Software is not written; it is decided, built, proved,
reviewed and released, in that order, and every one of those steps leaves
a document or a file behind in this repository that you can open and read.
This chapter walks through the steps using real examples from TempestOS,
so that when a later chapter says "the Work Package", "an ADR", "the gate"
or "a journey test", you know what kind of thing is meant, where it lives
and what it is for. Nothing here requires you to read code.

## The one idea to hold onto

Every stage below exists to answer a question **before** the next stage
makes it expensive to change the answer. Deciding what to build is cheap
on paper and ruinous in code. Deciding how a saved record is shaped is
cheap before any record is saved and painful after ten thousand are. That
is the whole reason a serious project has stages at all: not ceremony, but
putting each decision where it is still cheap.

## Stage 1 — Deciding what to build

The product's scope is written down in one place:
`docs/releases/v1.0.0/WorkPackages.md`. Its section "What v1.0.0 is"
states, in five plain sentences, what an engineer must be able to do with
the finished product (open a client project; record evidence; have it
checked and issued; record time and raise an invoice; see the business
figures on the Home screen). Everything the programme builds serves one of
those sentences, and everything that serves none of them is frozen,
archived or deleted.

Below that, the work is cut into **Work Packages**: numbered pieces of
work (`WP 18.0A`, `WP 18.2B`) each with a one-line goal, what it depends
on, what it closes, and an estimate in developer-days. A release
(`v0.17.0`, `v0.18.0`, `v0.19.0`) is a named group of Work Packages with a
theme.

When the scope itself changes, that is a **decision** and it gets its own
record. `docs/releases/v1.0.0/D-028 Evidence is the product, calculation
is where the engineer does it.md` is the best example: the Product Owner
asked whether the product should compute calculations at all, or record
calculations done in Excel as evidence. The answer changed what `v0.18.0`
was, and the document records the question, the answer, the reasoning and
exactly what changed as a result, so nobody has to remember the
conversation.

**Who decides:** the Product Owner decides what the product is and what is
worth building. The chief engineer decides how. Neither decides the
other's question.

## Stage 2 — Planning how

For a release, the chief engineer writes an **Execution Plan**
(`docs/releases/v0.18.0/Execution Plan.md` is the model). It says what the
Product Owner will get, groups the Work Packages into **waves** that can
run in parallel because they touch different files, names the files each
one owns so two people never edit the same file at once, lists the
engineering decisions taken while planning so the Product Owner can
overrule them before work starts, records the Product Owner's answers, and
ends with the manual test script the finished build must pass.

Two habits from that plan are worth noticing because they are how risk is
managed rather than hoped away: a **compiling checkpoint** is committed
within the first hour (so a half-finished piece of work is never lost), and
every Work Package carries a **kill switch** — stop and report if the goal
cannot be met without touching a file outside the list.

## Stage 3 — Recording the decisions that constrain the future

Some choices, once made, shape every line of code that comes after them:
how records are saved, what a unit of measurement is, who the software
believes is at the keyboard. Those are recorded as **Architecture Decision
Records** — ADRs — under `docs/adr/`, numbered in order. Open
`ADR-0148` (Evidence) and you will see the shape every one of them has:
**Status** (is it in force?), **Context** (what was true before, and what
forced the question), **Decision** (what was chosen, precisely), and
**Consequences** (what this buys and what it costs — the costs are written
down deliberately).

Why bother? Because code shows *what* was done and never *why*. Six months
on, a decision that looks odd in the code is either a mistake or a reason
you cannot see. The ADR tells you which. `CONTRIBUTING.md` makes an ADR
mandatory for "any decision that constrains future code".

## Stage 4 — Writing the code

The code lives under `src/`. For this product, four parts matter:

- `Tempest.Core` — the platform: how the program starts, saves, logs,
  runs commands, and the engineering objects themselves (a Part, a
  Requirement, a piece of Evidence).
- `Tempest.Workspace` — the engineering workspace: what a project
  contains, what each engineering discipline can do to it, and the
  commands that do it.
- `Tempest.Desktop` — what you see: windows, panels, the ribbon, the
  editors. This is the application you run.
- `Tempest.Harness` — a text-only console version of the workspace used
  to check things quickly without a screen. Not a product.

The rule that keeps them honest is **dependency direction**: the Desktop
may depend on the Workspace, which may depend on Core, and never the other
way round. That rule is not a convention; it is a test that fails the
build if broken (`44-invariants-that-fail-the-build.md`).

A single piece of work is done on its own **branch** (a private copy of
the code that can be changed without affecting anyone else's), committed
in small steps with a message that says what changed and why. The commit
messages in this repository are unusually informative; `git log` is the
first place the Academy's authors looked when writing every chapter.

## Stage 5 — Proving it works

Nothing is believed because it looks right. The tests live under `tests/`
and mirror the code's own layout. Several kinds appear in the chapters:

- A **unit test** checks one small piece of behaviour in isolation: a
  status move that must be refused is refused.
- A **journey test** (also called an acceptance test) drives the real
  application, with no screen attached, through what a user would do:
  create a project, attach a file, restart, find it again. These are the
  ones that catch a product promise that every unit test missed
  (`33-the-product-spine.md` tells that story).
- A **property-based test** does not check one example but a rule over
  thousands of generated ones: "the answer must not change when I change
  the input units" (`52-units-as-a-runtime-dimension-vector.md`).
- An **architecture test** checks a rule about the code's own shape, such
  as dependency direction.
- **Mutation testing** deliberately breaks the code in small ways and
  checks that some test notices. A mutation that survives means a test is
  missing (`41-project-tasks-and-delivery-workflow.md` shows one that
  found a real gap).
- An **adversarial rig** stages a failure on purpose — a disk write that
  fails halfway — to prove the code leaves nothing corrupt behind.

The standard the repository holds itself to is blunt: a test that cannot
fail proves nothing. Several chapters (`49-accessibility-baseline.md`, the
standards chapter `06 Engineering Standards/07-test-determinism-and-suite-hygiene.md`)
are about tests that were found to be exactly that, and what was done.

## Stage 6 — Getting it reviewed and merged

Finished work is proposed for inclusion through a **pull request** (a PR):
a request to merge the branch into `main`, the one branch that is the
product. In this repository the PR description *is* the write-up — what
changed, why, and the evidence — and the template under `.github/`
lists exactly what must be stated.

Before a human looks, the machines do. **Continuous integration** (CI, in
`.github/workflows/ci.yml`) builds the whole product in two configurations,
runs every test, runs the governance health check, and only if all of
those pass does the **CI Gate** go green. The build must produce zero
warnings, because a warning ignored today is a defect tomorrow. `main` is
protected: nothing can be merged into it without the gate.

Then one review, one round. A finding is fixed in the same PR or written
into `BACKLOG.md` as a numbered row of **technical debt** — a known
shortcoming, honestly recorded, with an owner. A finding is never silently
dropped. Only one kind of finding stops a merge: data loss a user can
reproduce from the running application.

## Stage 7 — Releasing

A release is not "the code is done". It is a **gate** re-run on the exact
final commit — build both configurations with warnings treated as errors,
every test, the health check, three consecutive green CI runs — with the
figures written into the Release Notes (`docs/releases/v0.18.0/Release
Notes.md`) and `PROJECT_STATUS.md`. The Release Notes say what a user will
notice, what shipped by Work Package, the figures, and, under **Warnings**,
everything that is known to be rough. That section is the most trustworthy
page in the repository precisely because it lists what is wrong.

Then a person who did not build it runs it. `PHYSICAL_REVIEW.md` is the
script: a clean machine, exact commands, where data is written, a ten to
fifteen minute smoke test. The Product Owner's own Windows runs of `v0.17.0`
and `v0.18.0` each found things no test had (a created Part that hung from
nothing and could not be found; a Libraries tab that never loaded), and each
produced a numbered hotfix round (`WP 17.9.1`–`17.9.4`, `WP 18.9.1`).
`58-where-things-land-and-open.md` is that story.

Only then is the version **tagged** (a permanent name pinned to one commit)
and `release.yml` rebuilds, retests and publishes it. Publication is
recorded by reading the published release's own asset list, not by
assuming the workflow succeeded.

## Stage 8 — Living with it

Every release leaves a `BACKLOG.md` a little different. Debt is paid down
by later Work Packages, whose "Closes" column names the rows. Two
disciplines from the archived `Governance Philosophy.md`
(`archive/docs-2026-09/governance/`) still run through every document you
will read: **unknown is
preferable to invented data**, and a mistake is recorded rather than
smoothed over. The Academy's own welcome page calls this "a note on
honesty"; you will find its evidence in nearly every chapter.

## What "done" means here

`CONTRIBUTING.md` lists it. A Work Package is done when it ships with:

- a build at 0 warnings, 0 errors, in both configurations;
- every test passing in both configurations;
- the architecture invariants green;
- an ADR for any decision that constrains future code;
- a row in `PHYSICAL_REVIEW.md` §7 for any new user-facing surface;
- one Release Notes line.

There is no separate retrospective, readiness review or certification any
more. From `v0.4.0` to `v0.16.0` there were, in volume, and the repository
decided at `WP 17.0B` that producing them had come to cost more than the
product they protected. The Academy chapters from `42` onward exist
because that reset also stopped the per-Work-Package Academy writing; they
were written afterwards, from the commits, the ADRs and the code, to fill
that gap (`06 Engineering Standards/09-the-governance-reset-and-how-a-release-is-now-run.md`).

## One Work Package, end to end

Follow `WP 18.0A`, the Evidence record, through every stage above:

1. **Decided** as a row under "Release `v0.18.0`" in
   `docs/releases/v1.0.0/WorkPackages.md`, re-scoped by `D-028`.
2. **Planned** as wave 1 of `docs/releases/v0.18.0/Execution Plan.md`,
   which names the files it owns (`src/Tempest.Core/Evidence/*`, the
   Workspace registration, one line in `DisciplineAreas.cs`) and the files
   it must not touch.
3. **Recorded** as `docs/adr/ADR-0148-…md`: Evidence is a canonical Kind
   with its own status vocabulary, citing only released reference data,
   refusing rather than throwing.
4. **Written** under `src/Tempest.Core/Evidence/` and
   `src/Tempest.Workspace/Evidence/`, in five commits whose messages read
   as a diary of the work (`git log --grep="WP 18.0A"`).
5. **Proved** under `tests/Tempest.Core.Tests/Evidence/`, with the
   independence rule tested in both positions.
6. **Merged** by the lead as merge commit `0655750`, after the gate.
7. **Released** as a row in the `v0.18.0` Release Notes and as §7a of
   `PHYSICAL_REVIEW.md`, the evidence journey by hand — where the first
   Windows run found the Libraries tab never loaded, fixed as `WP 18.9.1`.
8. **Living with it**: the Release Notes' Warnings list the issue sheet's
   size, the three-transaction issue, and `TD-157`, a warning that cannot
   yet fire, owned by a `v0.19.0` item.

The chapter that tells this story properly is `59-evidence.md`; the ones
around it (`60`–`64`) are the same release, stage by stage.

## How to read a chapter of this Academy

Every chapter from `42` onward opens with a one-line header (release, Work
Packages, debt rows, decisions, code) and then an **In plain terms**
paragraph written for you. After that the sections are narrative — each
heading states a point — and the chapter closes with **What to take away**:
one to three lessons that transfer beyond this product. If a section gets
too technical, skip to the next heading; the plain-terms paragraph and the
take-aways are enough to follow the thread of the whole Academy.
