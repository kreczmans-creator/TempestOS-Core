# Engineering Standard: Continuous Integration

> **Status note, September 2026.** The pipeline has grown since this was
> written: the CI Gate now also requires the Governance Health Check
> (`WP 16.1A`), the release workflow runs the same check and refuses to tag
> a commit whose CI did not succeed (`WP 16.1A-R1`), an advisory Linux
> launch smoke job and a scheduled mutation-testing job were added, and
> the health check itself was reduced to five source-derived checks plus a
> Markdown budget (`WP 17.0B`). See
> `08-the-physical-review-and-the-release-gate.md` for the gate as it is
> run today and `.github/workflows/ci.yml` for the current job list.

## Purpose

Every Work Package retrospective since `WP 2.1` has asserted a Build Gate
and Test Gate result — "N/N tests passing," "0 Warnings/0 Errors, Debug
and Release both clean" (Engineering Governance §2, §3). Through
`v0.10.0`, every one of those assertions was a manually-run, self-
reported claim: a contributor ran `dotnet build`/`dotnet test` locally
and wrote down what they saw. `WP11.0A Platform Architecture Review.md`
named this plainly (finding `R-1`): nothing prevented a regression from
reaching `main` other than the discipline of whoever was running the
Work Package — a discipline this project's own history shows to be
genuinely strong, but one a platform aiming at `v1.0` should not need to
rely on trust alone to verify.

`WP 11.1A` closes that gap. `.github/workflows/ci.yml` is now the
authoritative, machine-run check of exactly the same two claims every
retrospective has always made — it does not change what "done" means
(Engineering Governance §2/§3 are unchanged), it changes who checks it.

## CI Philosophy

**The pipeline verifies existing standards; it does not set new ones.**
Every gate the workflow enforces — build cleanly in both configurations,
pass every test — is a gate this project's own governance already
required of every Work Package. Nothing about `v1.0` readiness required
inventing a new bar; it required making the existing bar impossible to
quietly miss.

**Verification, not gatekeeping for its own sake.** The pipeline exists
to give an honest, reproducible answer to "does this actually build and
pass," not to add process weight. It runs the identical commands a
contributor already runs locally (`dotnet restore`/`build`/`test` against
`src/TempestOS.slnx`) — nothing in the pipeline is CI-only magic a
contributor cannot reproduce on their own machine before ever pushing.

**Warnings are now a build failure, not a visual check.**
`Directory.Build.props` sets `TreatWarningsAsErrors` to `false` —
unchanged by this Work Package, so a local `dotnet build` still behaves
exactly as it always has. The CI workflow instead passes
`-p:TreatWarningsAsErrors=true` as a build-invocation override, at the
CI step only. This was verified safe before being adopted, not assumed:
the tree at the point this pipeline was written builds with zero
warnings in both configurations under this flag, confirmed by a real,
local `dotnet build` run — so this closes a verification gap without
introducing a new failure mode on day one.

**Fail loudly, disclose fully.** Every build/test step checks its own
exit code explicitly and fails the job immediately on the first
build error or test failure — no step continues past a failure it
should have stopped on. The summary-publishing and artifact-upload steps
run even when an earlier step failed (`if: always()`), so a failing run
still surfaces exactly what happened, the same "disclose what actually
happened" instinct this project applies to its own Technical Debt and
Governance registers, applied here to its own CI output.

## Build Pipeline

`.github/workflows/ci.yml` runs on:

- every **pull request**, whatever its branch;
- a **push** to `main`, to a `release/**` branch, or of a `v*.*.*` tag,
  and to nothing else: since 2026-10-01 a push to any other branch starts
  no run, because its pull request already runs the same workflow;
- **manual dispatch** (`workflow_dispatch`), which is how to run CI on a
  branch that has no pull request yet.

One job, `build-and-test`, runs as a 2 × 4 matrix: two configurations
(`Debug`, `Release`) times four test shards (`core`, the whole
`Tempest.Core.Tests` project, and `desktop-1`/`desktop-2`/`desktop-3`,
`Tempest.Desktop.Tests` split by the first letter of each test's
namespace segment, A–D, E–P and Q–Z). That makes eight legs on separate
runners, with `fail-fast: false` so one leg's failure never hides
another's result. The `gate` job (named `CI Gate`) depends on every leg,
on the governance health check and on the dependency scan, and gives
branch protection one named status check to require rather than eight.

**On a pull request only the Release legs run tests** (`ADR-0160`, PO
decision 2026-10-01). The four Debug legs still restore and build with
warnings as errors, so a Debug-only compile break is still caught, but
their test steps are skipped. A push to `main`, a `release/**` branch or
a tag, a manual run and the weekly schedule run the full Debug and
Release test matrix, so every commit that lands is tested in both
configurations. Skipping is per step, not per job, so all eight legs
exist on every event and their names never change. `CI Gate` still
requires every leg to report `success`: a skipped step leaves its leg
green, while a failed or cancelled leg turns the gate red.

Each matrix leg:

1. Checks out the commit.
2. Installs the exact .NET SDK named in `global.json` (`10.0.302`,
   `rollForward: latestFeature`) via `actions/setup-dotnet`'s own
   `global-json-file` input — the identical single source of truth every
   local build already reads, so the SDK version cannot drift between a
   contributor's machine and CI.
3. Restores in locked mode (below), then builds `src/TempestOS.slnx`
   for its own configuration, with warnings promoted to errors (above).
4. Runs its own shard of the test suite (`Tempest.Core.Tests`, or one
   third of `Tempest.Desktop.Tests`, which exercises real Avalonia
   headless UI, not a mock) with TRX results written per configuration
   and shard (Release legs only on a pull request, above). Across the
   eight legs every test runs once per configuration; `CiShardCoverageTests` fails if a Desktop test falls
   outside the shard filters or inside two of them.
5. Publishes a Markdown build/test summary to the run's own Job Summary,
   and uploads the build log and TRX results as downloadable artifacts —
   always, even on failure, so a failing run is diagnosable from the
   Actions UI alone, without needing to reproduce it locally first.
6. The Release `core` leg additionally uploads the built
   `Tempest.Desktop` and `Tempest.Harness` output as two downloadable
   artifacts — a smoke-testable build of the exact commit, not a promise
   of one.

The runner image (`windows-2022`) is pinned explicitly rather than using
the floating `windows-latest` alias, for the same reason this project
pins every package version exactly (`Avalonia 11.2.3`, the SDK via
`global.json`): an unannounced runner-image change should never silently
change CI behaviour.

**Every restore is locked** (`ADR-0160`, PO decision 2026-10-01). Each
project commits a `packages.lock.json` that pins every direct and
transitive package by version and content hash
(`RestorePackagesWithLockFile` in `Directory.Build.props`). Every
`dotnet restore` in `ci.yml` and `release.yml` runs with
`--locked-mode`, and `RestoreLockedMode` is on whenever `CI=true`, so
implicit restores (the installer's `dotnet publish`) are locked too: a
lock that no longer matches a `PackageReference`, or a package whose
content changed on the feed, fails the restore (`NU1004`/`NU1403`)
instead of resolving something new. Every project also declares
`RuntimeIdentifiers` `win-x64;linux-x64`, because a lock records the
runtime identifiers it was restored for and a RID-specific restore
(`dotnet publish -r win-x64`) spans every project in `Tempest.Desktop`'s
graph; with both RIDs in every lock, the Windows legs, the release job's
`-r win-x64` publish and the Linux smoke job all restore against the
same committed files. Dependabot's `nuget` updates rewrite the affected
lock files in the same pull request. After changing a
`PackageReference` by hand, regenerate and commit the locks:

```
dotnet restore src/TempestOS.slnx --force-evaluate
```

## Release Verification

The pipeline is the mechanical realisation of Engineering Governance §2's
Build Gate and Test Gate — from `WP 11.1A` onward, "Build Gate: pass" and
"Test Gate: pass" in a Work Package retrospective can cite a specific,
green CI run rather than a local session's own output. It is not, on its
own, a release-readiness certification: the existing, heavier-weight
release-readiness review (mirroring `WP 6.8`/`WP 7.4.0`/`WP 8.9.0`/
`WP 9.9.0`/`WP 10.9A`'s own precedent) still performs the full governance
cross-check, ADR audit, and Technical Debt/Future Capability review a
green build alone does not cover. What the pipeline removes is the
possibility that a release-readiness review's own build/test claim is
wrong because no one re-ran it from a clean checkout — the exact,
recurring risk `WP11.0A` named.

`docs/releases/v0.11.0/WP11.0B Architecture Roadmap.md` names this
pipeline as `WP RC.0A`'s own prerequisite: the `v1.0.0` release-readiness
review is expected to cite a real CI run, not re-derive the Build/Test
Gate result locally a final time.

## Engineering Workflow

**For a contributor:** open a pull request and the pipeline runs on it
automatically, again on every push to its branch. A push to a branch
with no pull request runs nothing; to run CI on such a branch, start the
workflow by hand (**Actions → CI → Run workflow**). A failing run's
Job Summary names which configuration and shard failed and shows the build-error
or test-failure count directly, before anyone needs to open a log file.
The same commands the pipeline runs are exactly what to run locally
first:

```
dotnet restore src/TempestOS.slnx --locked-mode
dotnet build src/TempestOS.slnx -c Debug   -p:TreatWarningsAsErrors=true
dotnet build src/TempestOS.slnx -c Release -p:TreatWarningsAsErrors=true
dotnet test  src/TempestOS.slnx -c Release
```

A pull request is not tested in Debug (above), so a Debug-only test
failure shows up on the push run after merge. If a change touches
`#if DEBUG` code or Debug-only behaviour, also run
`dotnet test src/TempestOS.slnx -c Debug` before pushing.

**For a Work Package's own Definition of Done:** the Build Gate and Test
Gate (Engineering Governance §2/§3) are unchanged in substance — "verify
from a clean, fully-committed working tree" now means "confirm the CI
run for this commit is green," in addition to (not instead of) running
the same commands locally before pushing. A Work Package is not Done
because CI is green; CI being green is now part of how Done is verified,
the same relationship the Build/Test Gates have always had to a manual
run.

**What this standard deliberately does not claim.** The pipeline runs on
one platform (`windows-2022`) — this project has never verified
cross-platform correctness despite `Tempest.Desktop` depending on a
cross-platform framework (Avalonia); extending the matrix to Linux/macOS
runners is a genuine future enhancement, not silently assumed to already
work. Merges to `main` are gated: `CI Gate` is a required status check on
`main`'s branch protection (see `CONTRIBUTING.md`).

## Related Documents

`.github/workflows/ci.yml`; `ADR-0160` (pull requests test Release only;
the release is gated on `CI Gate`; NuGet lock files and locked restore); `docs/releases/v0.11.0/WP11.0A Platform
Architecture Review.md` (finding `R-1`, the source of this standard);
`docs/releases/v0.11.0/WP11.1A Implementation Report.md`; `docs/releases/
v0.11.0/WP11.0B Architecture Roadmap.md`; `Engineering Governance.md`
§2 (Review Gates), §3 (Definition of Done); `02-testing-strategy.md`
(what the Test Gate actually verifies).
