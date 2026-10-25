# M10 — Thermal expansion of a restrained bar (`mech.thermal-expansion@1.0.1`)

**In plain terms.** A bar that is heated wants to grow by its expansion coefficient times its length times the temperature rise. If walls stop it, the growth it is denied becomes a squeeze: the force is the denied movement times the bar's stiffness, and the stress is that force over the area. A gap lets the bar grow freely until it closes; a flexible restraint gives a little, so the force is shared by the bar and the restraint in series. Cooling does the same in reverse and puts the bar in tension.

**Scope.** Uniformly heated bar between two restraints, rigid or elastic, with an optional gap. Ported from the legacy `calc.thermal-expansion-stress` (ADR-0167). `E` and `alpha` fill from a released material record.

**Method.** `δ_free = αLΔT`; `δ_e = sign(δ_free)·max(|δ_free| − g, 0)`; `k_bar = EA/L`; `F = δ_e·k_bar` (rigid) or `δ_e/(1/k_bar + 1/k_s)`; `σ = −F/A`; `δ = δ_free − F/k_bar`.

**Inputs.** `L`, `A`, `E`, `alpha`, `dT`, `g`, optional `k_s`, `sigma_allow`. **Outputs.** `delta_free`, `delta_e`, `k_bar`, `F`, `sigma`, `delta`, `u`. **Checks.** |σ| against allowable.

**Limitations.** Constant α, elastic bar; buckling of the compressed bar is not checked (use `struct.column-buckling`).

**Test cases.** The legacy module's five hand calculations (rigid, gap, elastic, cooling, gap not closed).

<!-- hand-arithmetic:begin generated from the worked-example sheet by SpecHandArithmeticTests; do not edit -->
**Worked example, by hand.** Steel bar heated 60 K against a 0.5 mm gap ([sheet](../../worked-examples/mechanical/mech.thermal-expansion/bar-with-gap/sheet.txt)).

```text
delta_free = 0.000012 1/K × 2 m × 60 K = 1.44 mm
delta_e = 1.44 mm − 0.5 mm = 0.94 mm
k_bar = 200 GPa × 500 mm² / 2 m = 50 kN/mm
F = 0.94 mm × 50 kN/mm = 47000 N
sigma = −47000 N / 500 mm² = -94 MPa
delta = 1.44 mm − 47000 N / 50 kN/mm = 0.5 mm
u = abs(-94 MPa) / 150 MPa = 0.62667
Governing: PASS  Thermal stress against allowable: u = 0.62667 vs u ≤ 1  utilisation 0.627
```
<!-- hand-arithmetic:end -->
