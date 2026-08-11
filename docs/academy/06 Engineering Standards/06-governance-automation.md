# Engineering Standard: Governance Automation

## Purpose

`FCR-0005` (Governance Register Health-Check Tooling) was first
identified at `v0.7.0`'s own strategic-roadmap review and was
independently rediscovered **seven** times across every release since —
each time a register, index, or count had gone stale since the last
Work Package that actually touched its subject, and each time the only
thing that caught it was a human's manual audit, usually during a
release-closing review. `WP 11.2A` closes that gap: `scripts/governance-
healthcheck.ps1` is TempestOS's first automated check for exactly this
class of drift, run on every push, pull request, and manual dispatch.

## Governance Automation

The tool audits eight relationships between a governance document and
the repository content it describes — the ADR Register against
`docs/adr/`, the Academy Index against `docs/academy/`, the Release
Register against real git tags, the Documentation Register's own path
references, `PROJECT_STATUS.md`'s own version and path references,
`VERSION` against the planned release folder, each release folder's own
mandatory documentation, and the Governance Index against all of `docs/
governance/`. It does not audit whether a register's *prose* is
accurate (a stale count buried in a paragraph, for instance) — only
whether its *structural* claims (this file exists, this ADR has a row,
this link resolves) hold. That distinction is deliberate: structural
claims are what a machine can check without ambiguity; prose accuracy
still depends on a human reader, the same as it always has.

**Governance automation reports; it does not correct.** No check, and no
invocation flag, offers a fix. A register found stale is a Work
Package's own job to update, informed by what the tool found — the tool
narrows *where* to look, it never decides what the correct content
should be.

## Repository Validation

Every check is read-only and has no dependency on any production
project — the script never builds, loads, or references `Tempest.Core`/
`Tempest.App`/`Tempest.Desktop`, and never writes to the repository it
scans (an optional `-SummaryPath` refuses to resolve inside `-RepoRoot`).
`-RepoRoot` is a real, overridable parameter, not a hard-coded
assumption — the tool was validated against a small, deliberately broken
fixture built in an isolated temporary directory, never against a
mutated copy of the real repository, proving both the positive case
(the live repository's own real state) and the negative case (a fixture
with a genuinely absent ADR, a mismatched `VERSION`, a missing register)
without ever risking the tracked tree.

A crashing check is treated as a `Fail`, never a silent skip — three
genuine defects were found this way during the tool's own development
(a multi-line path-extraction crash, a git-unavailable crash, and a
PowerShell single-item-count sharp edge), each found by actually running
the tool against real and deliberately-broken input, not by inspection
alone. See `docs/releases/v0.11.0/WP11.2A Governance Health-Check
Tool.md`, "Evidence & Findings," for the full account, including two
genuine, real governance findings the fixed tool then surfaced on its
very first live run.

## Documentation Health Checks

The tool's own first real run found two genuine issues that had gone
undisclosed until now: `Academy Index.md`'s "Work Package Walkthroughs"
section stops at `WP 7.3A`, silently missing roughly fifty real
retrospectives shipped since; and three directories this project's own
Documentation Register describes as existing (`docs/roadmap/`,
`docs/diagrams/`, `docs/releases/v0.2.0/`, plus `src/Plugins/` found via
the same pattern in `PROJECT_STATUS.md`) are not actually present in the
working tree, because git cannot track an empty directory. Neither was
fixed by `WP 11.2A` — closing the first is a substantial undertaking in
its own right; the tag was never moved and the second finding is
structural, not something this Work Package's own scope covers fixing.
Both are disclosed in full, not silently absorbed, exactly the outcome
`FCR-0005` was raised to produce.

**Wired into CI, deliberately not yet a required check.**
`.github/workflows/ci.yml`'s `governance-health-check` job runs after
the build/test matrix completes and publishes its report to every run's
Job Summary — but `CI Gate` (the job branch-protection rules should
require) still depends only on the build/test matrix. Making the health
check itself a required, blocking gate today would fail every future
push and pull request on a backlog this Work Package did not clear.
Promoting it once that backlog is closed is named as the natural next
step, not left unstated.

## Related Documents

`scripts/governance-healthcheck.ps1`; `.github/workflows/ci.yml`;
`docs/releases/v0.11.0/WP11.2A Governance Health-Check Tool.md` (the
full specification, design rationale, and verification evidence this
article summarises); `docs/governance/Future Capability Register.md`
(`FCR-0005`); `03-governance-registers.md` (why the register suite
itself exists); `04-continuous-integration.md` (the CI pipeline this
tool's own job runs inside).
