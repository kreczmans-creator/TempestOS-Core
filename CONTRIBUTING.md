# Contributing to TempestOS

## The unit of work is a Work Package

A Work Package is a branch and a pull request. There is no separate
retrospective document, readiness review, or completion certification —
**the PR description is the retrospective.** It says what changed, why,
and what evidence backs the change (build, tests, physical review).

## Review

One review per PR, one round. The reviewer's findings are either fixed
in the same PR or filed to `BACKLOG.md` before merge — a finding is
never silently dropped, and it is never allowed to reopen the review
into a second round.

## Release Blocking

A finding is **Release Blocking** only if it is **data loss a user can
reproduce from the running UI.** Everything else — a rough edge, a
missing surface, a gap an engineer would only find by reading the
code — is real but is not Release Blocking, and belongs in
`BACKLOG.md`, not in the way of the merge.

## Markdown budget

A PR may not add more Markdown lines than code lines. This is enforced
by `scripts/governance-healthcheck.ps1`'s **"Markdown lines added must
not exceed code lines added"** check, which compares `git diff
--numstat` for the current branch against its merge base with `main`.
It is why this repository stopped producing prose in volumes nobody
could keep current: the process itself can no longer outweigh the
product.

## Definition of Done

Every Work Package still ships with:

- A build at **0 warnings, 0 errors**, both configurations, under
  `TreatWarningsAsErrors`.
- Every test passing in both configurations.
- The architecture invariants (`DependencyDirectionTests`) green.
- An ADR for any decision that constrains future code.
- A row in `PHYSICAL_REVIEW.md` §7 for any new user-facing surface.
- One Release Notes line.

## Branch protection on `main`

Configured in GitHub under **Settings → Branches → Branch protection
rules** for `main`:

- Required status check: `CI Gate`, strict (branch must be up to date
  before merging).
- Pull request required before merging.
- **0 required approvals** — this is a solo-owner repository, and an
  owner cannot approve their own PR; the required `CI Gate` check and
  the one-round review discipline above are the gate instead.
- No force pushes, no deletions of `main`.
- Conversation resolution required before merging.
- Administrators are **not** exempt from any of the above.

## Out of scope for a Work Package PR

No Engineering Readiness Review document, no Colour Review Board, no
Academy retrospective, no register re-derivation, no status
reconciliation. If a second opinion is wanted, it is one agent, one
round, against this file's own checklist, with its findings going
straight into the PR.
