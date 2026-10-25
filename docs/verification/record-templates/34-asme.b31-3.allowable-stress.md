# 34 asme.b31-3.allowable-stress

Kind: `pipe.allowable-range` (allowable displacement-stress range).
Status: existing CalcRecordKind `pipe.allowable-range`
Planned name: `asme.b31-3.allowable-stress`. The built calculator names the kind `pipe.allowable-range`. The file name stays.

## Used by
- B8 pipe expansion (built, `bs.pipe-expansion`) judges the stress in each leg against Sa.

## Symbol mapping
- `Sa` fill the B8 input of the same symbol.
- `basis` identifies the row. The importer stores it and the calculator never reads it.
- The importer saves one record per row. Pick the row that matches your case.
- B8 ships no allowable stress and no default. The sheet blocks until you enter Sa.
- Origin is `Derived`. Put the formula and the S_c, S_h and f values in notes.
- B8 does not read the table values S_c and S_h. Template 35 covers the factor f.

## Copy from
- Work out Sa from your piping code formula. For ASME B31.3, use Table A-1 stresses and the Table 302.3.5 factor.
- Type the result for the material and the cold and hot temperatures of your line.
- Cite the code, its edition and the clause.

## Columns
- `basis[text]`: material, temperatures, code edition and formula behind the value.
- `Sa[MPa]`: allowable displacement-stress range.

## Edition to record
Record the code edition and addenda. Note the units of the tables you used.

## Importer checks
- Reject a blank basis. Reject a duplicate basis row.
- Reject a blank or non-positive Sa. B8 refuses Sa outside 0.1 to 5000 MPa.
- Warn when Sa is outside 5 to 500 MPa.

## Maps to CalcDataRecord
Kind is `pipe.allowable-range`. The importer saves one CalcDataRecord per CSV row.
Each numeric column becomes one CalcDataValue with its Symbol, Number and Unit.
The `basis` text joins the record title. Columns the kind does not list are stored but never applied.
