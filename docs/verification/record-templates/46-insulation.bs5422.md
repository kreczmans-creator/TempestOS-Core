# 46 insulation.bs5422

Kind: `criteria.insulation` (heat-loss, surface-temperature and dew-point limits).
Status: existing CalcRecordKind `criteria.insulation`
Planned name: `insulation.bs5422`. The built calculator names the kind `criteria.insulation`. The file name stays.

## Used by
- B9 insulation (built, `bs.insulation`) solves the thickness against these limits.

## Symbol mapping
- `q_max_m`, `q_max_A`, `theta_se_max`, `dew_margin` fill the B9 inputs of the same symbol.
- `purpose` identifies the row. The importer stores it and the calculator never reads it.
- The importer saves one record per row. Pick the row that matches your case.
- B9 does not read tabulated thicknesses. It solves the thickness from the limits.
- Leave a limit blank when the specification sets none. B9 ships a default of 25 W/m for `q_max_m` only.

## Copy from
- Type the limits from your project specification or your copy of BS 5422 or ISO 12241.
- Cite the document, its edition and the clause or table.

## Columns
- `purpose[text]`: purpose the limits serve, such as heat conservation, personnel protection or condensation control.
- `q_max_m[W/m]`: largest heat flow per metre of pipe.
- `q_max_A[W/m2]`: largest heat flow per square metre of flat surface.
- `theta_se_max[degC]`: highest surface temperature.
- `dew_margin[K]`: margin kept above the dew point.

## Edition to record
Record the document edition and amendment.

## Importer checks
- Reject a blank purpose. Reject a duplicate purpose row.
- Reject a row with no limit filled. Reject a non-positive limit.
- B9 refuses q_max_m and q_max_A outside 1e-6 to 1e5.
- B9 refuses theta_se_max outside -50 to 300 degC and dew_margin outside 0 to 20 K.
- Warn when q_max_m is outside 1 to 1000 or q_max_A outside 1 to 500.
- Warn when theta_se_max is outside 30 to 70 or dew_margin is above 5.

## Maps to CalcDataRecord
Kind is `criteria.insulation`. The importer saves one CalcDataRecord per CSV row.
Each numeric column becomes one CalcDataValue with its Symbol, Number and Unit.
The `purpose` text joins the record title. Columns the kind does not list are stored but never applied.
