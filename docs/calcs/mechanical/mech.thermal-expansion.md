# M10 — Thermal expansion of a restrained bar (`mech.thermal-expansion@1.0.0`)

**In plain terms.** A bar that is heated wants to grow by its expansion coefficient times its length times the temperature rise. If walls stop it, the growth it is denied becomes a squeeze: the force is the denied movement times the bar's stiffness, and the stress is that force over the area. A gap lets the bar grow freely until it closes; a flexible restraint gives a little, so the force is shared by the bar and the restraint in series. Cooling does the same in reverse and puts the bar in tension.

**Scope.** Uniformly heated bar between two restraints, rigid or elastic, with an optional gap. Ported from the legacy `calc.thermal-expansion-stress` (ADR-0167). `E` and `alpha` fill from a released material record.

**Method.** `δ_free = αLΔT`; `δ_e = sign(δ_free)·max(|δ_free| − g, 0)`; `k_bar = EA/L`; `F = δ_e·k_bar` (rigid) or `δ_e/(1/k_bar + 1/k_s)`; `σ = −F/A`; `δ = δ_free − F/k_bar`.

**Inputs.** `L`, `A`, `E`, `alpha`, `dT`, `g`, optional `k_s`, `sigma_allow`. **Outputs.** `delta_free`, `delta_e`, `k_bar`, `F`, `sigma`, `delta`, `u`. **Checks.** |σ| against allowable.

**Limitations.** Constant α, elastic bar; buckling of the compressed bar is not checked (use `struct.column-buckling`).

**Test cases.** The legacy module's five hand calculations (rigid, gap, elastic, cooling, gap not closed).
