# ADR-0160: Pull Requests Test Release Only; a Release Publishes Only on a Green CI Gate for Its Tag; Packages Are Locked

## Status

Accepted — Product Owner decisions (2026-10-01) on the `v0.23.0` CI
colour review board (G-05, G-07, G-20, G-21) and the NuGet lock-file
proposal.

## Context

Five findings shared one cause: what CI verifies and what a release
ships were not the same thing.

- **G-20.** Every pull request ran the full Debug + Release test matrix,
  eight Windows legs, and was then tested again on the push to `main`.
  Debug and Release differ in very little the suite exercises.
- **G-21.** `release.yml` and `ci.yml` both started on the tag push and
  ran in parallel. A release was published even when `CI Gate` on the
  tagged commit was red.
- **G-05.** The release job held `contents: write` for its whole run,
  including restore, build, test and `vpk`: third-party code with a token
  that could write to the repository, persisted in `.git/config`.
- **G-07.** `package-installer.ps1` ran whichever `vpk` was on `PATH`,
  falling back to an install under `.tools/`; a global `vpk` of another
  version silently packaged the release.
- **Lock files.** Restores resolved floating transitive versions at run
  time, so two runs of one commit could build different package sets,
  and a package replaced on the feed went unnoticed.

## Decision

**1. A pull request runs the Release tests only.** Its four Debug legs
still restore and build with `TreatWarningsAsErrors`; every Debug test
step carries `github.event_name != 'pull_request'`. The skip is per
step, so all eight legs exist on every event and `CI Gate`'s strict
`success` check is unchanged: a skipped step leaves its leg `success`; a
failed or cancelled leg, or a cancelled run (`if: always()` makes the
gate run and report red), fails the gate. A push to `main`,
`release/**` or a tag, a manual run and the schedule run the full
matrix, so every commit that lands is tested in both configurations.

**2. A release publishes only on a green `CI Gate` for the tagged SHA.**
`release.yml`'s `ci-gate` job polls the Checks API (`checks: read`) for
`CI Gate` check runs on `github.sha` created by GitHub Actions. It waits
while none exists or any is still running, up to 75 minutes; the newest
then decides, and only `success` passes. Polling, not `workflow_run`:
a `workflow_run` workflow runs the default branch's copy of the file,
not the tag's, and does not carry the tag. Tag only when the commit is
ready; a red gate blocks the release and does not free the tag to be
moved (Engineering Governance §7.4). The failed jobs are re-run once
the cause is fixed.

**3. Publishing is a separate, write-scoped job.** `build` runs with
`contents: read` and `persist-credentials: false`, stages the assets,
`SHA256SUMS.txt` (LF, `sha256sum -c` compatible), the upload list and
the release notes, and uploads them as one artifact. `publish`
(`needs: [ci-gate, build]`) is the only job with `contents: write`; it
checks out nothing, verifies every checksum and that the upload list
equals the checksum list, and runs `gh release create --verify-tag`.

**4. `vpk` is pinned.** `.config/dotnet-tools.json` pins `vpk` 1.2.0
(`rollForward: false`), equal to the `Velopack` package version, and the
script runs only `dotnet tool restore` + `dotnet vpk pack`.
`PackagingScriptTests` hold the two versions equal.

**5. NuGet packages are locked.** `Directory.Build.props` sets
`RestorePackagesWithLockFile`; every project commits
`packages.lock.json`. Every `dotnet restore` in `ci.yml` and
`release.yml` runs with `--locked-mode`, and `RestoreLockedMode` is on
when `CI=true`, so implicit restores (the installer's `dotnet publish`)
are locked too. Every project declares `RuntimeIdentifiers`
`win-x64;linux-x64`: a lock records the RIDs it was restored for, a
locked restore fails (`NU1004`) when they differ, and
`dotnet publish -r win-x64` passes its RID to every project in
`Tempest.Desktop`'s graph. With both RID sections in every lock, the
Windows legs, the release publish and the Linux smoke job restore
against the same files on any host. Regenerate with
`dotnet restore src/TempestOS.slnx --force-evaluate`; Dependabot's
`nuget` updater rewrites the locks in its own pull requests.
`NuGetLockFileTests` pin all of this.

## Consequences

**Positive.** Pull requests use roughly half the Windows test time.
Nothing is published from a commit CI has not passed. Only a job that
runs no repository code can write. The installer is always packaged by
the reviewed `vpk`. Every build of a commit restores the same,
hash-verified packages.

**Negative.** A Debug-only test failure is found on the push after
merge, not on the pull request (the CI standard tells a contributor
touching `#if DEBUG` code to run Debug tests locally). A release takes
at least as long as CI on the tag. Lock files add churn to every
package change, and both RID sections make them larger; restoring
downloads the other RID's apphost pack.

## Alternatives Considered

**Skipping the Debug legs as jobs on pull requests.** Rejected: job
names would change by event and `CI Gate` would need a `skipped`
exception, which would also accept a genuinely skipped leg.

**`workflow_run` on CI completion.** Rejected: see Decision 2.

**Lock files without `RuntimeIdentifiers`.** Rejected: the installer's
`-r win-x64` publish fails a locked restore (`NU1004`) in every project
of its graph.

## Related Documents

`.github/workflows/ci.yml`; `.github/workflows/release.yml`;
`Directory.Build.props`; `.config/dotnet-tools.json`;
`scripts/package-installer.ps1`; `scripts/new-release.ps1`;
`04-continuous-integration.md`; `05-release-engineering.md`;
`Engineering Governance.md` §7; `CONTRIBUTING.md`.
