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

`TD-57` named a second, structurally identical instance of the same
pattern at `v0.13.x`: six *further* governance registers (Interface,
Exception, DI, Namespace, Platform Services, Validation) had drifted
from source with nothing checking them — because `WP 11.2A`'s own eight
checks never covered them. `WP 16.2A` resolved `TD-57` by re-deriving
all six once; `WP 16.1B` closes the root cause by adding eight further
checks so those six (plus the Technical Debt Register, the Future
Capability Register, and a second Governance Index relationship) cannot
go silently stale the same way again. `D-021` (Proposed, `WP 16.0A`)
names trustworthy governance registers as a `v1.0` readiness
precondition — this tool is the mechanism that keeps them trustworthy
between reviews, not just at one.

## Governance Automation

The tool now audits **sixteen** relationships between a governance
document and the repository content it describes:

**`WP 11.2A` (original eight)** — the ADR Register against `docs/adr/`;
the Academy Index against `docs/academy/`; the Release Register against
real git tags; the Documentation Register's own path references;
`PROJECT_STATUS.md`'s own version and path references; `VERSION` against
the planned release folder; each release folder's own mandatory
documentation; and the Governance Index against all of `docs/
governance/` (broken links and orphaned files).

**`WP 16.1B` (eight more, each derived directly from source, never from
another register)** — the Interface Register against every `public
interface` declared under `src/Tempest.Core/`; the Exception Register
against every class deriving from `Exception` there; the Namespace
Register against every `namespace` declaration across its own declared
scope (`src/Tempest.Core/`, `src/Tempest.App/`, `src/Samples/`,
`src/Validation/`), both by name and, advisory only, by per-namespace
file count; the Technical Debt Register's own summary/total line against
its `TD-nnn` rows actually present; the Future Capability Register's
highest `FCR-nnnn` entry against its own stated range, plus an advisory
staleness check of its "Last Reviewed" date against the newest release
folder's `WorkPackages.md`; the Governance Index's stated ADR count
against `docs/adr/`; the Academy Register's `## 03 Work Packages` table
against every file under `docs/academy/03 Work Packages/` (every file
must have a row); and the Documentation Register's own `docs/academy/03
Work Packages/` row count against the same directory.

It does not audit whether a register's *prose* is accurate (a stale
count buried in a paragraph, for instance) — only whether its
*structural* claims (this file exists, this interface has a row, this
count matches a direct re-derivation) hold. That distinction is
deliberate: structural claims are what a machine can check without
ambiguity; prose accuracy still depends on a human reader, the same as
it always has. Consistent with that scope, `WP 16.1B` deliberately did
not add checks for the Decision Register, Risk Register, or Validation
Register — registers whose content cannot be mechanically re-derived
from source at all, only from prose review — beyond the one staleness-
date pattern the Future Capability Register check demonstrates.

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

**`TD-43`, closed by `WP 16.1B`.** The top-level catch that turns a
crashing check into a `Fail` originally reported every crash under one
hard-coded label, losing which check and which exception actually fired
— empirically reproduced by `WP 11.9.0` against a zero-byte register
file and a link-less index document, both of which failed safely but
undiagnosably. The catch now carries the specific check's own declared
name, the exception's own runtime type, and its message into the `Fail`
result, so a `CI Gate` failure is diagnosable from the report alone,
without log archaeology; `-SummaryPath` behaviour and the script's exit
code are both unchanged.

**Non-vacuousness convention, `WP 11.2A`'s own, reused by `WP 16.1B`.**
Every check — the original eight and the eight `WP 16.1B` adds — is
proven non-vacuous the same way: run clean, break the real governance
document on purpose (a row deleted, a count changed, a range mismatched
— always the tracked file, reverted with `git checkout --` immediately
after, never committed), observe the specific `Fail` or `Warn`, restore,
re-run clean. `docs/releases/v0.16.0/WP16.1B Health-Check Extension
Report.md` records every induced-failure run for the eight new checks
plus `TD-43`'s own fix.

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

**`WP 16.1B`'s own first real run found two further genuine issues**,
neither previously disclosed, neither fixed here (`docs/governance/**`
is outside this Work Package's own declared file ownership — see
`docs/releases/v0.16.0/WP16.1B Health-Check Extension Report.md` for the
full account and the exact remediation each needs):

- **The Namespace Register's own re-derivation grep has a byte-order-mark
  blind spot.** `src/Tempest.Core/Models/ProjectModel.cs` opens with a
  UTF-8 BOM before its `namespace Tempest.Core.Models;` declaration —
  invisible in an editor, but enough to break `grep`'s anchored
  `^namespace` pattern, the register's own documented "Source of Truth"
  command. `WP 16.2A`'s full re-derivation, built on that same grep,
  filed this file under `*(no namespace declared — global namespace)*`
  instead. `.NET`'s own `Get-Content -Raw` strips the BOM automatically,
  so this check catches the real namespace `grep` cannot see.
- **The Technical Debt Register's own summary line is stale by one row
  in both directions** (`39 Resolved` / `72 Open` stated; `40` / `71`
  actually present) — not `WP 16.2A`'s own arithmetic error at the time
  it wrote that line, but a consequence of `v0.16.0`'s branch topology:
  `WP 16.5B` resolved `TD-116` on a sibling branch merged into
  `feature/v0.16.0` *after* `WP 16.2A`'s own branch point, so the row's
  `Status` cell changed in the merged tree without the summary prose
  (authored before that merge existed) ever being re-run against it.

Both are genuine, current governance-document defects, not artifacts of
this check's own construction — each was proven real by temporarily
correcting the stated figure in place and observing the check turn
`Pass`, then restoring the original text via `git checkout --`. Neither
is one of the two disclosed staleness items (the Academy Register header
count and the Documentation Register's own `03 Work Packages/` row
count, both awaiting `WP 16.2B`'s backfill) that this Work Package's own
scope names as a deliberate, temporary `Warn`; both are reported here as
hard findings for the next Work Package to close, and both checks
correctly report `Fail` against the real repository until then.

**Wired into `CI Gate` as a required check, `WP 16.1A`.** Contrary to
this article's own original text above, `governance-health-check` is no
longer merely visible-but-non-blocking: `.github/workflows/ci.yml`'s
`gate` job now depends on `[build-and-test, governance-health-check]`
(`WP 16.1A`, closing the "natural next step" this article originally
named). A consequence disclosed here rather than silently absorbed: the
two currently-`Fail`ing checks above (Namespace Register, Technical Debt
Register) mean `CI Gate` on this branch will not go green until one of
them corrects the underlying register or the other resolves the
concurrent-merge artefact — an intended, not accidental, effect of
`FCR-0005`'s own governing purpose finally having a required, blocking
gate behind it.

## Related Documents

`scripts/governance-healthcheck.ps1`; `.github/workflows/ci.yml`;
`docs/releases/v0.11.0/WP11.2A Governance Health-Check Tool.md` (the
original eight checks' full specification, design rationale, and
verification evidence); `docs/releases/v0.16.0/WP16.1B Health-Check
Extension Report.md` (the eight `WP 16.1B` checks' own specification,
induced-failure evidence, and the two genuine findings disclosed above);
`docs/governance/Future Capability Register.md` (`FCR-0005`);
`docs/governance/Quality/Technical Debt Register.md` (`TD-43`, `TD-57`);
`docs/releases/v0.16.0/WP16.0A v0.16.0 Scope Decision.md` (`D-021`);
`03-governance-registers.md` (why the register suite itself exists);
`04-continuous-integration.md` (the CI pipeline this tool's own job runs
inside).
