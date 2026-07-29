# TempestOS v0.5.0 — Release Checklist

## Purpose

The concrete, checkable gate this release must pass before it is tagged
and published — the same gates Engineering Governance already defines
(§2, §3, §7), applied specifically to v0.5.0. Nothing here is a new rule;
this is that existing process, made checkable for this specific release,
mirroring `docs/releases/v0.4.0/ReleaseChecklist.md`'s own precedent.
Produced by `WP 5.4` (v0.5.0 Release Candidate & Engineering Sign-Off).

## Per-Work-Package Definition of Done

Every Work Package in this release's scope (`WP 5.0A` through `WP 5.3`)
has met every item below — **Verified** directly against each Work
Package's own retrospective and this Work Package's own repository
review:

- [x] Build Gate: `dotnet build` — 0 warnings, 0 errors, re-verified at
      every Work Package boundary.
- [x] Test Gate: `dotnet test` — 100% pass, including every pre-existing
      test from v0.4.0 and every other v0.5.0 Work Package landed so far
      (355 → 552, zero regressions at any boundary).
- [x] No `TODO`, no dead code, no commented-out code in changed files.
- [x] Every public type/member introduced or touched has XML
      documentation.
- [x] A completion report exists for every Work Package (Engineering
      Governance §4).
- [x] Any decision meeting Governance §5's ADR criteria has an ADR — 9
      new ADRs (`ADR-0031`–`ADR-0039`).
- [x] Any proposed design meeting Governance §10's criteria has a
      Rejected Designs entry — 15 new entries (`RD-0030`–`RD-0045`).
- [x] The relevant Academy documentation was created or updated as part
      of the same Work Package — 13 new/updated retrospectives and
      concept guides.
- [x] `docs/architecture/Platform Service Map.md` updated for every new
      or changed platform service (Navigation, the Shell, the Command
      Framework, Diagnostics).
- [x] The work remains on `feature/v0.5.0-developer-experience`,
      unmerged into `main`, pending release approval.

## Release-Level Checklist (Before Tagging)

- [x] Every Work Package within the release's final scope (`WP 5.0A`
      through `WP 5.3`) meets its own Acceptance Criteria — **Verified**,
      `WP 5.4`, against `docs/releases/v0.5.0/WorkPackages.md`'s own
      "— Met" entries. Nothing was rescoped out of this release; unlike
      `v0.4.0`, the original plan's full scope shipped.
- [x] `CHANGELOG.md` reflects every landed change, with a final "Release
      Summary" section covering major capabilities, engineering/
      architecture improvements, testing/documentation/governance
      growth, breaking changes, migration notes, known limitations, and
      the next milestone — `docs/releases/v0.5.0/CHANGELOG.md`, written
      `WP 5.4`.
- [x] `docs/releases/v0.4.0/Risks.md`'s register (carried forward across
      releases) is reviewed — every risk relevant to this release is
      retired, with the decision that retired it, or explicitly marked
      still deferred. `R5`, `R7`, `R8`, `R9` retired in full during
      `WP 5.4`'s own review, each citing the specific Work Package that
      resolved its residual exposure.
- [x] Full solution build and test run from a clean, fully-committed
      working tree — 552/552 tests, 0 warnings/errors, re-verified
      directly by `WP 5.4`.
- [x] `docs/releases/v0.5.0.md` (the release notes, sibling to this
      directory, following the `v0.3.0.md`/`v0.4.0.md` format) is
      written, plus the fuller, GitHub-oriented `docs/releases/v0.5.0/
      Release Notes.md`.
- [x] Root `VERSION` file updated to `0.5.0`. Authorised explicitly by
      Product Approval as part of the release-engineering activity that
      followed `WP 5.4`'s own sign-off.
- [x] `docs/architecture/Platform Service Map.md` updated for every new
      or changed platform service this release introduced (Navigation,
      the Shell — via `ITempestHost.Services` — the Command Framework,
      Diagnostics).
- [x] `docs/architecture/Engineering Glossary.md` updated for any new
      term of art this release introduces.
- [x] Onboarding documentation (`docs/academy/Contributor Learning
      Path.md`) reflects this release's own current state — **found
      stale during `WP 5.4`'s own Developer Experience review** (still
      pointed at `v0.4.0/WorkPackages.md`, cited a 30-ADR count, never
      mentioned Navigation/the Shell/Commands/Diagnostics/the new
      template) and corrected as part of this checklist item.

## Note on What `WP 5.4` Deliberately Did Not Do

Per its own brief ("Produce a Release Candidate suitable for Product
Approval... do not begin v0.6.0"), `WP 5.4` itself prepared every
release-preparation document a merge/tag would need, but did not perform
the merge, the tag, or any push — each is a release-adjacent action
Engineering Governance §7 requires explicit, per-occasion Product
Approval for. That approval was given explicitly in a subsequent
instruction, and the sequence below was then executed exactly as
recorded here.

## Merge and Tag Sequence — Executed

Adapted from Engineering Governance §7 and the `v0.4.0` precedent:

1. On `feature/v0.5.0-developer-experience`: verified a clean,
   fully-committed working tree; created the release-preparation commit
   (`VERSION` → `0.5.0`, `CHANGELOG.md`/`Release Notes.md`/`v0.5.0.md`
   status lines updated from "Release Candidate" to "Released,"
   `PROJECT_STATUS.md` updated).
2. `git checkout main && git merge --no-ff feature/v0.5.0-developer-experience`
   (explicit merge commit, matching the `v0.3.0`/`v0.4.0` precedent).
3. Full solution build and test run on `main`, Release configuration —
   0 warnings, 0 errors, 552/552 passing.
4. Annotated tag `v0.5.0` created on `main`.
5. `git push origin main` and `git push origin v0.5.0`.
6. Remote verified independently (`git ls-remote`), not by trusting the
   push output alone.
7. GitHub Release for `v0.5.0` published using `docs/releases/v0.5.0/
   Release Notes.md` as the description.

## Post-Release

- [x] Cut `feature/v0.6.0-platform-services` from `main` for what comes
      after v0.5.0, rather than continuing work on `main`.
