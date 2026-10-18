# TempestOS v0.25.0 — Release Notes

**Status: release candidate (`v0.25.0-rc.1`).** Waiting on the Product
Owner's walk of `docs/releases/v0.25.0/PO Test Runbook.md` and its go/no-go
gates. The last published release is `v0.22.0`. `v0.23.0` and `v0.24.0`
were drafted and partly reviewed but never tagged, so this is the first
downloadable build that contains them.

## Summary

`v0.25.0` gathers everything since `v0.22.0` into one installable build:

- **`v0.23.0`** — the Product Owner's runbook fixes from 22 Sep and 1 Oct:
  - Customers & Suppliers;
  - project-centric numbering and project folders;
  - the second-person sign-off switch;
  - the read-only review queue export;
  - 19 calculators with live diagrams;
  - day-one reference libraries.

  See `docs/releases/v0.23.0/Release Notes.md`.
- **`v0.24.0`** — Xero:
  - drafts and copies of quotes, invoices, purchase orders and expenses;
  - company details, VAT rates and account codes read from Xero;
  - the forward cash picture;
  - the Demo Company guard.

  Every review-board fix the `v0.24.0` runbook marked *(needs …)*
  (`M1`–`M9`, `m1`–`m21`) is merged in this build. See
  `docs/releases/v0.24.0/Release Notes.md`.
- **New in `v0.25.0`: simpler Engineering navigation.** This acts on the
  Product Owner's `v0.24.0` finding that the nested discipline tabs inside
  the ribbon were hard to find things in and hard to get out of. The
  audit, screenshots and options are in
  `docs/reviews/engineering-navigation-2026-10/README.md`.

## What changed in v0.25.0

- **Disciplines are picked from the left.**
  - Engineering's tree has a new **Disciplines** node, expanded by default, listing Calculations, Documents, Evidence, Manufacturing, Mechanical, Requirements and Verification.
  - Picking one shows that discipline's commands, the Project Explorer, Documents/Cockpit and Properties **beside the tree, which stays on screen**.
  - **Dashboard**, **Tasks** or any other node takes you back out in one click.
  - The old *Modules → Mechanical* node is gone. It used to swap the whole tree away.
- **The Structure tab has the same list.** Inside a project, the Structure tab shows a narrow **Disciplines** list to the left of the engineering surface.
- **The ribbon is a one-row command bar.**
  - The discipline tab strip is gone.
  - The bar is headed *"<Discipline> commands"* and shows the **Create** tile, then **Organize (n) ▾**, **Lifecycle (n) ▾** and **Actions (n) ▾** selectors. One group shows at a time, so the bar never wraps into a tall block or scrolls off the edge.
  - A **Hide commands / Show commands** chevron replaces double-clicking a tab to minimise the bar (`TD-70`), so a minimised bar can always be restored.
  - Every command keeps its Id, grouping, enablement, prompts and Ctrl+K entry. Only where you choose the discipline moved.
- **The rail always shows where you are.**
  - Standalone engineering highlights **Engineering**.
  - Engineering inside a project highlights **Projects**.
  - Before this, the engineering surface highlighted nothing.
- **More room for the Explorer.** The Engineering tree narrows while a discipline is shown beside it.

Code: `EngineeringDisciplines.cs`, `RibbonView.cs` (`ShowCategory`,
`CurrentCategoryChanged`, the group selectors), `EngineeringAreaView.cs`,
`ProjectWorkspaceView.cs`, `GlobalNavigationRail.RefreshSelection`,
`MainWindow.ResolveEngineeringSurfaceHost`, `MainWindowComposer.Wire.cs`.
Tests: `EngineeringDisciplineNavigationTests.cs`, and
`RibbonViewTests.NarrowWindow_CommandBar_StaysOneShortRow_AndEachSelectorOpensItsGroup`,
which replaces `WP-Z4`'s two scrollbar tests because nothing scrolls any more.

## Also in this build

- CI's `actions/download-artifact` is bumped to 8.0.2 (Dependabot, PR #18).

## Not in this build, deliberately

- **`vpk` 1.2.161 (Dependabot PR #19).** The app references `Velopack`
  1.2.0, and `vpk` is pinned with `rollForward: false`. Moving the packager
  alone the night before an install test risks a mismatched installer. It
  should move together with the `Velopack` package in its own change.
- **Review-decision intake (`claude/review-decisions-intake`, 2 Oct).** It
  is switched off by default, and its `ADR-0162` number now clashes with
  the Xero ADR. It needs renumbering and its own review.
- **The 25 Sep outstanding-actions register
  (`claude/archive-review-actions-9zoo8e`).** It is superseded by the
  1 Oct register, `docs/reviews/RC PO Feedback Actions (2026-10-01).md`.

## Known limitations

These are carried from `v0.23.0` and `v0.24.0` unchanged:

- **Do not turn on *Allow live organisation*** until the Demo Company walk
  passes. `M1`, `M2` and `M7` are merged, so the runbook's `XG7`
  precondition is met, but going live is still the Product Owner's own
  call (gate G6 in the runbook).
- **Lost creates and Xero's key window.** A purchase order or bill whose
  create answer is lost for more than 5 minutes reads **Can't tell**, and
  someone must check Xero.
- **Issued evidence sheets cannot be opened from the shell** while Reports
  is withdrawn (`v0.23.0`).
- **First-run data-location dialog (`C-01`).** It can hang. Install with
  `scripts/install-test-build.ps1`, which records the data folder first,
  or launch the downloaded build from a shortcut with `--persistence-root`
  (runbook step A2).

## Gate

| Check | Result |
|---|---|
| `dotnet build src/TempestOS.slnx -p:TreatWarningsAsErrors=true` (Linux) | *filled at tag time* |
| Full solution tests (Linux) | *filled at tag time* |
| CI Gate on the tagged commit (Windows) | required by `release.yml` before anything is published |
| Real-shell journey (`scripts/run-realshell-linux.sh`) | *filled at tag time* |
| Remote stress runs (`docs/reviews/engineering-navigation-2026-10/stress/`) | *filled at tag time* |
| Product Owner runbook digest | *Product Owner* |
