# The Physical Review and the Release Gate

**Release:** `v0.14.0`–`v0.18.0` · **Work Package(s):** `WP-REVIEW`,
`WP 16.1A`, `WP 16.1B`, `WP 16.1A-R1` · **Debt:** `TD-45` (closed,
`WP 17.0B`), `TD-42` (open), `TD-116` · **Code:** `PHYSICAL_REVIEW.md`,
`.github/workflows/ci.yml`, `.github/workflows/release.yml`,
`scripts/governance-healthcheck.ps1`, `scripts/new-release.ps1`

**In plain terms.** A test suite tells you the code does what the code
expects of itself. It cannot tell you that a stranger can take a fresh
copy of the project, put it on their own machine, and actually use it —
a test never has to find the right file, follow the instructions, or
notice a button that silently does nothing. This chapter covers the two
things TempestOS built to close that gap: a written procedure a stranger
can follow on a clean machine (`PHYSICAL_REVIEW.md`), and a release
gate — a fixed set of checks re-run on the exact commit about to ship,
with the results written down rather than remembered.

## Tests passing is a different claim from "it works"

Through `v0.13.x`, every Work Package retrospective reported a Build
Gate and a Test Gate, machine-checked from `v0.11.0` on
(`04-continuous-integration.md`). That is a claim about the code talking
to itself. It says nothing about whether a person with no prior
knowledge of the repository can clone it, launch the real window, and do
something useful — a suite can be green while a README describes
directories deleted two releases ago, or on a platform where the
application does not launch at all, because the tests never open a real
window there. A **physical review** is the deliberately blunt check that
catches this: install it, launch it, use it, on a machine carrying none
of a development machine's accumulated state.

## `WP-REVIEW`: proving the clean machine, not assuming it

`WP-REVIEW` (`a13d1c3`, 1 September 2026) ran a genuine fresh clone with
an isolated package cache and nothing on `PATH` but the pinned SDK. It
disproved two worries — no undocumented machine state, no
non-deterministic first run — and confirmed a third: **the desktop
application would not launch on Linux.** A `TypeLoadException` during
Avalonia's X11 start-up, before any window existed, caused by a security
pin the test suite never noticed because headless tests never
initialise X11 at all. **A test suite that passes on a platform the
application cannot launch on is telling you something about the test
harness, not the product.** The fix was a real trade-off — reinstate a
known advisory, or upgrade Avalonia — so `WP-REVIEW` deliberately left
it unfixed and filed it as `TD-116` rather than choosing implicitly.
(`WP 16.5B` later resolved it; see `PHYSICAL_REVIEW.md` §8.) The other
product was `PHYSICAL_REVIEW.md` itself, written, in the retrospective's
own words, "only against behaviour that exists today."

## What `PHYSICAL_REVIEW.md` contains, and why

Nine sections, each answering one question a person at a clean machine
actually has: **(1) minimum environment** — one mandatory install, what
is CI-verified where, and an explicit "not required" list; **(2) build
and test**, the literal commands and the current, re-derived test counts;
**(3) launching**, including the traps that cost real review time — the
working directory decides where data lands, the title bar's commit hash
must match the build, only one instance may hold a data folder; **(4)
where runtime data lives**, so "it lost my work" is never confused with
"it looked in a different folder"; **(5) external dependencies**, stated
as none; **(6) clean reset**, one exact command; **(7) the smoke test**
itself — a fixed, numbered sequence pairing each action with what
specifically counts as failure, plus §7a's evidence journey added for
`v0.18.0`; **(8) known limitations**, stated plainly rather than found by
accident; **(9) if something goes wrong**, a symptom-to-section table.

`v0.14.0` was the first release prepared **specifically for a physical
review on Windows** — its Release Notes say so directly, pairing a
Validation status table with a platform-qualification table: Windows
verified, macOS expected but unverified, Linux building and testing
clean but not launching.

## Closing half a control: `WP 16.1A`

The governance health check (a machine check that a project's own
paperwork still matches its code) had existed since `WP 11.2A`,
deliberately advisory. `WP 16.1A` (`6338330`) made `CI Gate` depend on
it: from that commit, a governance failure turns the whole gate red, not
a note in a job summary. The retrospective is candid this closed only
half of `TD-45` — the GitHub branch-protection setting that actually
disables the Merge button needed a repository administrator, which no
engineering session can act as. **A control with a machine half and a
human-administered half is not done when the machine half lands.**
(`TD-45` closed for real once that setting was configured;
`CONTRIBUTING.md` now records `CI Gate` as a required status check.)

## Sixteen checks, and a namespace hidden by three invisible bytes

`WP 16.1B` (`b0a4150`, `8c0e791`) grew the health check from 8 checks to
16, changing what each compares against: instead of cross-checking one
governance register against another — which can drift together and
never disagree — the new checks derive the expected answer from source
directly. Its first real run found two genuine, previously undisclosed
defects. The sharper one: `src/Tempest.Core/Models/ProjectModel.cs`
opens with a UTF-8 byte-order mark — three invisible bytes before its
`namespace` line. The register's own documented derivation command,
anchored on the true start of the line, silently never matched this
file; an earlier manual re-derivation, built on the identical command,
had filed it under "no namespace declared." The new check used a tool
that strips a leading mark by default, and so saw what the file actually
declared. **Reading the register harder would never have found this;
deriving the same fact a second way, differently, did.**

## The release path was weaker than the merge path

`WP 16.1A` had hardened the path into `main`. The `v0.16.0` review board
asked whether the path that actually tags and publishes was equally
strong. It was not, and `WP 16.1A-R1` (`ac7daa6`) closed four gaps:
`release.yml` never ran the governance health check at all and now does,
as a hard gate; `new-release.ps1` printed a warning on a non-green CI
run and **continued anyway** — the board caught, live, a run that
completed `cancelled` rather than `failure` because a later push had
superseded it, which a "block on failure" check would have waved
through, so the script now requires the literal outcome `success` (a
`-SkipCiCheck` flag exists for the one legitimate case, and using it
moves the responsibility onto the person who confirmed the run
themselves); the advisory Linux launch check had accepted "still running
at timeout" as proof of a healthy launch, when a process hung after
start-up looks identical, so it now also requires a positive log marker
from a real launch; and `PHYSICAL_REVIEW.md` itself had drifted into
contradicting itself about whether Linux could launch at all.

## A green run reporting red

Not every gate defect makes the gate too soft. In one CI run, the tests
had all passed and the job still failed: a diagnostic summary step tried
to read a results file whose name had been de-duplicated with a `[1]`
suffix, and the command it used reads square brackets as a wildcard
rather than literal text, so it could not find a file that plainly
existed. The fix (`ad7ecf4`) was narrow — read the file literally, and
mark that summary step unable to fail the job, since it had already
called itself diagnostic in its own comments but nothing enforced that
promise. **A step that only reports must be structurally unable to fail
the gate it reports on**, or its own bugs are exactly as damaging to
trust as a real failure hiding behind a false pass.

## The gate as it is actually run, at `v0.17.0` and `v0.18.0`

Once `v0.17.0` and `v0.18.0` were run from a single release branch under
an Execution Plan
(`09-the-governance-reset-and-how-a-release-is-now-run.md`), the gate
took a fixed shape, stated in `v0.18.0`'s own
Execution Plan §5: build both configurations with `TreatWarningsAsErrors`
and grep every `error|warning` line rather than trust the exit code
alone, `dotnet test --no-build` for both test projects in both
configurations, and the health check (now five checks, after `WP 17.0B`'s
reduction) at 5/5 — plus, before a release candidate is accepted, three
consecutive green CI runs. The figures are re-derived directly on the
release-candidate commit, not carried forward: `PROJECT_STATUS.md`
records `v0.18.0`'s gate against head `2f4486c` — 3,991 Core tests and
532 Desktop tests passing in both configurations, 0 warnings, 5/5
architecture-invariant tests, 5/5 governance checks, CI green on every
pushed head from the first merge onward — and the same figures appear in
the Release Notes, so the claim a reviewer reads is the one the release
was actually gated on.

One warning stands as `v0.17.0`'s own Release Notes state it: by
decision, nothing was pushed to a shared branch until the Product
Owner's own Windows verification, so **CI first ran on that branch at
release time**, not throughout its development — every gate figure up
to that point had been produced locally. A genuine, disclosed weakening,
accepted deliberately; the eventual push and pull request did run the
full pipeline, with branch protection requiring `CI Gate` green on the
merged head before the tag.

## The payoff: what only a person's hands find

`WP 17.9.1` through `WP 17.9.4` are named, in their own commits, as
"hotfixes from the first Windows review of `v0.17.0`" — the Product
Owner's own session found the Project Explorer missing on entering
Engineering, a raw Windows security identifier where a name belonged,
and that a newly created object did not open where it had just been
created. None of these were things the automated suite disagreed about;
nobody had asked it to check them, because nobody had walked the exact
path a first-time user walks. `WP 18.9.1` repeats the pattern for
`v0.18.0`: the Libraries tab loaded blank beside the Evidence area,
invisible to every existing test because each one refreshed the view by
hand, which a real user never does. See `58-where-things-land-and-open.md`
and `63-the-evidence-workspace.md` for what those fixes changed; this
chapter's point is narrower — **a physical review is not only a check
that the product works, it is where certain defects can only be found.**

## Standing rules for a contributor

- Run `PHYSICAL_REVIEW.md` §2's commands locally before pushing — they
  are the same commands CI runs.
- Treat a `Fail` from `governance-healthcheck.ps1` as blocking both
  `CI Gate` and, since `WP 16.1A-R1`, `release.yml`.
- Never treat "did not fail" as "succeeded" — a workflow run can
  conclude `cancelled`, which is neither; check for the positive result.
- Any diagnostic or summary step must be unable to fail the gate it
  reports on.
- Confirm a publication by reading the Release's own asset list on
  GitHub, not a script's printed banner — `TD-42` is still open because
  `new-release.ps1`'s `git tag`/`git push` calls never check
  `$LASTEXITCODE`.
- The whole gate today: `ci.yml` runs `Build & Test (Debug)`,
  `Build & Test (Release)`, `Governance Health Check`, the advisory
  `Linux Launch Smoke`, a scheduled `Mutation Testing` job, and
  `CI Gate`; `release.yml` triggers only on a pushed version tag and
  runs `Build, Verify & Publish Release` on `windows-2022`.

## What to take away

- **A green test suite is a claim about the code, not about the
  product** — only a person, on a machine that was not built to run
  this code, closes that gap.
- **A gate is not a policy until it is machine-enforced on every path
  that matters** — the tagging path was quietly weaker than the merge
  path everyone had just hardened.
- **A check that reports must never be able to fail the thing it
  reports on**, or its own defects masquerade as the product's.
- **Deriving a fact a second, independent way finds what re-reading the
  first way never will** — a namespace was invisible to the very tool
  meant to catch drift, until a different tool's default behaviour
  disagreed with it.
