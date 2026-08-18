# TempestOS v0.13.1 — "Trust & Deployment Hardening" (corrected release)

## 1. Executive Summary

`v0.13.1` is the **published release of `v0.13.0`'s content**. It is a
corrective patch release in the narrowest sense the project's own
versioning rule allows: it carries `v0.13.0`'s complete scope plus a
single test-determinism fix, and **no product change whatsoever**.

`v0.13.0` was genuinely tagged and genuinely merged to `main`, but its
tag-triggered `release.yml` run failed at the Test (Release) step, so
packaging and publication never ran. No GitHub Release, no
`TempestOS-v0.13.0.zip`, and no engineering-harness asset were ever
produced for it.

**`v0.13.0`'s tag is not amended, moved, deleted, or recreated.** It
remains permanently at commit `6089a218`, exactly as cut, per Engineering
Governance §7.4 — *"a tag, once created and pushed, is not moved or
recreated; if a mistake is discovered after tagging, a new version and a
new tag are cut, the old one is never silently altered."* `v0.13.1` is
that new tag. `v0.13.0` stays in the Release Register as a valid but
unpublished historical tag, with its own row recording precisely what
happened.

Everything in §2 below shipped in `v0.13.0`'s tree and is unchanged here.
The only difference between the two trees is described in §3.

## 2. Scope — unchanged from `v0.13.0`

`v0.13.1` delivers TempestOS's plugin platform and its trust boundary:
the scope `FCR-0001` has named since `v0.7.0`, and `Security Roadmap.md`
items 1, 2, and 10 since `v0.6.0`. Six ADRs (`ADR-0107`–`ADR-0112`)
define it — plugin dependency-graph resolution and extended failure
classification; a load/upgrade/uninstall lifecycle with live in-process
unload retained as a named non-goal; plugin service registration adding
no new DI capability; **capability-scoped isolation rather than
`AssemblyLoadContext` or process separation**; a trust-capability model
extending `IPermissionEvaluator` via a component principal and
trust-ordered registration; and a detached manifest-and-assembly hash
signature verified at Plugin Discovery.

**Closes `TD-09`, `TD-10`, `TD-11`.**

For the complete per-Work-Package account of that scope — all
twenty-eight Work Packages, the four adversarial remediation chains, the
`TD-51`/`TD-52` findings and their closure, the statistics, and the full
list of deferred and open findings — see
**`docs/releases/v0.13.0/Release Notes.md`**, which remains accurate and
is not superseded in content. It is deliberately not duplicated here.

**No third-party plugin ships, and third-party plugin support is neither
enabled nor advertised.** `src/Plugins/` contains only a `README.md`;
`FCR-0002` remains not started; `TD-56` is a mandatory precondition to
enabling it.

## 3. What changed since `v0.13.0`

Two commits, **neither touching production code**:

- **`7449756` — Desktop async test determinism remediation (test-only).**
  `ObjectEditorViewTests.Save_RevisedContent_ActuallyAdvancesTheRealRevisionNumber`
  waited a fixed `Task.Delay(50)` for an asynchronous save whose task the
  test cannot observe — the Save handler is an `async void` click
  subscriber, and the revise path performs real disk I/O
  (`EngineeringDocumentStore.ReviseAsync` does one read plus two
  `File.WriteAllTextAsync` writes under an `AsyncKeyedLock`, continuing on
  the thread pool). The fixed delay was therefore a race against that
  chain rather than a wait for it, and it lost that race once — in
  `v0.13.0`'s own tag-triggered `release.yml` run, while the concurrent
  `ci.yml` run on the identical commit, runner image and minute passed.
  Replaced with the bounded condition-based wait `TD-46` already
  established for a sibling test in the same file: re-read the repository
  each iteration until the revision number actually advances, 2-second
  deadline, 10 ms interval. It still fails, exactly as before, if the
  revision genuinely never advances. One test method, one file,
  **+28/−2**.

- **`ea3fe07` — `v0.13.0` Release Register closure (documentation-only).**
  Adds `v0.13.0`'s row, recording it as *tagged and merged to `main`,
  GitHub Release not published*, with the workflow-failure account.
  **+7/−3**.

Combined delta `6089a218..ea3fe07`: **2 files, +35/−5, zero `src/`
files.**

## 4. Testing Summary

| Configuration | Tempest.Core.Tests | Tempest.Desktop.Tests | Combined | Failed | Skipped |
|---|---|---|---|---|---|
| Debug | 2,341 | 221 | **2,562** | 0 | 0 |
| Release | 2,341 | 221 | **2,562** | 0 | 0 |

Both builds report **0 Warnings / 0 Errors** with
`-p:TreatWarningsAsErrors=true`, the bar CI itself applies. Test count is
unchanged from `v0.13.0` — `7449756` altered one existing test's
synchronisation, adding no test and removing none.

Sequential verification discipline preserved throughout: no increase in
test parallelism, no `xunit.runner.json`, no `.runsettings`, and no
`CollectionBehavior`/`maxParallelThreads`/`DisableParallelization` change.
The other twenty-eight fixed `Task.Delay` waits in
`Tempest.Desktop.Tests` are deliberately **not** generalised — that
remains open work, recorded against `TD-46`.

**Disclosed honestly:** the flake was never reproduced locally. The
target test passed 15/15 in isolation, the Desktop suite passed
repeatedly, and the assertion held 8/8 even with the delay set to `0` —
this machine completes the write chain faster than the assertion needs.
The diagnosis therefore rests on source analysis, the `TD-46` precedent,
and the CI timing evidence, not on a local reproduction.

## 5. Known Technical Debt

Unchanged from `v0.13.0`: **56 tracked items — 18 Resolved, 1 Partially
resolved, 37 Open.** `TD-49`–`TD-56` are described in
`docs/releases/v0.13.0/Release Notes.md` §4 and are not restated here.

`TD-46` gains renewed relevance: its fixed-`Task.Delay` pattern, fixed
once for one test in `WP 11.4A`, was never generalised, and the same
idiom in a second test is what prevented `v0.13.0` from publishing.

## 6. Deferred / Open Findings

All of `v0.13.0`'s deferred findings carry forward unchanged — see that
release's §5. Specific to this release:

- **`v0.13.0`'s tag is permanently unpublished.** It is not deleted or
  repointed, and `release.yml` is not retried against it. The
  `WP 11.4B` tag-position exception explicitly cannot reach this case: it
  is bounded to a tag's mechanical position, never to build or test
  evidence, and only before the release branch closes.
- **First non-zero patch version in the project's history.** `v0.13.1`
  sets that precedent; `05-release-engineering.md` defines PATCH as *"a
  hotfix, and only a hotfix, never a vehicle for new capability"*, which
  this release honours in substance — the delta is one test and one
  register row.
- **`TD-45` remains Open and is now demonstrably unenforceable**: branch
  protection returns HTTP 403 on this repository plan, so the PR/CI
  discipline that gates `main` is upheld by convention, not tooling.

## 7. Final Engineering Assessment

Debug and Release builds **0 Warnings / 0 Errors**; full regression
**2,562/2,562 both configurations**; `governance-healthcheck.ps1` **7
passed, 1 warned (pre-existing), 0 failed**.

The authoritative readiness record for this release's engineering content
remains
`docs/releases/v0.13.0/WP13.12.2 Engineering Release Report.md` — its six
category assessments apply unchanged to `v0.13.1`, whose only delta is
one test's synchronisation and one register row. The single Release
Blocking finding that report carried, CI never having run on `main` at a
pre-tag commit, was cleared at `6089a218` and remains cleared in this
release's ancestry.

## Related Documents

`docs/releases/v0.13.0/Release Notes.md` (the full scope account, still
accurate); `docs/releases/v0.13.0/WP13.12.2 Engineering Release Report.md`
(authoritative readiness record); `docs/releases/v0.13.0/WorkPackages.md`;
`docs/governance/Delivery/Release Register.md` (rows for both `v0.13.0`
and `v0.13.1`); `docs/governance/Quality/Technical Debt Register.md`
(`TD-46`, `TD-45`, `TD-56`);
`docs/academy/06 Engineering Standards/Engineering Governance.md` (§7.4,
§7.5, §7.7); `docs/academy/06 Engineering Standards/05-release-engineering.md`.
