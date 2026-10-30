# Glossary of terms and symbols

One glossary for all 54 worksheet templates (mechanical, structural, building services, electrical and fluid power). It is part of the
[sheet standard](sheet-standard.md) (Addendum C, item C-2): the terms below are the words a description uses, the quantity-type table fixes the
unit and the least decimals of each kind of number (a result shows at least four and at most five significant figures), the reserved symbols say what a bare letter means, and the last table lists every input, table
column and output symbol of every template, so an engineer who sees `F_r` or `T_amb` on one sheet reads the same thing on the next.

The tables from "Quantity types" on are generated: the contract tests write `glossary-data.json` from the live catalogue
(`TEMPEST_WRITE_GLOSSARY=1 dotnet test tests/Tempest.Calc.Tests.Calculators --filter GlossaryTests`) and `scripts/generate-glossary.py`
turns it into this file. `GlossaryTests` fails when a template adds a symbol the glossary lacks, or uses a glossary symbol for another quantity type,
until both are regenerated and committed with the change. A symbol that two or more templates use also declares what it means in each (`symbol:meaning`, in `GlossaryMeanings.json`), so two meanings of one symbol in one quantity type are listed apart. Edit this introduction by hand; never edit the generated tables.

## Terms

| Term | Means | Symbol form |
|---|---|---|
| Young's modulus | The elastic modulus of a material. Never "modulus of elasticity" or "elastic modulus". A part prefix is added when needed: "Hub Young's modulus". | `E`, `E_<part>` |
| Yield strength | The stress at which the material yields; "0.2 % proof strength" is a yield strength. A part prefix is added when needed. | `f_y`, `f_y_<part>` |
| Ultimate tensile strength | The tensile stress the material fails at. | `f_u`, `f_u_<part>` |
| Design strength | A strength a code gives (a weld throat, a bolt in shear). | `f_<part>_d` |
| Allowable stress | A limit the user chooses. Not a code value. | `sigma_allow` |
| Required X | A demand the result must meet. The input ends `_req`. | `X_req` |
| X limit | A bound that must not be crossed. The input ends `_limit`. | `X_limit` |
| Target X | An aim for sizing only; nothing fails against it. The input ends `_target`. | `X_target` |
| Safety factor | A ratio of capacity to demand, shown to at least four significant figures and rounded down, so it never reads at its target when it is below it. The word "margin" is not used. | `n_<part>`, `s_<part>` |
| Utilisation | Demand divided by capacity, written to 2 decimals (two significant figures below 0.005) and rounded up when it exceeds its limit; above 1 fails. A template declares which of its outputs are utilisations. | `u_<part>` |
| Governing | The check with the highest utilisation. The sheet prints it first and marks its result "(governing)". | |
| Headline result | A result another calculation links to: an output port. Results list these first. | |
| Port | An output offered for linking, named `<area>.<quantity>` (for example `bearing.life_modified`). Its output symbol stays as its symbol and as an alias. | |
| Alias | An old symbol that still resolves to a renamed input or port, so a saved link, a pack binding, an inputs file or a CSV keeps loading. Nothing writes an alias. | |
| Default | The value an empty input takes. It is listed under "Defaults used", apart from the warnings. | |
| Axes (members) | y-y is the major axis and z-z the minor axis in every sheet, as in EN 1993. A section sheet that draws x horizontal says so in its scope. | `I_y`, `I_z` |

## How a symbol is written

- A symbol is ASCII letters, digits and `_`. What follows the first underscore is a subscript: `T_amb`, `F_v_Rd`, `sigma_vm_max`. A Greek letter is spelled out: `sigma`, `tau`, `Phi`.
- A temperature is `T` or `T_<qualifier>` (`T_amb`, `T_max`, `T_db`); a temperature difference is `dT` or `dT_<qualifier>`. Lower-case `t` is time. `theta` is an angle and never a temperature.
- `U` is a voltage, `V` a volume, `Q` a volumetric flow, `P` a power, `F` a force, `Phi` a heat flow, `T` a torque, `n` a rotational speed; the other quantities that used these letters take a qualified symbol (`V_Ed` for a shear force, `eps_rough`, `h_conv`, `mu_visc`, `R_rel`).
- A pressure is `p_<qualifier>` (`p_atm`, `p_int`, `p_ext`, `p_pre`); a bare `p` is not used.
- Two symbols of one sheet never differ only by letter case (`E_bolt` and `E_shaft`, not `E_S` and `E_s`).
- A symbol that was renamed keeps its old spelling as an alias of the input or port, so nothing saved before the rename stops loading.
