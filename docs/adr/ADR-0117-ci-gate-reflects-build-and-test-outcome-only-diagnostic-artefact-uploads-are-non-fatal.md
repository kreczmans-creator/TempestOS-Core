# ADR-0117: The CI Gate Reflects Build and Test Outcome Only — Diagnostic Artefact Uploads Are Non-Fatal

## Status

Accepted — `WP 14.2.1` (CI Pipeline Remediation), 2026-08-29.

Corrects a defect in the pipeline `WP 11.1A` established and `WP 11.2A`
extended. `ci.yml`'s three-job shape (matrix `build-and-test`,
`governance-health-check`, `gate`) is unchanged; only the coupling
between diagnostic steps and the gate's verdict is.

## Context

`WP 11.1A` built this repository's first CI pipeline precisely so that
"0 Warnings/0 Errors, N/N tests passing" would stop being a
self-reported claim, and `gate` exists to give branch protection one
unambiguous status check. `gate` decides by reading
`needs.build-and-test.result`.

That is where the defect lived. A GitHub Actions job's conclusion folds
in **every** step, so `build-and-test.result` was never "did the build
and the tests pass" — it was "did the build, the tests, **and every
artefact upload** pass". The two are not the same kind of fact.

The difference stopped being theoretical between 2026-08-28 and
2026-08-29, when this account's Actions artifact storage quota was
exhausted:

```
##[error]Failed to CreateArtifact: Artifact storage quota has been hit.
Body: { "code": "permission_denied", "msg": "insufficient usage to create artifact" }
```

Across runs `33150525673`, `33151166016` (three attempts),
`33212508984` and `33241023711`, the pattern is identical and
unambiguous: `Restore` ✅, `Build` ✅, `Test` ✅,
`Publish build & test summary` ✅, `Governance Health Check` ✅ — then
`Upload build log` ❌ and `Upload test results` ❌ on quota, job ❌,
and `CI Gate` ❌. Re-running only produced the same result, because
nothing about the commit was wrong.

Two facts fix the cause beyond doubt. First, the failures reached
`main`: run `33241023711` failed at commit `00f7f394`, an unchanged
commit that had passed as run `32170250522` eleven days earlier.
Second, the storage was being consumed by the pipeline itself — the
Release leg published `Tempest.Desktop`'s and `Tempest.App`'s full build
output on *every push to every branch* at 14-day retention, roughly
50 MB a push (the `v0.13.1` desktop asset is 49,877,891 bytes), for
trees that are a convenience copy and never the release mechanism.

## Decision

**The `CI Gate` reflects build and test outcome only. Every artefact
upload in `ci.yml` and `mobile-heads.yml` carries
`continue-on-error: true`, so no upload can decide whether a commit is
considered sound. Build-output artefacts are published from `main` and
tags only, at 7-day retention. And every fact an artefact carried about
a failure is now written to the Job Summary, which needs no storage.**

### Verification and diagnostics are different categories of step

Build and test are verification: they decide whether the commit is
sound, and a failure in either must stop the pipeline — unchanged, and
deliberately so. `TreatWarningsAsErrors` stays. No test is retried,
skipped, quarantined or made tolerant to reach green; this ADR changes
only what is *allowed to speak* for the code's soundness. Artefact
uploads are diagnostics: they carry evidence about a verdict already
reached. A diagnostic transport fault is not evidence about the code,
and a gate that a billing counter can redden is not reporting on the
code.

A failed upload remains visibly red inside the job, so the condition is
never hidden — it simply no longer votes.

### The summary is the primary diagnostic; artefacts are the backup

The quota outage exposed a second, quieter defect: when the TRX upload
failed, a real intermittent failure on `main` could be *counted* and not
*identified*. The summary step reported `Failed: 1` of 2,341 and nothing
more; the only artefact naming the test had failed to upload, and the
console log sat far outside the run-log API's own tail window. The
summary step therefore now parses the TRX itself and emits every failed
test's name, message and stack, to both the Job Summary and the console.
Storage-independent by construction, it works in exactly the conditions
that break artefacts.

## Consequences

**Positive:**

- The gate states a true fact about the commit. Infrastructure and
  billing conditions can no longer produce a false negative on a
  required status check, on any branch or on `main`.
- The recurring cause is removed, not merely tolerated: feature-branch
  pushes no longer publish ~50 MB apiece.
- A failing run names its failing tests from the run page alone, with no
  artefact, no download and no local reproduction.
- `mobile-heads.yml` loses two `if-no-files-found: error` traps on
  packaging paths that could never be verified when it was written.

**Negative:**

- A genuinely broken upload step is now easier to overlook, being amber
  rather than fatal. Accepted: the summary carries the same information,
  and the step still shows red in the job.
- Build-output artefacts are no longer available from feature-branch
  runs. Anyone wanting one builds locally, or pushes to `main`. This is
  a deliberate reduction in convenience to buy back a working gate.
- Storage already consumed is not released by this change; GitHub
  recalculates usage on its own cycle and old artefacts expire on theirs.
  This ADR stops the pipeline *adding* to the problem and stops the
  problem *reddening the gate*; it does not reset the counter.

## Alternatives Considered

**Leave the coupling and re-run until the quota window clears.**
Rejected: it was tried — run `33151166016` reached attempt 3 — and could
not work, because nothing about the commit was wrong. It also leaves the
defect armed for the next outage.

**Delete artefact uploads entirely.** Rejected: it would fix the gate by
destroying the diagnostics `WP 11.1A` deliberately added. The correct
split is to keep them and stop them voting.

**Make `gate` inspect step-level outcomes instead of the job
conclusion.** Rejected: GitHub exposes no step-level results to a
downstream job, so this would mean threading outputs through job
outputs — more machinery, more to drift, and it would still be reasoning
about the wrong thing. `continue-on-error` states the intent directly at
the step that has it.

**Disable artefacts only when quota is exhausted.** Rejected: a workflow
cannot know its own quota state, and a step that must guess at billing
conditions is worse than one that simply cannot fail the build.

**Reduce retention alone, keeping every-push build-output uploads.**
Rejected as insufficient: it slows the same accumulation rather than
addressing that the artefacts have no consumer on a feature branch.

## Related Documents

`WP 11.1A Implementation Report.md` (the pipeline this corrects);
`docs/academy/06 Engineering Standards/04-continuous-integration.md`
(updated by this Work Package); `ADR-0106` (Engineering Readiness
Review, which consumes CI evidence);
`docs/governance/Quality/Technical Debt Register.md` (`TD-59` the
intermittent Release-configuration test this outage exposed, `TD-60` the
Node 20 action deprecation);
`docs/academy/03 Work Packages/WP14.2.1-ci-pipeline-remediation.md`;
`.github/workflows/ci.yml`; `.github/workflows/mobile-heads.yml`.
