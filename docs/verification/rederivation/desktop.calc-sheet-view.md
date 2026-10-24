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
| ID | Severity | Reviewed | Finding | Evidence | Suggested fix (for Track S) |
|---|---|---|---|---|---|
| P1-desktop.calc-sheet-view-01 | High | High (coordinator) | A seeded quantity whose unit is valid but not in the pick list keeps its number and takes the default unit. The value silently changes scale. | CalcSheetView.cs BuildField: `unit.SelectedItem = seed?.Unit is { } u && units.Contains(u) ? u : spec.DefaultUnit`, while the text keeps `seed.Number`. Example: `{"Symbol":"T","Number":50,"Unit":"N*mm"}` for a field with default `N*m` shows 50 N·m, 1000 times too large. `N*mm`, `kN*mm`, `MW` and `daN` are absent from CommonUnits. | Convert the seed number to the default unit with UnitCatalog when the unit is not listed, or add the seed's unit to the list. Raise a warning naming the input when conversion happens. Add a Desktop test. |
| P1-desktop.calc-sheet-view-02 | Medium | Medium (coordinator) | A seed with an unknown unit text (for example `kn`) shows the default unit and runs. The engine path refuses it (Black team B-05), but the form path does not. | Same line as -01. | Refuse the seed with the engine's unknown-unit message, as the JSON import already does. |

## Notes for the owner's manual test
- Import a JSON input file with a torque in N·mm into a shaft or motor sheet. Expect the field to show the converted value or a warning. Today it shows the raw number in N·m.
- The P4 writer could not find a Desktop screen that reopens a saved calculation. Track S should confirm how the owner reopens one; the stale-upstream steps depend on it.

## Independent review
Coordinator review, 2026-10-24. The finding comes from reading BuildField and UnitChoices; no Desktop run was possible here. Track S should reproduce it in the app before fixing.
