# Engineering navigation — stress test runbook

This runbook repeats the stress loop in `tests-loop.md`. Use it after any change to the discipline tree, the command bar or engineering surface hosting.

**Time:** about 25 min for the filter loop, 37 min for the idle full runs and 60 min for the loaded full runs (4 vCPU).

## 0. Prerequisites

1. Check out the branch under test and bring in main. Main carries the scaled-deadline fix (`b4ce0cb1`); without it, `UnscaledTestDeadlineTests` fails every run.
   ```bash
   git fetch origin main && git merge origin/main
   ```
2. Install the .NET 10 SDK if it is missing:
   ```bash
   curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 10.0 --install-dir ~/.dotnet
   export PATH=$HOME/.dotnet:$PATH
   ```
3. Build the tests and make sure the build has 0 errors:
   ```bash
   dotnet build tests/Tempest.Desktop.Tests
   ```
4. Pick a directory for the output:
   ```bash
   OUT=${OUT:-/tmp/navstress}; mkdir -p $OUT
   ```

## 1. Targeted filter loop × 15

```bash
F="FullyQualifiedName~EngineeringDisciplineNavigation|FullyQualifiedName~RibbonViewTests|FullyQualifiedName~LayoutWalk|FullyQualifiedName~StructureTabContainment|FullyQualifiedName~AutomationName|FullyQualifiedName~ResponsiveWorkspace|FullyQualifiedName~MainWindowResize|FullyQualifiedName~ProductSpine|FullyQualifiedName~CreatedObjectOpensRightUp|FullyQualifiedName~FeatureCompletion|FullyQualifiedName~ObjectRehydration"
for i in $(seq 1 15); do
  dotnet test tests/Tempest.Desktop.Tests --no-build --filter "$F" > $OUT/filter$i.log 2>&1
  echo "filter$i exit=$? $(grep -E '^(Passed!|Failed!)' $OUT/filter$i.log)"
done
```
**Pass gate:** 15/15 runs pass with 60/60 tests and 0 failed.

## 2. Full suite × 3, idle

```bash
for i in 1 2 3; do
  dotnet test tests/Tempest.Desktop.Tests --no-build --logger "trx;LogFileName=$OUT/normal$i.trx" > $OUT/normal$i.log 2>&1
  grep -E '^(Passed!|Failed!)' $OUT/normal$i.log
done
```

## 3. Full suite × 3, under CPU load

Run one busy loop per core for the whole run:
```bash
for i in 1 2 3; do
  P=""; for c in $(seq $(nproc)); do yes > /dev/null & P="$P $!"; done
  dotnet test tests/Tempest.Desktop.Tests --no-build --logger "trx;LogFileName=$OUT/stress$i.trx" > $OUT/stress$i.log 2>&1
  grep -E '^(Passed!|Failed!)' $OUT/stress$i.log
  kill $P; wait $P 2>/dev/null
done
```
`stress-ng --cpu $(nproc)` works instead of `yes` if it is installed. A loaded run takes about 1.6× as long as an idle one.

## 4. Classify failures

1. List the failed tests in each run:
   ```bash
   for f in $OUT/*.trx; do grep -oP '<UnitTestResult [^>]*testName="\K[^"]+(?="[^>]*outcome="Failed")' $f | sort > ${f%.trx}.failed; done
   wc -l $OUT/*.failed
   ```
2. Check whether the failure set changes between runs. A run that prints `differs` is a flake candidate:
   ```bash
   for f in $OUT/*.failed; do cmp -s $f $OUT/normal1.failed || echo "$f differs"; done
   ```
3. Drop the environmental baseline. These are Linux-container PDF text-extraction failures, which also fail on main:
   ```bash
   grep -vE 'DocumentRendererTests|IssueSheetRendererTests|QuotationSheetRendererTests|QuotationJourneyTests\.NewProjectWithQuoteOption|ProjectQuoteReviewTests\.SaveDraft_Submit|QuotesViewExportTests\.Export_Starts|BadgeViewsTests\.PurchaseOrders_Issue|InvoicingXeroReviewFixesTests\.TheInvoicePdf' $OUT/normal1.failed
   ```
   - 17 tests in the renderer classes.
   - 5 export tests that assert on text read back from a PDF.
   - On Windows none of the 22 should fail. If one does there, it is real.
4. Whatever is left is real. Pull the message and stack top for each one from its `.trx` (look at the `<Message>` and `<StackTrace>` elements).

## 5. Verdict

| Result | Verdict |
|---|---|
| Filter 15/15 green, full-suite failure sets identical, nothing left after the baseline | **FLAKE-FREE** |
| A test fails in some runs but not others, or only under load | **FLAKY:** name each test and its pass rate |
| A test outside the baseline fails in every run | **BROKEN:** name each test |

For a flaky timing failure, check that the test waits with `DesktopTestHelpers.Deadline(n)` or `WaitUntil` and not a raw `DateTime.UtcNow.AddSeconds`. Then rerun with `TEMPEST_TEST_TIMEOUT_FACTOR=3`. If it goes green at 3×, it is a timing race, not a logic bug.

## 6. Record

1. Write the run table, the non-baseline failures (message and stack top) and the verdict to `docs/reviews/<review>/stress/tests-loop.md`. Use `tests-loop.md` in this folder as the template.
2. Commit only the report and push it to the review branch. Do not open a PR.
