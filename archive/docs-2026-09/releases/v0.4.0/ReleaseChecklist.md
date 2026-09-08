# TempestOS v0.4.0 — Release Checklist

## Purpose

The concrete, checkable gate this release must pass before it is tagged and
published — the same gates Engineering Governance already defines (§2, §3,
§7), applied specifically to v0.4.0. Nothing here is a new rule; this is
that existing process, made checkable for this specific release.

## Per-Work-Package Definition of Done

Before any work package in `WorkPackages.md` is considered complete:

- [ ] Build Gate: `dotnet build` — 0 warnings, 0 errors.
- [ ] Test Gate: `dotnet test` — 100% pass, including every pre-existing
      test from v0.3.0 and every other v0.4.0 work package landed so far.
- [ ] No `TODO`, no dead code, no commented-out code in changed files.
- [ ] Every public type/member introduced or touched has XML documentation.
- [ ] Every test category named in `Testing.md` for this work package has
      at least one identifiable, correctly-named test.
- [ ] A completion report exists (Engineering Governance §4: summary,
      files created/modified, architectural decisions, test results,
      build results, assumptions, observations, documentation summary).
- [ ] Any decision meeting Governance §5's ADR criteria has an ADR.
- [ ] The relevant Academy documentation is created or updated as part of
      the same work package — not a follow-up pass.
- [ ] `Architecture.md`'s reuse map is checked — if this work package
      needed a new decision `Architecture.md` didn't already anticipate,
      that gap is noted back into `Architecture.md`.
- [ ] The work remains on `feature/v0.4.0-platform-services`, unmerged into
      `main`, until release approval is explicitly given.

## Release-Level Checklist (Before Tagging)

- [x] Every work package within the release's actual, final scope
      (`WP 4.0` through `WP 4.5B` — an explicitly revised, documented
      subset; see `ReleasePlan.md`'s own "Scope" section) meets its own
      Acceptance Criteria. `WP 4.6A` through `WP 4.9` are rescoped out of
      this release, not measured against this gate.
- [x] `CHANGELOG.md` reflects every landed change, not just a summary
      written at the end, and carries a final "Release Summary" section
      covering major capabilities, engineering/architecture improvements,
      testing/documentation/governance growth, breaking changes,
      migration notes, known limitations, and the next milestone.
- [x] `Risks.md`'s register is reviewed — every risk is either retired
      (with the decision that retired it) or explicitly marked deferred
      to the next milestone (out of this release's rescoped scope), never
      left silently open with no disposition.
- [x] Full solution build and test run from a clean, fully-committed
      working tree — matching the same standard v0.3.0 met.
- [x] `docs/releases/v0.4.0.md` (the release notes, sibling to this
      directory, following the `v0.3.0.md` format) is written, plus a
      fuller, GitHub-oriented `docs/releases/v0.4.0/Release Notes.md`.
- [x] Root `VERSION` file updated to `0.4.0`.
- [x] `docs/architecture/Platform Service Map.md` updated for every new or
      changed platform service this release introduced (Event Bus,
      Background Services, and any other DI-public service).
- [x] `docs/architecture/Engineering Glossary.md` updated for any new term
      of art this release introduces (Plugin Manifest, hosted service,
      command/event distinction, and others as they arise).

**Note on sequencing (actual vs. originally drafted).** This checklist's
own "Merge and Tag Sequence," below, originally anticipated the `VERSION`
bump and release-notes authoring happening *after* the merge to `main`,
as a separate, dedicated commit. In practice, Release Engineering for
`v0.4.0` performed all release-preparation work (`VERSION`, `CHANGELOG.md`
finalisation, both release-notes documents, `PROJECT_STATUS.md`) as one
commit on `feature/v0.4.0-platform-services`, then merges that
already-prepared commit into `main` — simpler, since it avoids a second,
post-merge commit on `main` solely for version bookkeeping. The sequence
below is updated to reflect this.

## Merge and Tag Sequence

Adapted from Engineering Governance §7 and the v0.3.0 release precedent,
reflecting the actual sequence used for `v0.4.0` (see the Note above): the
release-preparation commit (`VERSION`, `CHANGELOG.md`, both release-notes
documents, `PROJECT_STATUS.md`) is made on
`feature/v0.4.0-platform-services` *before* the merge, not after.

1. On `feature/v0.4.0-platform-services`: verify a clean, fully-committed
   working tree; `dotnet build`/`dotnet test` both pass (0 warnings, 0
   errors, 100% pass). Create the release-preparation commit described
   above.
2. `git checkout main && git pull origin main`
3. `git merge --no-ff feature/v0.4.0-platform-services` (explicit merge
   commit, matching the v0.3.0 precedent — not a silent fast-forward).
   `main` now contains the release-preparation commit's `VERSION`/
   `CHANGELOG.md`/release-notes changes as part of the merge.
4. Run `scripts\new-release.ps1 -Version 0.4.0` (without `-Push`) —
   validates branch (`main`), clean state, `VERSION`, release notes
   presence (`docs/releases/v0.4.0.md`), builds and tests in Release
   configuration, creates the annotated tag.
5. **Stop.** Verify the new tag resolves to the expected commit, exactly as
   was done for v0.3.0, before any push.
6. Only after explicit, separate approval: `git push origin main` and
   `git push origin v0.4.0`.
7. Verify the remote independently (`git ls-remote`), not by trusting the
   push output alone.
8. Optionally, publish a GitHub Release using `docs/releases/v0.4.0/
   Release Notes.md` as the description — a separate, explicitly-approved
   action, not implied by the push.

## Post-Release

- [ ] Cut the next feature branch for whatever comes after v0.4.0, rather
      than continuing work on `main` — the same discipline this checklist
      itself depends on.
