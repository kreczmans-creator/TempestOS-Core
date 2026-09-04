# v0.15.0 — Work Packages

## Status

**In preparation.** Not yet merged to `main` (this document lives on
`feature/v0.15.0-release-prep`), not tagged, not published. `WP 15.1A`
is complete on this branch; no further Work Package is scoped or
approved yet.

## Scope of This Document

Every Work Package in the `v0.14.0..main` range at `a417ade` — 20
commits, none carrying a real Work Package number, `WorkPackages.md`
row, TD entry, or Academy retrospective before `WP 15.1A` retroactively
supplied one — **derived from `git log`/`git diff`, not from any prior
summary**, per this project's own established `WorkPackages.md`
convention (`WP 12.2A`'s precedent).

Two facts this document exists to state plainly, mirroring `v0.14.0`'s
own opening:

1. **The five Work Packages below (`WP 15.0A`–`D`, `WP 11.5A`) are a
   backfill, not a plan.** All five commits ranges were already on
   `main` before this document existed. `WP 15.1A` is the first Work
   Package in this release actually planned and numbered before it
   started.
2. **`b755685` ("WP-Z4 Stage 24: close the v0.14.0 release record") is
   the 20th commit and is not a `WP 15.x` item.** Found missing from this
   table by `WP 15.1B`'s own independent readiness review — `WP 15.1A`'s
   original accounting covered only 19 of the range's 20 commits. `git
   log v0.14.0..main` includes it because the `v0.14.0` tag points to
   `026ed7c` (the merge commit) while `b755685` landed on `main`
   afterwards, before the tag object itself was created (tagger date
   2026-09-03 08:56:07 UTC; `b755685` committed 2026-09-03 09:32:18
   UTC — the tag was cut against an earlier commit than `main`'s tip at
   the time, not against whatever `HEAD` was). Its own content —
   correcting `v0.14.0`'s Release Register row, `PROJECT_STATUS.md`,
   `TD-119`, and two `v0.14.0` release documents from "in preparation" to
   "released" — is squarely `v0.14.0`'s own release-closure work, not new
   `v0.15.0` material, and does not need a `v0.15.0`-scoped retrospective.
   It is recorded here, not backfilled into `v0.14.0`'s own
   `WorkPackages.md`, so that this document's own "20 commits" claim is
   honest and this range is completely accounted for. Zero `src/`/
   `tests/` files changed. **Documentation only; not fixed retroactively
   by `WP 15.1B`, corrected in place** — see `docs/releases/v0.15.0/
   WP15.1B v0.15.0 Release Readiness Report.md`.
3. **`WP 11.5A` predates this release's own numbering scheme** and keeps
   its own, out-of-sequence number (see its own report,
   `docs/releases/v0.11.0/WP11.5A Governance Currency & Documentation
   Integrity.md`, for why it is filed under `v0.11.0/` rather than here)
   — listed here only because its commits fall inside this release's own
   `v0.14.0..main` range.

## Work Packages

| Work Package | Commits | Type | Status |
|---|---|---|---|
| `WP 15.0A` | Desktop Shell Brand Recovery & Windows Startup Crash Fix — recovers `Tempest.Companion`'s brand chrome into `Tempest.Desktop` (theme, `ChromeStyles`, icon set, brand assets); fixes a real Windows startup crash (`Dispatcher.VerifyAccess` thrown off the UI thread by `WorkspaceLayoutController.RestoreAsync`'s continuation resuming on a thread-pool thread). Discloses an `FCR-0092` citation this repository's own Future Capability Register does not resolve (`FCR-0001`–`FCR-0088` only) — not fixed, likely a `Tempest.Companion`-scoped FCR cited without translation. 59 files, +3,863/−652. Retroactively numbered and retrospected by `WP 15.1A`. See `docs/academy/03 Work Packages/WP15.0A-desktop-shell-brand-recovery-and-windows-startup-crash-fix.md`. | Implementation | **Complete** |
| `WP 15.0B` | Desktop Productisation Phase 1 — closes navigation dead-ends, placeholder/static data, and chrome inconsistencies found by driving the real running application (Object Editor branding, project-chip navigation, Explorer selection/count persistence, a real Cockpit KPI card, auto-open on project creation, severity-coloured output, `InputGesture` wiring, `DigitalThreadGraphView` chrome, scroll-to-new-item). **Disclosed, not repeated as fact**: this Work Package's own three commits (`257bac7`/`ee15986`/`0151f35`) embed test pass-counts (e.g. "373/386", "3056/3088") obtained via a since-corrected, flawed local SDK-substitution technique (deleting `global.json` rather than editing its SDK version field in place) that manufactures false failures — see `WP 15.1A`'s own Release Preparation Report for the corrected baseline. 25 files, +935/−63. Retroactively numbered and retrospected by `WP 15.1A`. See `docs/academy/03 Work Packages/WP15.0B-desktop-productisation-phase-1.md`. | Implementation | **Complete** |
| `WP 15.0C` | Desktop Productisation Phase 2 — three genuinely file-independent fixes dispatched as parallel, isolated-worktree background agents (Ribbon/Property Inspector button hierarchy and read-only signalling; dialog framework and Command Palette keyboard workflow; `DeclaredCapabilityView` icon fix and `DocumentAreaView` tab-close context loss), each reviewed and merged sequentially by the lead session, never by an agent itself. 13 files, +742/−21 (plus `cab540b`, `.gitignore` for `.claude/` worktree state, enabling the parallel dispatch cleanly). Retroactively numbered and retrospected by `WP 15.1A`. See `docs/academy/03 Work Packages/WP15.0C-desktop-productisation-phase-2.md`. | Implementation | **Complete** |
| `WP 15.0D` | Ribbon Responsive Scrollbar Fix — root-causes the Ribbon's horizontal scrollbar never rendering at compact widths (~1000px) to a `ScrollViewer`'s `Auto` visibility never growing its own container to make room for itself; fixes via a content-driven `MinHeight` reservation (`DesignTokens.SpaceXl`), verified via real Xvfb interaction, both themes, both target widths. 2 files, +151/−0 (both test-only additions plus the one production fix). Retroactively numbered and retrospected by `WP 15.1A`. See `docs/academy/03 Work Packages/WP15.0D-ribbon-responsive-scrollbar-fix.md`. | Implementation | **Complete** |
| `WP 11.5A` | Governance Currency & Documentation Integrity — audits `WP 11.2A`'s own disclosed gaps, finds them already remediated (`WP 12.9.1`/`WP 12.9.2`, `v0.12.0`), fabricates no redundant fix; corrects genuinely current `Governance Index.md`/`Documentation Register.md` drift instead; discloses `main`'s then-undocumented divergence from `v0.14.0`, directly commissioning `WP 15.1A`. Out-of-sequence number, own report filed under `docs/releases/v0.11.0/`. 4 files, +376/−9. See `docs/academy/03 Work Packages/WP11.5A-governance-currency-and-documentation-integrity.md`. | Documentation/Governance | **Complete** |
| `WP 15.1A` | `v0.15.0` Release Preparation & Governance Closure — this document; the `docs/releases/v0.15.0/` structure; `VERSION` bumped `0.14.0` → `0.15.0`; TD/FCR/AT reconciliation; the FCR Register brought current; the five Academy retrospectives above; `PROJECT_STATUS.md`, `Documentation Register.md`, `Governance Index.md` and `Release Register.md` updated; governance health check, Debug/Release builds and the full test suite re-verified. On `feature/v0.15.0-release-prep`, not merged to `main`. See `docs/releases/v0.15.0/WP15.1A v0.15.0 Release Preparation Report.md`. | Release Preparation | **Complete on branch — not merged, not tagged, not published** |

## Related Documents

`docs/releases/v0.15.0/WP15.1A v0.15.0 Release Preparation Report.md`
(this release's own full evidence account); `docs/releases/v0.14.0/
WorkPackages.md` (the immediately preceding release); `docs/releases/
v0.11.0/WP11.5A Governance Currency & Documentation Integrity.md`;
`docs/governance/Quality/Technical Debt Register.md`;
`docs/governance/Future Capability Register.md`; `PROJECT_STATUS.md`.
