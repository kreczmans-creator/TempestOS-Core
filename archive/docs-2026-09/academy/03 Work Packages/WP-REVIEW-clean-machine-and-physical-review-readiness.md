# WP-REVIEW — Clean-Machine and Physical-Review Readiness

## 1. Introduction

`WP-REVIEW` (`a13d1c3`) established that TempestOS can be built, tested and
reviewed on a machine that has nothing but the pinned SDK — by doing it,
from a genuine clean clone, rather than by reasoning about it. It produced
`PHYSICAL_REVIEW.md`, corrected a stale `README.md`, and found one real
blocker, `TD-116`, which it deliberately did not fix.

## 2. Purpose

To make the first serious physical review of the product reproducible: a
reviewer should not have to discover the environment, the commands, where
the data lands, or what is known not to work.

## 3. Background

Every build and test figure the project reports had been produced on a
development machine that had accumulated state over many releases. Nothing
had verified that a fresh checkout with an isolated package cache produces
the same result — which is the only version of that claim a reviewer can
act on.

## 4. The Problem

Three distinct risks, only one of which turned out to be real:

- The build might depend on undocumented machine state (a private feed,
  a workload, an environment variable, a machine-specific path).
- First-run behaviour might not be deterministic, or might ship sample data.
- The application might not launch.

The first two were disproved by direct test. The third was true, on one
platform, for a reason nobody had noticed.

## 5. The Design

Audit, then an actual clean-environment test, then a smoke test. Verified
from a fresh checkout with an isolated NuGet package cache and nothing but
the pinned SDK on `PATH`:

| Step | Result |
|---|---|
| restore | nuget.org only, no `NuGet.config`, no private feed, ~9s |
| build Debug | 0 warnings, 0 errors, `TreatWarningsAsErrors=true` |
| build Release | 0 warnings, 0 errors, `TreatWarningsAsErrors=true` |
| test Debug | 3,069 + 353 passed |
| test Release | 3,069 + 353 passed |
| governance | 7 passed, 1 pre-existing informational warning, 0 failed |
| launch | see `TD-116` |

No workloads, no external services, no database, no secrets, no licence, no
environment variables, no machine-specific paths, no first-run
initialisation, and no sample or demo data in the shipped application.
First-run state is deterministic and genuinely empty.

Two changes, both required for a review that does not depend on
undocumented machine state:

**`PHYSICAL_REVIEW.md`** (new) — minimum environment, exact commands,
launch procedure, data locations, reset path, a 10–15 minute smoke test
**written only against behaviour that exists today**, and the known
limitations.

**`README.md`** — its `src/` tree still described `Tempest.Core` as holding
the pre-module-pipeline bootstrap and project code. `WP-C` had deleted all
of it, so a reviewer would have gone looking for directories that no longer
exist.

## 6. Alternatives Considered

**Fix `TD-116`.** Explicitly rejected, and the reasoning is the substance
of this work package's judgement. The remedies are reinstating a known
high-severity advisory (`GHSA-xrw6-gwf8-vvr9`) or an Avalonia upgrade, and
**neither should be chosen implicitly** as a side effect of a documentation
work package. It is recorded so somebody can choose.

**Install tooling to make the clean test pass.** Rejected as
self-defeating: the point of the test is what the machine does *not* need.

**Write the smoke test against the product as intended.** Rejected — it is
written only against behaviour that exists today, because a smoke test that
describes aspirations fails for the wrong reason.

## 7. Why This Solution Was Chosen

Because "it builds clean" is a claim about an environment, and the only
honest way to make it is in the environment concerned. The clean clone
turned three plausible risks into one confirmed defect and two disproved
worries — which is a better outcome than a review that assumed all three.

## 8. Architectural Principles

The project's standing preference for verifying from source rather than
carrying a prior figure forward, applied to the environment itself. And the
discipline that a defect found outside a work package's remit is
*documented*, not *absorbed* — `TD-116` is a dependency trade-off with a
security dimension, which is somebody's decision to make, not a tidy-up.

## 9. Benefits

A reviewer has exact commands, known data locations, a reset path and a
list of what is known not to work. The clean-machine claim is evidence
rather than assertion. `README.md` no longer sends readers to deleted
directories. And `TD-116` was found *before* a reviewer hit it, which is
the entire value of doing this ahead of the review rather than during it.

## 10. Trade-offs

The product ships with a known platform limitation. That is the accepted
cost of the security pin, and it is bounded: Windows and macOS never
initialise the affected path, and Windows is the verified review platform.

`PHYSICAL_REVIEW.md` documents current behaviour, so it will need
maintaining as the product changes — a document that describes what exists
always does.

## 11. Common Mistakes

**Testing the clean case on a dirty machine.** An isolated package cache
and a bare `PATH` are the test; without them it proves nothing.

**Assuming a green test suite means the application runs.** The Desktop
suite passes on Linux precisely because `Avalonia.Headless` does not
initialise X11 either — which is why nothing in the repository had noticed
`TD-116`.

**Fixing a defect because you found it.** The security pin is deliberate
and documented; reverting it in a readiness work package would have
silently reinstated a high-severity advisory.

## 12. Future Evolution

`TD-116` stays open and platform-conditional, with `PHYSICAL_REVIEW.md` §8
carrying the practical account. Its resolution is an explicit choice
between reinstating the advisory and upgrading Avalonia. Two further facts
are documented rather than changed and may deserve attention later:
persistence resolves a relative path against the process working directory,
so where the application is launched from decides where its data lands; and
a loopback REST listener binds `127.0.0.1:5080` at startup, whose failure
is isolated and non-fatal.

## 13. Key Takeaways

- A clean-machine claim is only worth making from a clean machine.
- Isolating a defect exactly — reverting the pin in an uncommitted working
  copy made the application launch under Xvfb — is what turns "it does not
  start" into a decision somebody can take.
- A test suite that passes on a platform the application cannot launch on
  is telling you something about the test harness, not the product.
- Document the trade-off; do not take it on the author's behalf.

## Related Documents

- `PHYSICAL_REVIEW.md` — the review guide this produced
- `docs/governance/Quality/Technical Debt Register.md` — `TD-116`
- `README.md` — corrected `src/` tree
- Commit `a13d1c3`
