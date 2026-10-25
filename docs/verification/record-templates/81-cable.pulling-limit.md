# 81 cable.pulling-limit

Kind: `cable.pulling-limit` (pulling tension, grip and sidewall limits).
Status: existing CalcRecordKind `cable.pulling-limit`

## Used by
- E8 containment (built, `elec.containment`) checks the pull tension and the sidewall pressure against these limits.

## Symbol mapping
- `T_grip`, `P_lim`, `k_n` fill the E8 inputs of the same symbol.
- `bundle` identifies the row. The importer stores it and the calculator never reads it.
- The importer saves one record per row. Pick the row that matches your case.
- Each cable's own maximum pulling tension goes in the E8 cable table, not here.

## Copy from
- Type the limits from the cable maker's installation data or your pulling procedure.
- Cite the data sheet or procedure, its date and the page.

## Columns
- `bundle[text]`: cable bundle and pulling method the limits apply to.
- `T_grip[N]`: grip or pulling-eye limit of the whole bundle.
- `P_lim[N/m]`: largest sidewall pressure per cable.
- `k_n[-]`: derating of the cable limits when more than 3 cables share one grip.

## Edition to record
Record the data sheet date.

## Importer checks
- Reject a blank bundle. Reject a duplicate bundle.
- Reject a non-positive T_grip or P_lim. k_n must lie above 0 and not above 1.
- E8 refuses T_grip outside 1 to 1e8 N and P_lim outside 1 to 1e7 N/m.
- E8 refuses k_n outside 0.05 to 1.

## Maps to CalcDataRecord
Kind is `cable.pulling-limit`. The importer saves one CalcDataRecord per CSV row.
Each numeric column becomes one CalcDataValue with its Symbol, Number and Unit.
The `bundle` text joins the record title. Columns the kind does not list are stored but never applied.
