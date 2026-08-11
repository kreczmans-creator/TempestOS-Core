# `v0.11.0` — Release Publication Report

Follow-on to `WP 11.9.0`'s own sign-off: pushing the branch and tag to
`origin` and independently verifying the result on real GitHub-hosted
infrastructure for the first time in this release's history — closing
(or, where it did not close cleanly, replacing with sharper, evidenced
findings) `TD-44`'s own disclosed "never executed on real
infrastructure" gap. No source code was modified to produce this
report; three genuine defects were found and diagnosed to root cause,
none fixed — remediation plans are presented for decision, per this
task's own explicit instruction.

## 1. Git Status Before Push

| Check | Result |
|---|---|
| Branch | `feature/v0.11.0-v1-architecture` |
| Working tree | Clean |
| Local tag `v0.11.0` → commit | `fda4172689405fe7a8c3fd51a66fbd6b9276aca9` |
| `HEAD` | `fda4172689405fe7a8c3fd51a66fbd6b9276aca9` (tag target confirmed identical to `HEAD`) |

## 2. Branch Pushed

`git push origin feature/v0.11.0-v1-architecture` — **succeeded.**
`[new branch] feature/v0.11.0-v1-architecture -> feature/v0.11.0-v1-architecture`.
Confirmed live: `git ls-remote --heads origin` returns
`fda4172689405fe7a8c3fd51a66fbd6b9276aca9 refs/heads/feature/v0.11.0-v1-architecture`.

## 3. Tag Pushed

`git push origin v0.11.0` — **succeeded.** `[new tag] v0.11.0 -> v0.11.0`.
Confirmed live: `git ls-remote --tags origin` returns the tag object
dereferencing to `fda4172689405fe7a8c3fd51a66fbd6b9276aca9`, matching
the local tag exactly.

## 4. Workflow Results

Three runs fired from this push (both branch-push and tag-push are
`push` events; `ci.yml` triggers on both, `release.yml` triggers on the
tag only):

| Run | Workflow | Trigger | Result |
|---|---|---|---|
| [`31497713750`](https://github.com/kreczmans-creator/TempestOS-Core/actions/runs/31497713750) | CI | push, `feature/v0.11.0-v1-architecture` | **Failed** |
| [`31497718682`](https://github.com/kreczmans-creator/TempestOS-Core/actions/runs/31497718682) | CI | push, tag `v0.11.0` | **Failed** |
| [`31497718930`](https://github.com/kreczmans-creator/TempestOS-Core/actions/runs/31497718930) | Release Publish | push, tag `v0.11.0` | **Success** |

### Run `31497713750` (CI, branch push)

| Job | Conclusion |
|---|---|
| Build & Test (Debug) | Success |
| Build & Test (Release) | **Failed** — `Test (Release)` step |
| CI Gate | **Failed** — direct consequence of the Release test failure above |
| Governance Health Check | **Failed** — `Run governance health check` step |

### Run `31497718682` (CI, tag push)

| Job | Conclusion |
|---|---|
| Build & Test (Debug) | Success |
| Build & Test (Release) | Success |
| CI Gate | **Success** |
| Governance Health Check | **Failed** — `Checkout` step itself |

### Run `31497718930` (Release Publish, tag push)

| Step | Conclusion |
|---|---|
| Checkout tag | Success |
| Build (Release) | Success |
| Test (Release) | Success |
| Package Release build output | Success |
| Resolve release notes | Success |
| Publish GitHub Release | Success |

**GitHub Release published**: [`v0.11.0`](https://github.com/kreczmans-creator/TempestOS-Core/releases/tag/v0.11.0),
not a draft, not a pre-release. Two assets, correctly separated per
`ADR-0101`/`WP 11.3B`: `TempestOS-v0.11.0.zip` (49.8 MB, the shipped
Desktop application) and `TempestOS-v0.11.0-engineering-harness.zip`
(0.6 MB, `Tempest.App`, labelled as the harness, never bundled with the
product).

## 5. CI Summary and Root-Cause Diagnosis

**Overall: the release itself is genuinely reproducible on GitHub
infrastructure — the workflow that builds, tests, and publishes it
(`Release Publish`) passed cleanly, independently, on real
infrastructure.** Three separate, genuine, previously-undetected
defects were found — this is exactly the category of finding `TD-44`
predicted was possible ("every claim this release makes about CI
remains `Inferred`, not `Verified`"), now replaced with real evidence.
None of the three is a regression in `v0.11.0`'s own product code; all
three are defects in release-engineering tooling/configuration that
could only be found by actually running on this specific infrastructure
— confirming the value of doing so before declaring `TD-44` closed
outright.

### Finding 1 — Flaky test: `ObjectEditorViewTests.VerificationResultSection_RecordPass_ActuallyRecordsARealVerificationRecord`

**Root cause, confirmed to the exact line**:
[`tests/Tempest.Desktop.Tests/ObjectEditorViewTests.cs:480`](tests/Tempest.Desktop.Tests/ObjectEditorViewTests.cs#L480)
uses a hardcoded `await Task.Delay(50);` to wait for an async
"Record Pass" command to complete and the UI status `TextBlock` to
update, before asserting at line 483 that the text contains
`"recorded"`. On this run, the assertion read `String: ""` — the delay
elapsed before the async update landed.

**Confirmed flaky, not a regression**: the identical commit
(`fda4172`) passed this exact test in the separate tag-push CI run
(`31497718682`) and in this session's own two prior local runs (Debug
and Release, §`WP11.9.0` Engineering Release Report §A2). A fixed-delay
wait is inherently more likely to lose its race on a shared,
variable-load GitHub-hosted runner (many parallel test classes
competing for CPU under default xUnit parallelization) than on a quiet
local machine — exactly the condition this was first run under.

**Not caused by `v0.11.0`**: `ObjectEditorViewTests.cs` is untouched by
this release's own diff (confirmed independently twice already, by the
Chief Architect and QA Lead reviews in `WP11.9.0 Engineering Release
Report.md`) — this is pre-existing, latent flakiness in `WP 10.3A`-era
test code, surfaced for the first time only because this is the first
time this suite has run on this infrastructure.

**Remediation plan (not applied)**: replace the fixed `Task.Delay(50)`
with a bounded poll/await on the actual condition (e.g. poll
`statusMessage.Text` for up to ~2s with a short interval, or await the
real completion signal the "Record Pass" command dispatch already
raises, if one exists) — the same category of fix this project's own
`WP 10.3B`/`WP 10.5A` precedent already applied to comparable
UI-timing risks. Estimated effort: small, single-file, test-only change.

### Finding 2 — `scripts/governance-healthcheck.ps1`: `SummaryPath` doubles into `RepoRoot`

**Root cause, confirmed to the exact line**:
[`scripts/governance-healthcheck.ps1:49`](scripts/governance-healthcheck.ps1#L49):

```powershell
$resolvedSummary = [System.IO.Path]::GetFullPath((Join-Path (Get-Location) $SummaryPath))
```

PowerShell's `Join-Path` cmdlet — unlike `[System.IO.Path]::Combine()`
— does **not** discard the parent path when the child is already
rooted; it concatenates unconditionally. `ci.yml`'s own invocation
(`$summaryPath = Join-Path $env:RUNNER_TEMP "governance-summary.txt"`)
produces an already-absolute Windows path (`D:\a\_temp\governance-summary.txt`
on this runner). Joining that again against `(Get-Location)` (the repo
root during this step) produces
`D:\a\TempestOS-Core\TempestOS-Core\D:\a\_temp\governance-summary.txt`
— a malformed path that happens to start with `RepoRoot`, which then
correctly, but for the wrong underlying reason, trips the script's own
"never write inside the repo" safety check and throws.

**Reproducible every time** this script is invoked in CI with an
absolute `-SummaryPath` from a working directory at `RepoRoot` —
i.e., on every future CI run, not an intermittent condition. Never
manifested in `WP 11.2A`'s own fixture-based testing because that
testing never exercised an absolute `-SummaryPath` value from
`RepoRoot` as the current directory — precisely the gap `TD-44`
predicted.

**Remediation plan (not applied)**: guard the join with
`[System.IO.Path]::IsPathRooted($SummaryPath)` — if true, resolve
`$SummaryPath` directly via `[System.IO.Path]::GetFullPath($SummaryPath)`
without joining against `(Get-Location)` at all; only join when
`$SummaryPath` is relative. Estimated effort: small, single-line change
plus a regression test (mirrors the QA Lead's own already-recommended
Pester coverage for this script, `WP11.9.0 Engineering Release Report.md`
§A4).

### Finding 3 — `ci.yml`'s `governance-health-check` job: `Checkout` fails on a tag-triggered push

**Root cause, confirmed to the exact line**:
[`.github/workflows/ci.yml:219-227`](.github/workflows/ci.yml#L219-L227) —
the job's `Checkout` step sets `fetch-tags: true` but no explicit
`ref:`. When the triggering event is itself a tag push, `actions/checkout@v4`
defaults to checking out `github.ref` (`refs/tags/v0.11.0`) *and*
separately fetches all tags via `fetch-tags: true` — both attempt to
land the identical tag at the same local ref path, and git refuses:
`fatal: Cannot fetch both fda4172... and refs/tags/v0.11.0 to refs/tags/v0.11.0`,
exit code 128.

**Reproducible on every future tag push** that reaches this job — not
intermittent. `release.yml`'s own `Checkout tag` step (§4, above) has
no `fetch-tags: true` and was never at risk of this collision, which is
why it succeeded while this job failed on the identical trigger.

**Remediation plan (not applied)**: pin `ref: ${{ github.sha }}`
explicitly on this step — checking out by commit SHA rather than by the
tag's own symbolic ref removes the competing `refs/tags/v0.11.0` write
target, and `fetch-tags: true` can then fetch tags independently without
collision. Estimated effort: one line added to `ci.yml`.

## 6. Reproducibility, `TD-44`, and `TD-45`

**Reproducible on GitHub infrastructure: yes, for the release action
itself.** `Release Publish` — the workflow whose job is specifically to
independently rebuild, retest, and publish the tagged release —
succeeded cleanly and produced a real, verifiable GitHub Release with
correctly separated assets. This is the strongest possible evidence
`v0.11.0` is genuinely reproducible on real infrastructure, not merely
asserted to be.

**`TD-44` (CI never executed on real infrastructure): do not close as
simply "Resolved."** The precondition is now true — CI has executed on
real GitHub-hosted infrastructure — but the exercise found three real
defects that were invisible to every prior local-only verification.
Recommend re-classifying `TD-44` as **"Verified — findings below"**,
superseded by three new, separately-tracked items for the concrete
defects found (Findings 1–3, above), rather than marked Resolved with
no residual trace. Closing it silently would understate what was
actually learned by finally running on real infrastructure.

**`TD-45` (branch protection not configured): not yet recommended.**
See §7 — a currently-flaky `CI Gate` and a `Governance Health Check`
job that fails 100% of the time on this infrastructure are exactly the
wrong conditions under which to make `CI Gate` a hard-required merge
check; doing so today would risk blocking a legitimate first PR on
test noise, not a real regression, which would undermine trust in the
gate from its very first real use.

## 7. Recommendation: Branch Protection

**Not yet — fix Finding 1 first, then enable.** `CI Gate` itself is
correctly designed and behaved correctly on both runs (it failed when
`Build & Test (Release)` genuinely failed, and passed when both
configurations genuinely passed) — the gate mechanism is sound. The
risk is specifically that `Build & Test (Release)`'s own flaky test
(Finding 1) can fail `CI Gate` on a rerun of the *exact same, correct*
code, which is precisely the failure mode that erodes trust in a
required check and trains contributors to re-run rather than
investigate. Recommend: fix Finding 1, confirm 2–3 consecutive clean CI
runs on real infrastructure, *then* configure the branch-protection
rule already fully specified in `WP11.1B Engineering Workflow.md` §4
(required `CI Gate` status check, required CODEOWNERS review,
merge-commit-only). Findings 2 and 3 (`Governance Health Check`) do not
block this recommendation on their own timeline — that job is
deliberately not in `CI Gate`'s own dependency chain — but leaving them
unfixed means the governance drift-detection tool this release built
cannot complete a single successful CI run today, which is itself worth
fixing promptly, independent of the branch-protection decision.

## 8. Recommendation: Merging into `main`

**Yes, safe to proceed** — with the same sequencing as §7, not as a
blocker to opening the PR itself. The `v0.11.0` tag is already
published from this exact commit via a workflow that independently,
successfully rebuilt and retested it; zero regression risk exists in
the product code (confirmed by two independent reviews plus this
report's own diagnosis that all three findings are pre-existing or
CI-configuration-only, not `v0.11.0` product defects). Recommend:
open the PR now (`https://github.com/kreczmans-creator/TempestOS-Core/pull/new/feature/v0.11.0-v1-architecture`,
already offered by the push output); do not merge until at least
Finding 1 is fixed and CI is observed green on this exact branch, so
the PR's own CI history does not carry a misleading red mark against
sound code. Findings 2/3 are recommended fixed in the same PR or an
immediate fast-follow, since they concern the release-engineering
tooling this whole release exists to deliver, not the product itself.

## Summary

| Item | Status |
|---|---|
| Branch pushed | ✅ `feature/v0.11.0-v1-architecture` |
| Tag pushed | ✅ `v0.11.0` |
| Release published | ✅ [GitHub Release](https://github.com/kreczmans-creator/TempestOS-Core/releases/tag/v0.11.0), two correctly-separated assets |
| CI reproducibility | ✅ Confirmed via `Release Publish`'s own independent rebuild/retest |
| Defects found | 3 (all diagnosed to root cause; none fixed, per this task's own instruction) |
| `TD-44` | Recommend: re-classify "Verified — findings below," not silently closed |
| `TD-45` / branch protection | **Not yet** — fix Finding 1 first |
| Merge to `main` | **Yes, after Finding 1 fix and one clean CI run** |
