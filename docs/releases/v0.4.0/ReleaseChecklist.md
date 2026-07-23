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

- [ ] All eleven work packages — `WP 4.0` through `WP 4.9`, with `WP 4.6`
      split into `4.6A`/`4.6B` (or an explicitly revised, documented subset
      — see `ReleasePlan.md`) — meet their own Acceptance Criteria.
- [ ] `CHANGELOG.md` reflects every landed change, not just a summary
      written at the end.
- [ ] `Risks.md`'s register is reviewed — every risk is either retired
      (with the decision that retired it) or explicitly still open and
      accepted going into release.
- [ ] Full solution build and test run from a clean, fully-committed
      working tree — matching the same standard v0.3.0 met.
- [ ] `docs/releases/v0.4.0.md` (the release notes, sibling to this
      directory, following the `v0.3.0.md` format) is written.
- [ ] Root `VERSION` file updated to `0.4.0`.
- [ ] `docs/architecture/Platform Service Map.md` updated for every new or
      changed platform service this release introduced (Event Bus,
      Background Services, and any other DI-public service).
- [ ] `docs/architecture/Engineering Glossary.md` updated for any new term
      of art this release introduces (Plugin Manifest, hosted service,
      command/event distinction, and others as they arise).

## Merge and Tag Sequence

Following the exact sequence Engineering Governance §7 and the v0.3.0
release established:

1. `git checkout main && git pull origin main`
2. `git merge --no-ff feature/v0.4.0-platform-services` (explicit merge
   commit, matching the v0.3.0 precedent — not a silent fast-forward).
3. Update root `VERSION` to `0.4.0`; commit on `main` as a dedicated
   release-preparation commit, separate from the merge itself.
4. Run `scripts\New-Release.ps1 -Version 0.4.0` (without `-Push`) — validates
   branch, clean state, `VERSION`, release notes presence, builds and tests
   in Release configuration, creates the annotated tag.
5. **Stop.** Verify the new tag resolves to the expected commit, exactly as
   was done for v0.3.0, before any push.
6. Only after explicit, separate approval: `git push origin main` and
   `git push origin v0.4.0`.
7. Verify the remote independently (`git ls-remote`), not by trusting the
   push output alone.
8. Optionally, publish a GitHub Release using `docs/releases/v0.4.0.md` as
   the description — a separate, explicitly-approved action, not implied
   by the push.

## Post-Release

- [ ] Cut the next feature branch for whatever comes after v0.4.0, rather
      than continuing work on `main` — the same discipline this checklist
      itself depends on.
