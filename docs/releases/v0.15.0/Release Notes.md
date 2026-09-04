# TempestOS v0.15.0 — Release Notes

**Status: released, published.** Merged to `main` (`350922d`, with one
follow-up commit `a35365a` adding this document), tagged `v0.15.0`
(pointing to `a35365a`), and published as GitHub Release `382812261`
on 2026-09-04T15:00:00Z with both required assets. Certification remains
Product Approval's own act — publication is not certification.

---

## Summary

**v0.15.0 makes an already-shipped body of work honestly represented in
governance.** Twenty commits landed on `main` after the `v0.14.0` tag —
a Desktop UI/UX brand recovery, a real Windows startup crash fix, two
phases of Desktop productisation, and a Ribbon responsiveness fix —
before any of it had a Work Package number, a `WorkPackages.md` row, a
Technical Debt entry, or an Academy retrospective. `VERSION` itself
still read `0.14.0` while `main`'s own tree had moved well past what
that tag describes.

This release does not add a new capability. It closes that gap: every
commit in the range is now numbered, retrospected, and accounted for;
two real, already-fixed defects (the crash, the scrollbar) are now
tracked in the Technical Debt Register; the Future Capability Register
was reviewed for the first time since `v0.13.0`; and a second,
independent review re-verified all of it before recommending release.

## What shipped (already on `main` since `v0.14.0`, now formally recorded)

**Desktop brand recovery.** `Tempest.Desktop`'s theme, `ChromeStyles`,
icon set and brand assets recovered from `Tempest.Companion`'s own,
already-verified alignment work.

**A real Windows startup crash, fixed.** `WorkspaceLayoutController.
RestoreAsync`'s continuation could resume off the UI thread and throw
`Dispatcher.VerifyAccess` unhandled, killing the process moments after
the window appeared. Now tracked as `TD-121`, Resolved.

**Desktop Productisation, two phases.** Navigation dead-ends, static
placeholder data, and chrome inconsistencies found by driving the real
running application — project-chip navigation, Explorer selection and
child-count persistence, a real Cockpit KPI card, severity-coloured
output, `InputGesture` wiring, dialog/Command Palette keyboard workflow
fixes, and two independent view-level defects (`DeclaredCapabilityView`
icon, `DocumentAreaView` tab-close context).

**The Ribbon's overflow affordance, fixed.** A horizontal scrollbar that
never rendered at compact widths even when content genuinely overflowed
— root-caused to `Auto` visibility never growing its own container to
make room for itself. Now tracked as `TD-122`, Resolved.

**Governance currency restored.** `WP 11.5A` found `Governance Index.md`
and `Documentation Register.md` had drifted (stale counts, a stale
`ADR-0095` cross-reference, missing release rows); `WP 15.1A` formalised
the range above as `WP 15.0A`–`D`, bumped `VERSION`, and reviewed the
Future Capability Register for the first time since `v0.13.0`; `WP 15.1B`
independently re-verified the whole branch and found one further gap (a
missing commit, `b755685`, in the range's own accounting), corrected in
place.

## Architecture and governance

No ADR. No architecture document changed. No production or test code
was touched by the governance/documentation Work Packages (`WP 11.5A`,
`WP 15.1A`, `WP 15.1B`) — only the Desktop UI Work Packages above
(`WP 15.0A`–`D`) contain real `src/`/`tests/` changes, all of which were
already on `main` before this release formalised them.

## Validation status

| Gate | Result |
|---|---|
| Core tests, Debug / Release | **3,088 / 3,088** — 0 failures, both configurations |
| Desktop tests, Debug / Release | **408 / 408** — 0 failures, both configurations |
| Total | **3,496** (from 3,460 at `v0.14.0`) |
| Build, Debug and Release | 0 warnings, 0 errors |
| Governance health check | 7 passed, 1 pre-existing informational warning, 0 failed |
| ADR Register vs `docs/adr/` | 119 / 119, exact match |
| Working tree | Clean |

Re-verified independently twice: once by `WP 15.1A` on
`feature/v0.15.0-release-prep`, and again by `WP 15.1B`'s own review
(all four test/configuration combinations run fresh, not carried
forward). Real GitHub-hosted CI confirmed green for the feature
branch's own final commit (`d2f1fae`, run `33861940494`), for `main`
itself at `a35365a` (run `33864515369` — `Build & Test (Debug)`,
`Build & Test (Release)`, `CI Gate`, `Governance Health Check` all
`success`), and again on the tag push itself (`ci.yml` run
`33885783239`). `release.yml` (run `33885783286`) then independently
rebuilt and retested Release a further time before publishing the
GitHub Release.

## Accepted technical debt and known limitations

Unchanged from `v0.14.0`, all pre-existing and non-blocking: `TD-118`
(Cockpit async conversion, deferred by decision), `TD-109` (remaining
`MainWindow` shell services, partial), `TD-116` (Desktop does not launch
on Linux/X11, platform-conditional), `TD-120` (Desktop test suite does
not delete its persistence roots, test-cleanup debt). Two items newly
tracked by this release, both already Resolved: `TD-121` (the Windows
startup crash), `TD-122` (the Ribbon scrollbar).

**New, disclosed by this release's own governance review, non-blocking**:
`v0.11.0`'s own ten Work Packages still have no Academy retrospectives
(already known via `WP-Z3`); `Academy Register.md`'s deeper per-article
annotations for this release's own five new retrospectives were not
backfilled (`Academy Index.md`, the artifact the automated health check
verifies, was updated and is orphan-free); an `FCR-0092` citation in
`WP 15.0A`'s own commit message and in `docs/design/Tempest Engineering
Design System Reference.md` does not resolve in this repository's own
Future Capability Register (`FCR-0001`–`FCR-0088`) — likely a
`Tempest.Companion`-scoped number cited without translation, not
independently confirmed.

## Next milestone

Not yet scoped. `v0.15.0` is itself a governance-closure release; the
next Work Package is expected to resume feature work against
`v0.14.0`'s own still-open items (`TD-118`, `TD-109`) or a newly
commissioned scope, per `WP11.0B Architecture Roadmap.md`'s own
established "each release scopes the next" convention.
