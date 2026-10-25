# 01 conductor.material

Kind: `conductor.material` (Conductor material constants).
Status: existing CalcRecordKind

## Used by
- E5 fault protection (built) reads every column. E3 voltage drop (built) reads rho_20 and alpha. E7 PCB trace (built) reads rho_20 and alpha only. E4 conductor sizing (built) lists no conductor material record.

## Copy from
- Type the material constants from your licensed copies of IEC 60228 and IEC 60949.
- Type the insulation temperatures from BS 7671:2018 (as amended), Table 43.1.

## Columns
- `material[text]`: label, for example the conductor metal and insulation type.
- `rho_20[ohm*mm]`: resistivity at 20 degC. E3 reads ohm*mm2/m and the engine converts the unit.
- `alpha[1/K]`: temperature coefficient of resistance.
- `Q_c[J/(K*mm3)]`: volumetric heat capacity.
- `beta[K]`: reciprocal temperature coefficient.
- `theta_i[degC]`: initial insulation temperature for the adiabatic check.
- `theta_f[degC]`: final insulation temperature for the adiabatic check.

## Edition to record
Record the edition of each standard. Use origin Derived when you calculate a value instead of reading it.

## Importer checks
- Reject any row with a blank or non-positive number in a numeric column.
- Reject a row where theta_f does not exceed theta_i.
- Warn outside these ranges: rho_20 1e-6 to 1e-4, alpha 0.001 to 0.01, Q_c 1e-3 to 1e-2, beta 100 to 400.
- Warn when theta_i is outside 20 to 250 degC or theta_f is outside 100 to 400 degC.

## Maps to CalcDataRecord
Kind is `conductor.material`. The importer saves one CalcDataRecord per CSV row.
Each numeric column becomes one CalcDataValue with its Symbol, Number and Unit.
The `material` text joins the record title. Columns the kind does not list are stored but never applied.
