# ADR-0166: A Checked Calculation Is Never a Dead End, and a Defaulted Sheet Says So

## Status

Accepted (v0.26.0, Phase 7). Supersedes three Phase 1 test expectations in `Tempest.Calc.Tests.Sheets` and `Tempest.Calc.Tests.Library`; raised by the Black team's black-box review (`docs/reviews/black-team.md`, B-01, B-12, B-13, B-17, B-18, B-20).

## Context

The Black team drove the v0.26.0 calculation surface through its public API with no specification and found four ways a sheet could mislead or strand its user:

1. A draft checked with a Rejected outcome moved its Evidence record to Checked (Core's check record always does) while the instance stayed Draft. Every later check was refused as "Checked; it cannot be checked from that status". The calculation could never be corrected and issued (B-12).
2. A draft whose inputs lacked provenance could be checked; the issue was then refused, the inputs could not be changed ("only a draft takes new inputs") and the record could not be revised ("only an issued calculation can be revised") (B-13).
3. An empty, truncated or mis-named inputs file ran on template defaults and produced a passing sheet whose only sign was a `default` source per row (B-01).
4. Table inputs (nodes, elements, loads, vertices, contributors) appeared on the sheet as "4 row(s)", so a checker could not verify the model from the sheet; utilisation checks printed the ratio as a demand (`3.99 vs 1`); valid ranges were printed in the template's unit, not the user's (B-17, B-18, B-20).

Three Phase 1 tests encoded the old behaviour: `Missing_Input_With_Default_Is_Fine` asserted no notice at all, `Checks_Show_Governing_First_With_Pass_Fail` and the `direct-stress` golden asserted the `0.375 vs 1` form, and `Issue_Is_Refused_Without_Provenance` asserted that the check succeeds and only the issue refuses.

## Decision

1. **Check demands what Issue demands.** `CalcInstanceService.CheckAsync` and `CheckPackAsync` refuse a draft whose inputs lack sufficient provenance, with the same message the issue would give. A checked calculation can therefore always be issued or revised.
2. **A rejection never touches the Evidence.** A Rejected check is recorded on the instance (`CalcInstance.LastRejection`: checker, organisation, date, statement); the instance and its Evidence stay Draft, so the calculation can be corrected, checked again and issued. Core's `RecordCheckAsync` is unchanged.
3. **A defaulted input is a warning.** `CalcInputValidator` raises one Warning per non-optional input that takes its template default, naming the default. The Desktop form seeds every input explicitly, so the warning appears only for a file or API caller that left the input out. A block is still never raised for a defaulted input.
4. **Table rows are on the sheet.** `CalcSheetBuilder` writes up to 100 rows of each table input under its summary row (`nodes[1]  x = 0 m; y = 0 m; support = pinned`) and counts the rest.
5. **Utilisation checks read as ratios.** `CheckUtilisation` writes `u = 0.375 vs u ≤ 1`; condition checks keep their real demand and capacity texts.
6. **Valid ranges follow the entered unit** when the unit parses and shares the template unit's dimension.
7. **Empty checker, organisation, issue reference and client are refusals**, not exceptions, from the Library service.

The three tests are amended to the new behaviour, with new tests for the rejection path and the empty-field refusals; the goldens and the worked-example sheets are regenerated. No stored data changes shape: `LastRejection` is an optional field of the instance document (D-26 still holds).

## Consequences

- Sheets and goldens change text (table rows, `u =` checks, ranges in the entered unit, warnings for defaulted inputs). Issued sheets from earlier builds keep their stored text; only new renders differ.
- A pack check also refuses when any member lacks provenance.
- A headless caller that relies on template defaults now sees warnings on every run; the Desktop form does not.
- The Evidence timeline no longer records a rejected check; the rejection is on the instance and in the checker's own words. If the governance process later wants rejections on the Evidence record, Core needs a Draft-preserving check outcome first.
