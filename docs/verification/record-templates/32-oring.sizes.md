# 32 oring.sizes

Kind: `oring.ring` (O-ring sizes with tolerances).
Status: existing CalcRecordKind `oring.ring`
Planned name: `oring.sizes`. The built calculator names the kind `oring.ring`. The file name stays.

## Used by
- F3 O-ring gland (built, `fp.oring-gland`) reads the ring inside diameter, cross-section and their tolerances.

## Symbol mapping
- `d_r`, `d_r_tol`, `W`, `W_tol` fill the F3 inputs of the same symbol.
- `size` identifies the row. The importer stores it and the calculator never reads it.
- The importer saves one record per row. Pick the row that matches your case.
- F3 ships no size table. One record is one ring size.
- Enter only the sizes you use. The importer saves each row as its own record.

## Copy from
- Type the sizes from SAE AS568, ISO 3601-1 or the supplier's catalogue.
- Cite the table, its edition and the tolerance class.

## Columns
- `size[text]`: ring designation, as the size table prints it.
- `d_r[mm]`: ring inside diameter.
- `d_r_tol[mm]`: inside diameter tolerance, plus or minus.
- `W[mm]`: ring cross-section diameter.
- `W_tol[mm]`: cross-section tolerance, plus or minus.

## Edition to record
Record the standard or catalogue edition.

## Importer checks
- Reject a blank size. Reject a duplicate size.
- Reject a blank or non-positive d_r or W. Tolerances may be zero.
- F3 refuses d_r outside 1 to 1000 mm and W outside 0.5 to 20 mm.
- F3 refuses d_r_tol outside 0 to 5 mm and W_tol outside 0 to 2 mm.
- Warn when W_tol exceeds 0.4 mm.

## Maps to CalcDataRecord
Kind is `oring.ring`. The importer saves one CalcDataRecord per CSV row.
Each numeric column becomes one CalcDataValue with its Symbol, Number and Unit.
The `size` text joins the record title. Columns the kind does not list are stored but never applied.
