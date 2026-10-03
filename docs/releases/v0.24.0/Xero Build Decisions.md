# v0.24.0 Xero — build defaults for the design's open questions

Chief-engineer defaults (2026-10-02) so the build is not blocked. Each
is easy to change and is listed for the Product Owner to confirm.

| Q | Default used by the build |
|---|---|
| Q1 | A quote goes to Xero as **DRAFT** on export and becomes **SENT** only when it is Sent in TempestOS; revisions update it while it is still DRAFT. |
| Q2 | Purchase orders go to Xero as **DRAFT** (same rule as invoices, D3). |
| Q3 | An expense gains an optional supplier (a Customers & Suppliers organisation); with none, the bill goes against one configured "General expenses" contact. |
| Q4 | Bill number: the supplier's invoice number when entered, otherwise `EXP-{expense id}`. |
| Q5 | Attached PDFs are **not** shown on Xero's online invoice (`IncludeOnline` false); the PO can switch it on. |
| Q6 | An expense recorded from a received PO's lines is **not** pushed as a separate bill (the PO uses Xero's "Copy to bill"); the expense records the link. |
| Q7 | When linking an existing Xero contact, TempestOS writes its customer code into `ContactNumber` only when that field is empty. |
| Q8 | Records raised before Xero sync began are not pushed automatically. Quotes, purchase orders and expenses each have a "Send to Xero" action; invoices have none — an invoice already in Xero from before v0.24.0 is imported as a link, and an unsent one goes to Xero through its own Send. (As built: X3 quote planner, X5 purchase-order and expense-bill planners, `IXeroBadgeSource.CanSendToXero`.) |
| Q9 | Drop `openid profile email`; the Demo Company smoke test confirms the connection still works. |
| Q10 | Account codes are read from Xero; Settings maps the sales account and each expense category, defaulting to the UK Demo Company's codes (200 Sales; 400-series expenses). |

## Chief-engineer sign-offs during the build

- **B2 follow-up for X6 (2026-10-02):** `PersistenceXeroOutbox` gains an additive claim filter by entry state, so the sync engine can recover lost creates before other work. Owned by B2; approved for X6.
- **X5 key lifetime:** a lost create not recovered within `IdempotencyKeyLifetime` (5 min, under Xero's documented 6 min key retention, to be confirmed on the Demo Company) stays "can't tell" and is never re-sent; X6 recovers lost creates first, every drain.
- **X3 hand-keyed quotes:** a quote keyed into Xero by hand is linked only when its values match what TempestOS sent; otherwise the user is told what to change and Retries.
- **UI wiring outside §11 rows (U1, U2, U3):** `src/Tempest.Desktop/Composition/MainWindowComposer.cs` (composition of the Xero section, contact linker and badges) and `tests/Tempest.Desktop.Tests/Tempest.Desktop.Tests.csproj` (linking the S1 simulator into Desktop tests) are shared; each UI task may make additive edits there, reconciled at merge by the chief engineer.

## U3 edits outside its §11 row — signed off by the chief engineer (2026-10-02)

U3 records these deviations here. All are additive and approved; the two open items were closed by the final clean-up pass (C1), as noted on each.

- **`src/Tempest.Desktop/Composition/MainWindowComposer.cs`** is composition only, as U1 and U2 also did. It adds: the Xero badge source (`XeroSyncServiceBadgeSource.TryCreate`), handed to the Quotes, project Quote, Invoicing and Purchase orders views; the purchase-order renderer and its collaborators for the PDF that Issue keeps; and the expense prompt's supplier picker, organisation catalogue, expense service and file picker (for the receipt).
- **`src/Tempest.Desktop/Views/TimesheetWeekView.cs`** (Record expense, about line 428) and **`src/Tempest.Desktop/Views/ProjectDetailsView.cs`** (Record expense, about line 452) each gain one call, `ExpenseEntryPrompt.ApplyPurchasingDetailsAsync`, made after the expense is recorded. The call saves the optional supplier and supplier invoice number (Q3/Q4, `IExpenseService.SetSupplierAsync`) and attaches the optional receipt. How the expense itself is recorded (`RecordExpenseCommand`) is unchanged.
- **Closed by C1 (`b7c8530a`, 2026-10-02) — was: open item, needs X5 (not U3):** the supplier details are saved in a follow-up commit after the record (verifier round 1, defect 8). In that short window the engine may plan and push the bill against "General expenses" as `EXP-{id}`. The next plan then updates the draft with the supplier and its invoice number, so the end state is correct. Saving them in the expense's first revision needs a `RecordAsync` overload in X5's `IExpenseService`/`ExpenseService` and a matching `RecordExpenseCommand`. Both are outside U3's row. *C1 added the additive `RecordAsync` overload and the `RecordExpenseCommand` constructor; the Record expense flows now save the supplier and invoice number in the expense's first revision, and only the receipt is applied afterwards.*
- **Closed by C1 (`001cc4f1`, 2026-10-03) — was: open item, needs X6/X5 (not U3):** X6's `XeroDocumentSyncStatus` carries no typed verdict for *Can't tell* (verifier round 1, defect 6). U3 now detects *Deleted in Xero* only from typed facts: the `DELETED` status, or X5's tombstone, which X6 exposes as `CanSendAgain`. U3 detects *Can't tell* from the opening words of `XeroPurchasingOwnership.CannotTell`, worked out from that producer at run time rather than by searching the text. A typed field on X6's status would remove the last string comparison. *C1 added it: X6 reports X5's "Can't tell" verdict typed, and the badge no longer matches its words.*
- **Expense badges (verifier round 1, defect 3):** these stay inside U3's row. Invoicing (`InvoicingView.cs`, owned by U3) gains a collapsed group, *Expense bills in Xero*, shown only when Xero is the connector. It lists every expense that is not already under *Available to invoice*, each with its bill badge. A badge in the Project Explorer's Expenses area or in the expense editor would need another sign-off: those files are not U3's.

## Review board fixes (2026-10-03)

- **F4 (docs, version, live smoke):** `VERSION` reads `0.24.0`. The live smoke test gains a second journey, `XeroProductionSmokeJourney`, that drives the production planners, mappers, handlers, invoicing service and sync engine through `XeroAccountingApi` and the real safety handler against the Demo Company (still refusing to write unless `IsDemoCompany`, still skipped without credentials); the same journey runs against the simulator on every CI run (`XeroProductionSmokeJourneyTests`). Q8 above is restated to match the code. Outside the doc rows: `PHYSICAL_REVIEW.md` §7m (Xero rows pointing at the runbook) and `.github/workflows/ci.yml` (shard comment counts only).
