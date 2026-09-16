# Product Owner Test Runbook — v1.0.0 release candidate (v0.21.0, build `30fe6e7`)

**Tester:** Steven (Product Owner). **Date:** ____________. **Machine:** ____________. **Data folder:** `C:\TempestOS-rc-data` (fresh).
**How to score:** fill the Result cell with `PASS`, `FAIL` or `SKIP` (with why). A FAIL is anything that differs from the Expect column — quote the wording you saw in Comments. Fill the Digest at the bottom last. Detailed wording per step: `PHYSICAL_REVIEW.md` §7c–§7k; last night's evidence: `docs/releases/v0.21.0/evidence/`.

## A. Install and launch

| # | Step | Expect | Result | Comments |
|---|---|---|---|---|
| A1 | Fresh clone of `claude/tempestos-v1-final-acceptance-19hka8`; `git log -1 --format=%h` | `30fe6e7` | | |
| A2 | `dotnet restore` then Release build with `-p:TreatWarningsAsErrors=true` | `0 Warning(s)`, `0 Error(s)` | | |
| A3 | Launch `Tempest.Desktop.exe --persistence-root C:\TempestOS-rc-data` | Title bar `TempestOS 0.21.0 (30fe6e7)`; rail exactly Home, Projects, Tasks, Engineering, Business | | |
| A4 | Home on the fresh root | Five task tiles at 0; "Quotes: 0 open, £0.00 total value."; "Invoices: unavailable — No accounts reading yet."; six status bars; "0 open project(s) in total." | | |

## B. Setup, project and quote

| # | Step | Expect | Result | Comments |
|---|---|---|---|---|
| B1 | Engineering → Reference data → *Add a rate card*: `Standard rates 2026`, `Engineer`, `95` → **Add Rate Card** | Record opens on the right as Draft; toast "Added rate card…"; **Release** → Released; "Rate cards (1)" | | |
| B2 | Same page → *Add a person* (name, role, email) → **Add Person** → **Release** | Person listed Released | | |
| B3 | Home → **New Project…** → Client → **Add organisation…** | Picker opens **above** the prompt and is clickable | | |
| B4 | Register `Client Ltd`, choose it; Rate card drop-down; PO `PO-1001`; OK | The released card is offered; project opens on its Quote tab | | |
| B5 | Details tab: budget `25000`, dates | Client and rate card shown by name, PO shown | | |
| B6 | Quote: hourly line 40 h × 120, fixed line 2500 | "Total £7,300.00" | | |
| B7 | **Export** → open the PDF | Wordmark, indigo rule, both lines | | |
| B8 | **Send** (confirm) | Reads Sent; sheet attached | | |
| B9 | **Accept** (confirm) | "2 deliverable(s) and requirement(s) created" | | |
| B10 | Without leaving the project: **Requirements**, **Deliverables** tabs | Both already list what Accept created | | |
| B11 | Deliverables → add one directly | Three deliverables listed | | |
| B12 | Structure → Calculations → create; Mechanical → two Parts, same identifier | Due defaults today + 14; duplicate refused in the status bar | | |

## C. Calculators, assets, evidence

| # | Step | Expect | Result | Comments |
|---|---|---|---|---|
| C1 | Engineering → Modules → Calculators | 16 calculations; Libraries panel counts released records | | |
| C2 | *Bolt shear capacity*: 20 mm, 400 MPa, 2 planes, SF 1.5 → **Calculate** | 167552 N | | |
| C3 | **Re-run** | Run 2, "Re-run of" run 1 | | |
| C4 | **Compare with previous** | "nothing differs" | | |
| C5 | SF 2 → Calculate → Compare | Only Safety factor (1.5 → 2) and capacity (167552 → 125664 N) rows | | |
| C6 | **Start a new calculation** → Calculate | New calculation at run 1; the old one untouched | | |
| C7 | `abc` in Bolt diameter → Calculate | Refusal beside the form; nothing recorded; no dialog | | |
| C8 | Release S355J2 (Reference data); *Beam bending*: 10 kN, 2000 mm, 2000000 mm⁴, 50 mm, 8 mm | E 210 GPa and 355 MPa read-only; 3.96825 mm; 125 MPa | | |
| C9 | Span 200 mm → Calculate | Refusal naming span-to-depth; no dialog | | |
| C10 | Engineering Assets → *Bracket verification* → **Check** → **Record** | Stress, allowable, margin, mass with units; artefact recorded; trace shows it | | |
| C11 | Project → Evidence → create with a real file, subject, cited material, key figures | Draft record listed with size and hash | | |
| C12 | Settings → Evidence → **Independent check required** on; Check as the author | Refused: checker must differ | | |
| C13 | Settings → **Switch person…** → the B2 person → **Check**; switch back → **Issue** | Checked, then Issued; issue sheet attached to the project | | |

## D. Time, invoice, dashboards, findability

| # | Step | Expect | Result | Comments |
|---|---|---|---|---|
| D1 | Business → Timesheets → **Record**: project, 2 h, grade, blank task | Grade offered `Engineer`; "A task is required." | | |
| D2 | Add a task → Record | Week shows 2 h; priced from the card | | |
| D3 | **Export week** | PDF: hours by day; blank signature blocks | | |
| D4 | Deliverables → **Complete** one with the Issued evidence | Draft invoice request raised by itself | | |
| D5 | Business → Invoices | Request under **New (1)** | | |
| D6 | **Export invoice** | PDF with client, lines, terms (bank details if set) | | |
| D7 | Home | Six bars; "1 open project(s) in total." | | |
| D8 | Projects → Dashboard + Reports; Business → Dashboard & Reports; Tasks | Project with schedule bar; "unavailable" tiles, not zeros; buckets populated | | |
| D9 | Ctrl+K, type the project name | Palette finds and opens it | | |
| D10 | Structure → object → Ctrl+Shift+M → move; Ctrl+Z; Ctrl+Y | Moved; back; forward | | |
| D11 | Create a Document → Ctrl+Z → Ctrl+Y | Removed; the same one restored | | |
| D12 | Documents → attach PDF and SVG → Open; rotate, annotate, **Save annotated copy…** | Both open in-app; annotated copy saved | | |
| D13 | Same file on two objects → delete one | Same sha256 shown; the other still opens | | |

## E. Docking, persistence, backup, lifecycle

| # | Step | Expect | Result | Comments |
|---|---|---|---|---|
| E1 | Structure: drag Inspector tab past the workspace edge; drag Explorer tab onto it | Floating window; Explorer becomes its second tab | | |
| E2 | Close the Explorer tab | Window stays with Inspector; main window's Documents untouched | | |
| E3 | Close the last tab | Floating window gone; nothing lost | | |
| E4 | Tab to a header; Ctrl+Shift+→ then ← | Focus ring stays on that header | | |
| E5 | Ctrl+Shift+, then Ctrl+Shift+. on a two-tab strip | Tab moves earlier, then later; stops at ends | | |
| E6 | Palette → **Reset Layout** | One window; defaults | | |
| E7 | Float a panel; close the main window; relaunch same root | Whole app closed; floating window back where it was; project listed | | |
| E8 | Second monitor: window there; relaunch; then monitor disabled; relaunch | Same monitor; then primary, fully visible | | |
| E9 | Settings → **Back up now…** | A `.db` file written | | |
| E10 | **Restore from backup…** with a project open | Refused | | |
| E11 | Close project → restore → relaunch | Data as the backup held it | | |
| E12 | Sign off: blank statement, then a statement | Blank refused; then signed | | |
| E13 | A project closed ≥ 90 days ago | Read-only banner; writes refused | | |

## F. Xero and installer (Product Owner only)

| # | Step | Expect | Result | Comments |
|---|---|---|---|---|
| F1 | Xero app redirect URI = `http://127.0.0.1:49301/callback/`; decide on port 49301 | Registered | | |
| F2 | Settings → Connector **Xero**, client id (+ secret) → **Authorise** | "Saved. Restart TempestOS to use Xero — this session is running the Fake connector." | | |
| F3 | Close and reopen → Settings | Xero selected; id shown; "Not authorised." | | |
| F4 | **Authorise** | Browser on Xero consent; "Waiting for you to sign in to Xero in your browser (up to 5 minutes)…" | | |
| F5 | Sign in, allow | Browser: "Authorisation complete…"; status "Authorised." | | |
| F6 | Business → Invoices, or Settings → Refresh accounts reading | Contacts / bills / cash position read from Xero | | |
| F7 | `pwsh scripts/package-installer.ps1` | `artifacts/installer/TempestOS-0.21.0-Setup.exe` produced | | |
| F8 | Install; launch from Start menu | First-run data-location dialog; same shell | | |
| F9 | Settings → **Check for updates now** | Honest feed message | | |

## Known before you start (filed, not defects to re-report)

Raw GUIDs in three places (`TD-186`); a literal "(TD-180)" in an Invoices heading (`TD-187`); no per-line VAT on the Quote tab, the Settings default applies (`TD-188` — your call).

## Digest

| Section | Steps | PASS | FAIL | SKIP | Blocking failures (step ids) |
|---|---|---|---|---|---|
| A. Install and launch | 4 | | | | |
| B. Setup, project, quote | 12 | | | | |
| C. Calculators, assets, evidence | 13 | | | | |
| D. Time, invoice, dashboards, findability | 13 | | | | |
| E. Docking, persistence, backup, lifecycle | 13 | | | | |
| F. Xero and installer | 9 | | | | |
| **Total** | **64** | | | | |

**Defects found (one line each: step id — what you saw — severity):**

-

**Decisions taken (`TD-183` port, `TD-188` VAT, `ADR-0153` steps 3–4):**

-

**Product Owner's verdict on the v1.0 release candidate:** ☐ Accepted ☐ Accepted with the listed fixes ☐ Not accepted — Signed: ____________ Date: ____________
