# Overnight Changes Runbook — what changed on 2026-09-15/16 and how to check each one

For the Product Owner. One section per user-visible change from the overnight acceptance campaign; tick the box when the "Expect" line matches what you see. Build to check against: `TempestOS 0.21.0 (30fe6e7)` on a fresh `--persistence-root`. Evidence from the overnight runs is under `docs/releases/v0.21.0/evidence/`. The full journey is `PRODUCT_OWNER_ACCEPTANCE.md` §4; the detailed wording per step is `PHYSICAL_REVIEW.md` §7c–§7k.

## 1. A rate card can be created (new)

- [ ] Engineering → Reference data → **Add a rate card**: name, grade `Engineer`, hourly rate `95` → **Add Rate Card**.
  **Expect:** the record opens on the right as Draft; a toast "Added rate card …"; **Release** takes it to Released; the list reads "Rate cards (1)".
- [ ] Home → **New Project…**. **Expect:** the Rate card drop-down already offers the released card.
- [ ] Business → Timesheets → **Record** on that project. **Expect:** Grade is enabled and reads `Engineer`; a blank Task is refused with "A task is required."
  Why: nothing in the shipped product could create a rate card, so a clean install could never price time or raise an invoice. Evidence: `evidence/fixes/01–06.png`.

## 2. The organisation picker opens above the New Project prompt (fixed)

- [ ] New Project… → Client → **Add organisation…**. **Expect:** the picker is on top and clickable, not hidden behind the prompt.
  Evidence: `evidence/realshell/` (the before-fix shot is in the WP 21.5C report).

## 3. Accepting a quotation fills the sibling tabs in place (fixed)

- [ ] Quote → Send → **Accept**, then click **Requirements** and **Deliverables** without leaving the project. **Expect:** both already list what Accept created. Previously they stayed empty until the project was reopened.

## 4. Home counts every open project (fixed)

- [ ] Home → Project status. **Expect:** six bars — On track, Ready to invoice, At risk, Overdue, Blocked, On hold — and "N open project(s) in total." counting all of them. Complete a deliverable and return to Home: the project must still be counted.

## 5. Xero can be authorised from Settings (fixed; live sign-in is yours)

- [ ] Settings (account chip, top right) → Connector authorisation → **Xero**, your client id → **Authorise**. **Expect:** "Saved. Restart TempestOS to use Xero — this session is running the Fake connector."
- [ ] Close and reopen. **Expect:** Xero selected, the id shown, "Not authorised."
- [ ] **Authorise**. **Expect:** the browser opens on Xero's consent page; the status reads "Waiting for you to sign in to Xero in your browser (up to 5 minutes)…"; after consent, "Authorised."
  Why: Authorise never started the sign-in, the client id was stored under a key the authoriser never read, and the connector chosen in Settings never reached the host. Redirect URI to register on the Xero app: `http://127.0.0.1:49301/callback/`. Full script: `PHYSICAL_REVIEW.md` §7k. Evidence: `evidence/xero/01–04.png`.

## 6. Docking: closing a floating tab no longer wipes other panels (fixed); keyboard reorder (new)

- [ ] Project → Structure. Drag the Property Inspector's tab past the workspace edge (a floating window), drag the Explorer's tab onto it. Close the Explorer tab. **Expect:** the floating window stays with the Inspector and the main window's Documents area is untouched. Close the last tab: the window disappears.
- [ ] Tab to a tab header, `Ctrl+Shift+→` then `Ctrl+Shift+←`. **Expect:** the focus ring stays on that header throughout.
- [ ] On a tab strip with two tabs, `Ctrl+Shift+,` then `Ctrl+Shift+.`. **Expect:** the tab moves earlier, then later; stops at the ends without complaint; the order survives a relaunch.
- [ ] Command Palette → **Reset Layout**. **Expect:** one window, every panel in its default place.
- [ ] Windows only: close the main window with a floating window open; relaunch. **Expect:** the whole app closed, and the floating window is back where it was. With a second monitor: K4/K5 in `PHYSICAL_REVIEW.md` §7j.
  Evidence: `evidence/docking/` (six screenshots).

## 7. The real-shell journey (new, for information)

- [ ] Nothing to click. `tests/Tempest.Desktop.RealShell/` drives the real application with genuine input on Linux; on the final head it passed 37 of 37 steps and 8 of 8 on relaunch (`evidence/realshell/gate-run-*-on-the-final-head.md`). Your Windows walk is the first on the supported platform.

## 8. Documentation (new, for information)

- [ ] `VISION.md` and `README.md` now say what the product does today, with the original text kept and dated. `PHYSICAL_REVIEW.md` §7j corrected (how a panel floats; reach a header with Tab), §7k added (Xero), §7c C2 (first rate card) and §7h R3 (VAT: the default from Settings applies to every quotation line — `TD-188` asks whether a per-line picker is owed) corrected.

## 9. Filed, not fixed — you will see these

- Raw GUIDs in three places: a completion's content text, an invoicing refusal, the status bar's Selected segment (`TD-186`).
- A literal "(TD-180)" in an Invoices heading (`TD-187`).
- No per-line VAT rate on the Quote tab (`TD-188`, a question for you).
- Never yet produced by the pipeline: the installer `Setup.exe` (`scripts/package-installer.ps1` on Windows, or a tag).

## 10. Sign-off

`PRODUCT_OWNER_ACCEPTANCE.md` §8. The last box is yours.
