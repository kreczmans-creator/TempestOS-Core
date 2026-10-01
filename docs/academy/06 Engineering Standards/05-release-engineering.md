# Engineering Standard: Release Engineering

> **Status note, September 2026.** The release mechanics below still hold
> (`new-release.ps1`, `release.yml`, the tag), with the tightening `WP
> 16.1A-R1` added and the gate figures now recorded in the Release Notes
> and `PROJECT_STATUS.md` rather than a Release Register. The `v0.17.0`
> and `v0.18.0` releases were run from an Execution Plan on a single
> release branch — see `09-the-governance-reset-and-how-a-release-is-now-run.md`
> and `08-the-physical-review-and-the-release-gate.md`.

## Purpose

`WP 11.1A` gave TempestOS a machine-verified Build Gate and Test Gate.
`WP 11.1B` defines the engineering workflow that surrounds them: how a
change actually moves from a feature branch to a released, tagged,
downloadable version — branching, pull requests, releases, versioning,
and the emergency hotfix path this project has never needed yet, but now
has a documented procedure for before it does. See `docs/releases/
v0.11.0/WP11.1B Engineering Workflow.md` for the full specification this
article summarises; nothing below overrides that document.

## Branching Strategy

`main` is the only permanent branch, always reflecting the latest
release. A release is integrated on a `release/vX.Y.Z` branch (for
example `release/v0.21.0`), cut from `main`; CI runs on every push to it.
Work Packages reach it, or `main` directly, as pull requests from
short-lived working branches, which have no required naming scheme (CI
runs on their pull request, not on a push to them — see
`04-continuous-integration.md`). The release branch merges into `main`
at release close. A hotfix is an ordinary pull request branched from the
affected release's tag — see "Emergency Hotfix Process," below. There is
no permanent `develop`/`staging` branch — deliberately not adopted; this
project has never needed one. (Through `v0.10.0` release branches were
named `feature/vX.Y.0-<slug>` and a hotfix branch
`hotfix/vX.Y.Z-<slug>`; those names are historical.)

## Pull Request Workflow

From `WP 11.1B` onward, every merge into `main` goes through a pull
request — one per release branch (not one per Work Package; Work
Packages continue landing as individual commits on the branch exactly as
before). `.github/pull_request_template.md` structures every one:
Work Package identification, a summary, the three review gates
(Build/Test/Technical Review, Engineering Governance §2), scope
confirmation (production code / architecture / ADR unchanged, or
justified), and a Product Approval field only that tier completes.

A pull request is merge-ready only once its `CI Gate` check
(`.github/workflows/ci.yml`) is green, and is merged with **"Create a
merge commit"** — never squash, never rebase, which would erase the
Work-Package-level history this project's Academy and Technical Debt
Register both depend on. `.github/CODEOWNERS` requests review from the
project's own Product Approval authority automatically, matching what
Engineering Governance §9 already required in prose.

This formalises what was, through `v0.10.0`, a direct local
merge-and-push by Product Approval with no pull request involved — that
history stands as written; it simply predates the CI pipeline a pull
request's own status check needs.

## Release Process

A release is cut only from `main`, **after** its feature branch has
already merged — not from the branch itself (see this standard's own
"Evidence & Findings" note, below, for a real, disclosed case where this
was not followed). `scripts/new-release.ps1` — corrected this Work
Package to check the release-notes path this project actually uses
(`docs/releases/vX.Y.Z/Release Notes.md`, not the stale
`docs/releases/vX.Y.md`) and to build with the same
`-p:TreatWarningsAsErrors=true` flag CI uses — verifies branch, clean
tree, `VERSION`, release notes, Build Gate, and Test Gate before creating
an annotated tag. Pushing that tag triggers
`.github/workflows/release.yml`, a second, independent Build Gate/Test
Gate run against the tagged commit itself, which then publishes a GitHub
Release with **two separate assets attached, never one** — `Tempest.Desktop`'s
own build (TempestOS's shipped application) and, separately,
`Tempest.Harness`'s own build (the Internal Engineering Harness, `ADR-0101`,
amended `WP 17.2B`), plus the Velopack installer and its update feed — so it is never ambiguous on the Release page which
download *is* TempestOS. A release is not considered shipped until this
second verification passes, not merely on the strength of an earlier CI
run or a local script's own printed success message.

**Tag only when the commit is ready; the release waits for CI on that
commit** (`ADR-0160`, PO decision 2026-10-01). The tag push also starts
`ci.yml` on the same commit, and `release.yml` publishes nothing until
that commit's `CI Gate` check has concluded `success`: its first job
polls the Checks API for up to 75 minutes, in parallel with the
build/test job. A red, cancelled or missing `CI Gate` fails the release
run; the tag stays where it is (§7.4), and once the cause is fixed (a CI
re-run on the tag for a flake, otherwise a new patch version) the failed
release jobs are re-run. The workflow is split so that only the final
`publish` job holds a token that can write to the repository: the
build/test job runs with a read-only, non-persisted token and hands the
packaged assets, `SHA256SUMS.txt` and the release notes to `publish` as
an artifact, and `publish` checks every asset against `SHA256SUMS.txt`
before it creates the Release. `vpk`, which packages the installer, is
pinned in `.config/dotnet-tools.json` and run as `dotnet vpk`.

A release-readiness review recommending **APPROVED** or **CERTIFIED**
remains required — the same pattern every release since `v0.6.0` has
already followed (`WP 6.8`, `WP 7.4.0`, `WP 8.9.0`, `WP 9.9.0`,
`WP 10.9A`), now named as a standing requirement rather than an observed
one. For a release carrying material risk or scope, a release-candidate
tag (`vX.Y.Z-rc.N`, cut directly on the release branch, the one
disclosed exception to "tag only from `main`") publishes a pre-release
build for smoke-testing before the real merge and tag happen — see
`WP11.1B Engineering Workflow.md` §8.

## Handing a Build Over for Testing

Every build handed to the Product Owner for testing ships with its own
installer and a versioned desktop shortcut (PO decision 2026-10-01). An
old shortcut once opened v0.22.0 and its data folder during a v0.23.0
test, which looked like lost projects.

1. On the test PC, from the branch under test, run
   `pwsh -NoProfile -File scripts/install-test-build.ps1 -Pull`. It
   packages the installer (`scripts/package-installer.ps1`), installs it
   over any earlier version, removes older `TempestOS * (test)` shortcuts
   and adds `TempestOS <version> (test)` on the desktop, opening the
   version's own data folder (`C:\TempestOS-rc<minor>-data` by default).
2. The runbook's first step (A0) is running that script; its last line
   prints the title-bar build to record.
3. Test only from that shortcut. The plain `TempestOS` shortcut also opens
   the newly installed build, but on whatever data folder was chosen at
   first run.

## Versioning Policy

`VERSION` (repository root) remains the single source of truth,
unchanged in mechanism since `Directory.Build.props` first established
it. `MAJOR.MINOR.PATCH`, optional `-rc.N` suffix. MAJOR is reserved for
`v1.0.0` and beyond — every release through `v0.10.0` is `0.Y.0`. MINOR
is an ordinary planned release. **PATCH is new**: this project has never
cut a non-zero patch version before `WP 11.1B` defined what one means —
a hotfix, and only a hotfix, never a vehicle for new capability.

## Emergency Hotfix Process

Triggered by a confirmed defect in an already-released version too
severe to wait for the next regular release
(`.github/ISSUE_TEMPLATE/bug_report.md` captures the triage question).
Branch from the affected release's own tag (`main`, if it is still the
latest release; the specific historical tag otherwise); scope is the
minimal fix only, no accompanying feature work; every gate — Build,
Test, `CI Gate`, Technical Review — applies exactly as it does to any
other branch, only the accompanying documentation is proportionate to
the smaller diff. Versioning bumps PATCH only. After merge and tag, the
fix is **forward-merged into whatever release branch is currently
active** — not optional; an omitted forward-merge is the standard way a
hotfix silently regresses in the very next regular release.

Rollback, in this project, is always forward: a published release tag
is immutable (Governance §7.4), so a defective release found *after*
its own branch has closed is never un-tagged — its GitHub Release is
instead marked superseded (the one narrow exception: editing a
Release's own description, never its tag or content), a fallback build
remains permanently downloadable from it, and a hotfix ships as soon as
one is ready. §7.4 carries one further, distinct, narrower exception
(added `WP 11.4B`): a documented release-process defect — not a defect
in the release's own content — found and Product-Owner-approved for
correction *before* the release branch closes, with a Release Process
Correction Report and full traceability. The two are not
interchangeable: rollback never touches a tag; the `WP 11.4B` exception
touches only a tag's own mechanical position, never its release
content. See `WP11.1B Engineering Workflow.md` §10 for the full
rollback procedure, including what it deliberately does not cover (a
user's own locally-persisted project data).

## Evidence & Findings (Summary)

Full detail in `WP11.1B Engineering Workflow.md`, "Evidence & Findings."
The headline: researching this standard's own branching history found
that the `v0.10.0` tag itself points to the feature branch's pre-merge
tip, not to `main` — a real, disclosed deviation from the release
process Engineering Governance §7 already specified, not something this
Work Package introduced. Per §7.4, the tag was not moved; the deviation
is disclosed here and the "cut only from `main`, post-merge" rule is
restated explicitly, precisely because relying on it being obvious once
already failed silently. **It failed silently a second time**: `v0.11.0`
repeated the identical defect despite this explicit restatement —
found and corrected under §7.4's own narrow, `WP 11.4B`-added exception,
since found before its own release branch closed. Full account:
`docs/releases/v0.11.0/WP11.4B Release Process Correction Report.md`.

## Related Documents

`docs/releases/v0.11.0/WP11.1B Engineering Workflow.md` (the full
specification); `04-continuous-integration.md`; `Engineering
Governance.md` §2, §7, §9; `ADR-0160`; `.github/workflows/ci.yml`,
`.github/workflows/release.yml`; `scripts/new-release.ps1`.
