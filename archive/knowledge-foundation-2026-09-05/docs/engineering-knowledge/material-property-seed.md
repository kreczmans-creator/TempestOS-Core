# Material Property Seed — v0.3.0

This is the first populated TempestOS engineering-material reference dataset.

## Authority status

**These values are engineering reference seeds, not yet calculation-authoritative design data.**

Values are representative room-temperature values or ranges intended to exercise the TempestOS material model. Final calculation-authoritative records must retain product form, temper/condition, temperature, test basis and an explicit source/provenance record.

For 6061-T6 and 7075-T6, the currently reviewed ASM/MatWeb records provide typical AA values; for example, 6061-T6 is listed with density 2.7 g/cc, tensile yield 276 MPa, ultimate tensile strength 310 MPa and electrical resistivity 3.99e-6 ohm-cm, while 7075-T6 is listed with density 2.81 g/cc, yield 503 MPa, UTS 572 MPa and modulus 71.7 GPa. These are explicitly described by the source as typical data and should not be treated as a substitute for the applicable material/product specification. 

The MatWeb/ASM pages also demonstrate why polymer records must preserve grade and conditioning: nylon datasets show very broad electrical resistivity ranges and substantial property variation between grades/forms.

## Qualitative commercial fields

`cost_tier` is deliberately qualitative:

- `very_cheap`
- `cheap`
- `medium`
- `medium_expensive`
- `expensive`
- `very_expensive`

It is **not a price list**. Cost depends on quantity, form, size, certification, supplier, geography and market conditions.

`availability` is similarly qualitative:

- `very_high`
- `high`
- `medium`
- `low`

It describes general engineering procurement availability, not guaranteed stock.

## Intended next step

Expand the dataset family-by-family and replace candidate-reference values with source-backed property observations. Preserve multiple source observations where they materially differ.
