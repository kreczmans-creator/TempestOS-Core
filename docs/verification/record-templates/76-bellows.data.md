# 76 bellows.data

Kind: `bellows.data` (axial bellows data).
Status: existing CalcRecordKind `bellows.data`

## Used by
- B8 pipe expansion (built, `bs.pipe-expansion`) uses the bellows area, spring rate and rated movement for the axial reaction.

## Symbol mapping
- `A_eff`, `k_b`, `dL_rated` fill the B8 inputs of the same symbol.
- `bellows` identifies the row. The importer stores it and the calculator never reads it.
- The importer saves one record per row. Pick the row that matches your case.

## Copy from
- Type the values from the bellows manufacturer's datasheet.
- Cite the datasheet, its date and the page.

## Columns
- `bellows[text]`: bellows model, as the datasheet names it.
- `A_eff[cm2]`: effective area.
- `k_b[N/mm]`: axial spring rate.
- `dL_rated[mm]`: rated axial movement.

## Edition to record
Record the datasheet date and revision.

## Importer checks
- Reject a blank bellows. Reject a duplicate bellows.
- Reject a blank or non-positive A_eff or dL_rated. k_b may be zero.
- B8 refuses A_eff outside 0.01 to 1e6 cm2 and k_b outside 0 to 1e6 N/mm.
- B8 refuses dL_rated outside 0.01 to 10000 mm.
- Warn when A_eff is outside 1 to 5000, k_b outside 1 to 5000 or dL_rated outside 1 to 500.

## Maps to CalcDataRecord
Kind is `bellows.data`. The importer saves one CalcDataRecord per CSV row.
Each numeric column becomes one CalcDataValue with its Symbol, Number and Unit.
The `bellows` text joins the record title. Columns the kind does not list are stored but never applied.
