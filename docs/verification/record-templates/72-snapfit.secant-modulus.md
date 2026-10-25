# 72 snapfit.secant-modulus

Kind: `snapfit.secant-modulus` (secant modulus at the design strain).
Status: existing CalcRecordKind `snapfit.secant-modulus`

## Used by
- M8 snap-fits (built, `mech.snap-fit`) uses Es for the deflection force and the forces derived from it.

## Symbol mapping
- `Es` fill the M8 input of the same symbol.
- `basis` identifies the row. The importer stores it and the calculator never reads it.
- The importer saves one record per row. Pick the row that matches your case.
- For an annular snap, read Es at the nominal hoop strain. The temperature of use is the input `T_use`.
- A W3 material curve (template 64) can supply Es(eps) instead.

## Copy from
- Read Es from the plastic's stress-strain curve at the design strain and temperature.
- Cite the curve, its source and its edition.

## Columns
- `basis[text]`: plastic, temperature and design strain that Es was read at.
- `Es[MPa]`: secant modulus at the design strain.

## Edition to record
Record the data sheet edition and the test temperature.

## Importer checks
- Reject a blank basis. Reject a duplicate basis.
- Reject a blank or non-positive Es. M8 refuses Es outside 0.01 to 1e6 MPa.
- Warn when Es is outside 10 to 20000 MPa.

## Maps to CalcDataRecord
Kind is `snapfit.secant-modulus`. The importer saves one CalcDataRecord per CSV row.
Each numeric column becomes one CalcDataValue with its Symbol, Number and Unit.
The `basis` text joins the record title. Columns the kind does not list are stored but never applied.
