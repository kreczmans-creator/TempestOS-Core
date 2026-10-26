# C-5 — Guided test (`Tempest.Calc.Library.ManualTesting`, `Tempest.Desktop.GuidedTest`, ADR-0175)

**In plain terms.** The owner test pack asks you to open each worksheet, type or import a set of inputs, run it and compare the outputs with a table. The Guided test does the opening, loading and showing in the app, and keeps your Pass, Fail, Blocked or Skipped (with a note) in a test run you can export. It never saves, checks or issues a calculation and never changes Evidence.

**Where it is.** Engineering → Modules → Calculations → **Guided test**. The panel sits to the left of the worksheet form; the form is the same one as under Worksheets.

**How a run goes.**

1. **Start a new run** (or **Resume the unfinished run**, offered when an earlier run on this machine was not finished). A run is yours: it records the person the app signs work off as, the build (the title bar's version and commit), the start time and the script version. Only that person can add to it.
2. Pick a step from the list (filter by runbook section, or search by title, template or discipline). A worked example shows its expected outputs, each with its allowance, and its governing check. A manual step (smoke test, linked chains, imports, lifting, legacy records) shows the runbook's instructions and what must be seen.
3. **Open worksheet with these inputs** opens the template as a scratch sheet and loads the example's inputs through the form's own "Import inputs (CSV/JSON)" path. The sheet is not saved. If the form holds changes you have not saved, the first press says so and the second press for the same step discards them; a form that still holds exactly the loaded example asks nothing.
4. Run it, then compare. Either read the form against the expected outputs yourself, or press **Compare with the form**: it reads the form's current outputs, fills "Values shown", and ticks the result it would give. Nothing is recorded by that. You confirm or change the tick and press **Record result**.
5. Tick **Pass**, **Fail**, **Blocked** or **Skipped**, write a note (up to 2,000 characters) and press **Record result**. Record again on the same step to replace the result. **Finish the run** when you are done: it is dated and read-only from then on.
6. **Copy results sheet** or **Save results sheet…** gives the run as markdown in the runbook's section 11 table (Step, Item, Result, Value shown, Expected, Note), headed with the build, the commit, the tester and the dates. Send it back with the commit.

**The tolerance rule** (shown on the panel). A number passes when it differs from the expected value by no more than half a unit in the last digit of the less precise of the two, counting the digits as written (the form shows 5 significant figures). A text, such as a governing PASS or FAIL, must be identical. A different unit is left to you.

Some worked examples (12 of 87) print an expected value that is a hand calculation held to the example's own verification tolerance, so a line can differ from the printed digits although the form shows what the engine computed when the example was made. Compare marks such a line **your call** and ticks nothing; it never calls it a fail on its own. They are listed in `GuidedAutoCompareTests` (`ForReview`).

**What is kept.** A test run is a record in the `Manual.TestRuns` collection (ADR-0175): run id, started and finished (UTC), build, build commit, tester, script version and one result per step (step id, outcome, note, values shown, time). Nothing else is written.

**For the verification dashboard.** `IManualTestStatusSource.LatestByTemplateAsync()` (in `Tempest.Calc.Library.ManualTesting`) returns, per worksheet template, the worst of the latest result of each of its worked examples (Fail, then Blocked, then Pass), the date, the commit and the tester of the most recent, and how many of the template's examples have a result. It is read-only. `WorkspaceHost.ManualTests` implements it.

**Keeping the script current.** The steps come from `scripts/generate-guided-test-script.py`, which reads the worked examples and `docs/testing/v0.26.0-runbook.md` and writes `src/Tempest.Calc/Tempest.Calc.Library/ManualTesting/guided-test-script.json` (embedded in the library, because `docs/` is not in an installed build). After a change to a worked example or the runbook, run `python3 scripts/generate-guided-test-script.py`; `python3 scripts/generate-guided-test-script.py --check` says whether the shipped script is stale, and `GuidedTestScriptStalenessTests` fails when it is.

**Limits.** The script: 400 steps, 200 expected outputs, 400 input rows, 60 instruction lines and 2,000 characters a field, 200,000 characters of inputs, 4 MB. A run: a 2,000-character note (longer is refused, not cut), 4,000 characters of values shown (longer is cut with a mark), 1,000 results, 200 characters for the tester, build and script version; the store keeps 1,000 runs. The search box takes 100 characters.
