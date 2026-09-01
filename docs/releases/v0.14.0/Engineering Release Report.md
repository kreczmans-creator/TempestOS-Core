# v0.14.0 — Engineering Release Report

**Work Package:** `WP-Z4` (Release Preparation)
**Executed against:** `44c4701`, branch
`claude/stage-3-descriptor-binding-6wz401`
**Amended:** `WP-Z4` Stage 5, 2026-09-01, at HEAD `384e47f` — §4, §6, §7
and §8 record the PR #5 CI recurrence now tracked as `TD-119`. §1's
release range and commit count remain anchored at `44c4701`, the commit
they were derived from, and are deliberately not restated. **The
recommended verdict is unchanged.**
**Model:** `ADR-0106` / `docs/architecture/Engineering Readiness Review
Architecture.md` — five categories, three-kind blocking taxonomy, four-verdict
vocabulary.

> **This report does not claim the release has shipped.** At the time of
> writing, `v0.14.0` is **not merged to `main`, not tagged, not published,
> and not certified.** Certification is Product Approval's act
> (Engineering Governance §9); this report *recommends*. Two mandatory
> verifications — the Build and Test Gates on `main` itself immediately
> pre-tag (§7.3), and `release.yml`'s independent run against the tagged
> commit — **have not yet been performed and cannot be, from a feature
> branch.**

---

## 1. Scope reconciliation against git

The release range `v0.13.1..44c4701` holds **52 commits**, derived from
`git log`, not from a prior summary. `WP-Z4`'s own commit — this report,
the Release Notes, the Work Package inventory, `VERSION`, the Release
Register row, `WP 13.13.2`'s retrospective and the `PHYSICAL_REVIEW.md`
correction — makes **53**. Every figure in this report is measured at
`44c4701`, before that commit, and the tables below are therefore the
diff this release-preparation work package inherited rather than the one
it produced.

`origin/main` is `00f7f394`, which is **ahead of the `v0.13.1` tag**
(`6eca5fde`) by two commits — `194b067` (`WP 13.13.2`) and its merge. Both
are unreleased and ship first under this tag. The branch was **50 ahead of and 0 behind `origin/main`** at `44c4701`
(51 ahead with this commit), so the merge is a clean fast-forward
candidate; per Governance §7.7 it must nonetheless go through a pull
request, gated on `CI Gate`, merged as a merge commit.

| Area | Files | Insertions | Deletions |
|---|---|---|---|
| `src/` | 215 | 19,997 | 2,984 |
| `tests/` | 108 | 21,979 | 723 |
| `docs/` | 54 | 6,877 | 136 |
| `.github/` | 1 | 41 | 0 |
| root (`PHYSICAL_REVIEW.md`, `PROJECT_STATUS.md`, `README.md`) | 3 | 1,185 | 11 |
| **Total** | **377** | **49,905** | **3,680** |

Production `.cs` files 725 → 794; test `.cs` files 270 → 337.

**Disclosed:** `.github/workflows/ci.yml` was modified in this release, by
two pre-programme commits (`7b53ce6`, `416f2c8`). The remediation
programme itself changed no CI.

Every Work Package in the range is accounted for in `WorkPackages.md` as
**Complete** or **Not started — out of this release's scope**. None is in
an undocumented state.

## 2. Architecture readiness

*Evidence and assessment.*

| Required evidence | Result |
|---|---|
| ADR Register vs `docs/adr/` | **119 / 119, exact match** (was 111 at `v0.13.1`) |
| ADRs produced this release honoured by shipped code | `ADR-0095`, `0113`–`0119`. Each cites its implementing change; `ADR-0118` authorises no code change and none was made; `ADR-0119`'s rule is implemented in `UndoRedoCoordinator` and mutation-verified. |
| Layering — dependencies flow downward (`ADR-0023`) | **Enforced at build time**, newly this release: `DependencyDirectionTests` asserts `Desktop → App → Core` and no Avalonia below the shell, from the declared graph *and* `Assembly.GetReferencedAssemblies()`. |
| Dependency health — no new circular or upward reference | `dotnet build` succeeds in all four configurations; the invariant test covers the namespace-level case the compiler cannot. |

**Status: Pass.**

## 3. Implementation readiness

| Required evidence | Result |
|---|---|
| Work Package reports vs `git diff v0.13.1..HEAD` | Reconciled in §1 and `WorkPackages.md`. No material undisclosed diff found. |
| No partial work | Every Work Package **Complete** or explicitly out of scope. `TD-109` is **partially resolved and says so** — `WP-G` deliberately did not close it. |
| Public API review | `IInputBindingRegistry` gained two optional, defaulted properties (`ContextSource`, `ParameterPrompt`) — additive, non-breaking, recorded in `WP-A2`. `ServiceProviderExtensions.GetService<T>()` was **deleted** (`WP-F`): a public API on a plugin-hosting assembly with zero production callers; disclosed rather than silently removed. `IUndoRedoStack` unchanged. |

**Finding (Pre-Existing, Unaffected):** `TD-109` — `MainWindow` remains
1,052 lines. Reduced 34% this release; the remainder is a different
question from the row's stated defect.

**Status: Pass, with observations.**

## 4. Verification readiness

| Gate | Debug | Release |
|---|---|---|
| `Tempest.Core.Tests` | **3,088 / 3,088** | **3,088 / 3,088** |
| `Tempest.Desktop.Tests` | **372 / 372** | **372 / 372** |
| Build, `TreatWarningsAsErrors=true` | 0 warnings, 0 errors | 0 warnings, 0 errors |

Total **3,460**, from **2,562** at `v0.13.1` (+898). Test `.cs` files
270 → 337.

Mutation evidence is recorded per Work Package rather than aggregated:
`WP-B1` 2/2, `WP-D2` 3/3, `WP-A1` (allow-list and all four policy failure
directions), `WP-H` 5/5, `WP-D1` 4/4, `WP-F` 4/4, `WP-G` both event seams,
`WP-A2` 3 run — **one survived, was investigated rather than absorbed, and
is now killed** — `WP-E` 8/8 after a survivor exposed genuinely redundant
code, `WP-Z2` 2/2.

**Finding (Disclosed, Non-Blocking) — `TD-119`, the third recurrence of a
documented Desktop test flake.** The figures above were obtained locally
and on the push-triggered CI run. They did **not** reproduce on the
pull-request run of PR #5.

| | Push run `33543456336` | PR run `33545260056` |
|---|---|---|
| Head SHA | `384e47fc` | `384e47fc` — **identical** |
| `Build & Test (Debug)` | Pass 372/372 | **Fail 371/372** |
| `Build & Test (Release)` | Pass 372/372 | **Fail 371/372** |
| `Governance Health Check` | Pass | Pass |
| `CI Gate` | Pass | **Fail** (aggregation of the two above) |
| Window (UTC) | 18:24 – 18:36 | 18:42 – 18:52 |

The two failures are different tests in different files:
`ProjectGovernanceAcceptanceTests.Journey_ProposeADecision_AcceptIt_ThenRelaunch_AndSupersedeIt`
in Debug (`Assert.Single() Failure: The collection was empty`) and
`FeatureCompletionTests.MechanicalDuplicate_OnARealPart_ActuallyCreatesARealCopy`
in Release (`Assert.Equal() Failure: Expected 13, Actual 12`). Both assert
after a fixed `Task.Delay` against an asynchronous command and persistence
chain the test never joins — the mechanism `TD-46`/`WP 11.4A` named and
`WP 13.12.8`/`WP 13.12.9` met again at `v0.13.0`. Both builds succeeded
with zero warnings; both runs uploaded their artefacts successfully; no
timeout, runner loss, out-of-memory, disk-space or quota evidence exists.

**Classified as test-suite synchronisation debt, not a product defect**,
on four grounds: the identical SHA passed all four checks on a
non-overlapping push run; four local Desktop runs at this SHA passed
372/372; no regression path exists in the `v0.13.1..384e47f` range — its
only behavioural changes are `CockpitReadScope` (inert outside an active
scope, and on neither failing path) and one dispatcher wrapper in
`UndoRedoCoordinator`; and `MechanicalDuplicate` is pre-existing, having
shipped green through `v0.12.0`, `v0.13.0` and `v0.13.1`. Recorded as
`TD-119`, **deferred — remediation is explicitly not part of this
release's implementation.**

**This report does not claim the tests are fixed, that `CI Gate` has
passed on PR #5, or that the flake will not recur.** It records that the
failure has been investigated, classified against evidence, and tracked.

**Status: Pass, with observations.**

## 5. Governance readiness

| Required evidence | Result |
|---|---|
| Register consistency (`governance-healthcheck.ps1`, run this review) | **7 passed, 1 warned, 0 failed.** The warning is pre-existing and informational (`v0.9.0`/`v0.10.0` have no `WorkPackages.md`), long disclosed. |
| Academy completeness — every Work Package has a retrospective | **158 retrospectives** (was 142 at `v0.13.1`). `WP-Z3` wrote the programme's fifteen; `WP-Z4` wrote `WP 13.13.2`'s. **Every Work Package in this release's range has exactly one.** |
| Documentation completeness | ADR, Test, Academy and Technical Debt Registers re-derived from the repository by `WP-Z1`, advanced by `WP-Z2`/`Z3`/`Z4`. |
| `PROJECT_STATUS.md` accuracy | Updated through `WP-Z4`; lower sections retain pre-existing drift under the file's own retention convention. |

**Finding (Disclosed, Non-Blocking):** retrospectives do **not** exist for
the ~34 pre-programme commits in §1's range, nor for `v0.11.0`'s ten Work
Packages. Both gaps are older than this release's own Work Packages, are
recorded in `WP-Z3`'s retrospective §12, and are **deliberately not closed
here**. Every Work Package *of this release* has one.

**Finding (Pre-Existing, Unaffected):** the Repository Metrics, Risk,
Feature, Traceability and Validation Registers were last reviewed
2026-07-28 and were not advanced by this release. The `Validation
Register.md` Test Gate row still reads 552.

**Status: Pass, with observations.**

## 6. Release readiness

| Item | State |
|---|---|
| `VERSION` | **`0.14.0`** — bumped by `WP-Z4`. Single source of truth; `Directory.Build.props` reads it, and no other file carries a version string. |
| Release notes | `docs/releases/v0.14.0/Release Notes.md` — present, summary/features/fixes/validation/limitations/next milestone. |
| Work Package inventory | `docs/releases/v0.14.0/WorkPackages.md` — present, derived from git. |
| Release Register | `v0.14.0` row added, stating **in preparation** — accurate at the time of writing. |
| PR #5 `CI Gate` | **Failed once, on run `33545260056`, to `TD-119` (§4).** Not passed at the time of writing. The gate must be green on the pull request before the merge is proposed; this report neither claims that it is nor authorises proceeding without it. |
| Build/Test Gates on `main` pre-tag (§7.3) | **Not yet performed.** Cannot be, before the merge. |
| `release.yml` against the tagged commit | **Not yet performed.** |
| Product Approval authorisation | **Not sought.** Required per-occasion for the branch push, the merge, and the tag push (§7.5, §7.6). |

**Status: Pass, with observations** — every release-preparation artefact
is complete; the outstanding items are a pull-request gate that has not
yet gone green and two release-time verifications that cannot be performed
from a feature branch. This report explicitly does **not** claim to have
satisfied any of the three.

## 7. Technical debt and limitations, classified

| Item | Classification |
|---|---|
| `TD-116` — Desktop does not launch on Linux/X11 | **Pre-Existing, Unaffected.** Introduced by a deliberate security pin remediating `GHSA-xrw6-gwf8-vvr9`. **Windows and macOS unaffected; Windows is the verified review platform.** Not blocking: the release's own review platform is unaffected, the cause is isolated exactly, and both remedies (reinstating a high-severity advisory, or an Avalonia upgrade) are decisions requiring explicit authorisation rather than an implicit fix. Disclosed in the Release Notes and `PHYSICAL_REVIEW.md` §8. |
| `TD-108` — 60 blocking calls remain | **Pre-Existing, Unaffected.** 36 wait on completed `Task`s; no deadlock possible; residual `O(N²)` pinned by test. |
| `TD-109` — `MainWindow` 1,052 lines | **Pre-Existing, Unaffected.** |
| `TD-115` — three unreachable commands | **Pre-Existing, Unaffected.** Pinned both ways. |
| `TD-118` — async Cockpit conversion | **Disclosed, Non-Blocking.** Deferred with a revisit trigger. |
| `TD-119` — fixed `Task.Delay` synchronisation in `Tempest.Desktop.Tests` | **Disclosed, Non-Blocking.** Test-suite debt, not product behaviour: 61 fixed waits across 16 Desktop test files, third documented recurrence, remedy known (`WP 13.12.9`'s bounded poll, generalised) and **deferred out of this release**. See §4. |
| `AT-10`, `AT-23`, `AT-26` | Decided positions, not debt. |
| Pre-programme / `v0.11.0` retrospective gaps | **Disclosed, Non-Blocking.** |

No finding in any category is classified **Release Blocking**.

## 8. Recommendation

Per §4's priority table: no category is **Not Ready**; **Disclosed,
Non-Blocking** findings exist (§4, `TD-119`; §5, `TD-118`; §5, the
pre-programme and `v0.11.0` retrospective gaps). Row 2 fires. `TD-119`
does not change the verdict: it is tracked test-suite debt with no
identified product-defect path, and §4's gates pass in every execution not
lost to it — locally in both configurations, and on the push-triggered CI
run at the identical SHA. It does, however, add a precondition to the
sequence below: **step 2 requires a green `CI Gate` on PR #5, which does
not exist at the time of writing.**

> ### Recommended verdict: **ACCEPT WITH OBSERVATIONS**

The repository is **ready for the final merge and tag sequence**, and only
now that the release-preparation artefacts are complete. The recommendation
is contingent on the mandatory verifications that remain — a green
`CI Gate` on PR #5, the Build and Test Gates on `main` itself, and
`release.yml` against the tagged commit — in this order:

1. **Push** this branch — explicit, per-occasion Product Approval (§7.5).
2. **Pull request into `main`**, gated on `CI Gate`, merged as a merge
   commit, never squash or rebase (§7.7) — explicit approval (§7.6).
3. **Build and Test Gates green on `main` itself**, immediately pre-tag
   (§7.3).
4. **`scripts/new-release.ps1 -Version 0.14.0`** from `main`, without
   `-Push`.
5. **Push the tag** — a separate, explicit approval (§7.5).
6. **Verify publication independently** (`gh release view`), not from
   workflow success — `TD-42`. Both assets required.
7. Close the Release Register row.

Only Product Approval issues the verdict. This report recommends it; it
does not confer it, and nothing in this release has been merged, tagged,
published or certified at the time of writing.
