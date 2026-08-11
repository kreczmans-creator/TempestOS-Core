# Engineering Standard: Release Engineering

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
release. Each minor release gets its own branch,
`feature/vX.Y.0-<slug>`, cut from `main` at the prior release's tag —
confirmed against this project's own history, `feature/v0.5.0-developer-experience`
through `feature/v0.10.0-user-experience`. Every Work Package for that
release lands as its own commit directly on the branch; the branch
merges into `main` once, as a single non-fast-forward merge, at release
close. Branches are never deleted. A hotfix gets its own branch,
`hotfix/vX.Y.Z-<slug>` — see "Emergency Hotfix Process," below. There is
no permanent `develop`/`staging` branch — deliberately not adopted; this
project has never needed one.

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
`Tempest.App`'s own build (the Internal Engineering Harness, `ADR-0101`,
`WP 11.3B`) — so it is never ambiguous on the Release page which
download *is* TempestOS. A release is not considered shipped until this
second verification passes, not merely on the strength of an earlier CI
run or a local script's own printed success message.

A release-readiness review recommending **APPROVED** or **CERTIFIED**
remains required — the same pattern every release since `v0.6.0` has
already followed (`WP 6.8`, `WP 7.4.0`, `WP 8.9.0`, `WP 9.9.0`,
`WP 10.9A`), now named as a standing requirement rather than an observed
one. For a release carrying material risk or scope, a release-candidate
tag (`vX.Y.Z-rc.N`, cut directly on the release branch, the one
disclosed exception to "tag only from `main`") publishes a pre-release
build for smoke-testing before the real merge and tag happen — see
`WP11.1B Engineering Workflow.md` §8.

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

Rollback, in this project, is always forward: Governance §7.4 already
forbids moving or recreating a pushed tag, so a defective release is
never un-tagged — its GitHub Release is instead marked superseded (the
one narrow exception: editing a Release's own description, never its
tag or content), a fallback build remains permanently downloadable from
it, and a hotfix ships as soon as one is ready. See `WP11.1B Engineering
Workflow.md` §10 for the full procedure, including what it deliberately
does not cover (a user's own locally-persisted project data).

## Evidence & Findings (Summary)

Full detail in `WP11.1B Engineering Workflow.md`, "Evidence & Findings."
The headline: researching this standard's own branching history found
that the `v0.10.0` tag itself points to the feature branch's pre-merge
tip, not to `main` — a real, disclosed deviation from the release
process Engineering Governance §7 already specified, not something this
Work Package introduced. Per §7.4, the tag was not moved; the deviation
is disclosed here and the "cut only from `main`, post-merge" rule is
restated explicitly, precisely because relying on it being obvious once
already failed silently.

## Related Documents

`docs/releases/v0.11.0/WP11.1B Engineering Workflow.md` (the full
specification); `04-continuous-integration.md`; `Engineering
Governance.md` §2, §7, §9; `.github/workflows/ci.yml`,
`.github/workflows/release.yml`; `scripts/new-release.ps1`.
