# 17 hydraulics.velocity-limits

Kind: `hydraulics.velocity-limits` (Hydraulic line velocity limits).
Status: proposed — no CalcRecordKind yet; Track S confirms symbols when the calculator is built

## Used by
- F1 cylinder circuit (planned) will check line velocity.
- F2 accumulator (built, `fp.accumulator`) lists no velocity record. It does not check line velocity.
- B1 pipe and pump (built) lists no record kind. It may take the limits in the HPU chain.

## Copy from
- Type the limits from the hose or pipe manufacturer's sizing guide, or from a standard you hold.
- Cite the guide title, edition or date, and the table or chart number.

## Columns
- `line[text]`: pressure, return or suction line.
- `p_from[bar]`: lower bound of the pressure band.
- `p_to[bar]`: upper bound of the pressure band.
- `v_max[m/s]`: maximum recommended flow velocity.

## Edition to record
Record the guide edition or date. State the fluid class in notes.

## Importer checks
- Reject a blank or negative number in a numeric column. Reject a non-positive v_max.
- Reject a row where p_to does not exceed p_from.
- Reject overlapping pressure bands within one line type.
- Warn when v_max is outside 0.1 to 15 m/s.

## Maps to CalcDataRecord
Kind is `hydraulics.velocity-limits`. The importer saves one CalcDataRecord for the file.
One CalcDataValue with Symbol `v_limit_table` holds Rows. Each CalcRow has one cell per column, keyed by column symbol.
