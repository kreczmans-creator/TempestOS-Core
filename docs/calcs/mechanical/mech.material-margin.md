# Material strength margin (`mech.material-margin@1.1.0`)

**In plain terms.** A part is safe in static loading while the stress in it stays below what the material can take, with a margin for what is not known. The factor of safety on yield is the yield strength over the working stress; on ultimate strength it is the tensile strength over the stress, which is usually required to be larger because fracture is worse than yielding. Filling the strengths from a released material record makes the margin cite where the strengths came from.

## Scope

Calculates the factors of safety of a material against an applied equivalent stress, on yield and, when given, on ultimate strength. It does not check fatigue or stability.

## Method

- Factor of safety against yield and against ultimate: n_y = f_y/σ; n_u = f_u/σ.
- Governing factor, the ultimate margin scaled to the yield basis by k_u ≥ 1: n_gov = min(n_y, n_u/k_u).
- Utilisation against the required factor: u = n_req/n_gov.

## Inputs

| Symbol | Description | Unit | Default | Usual range | Group |
|---|---|---|---|---|---|
| `sigma` | Applied (equivalent) stress | MPa | 120 | 0.01 to 5000 |  |
| `f_y` | Yield strength | MPa | 300 | 1 to 5000 |  |
| `f_u` | Ultimate tensile strength (optional) | MPa | optional | 1 to 5000 |  |
| `k_u` | Ratio of the ultimate to the yield safety factor required |  | 1.5 | 1 to 3 |  |
| `n_req` | Required factor of safety on yield |  | 1.5 | 1 to 10 |  |

## Assumptions

1. The load is static and the applied stress is the equivalent stress at the critical point. Basis: Fatigue and stability are separate checks.

## Outputs

| Symbol | Description |
|---|---|
| `n_u` | Factor on ultimate |
| `n_y` | Factor on yield |
| `n_gov` | Governing factor |
| `u` | Utilisation |

## Ports

A port keeps its output symbol; a link, a pack binding or an inputs file written with the symbol or an alias still resolves.

| Public name | Symbol | Unit | Description |
|---|---|---|---|
| `material.safety_governing` | `n_gov` |  | Governing factor of safety |
| `material.safety_yield` | `n_y` |  | Factor of safety on yield |
| `material.safety_ultimate` | `n_u` |  | Factor of safety on ultimate strength |

## Checks

- Required factor of safety against the governing factor (`n_req` against `n_gov`); the utilisation is `u = n_req/n_gov`.

## Limitations

Static loading only. The applied stress must already be the equivalent stress at the critical point. `f_y` and `f_u` fill from a released material record. The sheet was ported from the legacy `calc.material-selection-margin` (ADR-0167).

## Changes

1.1.0 (C-2): the scope is two sentences, the assumption is one sentence, the book reference names its author and edition, the sheet declares the ports `material.safety_governing`, `material.safety_yield` and `material.safety_ultimate`, and the check shows the required and the governing factor as values. No calculated value changes.

## References

- Budynas and Nisbett, Shigley's Mechanical Engineering Design, §1-10, 11th ed.: Design factor and factor of safety.

## Test cases

Two hand calculations: `ka-margin-hand` (sigma 120 MPa on f_y 300 MPa and f_u 450 MPa, n_gov 2.5, u 0.6) and `ka-margin-yield-only` (S355 at 250 MPa, n_y 1.42, u 1.0563, fails). The legacy module had property tests only.

<!-- hand-arithmetic:begin generated from the worked-example sheet by SpecHandArithmeticTests; do not edit -->
**Worked example, by hand.** Bracket steel margin on yield and ultimate ([sheet](../../worked-examples/mechanical/mech.material-margin/bracket-steel-margin/sheet.txt)).

```text
n_y = 300 MPa / 120 MPa = 2.5
n_u = 450 MPa / 120 MPa = 3.75
n_gov = min(2.5, 3.75 / 1.5) = 2.5
u = 1.5 / 2.5 = 0.6
Governing: PASS  Required factor of safety against the governing factor: 1.5 vs 2.5  utilisation 0.60  « Shigley §1-10
```
<!-- hand-arithmetic:end -->
