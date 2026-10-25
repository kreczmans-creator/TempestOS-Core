# desktop.calc-sheet-view — seeded unit handling (Track P, P1 side finding)
Component: Desktop calculation sheet form. Source file: src/Tempest.Desktop/CalcSheets/CalcSheetView.cs (BuildField, UnitChoices).
Found while writing the P4 manual tests; the coordinator read the code to confirm it. No probe exists for the Desktop form.
Verdict: FINDING

## What the code does
- BuildField fills a quantity field from a seed value: an imported JSON input, a worked example or a reopened instance.
- The text box takes the seed's number unchanged.
- The unit box offers the template default unit plus those of about 90 common units whose dimension matches.
- If the seed's unit is not in that list, the unit box shows the default unit instead.
- The number is not converted, and no notice is raised.

## Findings
| ID | Severity | Reviewed | Finding | Evidence | Suggested fix (for Track S) | Status @ 614ddf89 | Status @ 0772fcf8 |
|---|---|---|---|---|---|---|---|
| P1-desktop.calc-sheet-view-01 | High | High (coordinator) | A seeded quantity whose unit is valid but not in the pick list keeps its number and takes the default unit. The value silently changes scale. | CalcSheetView.cs BuildField: `unit.SelectedItem = seed?.Unit is { } u && units.Contains(u) ? u : spec.DefaultUnit`, while the text keeps `seed.Number`. Example: `{"Symbol":"T","Number":50,"Unit":"N*mm"}` for a field with default `N*m` shows 50 N·m, 1000 times too large. `N*mm`, `kN*mm`, `MW` and `daN` are absent from CommonUnits. | Convert the seed number to the default unit with UnitCatalog when the unit is not listed, or add the seed's unit to the list. Raise a warning naming the input when conversion happens. Add a Desktop test. | Open. Diff read: the line `unit.SelectedItem = seed?.Unit is { } u && units.Contains(u) ? u : spec.DefaultUnit` is unchanged (now line 773), and the text still takes seed.Number. | Open. Diff read: CalcSheetView.cs has no change between 298ade36 and 0772fcf8. The seeded-unit line is unchanged (now line 777, the `unit.SelectedItem = …` assignment). The text box still takes seed.Number. CalcInputJson.cs gained a lenient yes/no converter and a refusal of withheld licensed values; neither touches units. |
| P1-desktop.calc-sheet-view-02 | Medium | Medium (coordinator) | A seed with an unknown unit text (for example `kn`) shows the default unit and runs. The engine path refuses it (Black team B-05), but the form path does not. | Same line as -01. | Refuse the seed with the engine's unknown-unit message, as the JSON import already does. | Open. Same unchanged line and the same UnitChoices list; no refusal added. | Open. Same unchanged line and the same UnitChoices list. CalcInputJson.Read(json, template) adds no unit check, so an unknown unit text still reaches the form. |
| P1-desktop.calc-sheet-view-C1 | Low | New | The new Attach button files any chosen file as 'application/pdf'. The coordinator hard-codes the content type and the service checks only size and name, not that the bytes are a PDF. A non-PDF file from an unfiltered picker is stored and labelled as a PDF. Found by reading, not run. | CalcSheetCoordinator.PickAndAttachAsync passes "application/pdf"; CalcInstanceService.AttachExternalEvidenceAsync checks length and file name only. | Check the first bytes for the %PDF- header before filing, or take the type from the file. | Open (new) | Open. PickAndAttachAsync still passes "application/pdf" (line 234) and no byte check was added. The picker filters on *.pdf (line 209), which reduces but does not remove the risk. |

## Notes for the owner's manual test
- Import a JSON input file with a torque in N·mm into a shaft or motor sheet. Expect the field to show the converted value or a warning. Today it shows the raw number in N·m.
- The P4 writer could not find a Desktop screen that reopens a saved calculation. Track S should confirm how the owner reopens one; the stale-upstream steps depend on it.

## Independent review
Coordinator review, 2026-10-24. The finding comes from reading BuildField and UnitChoices; no Desktop run was possible here. Track S should reproduce it in the app before fixing.

## Re-check after DA-17/DA-18 and A-4/A-5 (2026-10-24)
- There is no probe for the Desktop form. I read the diff of CalcSheetView.cs and CalcSheetCoordinator.cs.
- The seeded-unit line in BuildField did not change. It is now line 773 and reads the same. Both findings are still Open.
- What changed: a new "Attach external report…" button (A-5). It shows on a draft whose template has a `_ref` text input.
- New sheets also start from `NewSheetInputs` now.
- New: C1 (Low), the attached file is labelled a PDF without any check. I found it by reading only and did not run the Desktop app.
- No script exists for this report, so there was none to run or update.

## Re-check after the Addendum A Blue round (0772fcf8)
- No probe exists for the Desktop form. I read `git diff 298ade36 0772fcf8` for src/Tempest.Desktop/CalcSheets/ and src/Tempest.Calc/Tempest.Calc.Sheets/CalcInputJson.cs.
- The seeded-unit defect is not fixed. CalcSheetView.cs, the file that holds BuildField and UnitChoices, was not changed. The unit line is now at line 777 and reads the same.
- What changed: CalcSheetCoordinator.cs gained a Reopen pack action and limits link targets to declared output ports. It reads staleness once for the whole list and passes the template to the JSON import. CalcSheetView.SavedAndPacks.cs gained the Reopen pack button. CalcInputJson.cs now accepts yes/no text for flags and refuses a withheld licensed value.
- Findings: 3 rows (-01 High, -02 Medium, C1 Low), 0 fixed, 3 open. The High finding -01 is still open.
- New defects: none found in the changed lines. I did not run the Desktop app.
- No script exists for this report.
