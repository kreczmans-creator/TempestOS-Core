# TempestOS v0.23.0 — Release Notes

**Status: draft.** Awaiting the Product Owner's verdict. This draft lives
on `claude/focused-dirac-k0qilf`, on top of `main` (`v0.22.0`). The
Product Owner's acceptance runbook for this build is the "TempestOS
v0.23.0 Runbook" page (80 steps, sections A–J).

## Summary

`v0.23.0` acts on the Product Owner's RC runbook results (22 Sep) and
the follow-up messages of 2026-10-01. The full action register,
including items waiting on a decision, is
`docs/reviews/RC PO Feedback Actions (2026-10-01).md`.

## What changed

- **Home right rail entries are working links.** Continue entries open
  their project. Recent, Favourite and Recently changed now switch to
  Engineering before opening the object. Before, they opened it into the
  hidden Engineering document area, and the click appeared to do nothing.
- **Every module's landing page is called "Dashboard".** "Dashboard +
  Reports" (Projects, Engineering) and "Dashboard & Reports" (Business)
  are renamed, and Home's page title now reads Dashboard.
- **Reports is withdrawn from the shell for now.** The issued-sheet and
  project-document list is no longer shown under Engineering's
  dashboard. `ReportsView` is kept so it can return once real client use
  shows what reports need to be. Until then, an issued evidence sheet
  cannot be opened or exported from the shell.
- **Ctrl+K and the header search box (runbook D9).** Ctrl+K now opens
  the Command Palette even when the focused control has already handled
  the key. Clicking the header search box now opens the palette with the
  cursor in its query. Before, the box sat inert until Enter was pressed.
- **Fixed-list choices are dropdowns.** Every command parameter with a
  fixed option list is now picked from a dropdown instead of typed. This
  covers Create Document's Kind and every other `Choice`/`EnumChoice`
  parameter, in the Ribbon and the Command Palette alike.
- **Xero sign-in will be accepted for a new Xero app.** Xero apps
  created on or after 2 March 2026 cannot be granted the broad
  `accounting.transactions` scope, so the consent would have been
  refused. TempestOS now requests `accounting.invoices`,
  `accounting.contacts.read` and `accounting.reports.banksummary.read`,
  one scope per endpoint it calls. The Bank Summary reading also lacked
  a reports scope until now.
- **Review queue on the dashboard and phone (read-only).** The
  Dashboard Export writes a fifth file, `reviews.json` (`ADR-0157`):
  every live item awaiting review, oldest first, with its project, how
  long it has waited and who submitted it. It covers Documents,
  Drawings and CAD Models, as the August Companion app did, plus
  Calculations and Verification Activities in review, checked Evidence
  awaiting issue, and Reviewed Requirements. Tempest-Dashboard shows it
  on the Engineering view and counts it on Home. There is no approve
  action.

- **People, customers and suppliers are separate lists (PO decisions
  2026-10-01).** Internal staff (Business → Staff) keep contact
  details only, now with a phone number. Business → **Customers &
  Suppliers** holds organisations with full company details, a
  Customer/Supplier/Both type and their own contacts. New Project's client
  and a purchase order's supplier are dropdowns from that list.
- **Project-centric numbering (`ADR-0156`).** An organisation has a
  unique 5-character customer code and a project a unique 6-character
  project reference — letters A–Z and digits 0–9 — both suggested from the
  name and editable. A new project with a coded client is identified
  `CUSTOMER-PROJECTREF` (for example `ACME1-BRIDG1`); its documents are
  `CUSTOMER-PROJECTREF-DOCTYPE-NNN` (Q, CO, PO, INV, DOC, DWG, CAD; CALC
  reserved), counted per project per type from 001. Existing projects and
  records keep their numbers.
- **Project folders on disk.** Opening or creating a project finds or
  creates `D:\01 Projects\<customer code>\<project identifier>\` (for
  example `ACME1\ACME1-BRIDG1`; configurable as `Projects:FolderRoot`),
  and quote export starts in that folder. A customer with no code is
  filed under its name. The standard subfolder set is not yet defined
  (`V1-BLOCKER-01`).
- **Documents use the light-ground logo.** Every generated PDF carries the
  design system's ink lockup (dark text, transparent ground) instead of
  the white-on-navy box. The timesheet heading shows the person's name,
  never a raw Windows SID.
- **Day-one reference libraries.** Materials 6 → 77, fasteners 7 → 131,
  bearings 2 → 39, standards 14 → 29, every value cited to a recognised
  public source (`docs/governance/Data/Seed Data Sources Register.md`).
  Shipped records are released at seed through the normal review path, so
  calculators work on a fresh install; `ReferenceData:ReleaseAtSeed=false`
  restores Draft seeding. 60 of the 89 materials in the recovered
  knowledge foundation are seeded; the register lists the rest.
- **Three calculators recovered from v0.16.0 (16 → 19).** Tolerance
  stack-up (worst case, plus RSS, Cpk and ppm when a statistical basis is
  stated; asymmetric limits), thermal resistance chain (any number of
  stages, heatsink budget) and plane-wall heat transfer (conduction with
  surface films), each with textbook worked examples (Shigley, Çengel).
- **Recovered work archived.** Five branches deleted unmerged before this
  release are kept as git bundles under `archive/recovered-branches/`, and
  the 2026-09-05 knowledge foundation under
  `archive/knowledge-foundation-2026-09-05/`.

- **Runbook round 1 fixes (PO comments, 2026-10-01).**
  - Staff moved to Business → **Staff** (B1).
  - Rate cards moved to Business → **Rate cards**.
  - A reference record is rev 1 until its content is revised; verify,
    check, validate and release no longer bump the revision (B2).
  - Customer codes are 5 characters and project references 6, letters or
    digits (`ACME1-BRIDG1`) (C1).
  - Quotes: **Save draft**, **Submit for review**, approval issues **R1**
    (then R2 after an edit) — by a different person when second-person
    sign-off is on (`ADR-0161`); Send only after approval;
    each line's rate is picked from the project's pinned rate card, or
    **Fixed** (C3).
  - Project folders are `<root>\ACME1\ACME1-BRIDG1` (C6).
  - Library lists show title and status only, grouped under collapsible
    family headings (F1).
  - Timesheet task is a dropdown of the project's deliverables (G1); a
    Settings → Timesheets export folder, default
    `D:\11 Business Admin\02 Timesheets` (G2).
- **Second-person sign-off is a Settings switch, off by default (PO
  decision 2026-10-01, `ADR-0161`).** Settings → **Sign-off** →
  *Second-person sign-off*. Off (the default, for a one-person
  consultancy): you may approve your own quote and check your own
  evidence. Somebody must still be signed in, and the quote revision,
  evidence check and audit row record it as a self-approval. On: a
  different person must approve or check, exactly as before. Every change
  of the switch is audited (who, when, old → new). The Quote tab shows
  which applies.
- **CI runs once per commit**, with one exception. Push builds run for
  `main`, release branches and tags only; other branches are gated by
  their pull request, or run by hand. A push to a release branch with an
  open pull request still runs twice (push and PR), deliberately. Only
  superseded pull request runs are cancelled; every `main` and release
  branch commit is gated. Each test step has a 25-minute limit, each
  shard's summary lists its ten slowest tests, and a test that could not
  finish in time now fails, naming what it waited for. A pull request
  runs the Release tests only: Debug is still built, and its tests run
  on the push to `main`, a release branch or a tag (`ADR-0160`).
- **Desktop tests write at `synchronous=NORMAL`.** A new configuration
  key, `Persistence:Synchronous=Normal`, opens the SQLite store at
  `synchronous=NORMAL` instead of `FULL`. Only the Desktop test suite sets
  it (`TEMPEST_Persistence__Synchronous`), to keep its shards inside the CI
  timeout. A store opened that way logs a Warning naming the key; a normal
  launch keeps `FULL` (`ADR-0144`, amended).
- **Release assets are all checksummed.** `SHA256SUMS.txt` now covers the
  installer and its update feed, not just the two zips, and the release
  job re-runs the tests with the same timeout scaling and hang detection
  as CI.
- **A release publishes only when CI is green on its tag, and packages
  are locked.** `release.yml` waits for `CI Gate` on the tagged commit
  and publishes nothing unless it passed; building and testing run with
  a read-only token, and a separate publish job, the only one that can
  write, checks every asset against `SHA256SUMS.txt` first. `vpk` is
  pinned in the tool manifest. Every project commits a
  `packages.lock.json` and CI and the release restore in locked mode;
  after a package change run
  `dotnet restore src/TempestOS.slnx --force-evaluate` (`ADR-0160`).

## Documentation

- `scripts/install-test-build.ps1` — every test build is installed with
  its own `TempestOS <version> (test)` desktop shortcut (PO decision
  2026-10-01; Release Engineering standard, "Handing a Build Over for
  Testing"). Runbook step A0.

- `docs/guides/Xero Setup - Step by Step.md` — a numbered, one-action-
  per-step guide to the first live Xero connection, with a
  troubleshooting table.
- `docs/reviews/RC PO Feedback Actions (2026-10-01).md` — the action
  register for every runbook comment. It includes the scope and work
  packages for live 2D reference diagrams on every calculation.

## Known, carried forward

- F8 — the first-run data-location dialog hangs when TempestOS is
  launched from the Start menu; the desktop shortcut works (`C-01`).
- The Xero loopback port moves from 49301 to 48131 (`TD-183`) on
  `claude/next-stretch-j1fqz2`, which is not part of this release. Until
  that branch merges, register `http://127.0.0.1:49301/callback/`.
- The standard project subfolder set (`V1-BLOCKER-01`) and full
  reference-library coverage (`WP RC.0G`) are firm v1.0.0 blockers.

## Gate (Linux, this branch's head, runbook round 1 fixes merged)

- Build: Desktop, Core and RealShell test projects build with 0 errors.
- Core tests: 5,679/5,690 — the 10 known Windows-only failures (DPAPI
  secret store, PowerShell scripts) plus the new install-script syntax
  check, which also needs Windows PowerShell.
- Desktop tests: 929/930 — the one failure is the known Linux-only
  `StatusBarCollapseTests`, unchanged from `v0.22.0`.
- Tempest-Dashboard 0.1.2 (`main`): `npm test` 126/126, lint clean.
- Not run here: the Windows real-shell journey and the installer
  (`scripts/install-test-build.ps1`, runbook A0).
