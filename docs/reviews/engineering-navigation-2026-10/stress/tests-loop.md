# Engineering navigation — test stress loop (2026-10-18)

Branch under test: `claude/nice-archimedes-airhlx` @ `9306eca3` (4 vCPU Linux container, .NET SDK 10.0.402).
Build: `dotnet build tests/Tempest.Desktop.Tests` — succeeded, 0 warnings, 0 errors.

## 1. Targeted filter × 15

Filter: `EngineeringDisciplineNavigation | RibbonViewTests | LayoutWalk | StructureTabContainment | AutomationName | ResponsiveWorkspace | MainWindowResize | ProductSpine | CreatedObjectOpensRightUp | FeatureCompletion | ObjectRehydration`

| Run | Result | Passed | Failed | Wall |
|---|---|---|---|---|
| 1–15 | Pass (all 15) | 60 | 0 | 92–102 s |

Every one of the 60 tests passed in all 15 runs.

## 2. Full Desktop suite

1122 tests per run. The CPU contention was one `yes > /dev/null` per core (4 total) running the whole time.

| Run | Load | Passed | Failed | Baseline PDF | Other env (PDF text) | Change-related | Duration |
|---|---|---|---|---|---|---|---|
| normal1 | idle | 1099 | 23 | 17 | 5 | 1 | 12 m 29 s |
| normal2 | idle | 1099 | 23 | 17 | 5 | 1 | 12 m 18 s |
| normal3 | idle | 1099 | 23 | 17 | 5 | 1 | 12 m 27 s |
| stress1 | 4× `yes` | 1099 | 23 | 17 | 5 | 1 | 19 m 39 s |
| stress2 | 4× `yes` | 1099 | 23 | 17 | 5 | 1 | 19 m 33 s |
| stress3 | 4× `yes` | 1099 | 23 | 17 | 5 | 1 | 20 m 2 s |

All six runs failed exactly the same 23 tests. Under contention nothing new failed and nothing passed intermittently.

### Baseline (known Linux PDF renderer failures, not counted) — 17

- `DrawingRegisterDocumentRendererTests`: `Render_EveryFieldTheRegisterNames_IsReadableBackOutOfTheText`, `Render_SixtyRows_Paginates_AndEveryPageCarriesTheFooter`
- `InvoiceDocumentRendererTests`: `Render_EveryFieldTheInvoiceNames_…`, `Render_NoBankDetailsRecorded_…`, `Render_SixtyLines_Paginates_…`
- `ProgressReportDocumentRendererTests`: `Render_EveryFieldTheReportNames_…`, `Render_NoRisks_…`
- `PurchaseOrderDocumentRendererTests`: `Render_EveryFieldTheOrderNames_…`
- `TechnicalReportDocumentRendererTests`: `Render_EveryFieldTheReportNames_…`, `Render_NoRevisions_…`
- `TimesheetDocumentRendererTests`: `Render_EveryFieldTheTimesheetNames_…`, `Render_NoTimeRecorded_…`
- `IssueSheetRendererTests`: `Render_EveryFieldTheSheetNames_…`, `Render_WithNoIdentity_UsesTheProvider_…`
- `QuotationSheetRendererTests`: `Render_EveryD4Field_…`, `Render_TheReferenceAndTotal_…`, `Render_WithNoIdentity_UsesTheProvider_…`

Every one has the same symptom: `Assert.Contains() Failure: Sub-string not found` on text read back from the PDF. Only the first line of text comes back (for example `"INVOICEINV-2026-001 · …"`).

### Same PDF text-extraction symptom outside the renderer classes — 5 (environmental)

These export or issue a PDF, then assert on text extracted from it. The failure is the same truncated-text case as the baseline. None of these tests touches the ribbon, the tree or the discipline navigation.

| Test | Message | Stack top |
|---|---|---|
| `QuotationJourneyTests.NewProjectWithQuoteOption_ThroughSendAcceptExportAndBusinessQuotes` | Not found: `"Revision: R1"` in `"QUOTATIONQ-2026-001 · R1 · …"` | `QuotationJourneyTests.cs:343` |
| `ProjectQuoteReviewTests.SaveDraft_SubmitForReview_…_ExportNamedR1` | Not found: `"Revision: R1"` | `ProjectQuoteReviewTests.cs:207` |
| `QuotesViewExportTests.Export_StartsInTheProjectsQuoteFolder_…_WithItsRevision` | Not found: `"Status: In review"` | `QuotesViewExportTests.cs:68` |
| `BadgeViewsTests.PurchaseOrders_IssueKeepsTheRenderedPdfOnTheOrder_…` | Not found: `"Deliver to site office."` | `BadgeViewsTests.cs:369` |
| `InvoicingXeroReviewFixesTests.TheInvoicePdf_CarriesTheInvoiceNumberAndTheSendDate` | Not found: `"ACME1-BRIDG1-INV-007"` | `InvoicingXeroReviewFixesTests.cs:146` |

### Caused by the change — 1 (deterministic)

| Test | Message | Stack top |
|---|---|---|
| `UnscaledTestDeadlineTests.NoDesktopTest_BuildsAnUnscaledUtcNowAddSecondsDeadline` | `Unscaled deadline(s) - use DesktopTestHelpers.Deadline(seconds) instead: EngineeringDisciplineNavigationTests.cs:151: var deadline = DateTime.UtcNow.AddSeconds(5);` | `UnscaledTestDeadlineTests.cs:43` |

- Commit `7f8f3858` (Engineering navigation) added `WaitUntilAsync` in `EngineeringDisciplineNavigationTests.cs` with a hard-coded 5 s deadline that ignores `TimeoutFactor`.
- `origin/main` already fixes this: `b4ce0cb1` (v0.25.0) uses `DesktopTestHelpers.Deadline(5)`. The branch does not contain that commit (`origin/main` is not an ancestor of `9306eca3`). Merging main into the branch should fix this test.
- Under contention the navigation tests still finished inside the 5 s window in every run, so the unscaled deadline did not cause any timing failure here.

## Rerun after merging main (2026-10-19) — current verdict: **FLAKE-FREE**

What changed: `claude/stress-nav-tests` @ `695351b3` merges `origin/main` into the branch. The branch now carries `b4ce0cb1`, so `EngineeringDisciplineNavigationTests` uses `DesktopTestHelpers.Deadline(5)`. The steps followed `runbook.md`.

| Step | Runs | Passed | Failed | Wall |
|---|---|---|---|---|
| Targeted filter | 15/15 pass | 61 | 0 | 93–112 s |
| Full suite, idle | normal1–3 | 1101 | 22 | 13 m 10 s – 13 m 20 s |
| Full suite, 4× `yes` | stress1–3 | 1101 | 22 | 21 m 17 s – 21 m 30 s |

- All six full runs failed the same 22 tests.
- Those 22 are the 17 baseline renderer tests plus the 5 PDF text-extraction tests listed below.
- Nothing was left after removing that baseline.
- `UnscaledTestDeadlineTests` now passes.
- No failure was intermittent and none appeared only under load.

## Original verdict (2026-10-18, before the merge)

**BROKEN: `UnscaledTestDeadlineTests.NoDesktopTest_BuildsAnUnscaledUtcNowAddSecondsDeadline`.** It fails deterministically; the fix is on main, so merge `origin/main`.

Otherwise the navigation change is **FLAKE-FREE**:

- The targeted filter passed 15/15.
- The full suite failed the same set in 6/6 runs, idle or under 4-core contention.
- No failure was intermittent and none is a timing race.
- Apart from the one deadline lint above, every failure is the PDF text-extraction problem in this environment.
