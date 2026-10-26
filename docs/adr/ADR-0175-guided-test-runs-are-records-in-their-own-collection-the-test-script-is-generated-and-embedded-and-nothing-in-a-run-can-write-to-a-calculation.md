# ADR-0175: Guided Test Runs Are Records in Their Own Collection, the Test Script Is Generated and Embedded, and Nothing in a Run Can Write to a Calculation

## Status

Accepted (v0.26.0, Addendum C, Track S, C-5). Builds on `ADR-0144` and `ADR-0145` (one SQLite store of collections of JSON documents), `ADR-0120` (durable state carries a version, additions only), `ADR-0165` (the worksheet engine, whose "Import inputs (CSV/JSON)" path the guided test reuses) and `ADR-0173` (the Lifting feature flag, which the lifting steps of the script are about).

## Context

Addendum C §C6 C-5 asks for a guided test mode: the app walks the owner through the owner test pack, shows each step, opens the worksheet with the example's inputs loaded, shows the expected outputs and the tolerance, and lets the tester mark Pass, Fail, Blocked or Skipped with a note. The results are kept as a test-run record with the date, the build commit and the tester, and the dashboard (C-4, built at the same time) wants to show "manual test status" per calculator. It must never alter a calculation or an Evidence status.

Five questions follow.

1. Where do the steps come from, given that `docs/` is not in an installed build?
2. What does "compare to the digits shown" mean when the form rounds to five figures and the printed expected value has up to six?
3. Where is a run stored, and how does adding it stay additive?
4. How does the dashboard read the latest manual result without being able to write one?
5. How is "a guided run changes no calculation" made true rather than promised?

## Decision

1. **The script is generated from the docs and embedded.** `scripts/generate-guided-test-script.py` reads each worked example (`inputs.json`, the inputs table of `sheet.txt`, the README "Output | Expected | Computed" table and its governing-check line) and the manual sections of `docs/testing/v0.26.0-runbook.md` (smoke test, linked packs, imports, lifting, legacy records), and writes `src/Tempest.Calc/Tempest.Calc.Library/ManualTesting/guided-test-script.json`, which the library embeds as a resource. The script has a format number and a version (`<release>+<8 hex of a hash of the steps>`), so a run knows which script it was started on. A test (`GuidedTestScriptStalenessTests`) re-reads the docs with its own parsers and fails when the shipped script differs from them; it also runs the generator's `--check` where Python is present. Every size in the script is capped and `GuidedTestScript.Parse` refuses a script over a cap with a one-sentence reason.

2. **One tolerance rule, stated on screen.** A number passes when it differs from the expected value by no more than half a unit in the last digit of the less precise of the two, the digits counted as written; the form shows five significant figures with trailing zeros dropped, so a shown value is taken at its five-figure place whatever zeros were dropped. This is exactly the difference two roundings of one true value can reach, so a correct result never fails and a wrong one that differs in a digit both show does. A text (a governing PASS or FAIL) must be identical. The arithmetic is exact (integers scaled by powers of ten). A different unit is not decided by rule; it is left to the tester. The rule is `GuidedTolerance`; it is not a looser tolerance than the brief's, it is the brief's applied to a form that rounds.

   **A finding the rule exposed.** The expected values in the worked-example READMEs are hand calculations or published figures held to each example's own verification tolerance (`CalcExpectedValue.RelativeTolerance`, 1 % by default, and in one case a placeholder `0 Pa` at 1e9), not to the digits printed. In 12 of the 87 examples one or more outputs differ from the printed expected value by more than half a unit in its last digit although the engine is right (it equals the README's own "Computed" column). The auto-compare therefore puts such a line to the tester ("decide by hand") instead of calling it a fail, and records the rule it applied; it never passes it silently and never fails it. The 12 are listed in a closed list in the tests, so a thirteenth is noticed. Tightening the printed expected values, or adding a tolerance column to the READMEs, is a decision for the owner (it touches all 87 READMEs and every regeneration of them).

3. **A run is a document in a new collection, `Manual.TestRuns`.** The persistence store is a set of collections of JSON documents (`ADR-0144`); a collection comes into being on its first write, so there is no DDL and no change to the store's `SchemaVersion`, and nothing existing is read, rewritten or dropped (the "additive migration" is that: the migration is the absence of one). A run carries its own `Schema` (1): a reader that finds a newer schema, or a document it cannot parse, leaves it out of every list rather than failing, so a database written by a later build still opens. A run holds the run id, started and finished (UTC), the build as the title bar shows it and its short commit, the tester (the person the app signs work off as: display name and sign-in id), the script version and one result per step (outcome, note capped at 2,000 characters and refused, never cut, when longer; values shown capped at 4,000 characters; recorded time). A run is unfinished until it is finished; an unfinished run can be resumed, only by the person who started it and only on the script it was started on; a finished run is never changed. The store keeps at most 1,000 runs and refuses to start another.

4. **The dashboard reads through `IManualTestStatusSource`**, in `Tempest.Calc.Library.ManualTesting` (the non-UI project the Desktop and the tests already reference): one method, `LatestByTemplateAsync`, returning template id to `ManualTestStatus` (outcome, date, build commit, tester, run, examples recorded of examples in the script). A template's status is the worst (Fail, then Blocked, then Pass) of the latest non-skipped result of each of its worked examples, dated by the most recent of them. `ManualTestRunService` implements it; a dashboard that holds the interface cannot write a run.

5. **A guided run cannot write to a calculation.** `ManualTestRunService` is given the persistence store and writes only `Manual.TestRuns`; it holds no calculation service, no Evidence service and no issue state. The worksheet is opened for a step as a scratch, unsaved form (the same path as "Import inputs", which seeds the form and saves nothing); a form with unsaved edits is not replaced without a second press. `GuidedRunNoSideEffectsTests` runs a full guided run over the shipped script beside a draft and an issued calculation and asserts that every other collection, text and byte, is identical afterwards.

## Consequences

- The owner can run the whole pack inside the app and get the results sheet (the runbook's section 11 table, plus the commit, tester and dates) as markdown.
- A change to a worked example, its expected table or the runbook makes `GuidedTestScriptStalenessTests` fail until `python3 scripts/generate-guided-test-script.py` is run. The same command belongs after any regeneration of the worked examples.
- The script adds about 0.5 MB to the library.
- A tester who presses Save on the scratch worksheet creates a calculation as any worksheet user would; that is the worksheet's own act, not the guided test's, and the panel says the form is a scratch.
- Followed up: the dashboard (C-4) can show manual status per template with no further work in this project; a tolerance column for the READMEs would make the 12 examples auto-comparable.
