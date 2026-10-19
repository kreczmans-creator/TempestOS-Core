# ADR-0164: A Release Is Tested Once, by CI on Its Commit, Not Again by the Tag

## Status

Accepted. This is the Product Owner's decision of 2026-10-19, made after watching the `v0.25.0` release run. It amends `ADR-0160` §1 (the tag push no longer starts `ci.yml`) and §2 (the gate is met by the CI run the commit already has). It also retires `WP 11.1B`'s second, independent test run in `release.yml`.

## Context

Publishing `v0.25.0` tested commit `6dbc47d` three times:

1. **The push to `main` (CI run 542).** Eight Windows legs ran the full Debug + Release matrix. `CI Gate` was green at 14:07.
2. **The tag push (CI run 543).** It ran the same eight legs on the same SHA. `ci.yml` still listed `tags: v*.*.*`.
3. **`release.yml`'s `Test (Release)`.** It ran the whole suite unsharded on one runner and took 22 min 53 s, out of a job of about 27 min.

`ADR-0160` §2 already makes a release publish only on a green `CI Gate` for the tagged SHA. That gate is satisfied by run 542 alone. Runs 2 and 3 tested an identical tree with identical locked packages (`ADR-0160` §5), so neither could find anything run 1 had not. Their only effect was to keep the release waiting about 25 minutes longer.

## Decision

1. **`ci.yml` no longer runs on tag pushes.** Its `push` trigger is `main` and `release/**` only. Pull requests, the manual run and the weekly schedule are unchanged.
2. **`release.yml` no longer tests.** Its `build` job restores (locked), builds Release with `TreatWarningsAsErrors`, runs the Governance Health Check, packages and stages the assets. `ci-gate` is unchanged: it requires `CI Gate` = `success` on the tagged SHA, and `publish` still needs both jobs.
3. **Tag only a commit CI has run on.** That means a merge to `main` or a push to `release/**`. To release any other commit, run CI on it first (Actions → CI → Run workflow, choosing the tag). Otherwise `ci-gate` finds no run and fails after its 75-minute wait, with a message saying exactly that.

## Consequences

- A release takes the time of restore, build, governance and packaging (about 6 minutes) once `CI Gate` is green, instead of about 27 minutes. One full CI run per release is saved.
- Every shipped commit is still tested in both configurations, on Windows, before it is published: by `main`'s CI, which `ci-gate` reads.
- A flaky test can no longer fail the release step after CI has passed. Equally, the release no longer re-checks for a flake that CI happened to pass. That is accepted: `ADR-0160` §2 makes `CI Gate` the release gate, and running the suite a second time is not a stronger gate.
