# Engineering Standard: Continuous Integration

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

**Verification fails the gate; diagnostics never do** (`WP 14.2.1`,
`ADR-0117`). Build and test are *verification*: they decide whether the
commit is sound, and a failure in either must stop the pipeline. Artefact
uploads are *diagnostics*: they carry evidence about a decision already
made. Every upload step therefore carries `continue-on-error: true`. This
is not leniency — it is the gate meaning what it says. Until `WP 14.2.1`
the `CI Gate` job keyed off the matrix job's conclusion, which folded in
upload outcomes, so when this account's Actions artifact storage quota
was exhausted the required gate went red across every branch *and*
`main`, at commits whose build and tests had both passed — including a
commit on `main` that had gone green a week earlier and was never
touched since. A gate that can be reddened by a billing counter is not
reporting on the code.

## Build Pipeline

`.github/workflows/ci.yml` runs on every push, every pull request, and
on manual dispatch. One job, `build-and-test`, runs as a two-leg matrix
(`configuration: [Debug, Release]`) so both configurations are built and
fully tested independently, on separate runners, with `fail-fast: false`
so one configuration's failure never hides the other's result. A second
job, `gate`, depends on the matrix and gives branch-protection rules one
unambiguous, named status check to require, rather than needing to
enumerate both matrix legs individually.

Each matrix leg:

1. Checks out the commit.
2. Installs the exact .NET SDK named in `global.json` (`10.0.302`,
   `rollForward: latestFeature`) via `actions/setup-dotnet`'s own
   `global-json-file` input — the identical single source of truth every
   local build already reads, so the SDK version cannot drift between a
   contributor's machine and CI.
3. Restores, then builds `src/TempestOS.slnx` for its own configuration,
   with warnings promoted to errors (above).
4. Runs the complete test suite (`dotnet test` against the same
   solution — `Tempest.Core.Tests`, `Tempest.Desktop.Tests` and
   `Tempest.Companion.Tests`, the latter two exercising real Avalonia
   headless UI, not a mock) with TRX results written per configuration.
5. Publishes a Markdown build/test summary to the run's own Job Summary
   — error and warning counts, per-assembly test totals, **and the name,
   message and stack of every failed test**, parsed from the TRX
   (`WP 14.2.1`). That last part is what makes a failing run genuinely
   self-describing: this section previously claimed a failing run was
   "diagnosable from the Actions UI alone", but the only thing naming the
   failing test was the uploaded TRX, so when uploads began failing an
   intermittent failure on `main` could be counted ("Failed: 1" of 2,341)
   and not identified — the console log being far past the run-log API's
   own tail window. The summary needs no storage quota and no artefact
   retention, so it survives exactly the conditions under which the
   artefacts do not.
6. Uploads the build log and TRX results as artefacts — best-effort, and
   never able to fail the job (above).
7. On `main` and on tags only, the Release leg additionally uploads the
   built `Tempest.App`/`Tempest.Desktop` output — a smoke-testable build
   of the exact commit, not a promise of one. `WP 14.2.1` restricted this
   from *every push on every branch*: those two trees are roughly 50 MB
   per push, they are a convenience copy rather than the release
   mechanism (`release.yml` packages the real assets from the tag), and
   publishing them from every feature-branch push is what exhausted the
   account's artifact storage in the first place.

The runner image (`windows-2022`) is pinned explicitly rather than using
the floating `windows-latest` alias, for the same reason this project
pins every package version exactly (`Avalonia 11.2.3`, the SDK via
`global.json`): an unannounced runner-image change should never silently
change CI behaviour.

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

**For a contributor:** push to any branch, or open a pull request — the
pipeline runs automatically, no configuration required. A failing run's
Job Summary names which configuration failed and shows the build-error
or test-failure count directly, before anyone needs to open a log file.
The same commands the pipeline runs are exactly what to run locally
first:

```
dotnet restore src/TempestOS.slnx
dotnet build src/TempestOS.slnx -c Debug   -p:TreatWarningsAsErrors=true
dotnet build src/TempestOS.slnx -c Release -p:TreatWarningsAsErrors=true
dotnet test  src/TempestOS.slnx -c Release
```

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
work. The pipeline also does not yet gate merges via a required branch-
protection rule — the `gate` job exists so that configuration is a
one-line addition whenever the Product Owner chooses to make it
mandatory, not because it is mandatory today.

## Related Documents

`.github/workflows/ci.yml`; `docs/releases/v0.11.0/WP11.0A Platform
Architecture Review.md` (finding `R-1`, the source of this standard);
`docs/releases/v0.11.0/WP11.1A Implementation Report.md`; `docs/releases/
v0.11.0/WP11.0B Architecture Roadmap.md`; `Engineering Governance.md`
§2 (Review Gates), §3 (Definition of Done); `02-testing-strategy.md`
(what the Test Gate actually verifies).
