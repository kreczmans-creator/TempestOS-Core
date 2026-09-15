# CI in Shards, and the Gate at Scale

**Release:** `v0.19.1` → `v0.20.0` release candidates (`release/v0.19.1`,
`release/v0.20.0`); quality items carried into the `v0.21.0` execution
plan (opened, no code yet) · **Work Package(s):** `WP 19.9.1`,
`WP 19.10M`, `WP 20.3D`, `WP 20.9.0`; `v0.21.0` plan `WP 21.5C`–`21.5F` ·
**Debt:** `TD-42` (closed), `TD-154` (closed), `TD-183` (open) ·
**Code:** `.github/workflows/ci.yml`, `scripts/new-release.ps1`,
`stryker-config.json`

**In plain terms.** Every change to TempestOS runs an automatic
checklist before anyone trusts it: build the program, run every test.
That checklist has a pass-or-fail result, but also a second number
nobody notices until it becomes a problem — how long it takes. The
script that runs the checklist is itself software, with its own bugs,
invisible in review because nobody reads a workflow file the way they
read a class. This chapter is the release the suite outgrew its time
budget, the script bugs found while buying it back, and the races a
faster checklist then found that a slower one had hidden.

## An empty upload fails the leg

The layout walk (`68-the-layout-walk-and-the-composer.md`) renders every
screen for real and checks nothing has collapsed to nothing; in CI it
also uploads its screenshots, so a reviewer can see what it saw.
`6bf0319` fixed a case where that evidence never arrived: its output
path was relative, and the test host's working directory during a CI
run is the compiled test project's own folder — so the PNGs landed
under `bin/`, the upload found nothing, and it only warned
(`if-no-files-found: warn`, `continue-on-error: true`) and let the leg
pass anyway. The fix made the path absolute and turned that warning
into a hard `error`.

This looks like it breaks the rule in
`08-the-physical-review-and-the-release-gate.md` that a step which only reports must never fail the
gate. It does not: a build-log summary reports on tests that already
passed or failed on their own terms, so its own failure is noise — but
the layout walk's screenshots are not a summary, they **are** the
walk's evidence, and a walk producing none proved nothing however its
assertions read. **Whether a step may fail the gate depends on what it
proves, not on whether it looks like "just an upload."**

## A suite that outgrew its own ceiling

By `WP 19.9.1` the Desktop suite had grown to 594 tests. `232291c`
records what that did: on a hosted runner, the Debug leg's Test step
alone took 43 minutes against a 45-minute whole-leg ceiling
(`timeout-minutes`, the point GitHub simply kills the job). The very
next run was cancelled at that line — not failed — while its tests were
still passing, several seconds each; nothing had hung, the per-test
backstop never fired. A cancelled run is neither a pass nor a failure,
the same trap that chapter names from the other side: real, passing work with
nothing to show for it. The fix said so plainly rather than pretending
the new number was always right: the ceiling went from 45 to 90 minutes.

`8a5de24` found where the time was going: **coverage instrumentation** —
bookkeeping code injected while tests run, to record which lines
executed — ran on both legs, and switching it on roughly doubled the
suite's time. It moved to the Release leg only; the summary step
already stated plainly when no coverage file existed, so Debug's now
says that honestly rather than lying.

## A build script is still a script

The same commit introduced a bug of its own. It computed a PowerShell
variable meant to add the coverage switch on Release only — but the
line meant to splice it into the `dotnet test` command was left blank,
so no leg collected coverage at all, and nothing failed loudly: the
command simply ran without that switch and reported success as normal.
`5abeb57`'s own message names the cause — "a Perl interpolation had
emptied it": the edit was made by a text-substitution tool, not typed
by hand, and it silently discarded the value it was meant to insert.
The same commit closed `TD-42` in `scripts/new-release.ps1`: its `git
tag`/`git push` calls never checked `$LASTEXITCODE`, so a failed tag or
push printed nothing wrong and the script carried on as if the release
had shipped. Three explicit checks now `throw` on the first non-zero
exit code.

A quarter of an hour later, `4e69525` found a stranger relative: splatting a
*conditional* argument list into a native command under `pwsh` did not
do what it looks like — on run `34895195651` the switch reached
`dotnet` one character at a time rather than as one argument. The fix
wrote two full steps, one per configuration, each spelling its own
command out with nothing conditional inside it.

**Read as one lesson:** a workflow file or release script is code, with
real failure modes — PowerShell's own splatting and interpolation
rules, here — just as capable of silently doing nothing while
reporting success as a bug in `Tempest.Core`. Only reading what a step
actually did catches this, not trusting that a green tick means it did
what its author meant.

## `TD-154`: proving the shell composed, not just the host

`63d8b94` fixes a narrower version of the same trap. The advisory
`linux-launch-smoke` job judged a still-running process healthy once its
log reached `TempestHost.EnterRunning`'s "Host -> Running." line — which
fires deep inside `WorkspaceHost.StartAsync`, before `MainWindowComposer`
had built a single view. A hang while actually composing the Desktop
shell looked identical to a healthy launch. `MainWindowComposer.Layout`
now logs a second marker, "Desktop -> Composed.," once every view,
dialogue, overlay and the docking workspace it assembles already
exists; the smoke job requires both, in order, and names which is
missing.

## `WP 20.3D`: sharding instead of waiting

Raising the ceiling bought time; it fixed nothing. `453cc64` and
`20e8eb1` split one long run into several shorter ones that run at
once. `Tempest.Core.Tests` stays a single, unsharded leg — already fast,
about five minutes. `Tempest.Desktop.Tests` becomes three shards,
selected by the first letter of each test class's own **namespace
segment** — its sub-folder, such as `Layout`, or the class's own name
where it has none — not by hand-listing classes: `desktop-1` is A–D,
`desktop-2` is E–P, `desktop-3` is Q–Z, at 195 / 219 / 214 of 628 tests.
Any class written from now on falls into exactly one shard by its name
alone, with no list to maintain; rebalancing later means moving one
letter boundary.

Two details matter. `Category=LayoutWalk`'s five tests live entirely
under `Layout`, so the first section's screenshot rerun is pinned to
that one shard only — run on every Release shard it would find nothing
on the other three and error on all of them. And the shipped-application
build artefact publishes from `core` alone: every Release shard's Build
step produces an identical binary, so uploading it four times would
only burn the same storage quota the workflow already records as a
real, previously-hit limit. `timeout-minutes` returns to 45.

The first hosted timings, on run `34916910336`: individual shards
between 5 and 14 minutes, the whole CI Gate green in about 16 minutes
wall-clock — against a single Desktop leg that had needed a
45-to-90-minute ceiling to survive one run. Sharding did not shrink the
work; it let the machine do it at once instead of in a queue.

## A faster gate finds what a slower one hid

`WP 20.9.0` re-ran the sharded workflow on the `v0.20.0` candidate and
hit two real failures on the same head, `94cbb5ba`. One was paperwork:
the governance health check caught a missing `ADR-0153` row in the ADR
Register. The other is the interesting one: `ProjectAreaAcceptanceTests`
asserts that a document's row names where it went, but that note is
written on the open's own continuation, after the viewer is already
showing — and CI's Debug shard reached the assertion before the
continuation had run. `4d06cf1`'s fix is the idiom
`07-test-determinism-and-suite-hygiene.md` already names: a bounded,
re-reading poll on the real state, the assertion itself untouched.

The race had presumably always existed; sharding changed the timing
conditions enough that it actually fired, on a commit the old, slower
single leg had run clean. **A faster or differently-shaped CI does not
just save time — it changes which timing defects get the chance to show
themselves.** The same Work Package raised, rather than quietly working
around, a third one it could not fix on the spot: `TD-183`, an OAuth
loopback test (`66-outbound-invoicing-and-the-connector-seam.md`) that
binds a real port and collided once a night under ten concurrent
agents — filed unowned, not patched under pressure.

## What stayed advisory

`stryker-config.json`'s mutation-testing job
(`07-test-determinism-and-suite-hygiene.md`) has run advisory since
`WP 17.0C`, with a 70% break threshold already set in its own config;
the measured score on the `v0.20.0` candidate is 67.58%, below it
already, invisible only because the job cannot fail anything. `WP 21.5D`
answers the surviving mutants with real assertions until the threshold
is met — not by lowering the number to match the score. `WP 21.5E` adds
`dotnet list package --vulnerable` as a required check, Dependabot and
third-party notices; `WP 21.5F` SHA-pins every action and moves every
job to least-privilege permissions; `WP 21.5C` adds a run of the real
built application on the Windows runner, driven by UI Automation
through a mouse-only smoke journey — what sharding cannot replace, a
headless suite passing on a shell the real one fails on.

## Standing rules for a contributor now

- **Name the shards.** `core`, `desktop-1` (A–D), `desktop-2` (E–P,
  carries `Category=LayoutWalk`), `desktop-3` (Q–Z) — a new test class
  needs no list updated, only to exist.
- **Never raise a ceiling without saying why.** Every `timeout-minutes`
  change carries the measured minutes and the run that forced it, so
  the next person can tell a deliberate decision from a number nobody
  has looked at since.
- **A diagnostic step must not fail the gate it reports on — unless the
  step is itself the evidence,** in which case it must: a build-log
  summary is the former, the layout walk's screenshots the latter.
- **Check exit codes in scripts.** `TD-42` existed because `git tag`/
  `git push` went unchecked; the coverage splat bug existed because
  nothing could see, from a green run, that the switch had vanished.

## What to take away

- **A test suite has a time budget as well as a result** — outgrowing it
  produces its own failure, a cancelled, still-passing run that is
  neither a pass nor a genuine defect.
- **A build script is code, with its own silent failure modes** — the
  coverage switch vanished with no red anywhere, because nobody was
  reading the command it actually produced.
- **Sharding changes what CI's timing can expose, not just its
  length** — a race two commits found the same week it shipped is
  the proof that re-running for real, not just for less time, mattered.
