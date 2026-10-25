# 60 pcb.method

Kind: `pcb.method` (curve-fit constants for PCB trace current).
Status: existing CalcRecordKind `pcb.method`

## Used by
- E7 PCB trace (built, `elec.pcb-trace`) reads all five constants.
- E7 ships with the legacy constants as defaults. A record replaces them and cites the edition.
- E7 also lists `conductor.material` for rho_20 and alpha. Template 01 holds that kind.

## Symbol mapping
- Each header is the symbol of an E7 input. The importer fills the input with the same symbol.
- `method` names the curve fit and its edition. The importer stores it and E7 never reads it.
- The importer saves one record per row. Pick the row for the method you follow.

## Copy from
- Type the constants from your copy of the IPC standard that publishes the curve fit.
- The constants are published closed-form numbers. Cite the standard, its edition and the equation or figure.
- t_oz is the copper thickness for one ounce per square foot. Type it in mil, or derive it and mark origin Derived.

## Columns
- `method[text]`: curve fit and edition, as your source names it.
- `k_ext[-]`: curve-fit constant for an external layer.
- `k_int[-]`: curve-fit constant for an internal layer.
- `n_dT[-]`: exponent of the temperature rise.
- `n_A[-]`: exponent of the cross-section.
- `t_oz[mil]`: copper thickness for one ounce per square foot.

## Edition to record
Record the standard edition and amendment printed on your copy.

## Importer checks
- Reject a blank method. Reject a duplicate method.
- Reject a blank or non-positive number in a numeric column.
- E7 refuses k_ext and k_int outside 0.001 to 1.
- E7 refuses n_dT outside 0.1 to 1 and n_A outside 0.3 to 1.5.
- E7 refuses t_oz outside 0.5 to 3 mil.
- Warn when k_int exceeds k_ext.

## Maps to CalcDataRecord
Kind is `pcb.method`. The importer saves one CalcDataRecord per CSV row.
Each numeric column becomes one CalcDataValue with its Symbol, Number and Unit.
The `method` text joins the record title. Columns the kind does not list are stored but never applied.
