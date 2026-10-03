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
| Q8 | Pre-v0.24.0 quotes and invoices are not pushed automatically; each has a "Send to Xero" action. |
| Q9 | Drop `openid profile email`; the Demo Company smoke test confirms the connection still works. |
| Q10 | Account codes are read from Xero; Settings maps the sales account and each expense category, defaulting to the UK Demo Company's codes (200 Sales; 400-series expenses). |

## Chief-engineer sign-offs during the build

- **B2 follow-up for X6 (2026-10-02):** `PersistenceXeroOutbox` gains an additive claim filter by entry state, so the sync engine can recover lost creates before other work. Owned by B2; approved for X6.
- **X5 key lifetime:** a lost create not recovered within `IdempotencyKeyLifetime` (5 min, under Xero's documented 6 min key retention, to be confirmed on the Demo Company) stays "can't tell" and is never re-sent; X6 recovers lost creates first, every drain.
- **X3 hand-keyed quotes:** a quote keyed into Xero by hand is linked only when its values match what TempestOS sent; otherwise the user is told what to change and Retries.
- **UI wiring outside §11 rows (U1, U2, U3):** `src/Tempest.Desktop/Composition/MainWindowComposer.cs` (composition of the Xero section, contact linker and badges) and `tests/Tempest.Desktop.Tests/Tempest.Desktop.Tests.csproj` (linking the S1 simulator into Desktop tests) are shared; each UI task may make additive edits there, reconciled at merge by the chief engineer.

## U3 edits outside its §11 row — signed off by the chief engineer (2026-10-02)

U3 records these deviations here. All are additive and approved; the two open items are scheduled for the final cleanup pass.

- **`src/Tempest.Desktop/Composition/MainWindowComposer.cs`** is composition only, as U1 and U2 also did. It adds: the Xero badge source (`XeroSyncServiceBadgeSource.TryCreate`), handed to the Quotes, project Quote, Invoicing and Purchase orders views; the purchase-order renderer and its collaborators for the PDF that Issue keeps; and the expense prompt's supplier picker, organisation catalogue, expense service and file picker (for the receipt).
- **`src/Tempest.Desktop/Views/TimesheetWeekView.cs`** (Record expense, about line 428) and **`src/Tempest.Desktop/Views/ProjectDetailsView.cs`** (Record expense, about line 452) each gain one call, `ExpenseEntryPrompt.ApplyPurchasingDetailsAsync`, made after the expense is recorded. The call saves the optional supplier and supplier invoice number (Q3/Q4, `IExpenseService.SetSupplierAsync`) and attaches the optional receipt. How the expense itself is recorded (`RecordExpenseCommand`) is unchanged.
- **Open item, needs X5 (not U3):** the supplier details are saved in a follow-up commit after the record (verifier round 1, defect 8). In that short window the engine may plan and push the bill against "General expenses" as `EXP-{id}`. The next plan then updates the draft with the supplier and its invoice number, so the end state is correct. Saving them in the expense's first revision needs a `RecordAsync` overload in X5's `IExpenseService`/`ExpenseService` and a matching `RecordExpenseCommand`. Both are outside U3's row.
- **Open item, needs X6/X5 (not U3):** X6's `XeroDocumentSyncStatus` carries no typed verdict for *Can't tell* (verifier round 1, defect 6). U3 now detects *Deleted in Xero* only from typed facts: the `DELETED` status, or X5's tombstone, which X6 exposes as `CanSendAgain`. U3 detects *Can't tell* from the opening words of `XeroPurchasingOwnership.CannotTell`, worked out from that producer at run time rather than by searching the text. A typed field on X6's status would remove the last string comparison.
- **Expense badges (verifier round 1, defect 3):** these stay inside U3's row. Invoicing (`InvoicingView.cs`, owned by U3) gains a collapsed group, *Expense bills in Xero*, shown only when Xero is the connector. It lists every expense that is not already under *Available to invoice*, each with its bill badge. A badge in the Project Explorer's Expenses area or in the expense editor would need another sign-off: those files are not U3's.

## F2 review-board fixes — edits outside the owning rows and contract growth (2026-10-03)

F2 fixed the review board's core-sync findings (M2, M6, M7, M8, m1, m16, m18, m19, n1–n4) across several tasks' rows. Every change is additive or a bug fix; none weakens D3, D4 or the Demo guard (the safety handler, the write models and the architecture tests are unchanged or stricter).

**Files changed outside a single owning row** (owner in brackets):

- `Sync/XeroSyncService.cs` [X6]: the invoice planner's slot gets a start-up hook (M8, `RecoverInterruptedSendsAsync`), and the badge note carries `XeroLink.AttachmentNote` (M6). The follow-up adds `XeroPlannerSlot.PrimeDescription`/`PrimeLabel` (additive), so a failing hook is logged as *recovering Invoice sends a stop interrupted*, not as *recording when automatic Invoice sync began*.
- `Sync/XeroSyncContracts.cs` [T0 contract, §12]: `XeroLink.AttachmentNote` (M6), an optional trailing record member. Older links read it as absent.
- `XeroConnector.cs` [B1/X4]: `AccessTokenOutcome.Unavailable` maps to `ConnectorOutcome.Unavailable` and to `Authorised` in `AuthorisationStateAsync` (M2). The follow-up adds the internal `EnsureAccessAsync`.
- `src/Tempest.Core/Invoicing/QuickBooksOnline/QuickBooksOnlineConnector.cs` [outside v0.24.0]: the same M2 mapping, so a new enum member is never read as a re-authorise.
- `src/Tempest.Core/Invoicing/OAuth/OAuthAuthoriser.cs`, `OAuthResults.cs`, `InvoicingHttpLoggingHandler.cs` [B1, shared OAuth]: `AccessTokenOutcome.Unavailable` / `AccessTokenResult.Unavailable` (M2); the HTTP log names the path only (n2).
- `Api/XeroAccountingApi*.cs`, `Api/XeroWriteSafetyHandler.cs`, new `Api/XeroLineRules.cs` [B1/X3/X5]: `?unitdp=4` (M7), description limit (m19), contact write-key allow-list (n1), attachment names (n3).
- `Sync/Purchasing/XeroPurchasingAttachmentHandlers.cs`, `XeroPurchasingMapper.cs`, `XeroPurchasingOwnership.cs` [X5]: M6 refusal, M7/m19 line rules, m1 `Status: DRAFT` on content updates, n4 shared VAT inference. The follow-up adds the optional `restampSameSend` to `XeroPurchasingCreateLog.RecordSendingAsync` (default off: purchasing behaviour is unchanged).
- `Sync/Quotes/XeroQuoteMapper.cs`, `XeroQuotePushHandler.cs` [X3]: m1, M6, M7.
- New `Sync/XeroAttachmentRefusal.cs`, new `src/Tempest.Core/Invoicing/ExpenseVatInference.cs` (shared helpers, M6/n4).
- Tests, test-only and additive: `Api/XeroApiTestSupport.cs` (`tokenEndpoint`), `Sync/Engine/EngineTestKit.cs`, `Sync/Invoices/InvoiceExportKit.cs` (`tokenEndpoint`), and the simulator (`XeroApiSimulator.Documents.cs`, `.Routing.cs`: rounds to four places like Xero, enforces the 4,000-character description).

**Contract growth:** `XeroLink.AttachmentNote`; `AccessTokenOutcome.Unavailable` with `AccessTokenResult.Unavailable`; `InvoiceNumberHolder.OwnUnproven`; `XeroInvoiceDrafts.LinkedByMatched` (`"matched"`); `XeroPlannerSlot.PrimeDescription`/`PrimeLabel`; the optional `creates` parameter of `XeroInvoiceDrafts` (the shared create log, resolved through dependency injection).

**m16 — the smaller option (no Product Owner decision needed).** The board asked for a PO decision before m16 overrode design §6.4's lost-create recovery. F2 now applies the board's smaller option instead:

- An invoice found under the request's number is adopted as `"reconciled"` when proven: its id came back to a logged create, or the logged create's key replays to it while Xero holds the key (5 minutes from the latest send; a create re-sent after a look-up found nothing re-stamps its send time).
- Otherwise, after a **lost create of this request** (a create TempestOS logged with that number, contact and reference, whose id never came back), the one live invoice under that number, reference and contact is linked as `"matched"`. That is design §6.4's recovery, so M8's restart after a long outage ends Sent, not Failed. A matched invoice is kept up to date while it is a draft. **TempestOS never deletes it.** Voiding the request is refused with the reason until the person deletes or voids it in Xero; then the void goes ahead.
- With no lost create of its own (for example, an invoice entered by hand), a match stays `OwnUnproven`: refused, and never adopted, changed or deleted. The advice depends on the invoice's state in Xero. For a draft: delete it in Xero, then Retry. For an approved invoice: it can only be voided there, so void it there and then void this request. For a voided invoice: Xero keeps a voided invoice's number, so void this request.
- A create that could not leave the machine is not logged. This covers a sign-in service that cannot be reached, or a needed re-authorisation.
- A send that throws after Xero answered no longer hides the request from the start-up recovery.

