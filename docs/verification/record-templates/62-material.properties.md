# 62 material.properties

Kind: `material.properties` (W3 material library: one scalar property set for each material).
Status: existing CalcRecordKind `material.properties`. The W3 material library owns this kind.

## Used by
- S7 LTB (built, `struct.ltb-interaction`) takes `E`, `G` and `f_y` from a material.
- M7 springs (built, `mech.spring`) take `G`, `E`, `rho`, `Rm_A` and `Rm_m` from a material.
- F1 cylinder circuit (built, `fp.cylinder`) takes `E` and `f_y` of the rod.
- F3 O-ring gland (built, `fp.oring-gland`) takes `alpha` and `nu` of the ring compound.
- M8 snap-fits (built, `mech.snap-fit`) take `nu`, `eps_perm_once`, `eps_perm_repeated` and the curve `Es_pts` (template 64).
- S9 plate bending (built, `struct.plate-bending`) takes `E`, `nu`, `rho` and `f_y`.
- B8 pipe expansion (built, `bs.pipe-expansion`) takes `E` and `alpha`. It uses um/(m*K) for alpha.
- B9 insulation (built, `bs.insulation`) takes `lambda`.
- A material can replace templates 39 (the `Rm_A` and `Rm_m` curve) and 12 (the yield strength) for those calculators.

## Symbol mapping
- Each header is a property symbol of the W3 catalogue. The library refuses any other symbol.
- A template input names the property it takes. Applying the material fills those inputs in the input's own unit.
- A blank cell means the material does not state that property. A calculator that needs it then refuses.
- The importer saves one material for each row, at revision 1. A later change saves the next revision.
- The library exports CSV and imports JSON today. Track S confirms the CSV import against this template.

## Copy from
- Type each property from the material standard, the supplier's data sheet or your test certificate.
- Cite the source, its edition and the table or page. Each value carries its own source.
- `Rm_A` is the coefficient in R_m = A/d^m, in MPa for d in mm. `Rm_m` is the exponent m.
- Fit A and m to your own wire data and mark origin Derived. Do not copy a fit you cannot cite.

## Columns
- `material[text]`: material name. It becomes the record title and the name you search for.
- `E[GPa]`: Young's modulus.
- `G[GPa]`: shear modulus.
- `nu[-]`: Poisson's ratio.
- `rho[kg/m3]`: density.
- `alpha[1/K]`: coefficient of thermal expansion.
- `lambda[W/(m*K)]`: thermal conductivity.
- `f_y[MPa]`: yield strength.
- `f_u[MPa]`: ultimate tensile strength.
- `Rm_A[MPa]`: coefficient A of the wire strength curve, for d in mm.
- `Rm_m[-]`: exponent m of the wire strength curve.
- `eps_perm_once[-]`: permissible strain under one-time loading.
- `eps_perm_repeated[-]`: permissible strain under repeated loading.

## Edition to record
The provenance row sets the source, edition and table for every value that has no source of its own.
Fill source_title, edition and table_or_clause. The library refuses a value without them.

## Importer checks
- Reject a blank material or a duplicate material.
- Reject a row with no property filled.
- Reject E, G, rho, lambda, f_y, f_u and Rm_A at zero or below.
- Reject nu outside the range from above -1 to below 0.5.
- Reject Rm_m outside 0 to 5. Reject an eps_perm value at zero or below, or above 1.
- Reject a row whose provenance row lacks source_title, edition or table_or_clause.
- A licensed value makes the whole material licensed. The library never exports its values.

## Maps to CalcDataRecord
Kind is `material.properties`. The importer saves one CalcDataRecord per CSV row.
Each filled column becomes one CalcDataValue with its Symbol, Number and Unit.
The `material` text becomes the record title.
Temperature tables, the secant curve and friction pairs go in templates 63, 64 and 65.
