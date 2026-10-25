# 17 hydraulics.velocity-limits

Kind: `cylinder.line-limits` (largest line velocities for the suction, pressure and return lines).
Status: existing CalcRecordKind `cylinder.line-limits`
Planned name: `hydraulics.velocity-limits`. The built calculator names the kind `cylinder.line-limits`. The file name stays.

## Used by
- F1 cylinder circuit (built, `fp.cylinder`) checks the suction, pressure and return line velocities against these limits.

## Symbol mapping
- `v_suc_max`, `v_prs_max`, `v_ret_max` fill the F1 inputs of the same symbol.
- `basis` identifies the row. The importer stores it and the calculator never reads it.
- The importer saves one record per row. Pick the row that matches your case.
- F1 ships placeholder defaults of 1.2, 5 and 3 m/s. A record replaces them and cites the guide.
- B1 pipe and pump (built) and F2 accumulator (built) list no velocity record.

## Copy from
- Type the limits from the hose or pipe manufacturer's sizing guide, your design guide or the component maker.
- Cite the guide title, edition or date, and the table or chart number.
- The older banded layout (line, p_from, p_to) is gone. Enter one row for each pressure band you design in.

## Columns
- `basis[text]`: fluid class and pressure range the limits apply to, as the guide names them.
- `v_suc_max[m/s]`: largest velocity in the suction line.
- `v_prs_max[m/s]`: largest velocity in the pressure line.
- `v_ret_max[m/s]`: largest velocity in the return line.

## Edition to record
Record the guide edition or date. State the fluid class in notes.

## Importer checks
- Reject a blank basis. Reject a duplicate basis row.
- Reject a blank or non-positive limit. F1 refuses a limit outside 0.1 to 20 m/s.
- Warn when v_suc_max exceeds v_prs_max.

## Maps to CalcDataRecord
Kind is `cylinder.line-limits`. The importer saves one CalcDataRecord per CSV row.
Each numeric column becomes one CalcDataValue with its Symbol, Number and Unit.
The `basis` text joins the record title. Columns the kind does not list are stored but never applied.
