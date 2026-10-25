# 64 material.secant-curve

Kind: `material.properties` (W3 material library: secant modulus against strain).
Status: existing CalcRecordKind `material.properties`. The W3 material library owns this kind.

## Used by
- M8 snap-fits (built, `mech.snap-fit`) read the curve as the table input `Es_pts`. The columns are `eps` and `Es`.
- The library also stores it so a later calculator can read the curve for a nonlinear material.

## Symbol mapping
- Each material becomes one value with the Symbol `Es_curve`. Its rows hold the points of the curve.
- Each row has the columns `eps` and `Es`.

## Copy from
- Type the points from the material standard, the supplier's data sheet or your test certificate.
- Cite the source, its edition and the table or figure.

## Columns
- `material[text]`: material name. It must match a material row in template 62.
- `eps[-]`: strain of the point.
- `Es[MPa]`: secant modulus at that strain.

## Edition to record
Record the source edition and the table or figure. The provenance row covers every point.

## Importer checks
- Reject a blank material, eps or Es.
- Reject fewer than two points for one material.
- Reject a negative eps. Reject strains that do not rise strictly from point to point.
- Reject an Es at zero or below. Reject a unit that is not a stress unit.

## Maps to CalcDataRecord
Kind is `material.properties`. The importer adds one CalcDataValue with Symbol `Es_curve` to the material.
That value has Rows with the columns `eps` and `Es`. Each Es cell keeps its unit.
