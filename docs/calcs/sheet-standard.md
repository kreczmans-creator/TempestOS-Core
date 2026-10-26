# Calculation sheet standard

Addendum C, item C-2 (consistency). One standard for how every worksheet template (54 of them: mechanical, structural, building services,
electrical and fluid power) lays out its sheet, names its quantities, shows its numbers, words its messages, names its ports, titles itself in the
catalogue, cites its sources and writes its spec page. It adopts the proposals of the consistency audit (`docs/verification/consistency-audit.md`,
60 findings in 12 categories A to L, one row per finding) unless a proposal would break saved data; each place it departs from the audit is a
decision in section 1.

How it is held:

- **Renderer and engine rules** (audit rows 1 to 4, 29 to 31) are applied by `Tempest.Calc.Sheets` and the Desktop PDF renderer to every sheet,
  and tested once in `ConsistencyStandardTests` (Sheets) and `CalcSheetPdfRendererTests` (Desktop). A template does nothing to get them.
- **Catalogue rules** (47, 48) are applied to all 54 templates in this phase and held by contract tests with no exceptions.
- **Template rules** (everything else) are held by the contract tests in `tests/Tempest.Calc.Tests.Calculators/Consistency/`: one rule per row
  of the audit, run over all 54 templates. A template that does not meet a rule yet is named in
  `ConsistencyAllowList.json` (one entry per template and rule). The allow-list **only shrinks**: a template that breaks a rule and is not on the
  list fails the build, and so does a template that is on the list and now meets the rule (remove its entry). Phases B and C (the
  per-template work) empty the list; `docs/calcs/consistency-work-list.md` is generated from it and says what is wrong in each template.
- **The glossary** (`docs/calcs/glossary.md`) is generated from the catalogue; `GlossaryTests` fails when a template uses a symbol it lacks or
  lists under another quantity type.

Each section below is one category of the audit: the rule, one good example, and how it is enforced (the contract test, by rule id).

## 1. Decisions taken in this phase

Where the audit offered options or a proposal met saved data, the choice is recorded here (ids C2-xx; the integrator copies them to DECISIONS).

| Id | Question | Choice | Options not taken |
|---|---|---|---|
| C2-01 | How many sections does a sheet print, and what is in an empty one? | Nine, in both text and PDF: Scope and method, Inputs, Assumptions, Working, Results, Checks, Applicability, Verification, References. An empty section says `None.` The References section replaces the text sheet's `Ref:` lines and is the PDF's numbered list. | The audit's eight sections with references as a line under Scope. |
| C2-02 | One line form for a warning or block. | `BLOCK  <message>  [<rule id>]` and `WARNING  <message>  [<rule id>]`, in text and PDF (audit row 1). | The old `BLOCK   <id>: <message>` and `BLOCK — <id>: …`. |
| C2-03 | What does a yes/no check print where a ratio check prints its utilisation? | The text sheet prints `PASS` or `FAIL` and the check, no utilisation; the PDF column shows `—`. A check carries `HasUtilisation` (false for `CheckCondition`). A ratio check that fails by an infinite ratio still prints `utilisation —`. | Printing `—` in the text line as well (the audit's wording); printing 0. |
| C2-04 | Where do default-value notices go, and in what words? | Under Applicability in their own sub-list, `Defaults used (inputs left empty)`, one line each: `F_r: not entered, so the default of 2 kN is used.` They are `CalcNoticeKind.DefaultUsed`, not warnings (`CalcRunResult.EngineeringWarnings` leaves them out). | The audit's two-sentence example (`F_r was not entered. The default of 2 kN is used.`), which does not fit the one-sentence message shape of row 31; dropping them. |
| C2-05 | Order of the Results. | Headline results first (the output ports, in the order the template declares them), then supporting values in the order produced, then utilisations; the governing result is marked `(governing)`. A template with no ports has no headline group. | Declared order only. |
| C2-06 | How are numbers rounded for display? | By quantity type, by the renderers and `CalcOutput`, from one table (`CalcDisplay.Rules`): fixed decimals per dimension and unit, never fewer than 2 or more than 5 significant figures, zero is `0`, a value below 1e-9 of the largest of its dimension in the same results is shown as `0`. A utilisation is 2 decimals, rounded up above 1. A plain number that is not a utilisation, safety factor, efficiency or power factor keeps five significant figures. A template may override the decimals of one output (`c.Output(symbol, text, decimals: n)`) where it can justify it. | The audit's bare decimals (no limit on significant figures), which print `321190.4 kN/m` and `0.0 MPa`. |
| C2-07 | Message shape. | `<symbol>: <one sentence>.` The symbol is the one input the user changes; a failure that belongs to no one input is written under the symbol `calculation`. The engine wraps every exception in this shape (`CalcMessageShape.Engine`) and the input validator speaks it. Messages of an import file (CSV, JSON) keep their `Row n:` form: they are about a file, not an input. | Free wording with the symbol anywhere. |
| C2-08 | Severity. | A rule's emitted severity equals its declared severity; every emitted rule id is declared once; a broken engineering limit is a check or a block, not only a warning; a block is for a method that gives no result and a warning for a documented fallback (rows 34 to 36). | Warnings for broken limits. |
| C2-09 | Port names and old symbols. | A port keeps its output symbol (what saved links and stored outputs are keyed by) and gains a public name `<area>.<quantity>` (`CalcOutputPort.Name`) and optional aliases. A link, a pack binding or an inputs file that names the old symbol, the public name or an alias resolves to the same output. Nothing writes an alias. A new link made in the Desktop stores the output symbol, so a build before this one can still read it. | Renaming the output symbol (breaks every saved link); storing only the new name. |
| C2-10 | Renaming an input. | An input renamed by phases B and C keeps its old symbol in `CalcInputSpec.Aliases`. An inputs file, a CSV, a saved draft carried to a newer version, a pack binding or example and a link bound by input symbol accept the old symbol and store the new one. Phase A renames no input. | Renaming without an alias; a migration of every stored instance. |
| C2-11 | Which templates need a version bump? | Those whose content hash changes: a changed title, scope, port name or text. Patch for text, minor for ports. A category change alone is not part of the hash and does not bump, so a draft is not offered an upgrade for a catalogue label. | Bumping all 54. |
| C2-12 | Units. | One standard unit per quantity type (section 4), shown as symbols (`°C`, `Ω`, `kN·m`, `m²`) on the sheet, in notices and in the glossary; ASCII stays accepted as input. Named exceptions: forces in N for the small-force templates (springs, snap-fits, mechanisms, drive sizing, containment, enclosure door force), N·m for drive torque, mil for PCB, and in fluid power bar (absolute or gauge), L/min and cm³/rev. | One unit with no exceptions (forces of 4 N would print `0.0045 kN`). |
| C2-13 | Reserved symbols. | The bare letters of section 3 mean one quantity each; the other quantities take a qualified symbol. `theta` is an angle only. | Leaving the letters shared. |
| C2-14 | Catalogue titles and categories. | `<Subject> (<standard or method>)`, sentence case, no year, no version, no colon list; categories are the per-discipline lists of section 9, one word each; bolt, weld and snap-fit connections are `Connections` in mechanical as in structural. Applied to all 54 templates now. | Leaving them to phases B and C. |
| C2-15 | Record kind ids. | Left as they are: the id is stored on every record of its kind, so renaming it needs a migration. The contract holds only that the title repeats the noun of the id (`H46`). | The audit's `<area>.<plural noun>` ids. |
| C2-16 | Group names. | `Setup`, `Geometry`, `Material`, `Loads`, `Conditions`, `Factors`, `Requirements` in that order; a domain group (`Pump`, `Fan`) may follow. Retired synonyms and numbered groups are listed in `G40`. | A closed list of seven. |
| C2-17 | Contract tests that most templates fail today. | Held by the allow-list (section 2). | Failing the build; skipping the tests. |
| C2-18 | Display precision of a yes/no check and of `Working`. | Working steps keep five significant figures (they show the arithmetic); only Results, Checks and messages use the precision table. | Applying the table to working. |
| C2-19 | Where is the standard written? | This page, the glossary, and the code tables they are generated from or tested against (`CalcDisplay.Rules`, `CalcUnitStandard`, `CalcSymbolStandard`, `CalcPortNames`, `CalcCatalogueStandard`). A test holds each page to its table. | Prose only. |

## 2. Contract tests and the allow-list

`ConsistencyRules.All` holds one rule per audit row that a program can check. `ConsistencyContractTests` runs each rule over all 54 templates:
a template that breaks it is on the allow-list or the test fails; a template on the list that no longer breaks it fails the test too. To rewrite the
list and the work list after fixing templates:

```
TEMPEST_WRITE_CONSISTENCY_ALLOWLIST=1 dotnet test tests/Tempest.Calc.Tests.Calculators --filter ConsistencyContractTests
```

Never add an entry. A reviewer rejects a diff that does. The rules with no entry hold for every template today: `E29`, `E30`, `F34`, `I47`, `I48`.

## A. Layout and section order (rows 1 to 4)

**Rule.** Text and PDF sheets print the same nine headings in the same order, in sentence case:
`1. Scope and method`, `2. Inputs`, `3. Assumptions`, `4. Working`, `5. Results`, `6. Checks (governing first)`, `7. Applicability`,
`8. Verification`, `9. References`. A section with nothing in it says `None.` A notice is one line, `BLOCK  <message>  [<rule id>]` or
`WARNING  <message>  [<rule id>]`. Results list the headline results (the ports) first, then supporting values, then utilisations, and mark the
governing result `(governing)`. A check line is `PASS|FAIL  <name>: <demand> vs <capacity>  utilisation <u>`; a yes/no check has no utilisation. An
input left empty that has a default is listed under Applicability, in `Defaults used (inputs left empty)`, one sentence each; it is not a warning.

**Good example** (`mech.bearing-life`, text sheet):

```
5. Results
  L_nmh = 10632 h    Modified rating life
  L_10h = 16667 h    Basic rating life
  P = 2.00 kN    Equivalent dynamic load
  u_life = 0.94    Life utilisation (governing)

6. Checks (governing first)
  PASS  Modified rating life against required life: u_life = 0.94 vs u_life ≤ 1  utilisation 0.94  « ISO 281:2007 cl. 9

7. Applicability
  WARNING  records: tabulated values not taken from a reference record are C, C_0 (Bearing load ratings); e_lim, X_1, Y_1, X, Y, X_0, Y_0 (Bearing X, Y, e factors); a_iso (Bearing a_ISO). Enter each as one so its source, edition and licence travel with it.  [record]
```

The excerpt is the bearing-life golden (`tests/Tempest.Calc.Tests.Calculators/Golden/mech.bearing-life.txt`) with the ports first and the governing result marked.
A sheet whose inputs were left empty adds, under Applicability (form from the Sheets test calculator):

```
  Defaults used (inputs left empty):
    A: not entered, so the default of 200 mm² is used.
```

**Enforced by** `CalcSheetStandard` (the headings, `None.`, the line forms and the order of the Results are written once and used by both renderers).
Tests: `ConsistencyStandardTests.The_Text_Sheet_Prints_Every_Section_In_The_Standard_Order_Even_When_It_Is_Empty`,
`A_Notice_Is_One_Line_In_The_Standard_Form_With_The_Rule_Id_Last`, `Results_List_The_Ports_First_Then_The_Supporting_Values_Then_The_Utilisations`,
`The_Governing_Result_Is_Marked_And_A_Yes_No_Check_Prints_No_Utilisation`, `Default_Notes_Are_Listed_Apart_From_The_Warnings_In_Plain_Words_With_Unit_Symbols`
(Sheets); `CalcSheetPdfRendererTests.The_Pdf_Prints_The_Same_Headings_In_The_Same_Order_As_The_Text_Sheet` and the neighbouring PDF tests (Desktop);
the 54 golden sheets (`Golden_Sheet_Matches`) fail on any drift. The two things a template itself writes in row 3, the name of a check and its demand and capacity as physical values,
are the contract rules `A03-check-names` and `A03-check-shows-physical-values`.

## B. Terminology (rows 5 to 9)

**Rule.** Use the terms of the glossary. The elastic modulus is `Young's modulus` (with a part prefix when needed: `Hub Young's modulus`).
Strength is `Yield strength` and `Ultimate tensile strength` with a part prefix, `Design strength` for a code value, `Allowable stress` only for a
limit the user chooses. `Required X` (`X_req`) is a demand the result must meet, `X limit` (`X_limit`) a bound not to be crossed, `Target X`
(`X_target`) a sizing aim; a ratio is a safety factor and the word "margin" is not used. A description is plain words: it does not repeat its
symbol, and a unit appears only in the unit field. A hint for an optional input is `(optional; blank = <what happens>)`.

**Good example.** `E` — `Young's modulus`; `L_req` — `Required life`; `x_A` — `Bearing A position`; `kappa` — `Viscosity ratio (optional; blank = 1)`.

**Enforced by** `B05-youngs-modulus`, `B06-strength-words`, `B07-requirement-words`, `B08-description-pattern`, `B09-optional-hint`.

## C. Symbol names (rows 10 to 19)

**Rule.** The bare letters of the glossary's "Reserved symbols" table mean one kind of quantity wherever they appear: `T` torque or moment, `t` time,
`theta` an angle (never a temperature), `V` volume, `Q` volumetric flow, `P` power, `U` voltage, `F` force, `Phi` heat flow, `mu` friction, `rho`
density, `nu` Poisson's ratio, `eta` efficiency, `eps` effectiveness, `h` depth, `n` rotational speed, `R` resistance, `D` diameter, `L` length, and a bare
`p` is not used. A temperature is `T` or `T_<qualifier>` (`T_amb`, `T_max`, `T_db`) and a difference is `dT`. Another quantity takes a qualified symbol
(`V_Ed`, `eps_rough`, `h_conv`, `mu_visc`, `rho_res`, `R_rel`, `t_f` for a flange). Two symbols of one sheet never differ only by letter case after a
subscript. One symbol per quantity across sibling sheets: `cos_phi` (not `cosphi`), `M_z`, `F_v_Rd`, `F_t_Rd`, `v_limit`. Members use the EN 1993 axes in every
sheet: y-y is major and z-z minor; a section sheet that draws x horizontal says so in its scope.

**Good example.** `T_amb` (ambient temperature, °C), `T` (torque transmitted, kN·m), `t_f` (flange thickness, mm), `p_atm` (atmospheric pressure, kPa).

**Enforced by** `C10-temperature-symbols`, `C11-theta-is-an-angle`, `C12-torque-and-time`, `C13-no-case-only-pairs`, `C14-quantity-letters`, `C15-no-bare-p`,
`C16-greek-letters`, `C17-single-letters`, `C18-sibling-symbols`, `C19-iso-axes`; and `GlossaryTests` (a symbol absent from the glossary, or listed under
another quantity type, fails; every symbol with several meanings is marked `RESOLVE` for phases B and C). Renames in B and C keep the old symbol as an alias
(section H).

## D. Units for the same quantity type (rows 20 to 28)

**Rule.** One unit per quantity type, the same wherever the quantity appears, so a linked output and the input it feeds agree:

| Quantity type | Unit | Named exceptions |
|---|---|---|
| Force | `kN` | `N` in `mech.spring`, `mech.snap-fit`, `mech.mechanism`, `elec.motor-sizing`, `elec.containment`, `elec.enclosure-heat`, `fp.*` |
| Line force, distributed load, stiffness | `kN/m` | `N/mm` in `mech.spring` and `mech.weld-group`; `N/m` in `elec.containment` |
| Moment, torque | `kN·m` | `N·m` in `elec.motor-sizing`, `mech.mechanism`, `mech.spring`, `mech.snap-fit`, `fp.*` |
| Moment per unit length | `kN·m/m` | |
| Stress, pressure | `Pa` (a pressure drop), `kPa` (a fluid pressure), `MPa` (a stress), `GPa` (a modulus) | `bar`, `bar a`, `bar g` in `fp.*` |
| Length | `m` (spans, coordinates, building-services lengths), `mm` (part and section sizes, deflections), `µm` | `mil` in `elec.pcb-trace` |
| Area | `cm²` (member and section properties), `mm²` (stress areas, bolt areas, conductor sections), `m²` | `mil²` in `elec.pcb-trace` |
| Second moment of area | `cm⁴` (members and sections), `mm⁴` (bolt and weld groups) | |
| Warping constant | `cm⁶` | |
| Volume, section modulus | `L` (a volume), `cm³`, `mm³`, `m³` | `cm³/rev` in `fp.*` |
| Volumetric flow | `m³/h` | `L/min` in `fp.*` |
| Power, heat flow | `kW` (3 decimals below 1 kW) | `mW`, `W` in `elec.pcb-trace` |
| Apparent and reactive power | `kVA`, `MVA`, `kvar` | |
| Resistivity | `Ω·mm²/m` | |
| Temperature, difference | `°C`, `K` | |
| Percent quantity | `%` (a percentage is a quantity in %, never a bare 0 to 100 number, and the word is never `percent`) | |
| Angle | `deg` (a slope is `rad`) | |

The glossary's quantity-type table is generated from `CalcUnitStandard`. Units are shown as symbols (`°C`, `Ω`, `kN·m`, `m³/h`) in every column, notice and
message; ASCII (`degC`, `ohm`, `kN*m`) is accepted as input only.

**Good example.** `F_p` — `Process force opposing the move`, `kN` (not `N`); `rho_20` — `Conductor resistivity at 20 °C`, `Ω·mm²/m`, default 0.01724.

**Enforced by** `D20-units-per-quantity-type` (every input, table column, output and port against `CalcUnitStandard`), `D27-unit-spelling-in-text`, `D28-percent`.
The sheet shows symbols by `CalcUnitText.Pretty` (Sheets test `Units_Are_Written_As_Symbols_On_The_Sheet`).

## E. Decimals and significant figures (rows 29 and 30)

**Rule.** The decimals depend on the quantity type, not on the template: one table (`CalcDisplay.Rules`, in the glossary as "Quantity types: units and
decimals"). A force is 2 decimals in kN, a stress 1 in MPa, a length 1 in mm and 2 in m, a temperature 1, a time 2 s and 0 h. Never fewer than two or more than
five significant figures; zero is `0`; a value more than nine orders of magnitude below the largest of its dimension in the results is solver noise and is
shown as `0` (`1.8626e-15 MPa` beside `144.9 MPa`). A utilisation, a safety factor, an efficiency and a power factor are 2 decimals; a utilisation above 1 is
rounded up, so a failing check never prints `1.00`. The same rule applies in Results, Checks and the Desktop form. Rounding is for display only: no value,
output, stored output, hash or export is rounded.

**Good example.** `sigma_t_a = 144.9 MPa`, `T = 2194.8 N·m`, `u_life = 0.94`, `sigma_r_b = 0 MPa`.

**Enforced by** `ConsistencyStandardTests.A_Scalar_Is_Shown_At_The_Decimals_Of_Its_Quantity_Type`, `Display_Rounds_The_Text_Only_And_Never_The_Value`,
`A_Value_Below_A_Billionth_Of_The_Largest_Of_Its_Dimension_Is_Shown_As_Zero`, `A_Utilisation_Is_Shown_To_Two_Decimals_And_A_Failing_One_Is_Rounded_Up`,
`Every_Rule_Of_The_Table_Names_A_Unit_The_Engine_Parses_In_Its_Dimension_And_Is_Listed_Once`; contract rules `E29-output-unit-has-a-precision` and
`E30-utilisation-two-decimals` (no exceptions); `GlossaryTests.The_Quantity_Type_Table_States_The_Units_And_Decimals_Of_The_Code`.

## F. Warning and block wording (rows 31 to 37)

**Rule.** A message is `<symbol>: <one plain sentence>.` The symbol is the one input the user changes; there is one symbol, not `F_r/F_a`. A missing input reads
`<symbol>: enter <thing>.` A message states the condition in sentence 1 and, if it helps, the action in sentence 2; no semicolon; each sentence at most 20 words.
A rule's emitted severity equals its declared severity and every emitted rule id is declared once, without the engine's `input.` prefix. A block is for a method that
gives no result; a warning for a documented fallback, named in the message. A broken engineering limit is a check, or a block, never only a warning. A number in a message
has the decimals of its glossary entry; a range in a sentence is `<low> to <high> <unit>`; no comma as thousands separator.

**Good example.**
`Block  A: enter the heat transfer area.  [area-missing]` · `Warning  kappa: κ above 4 is treated as 4 when a_ISO is read.  [kappa-high]` ·
`Block  segments: enter at least one row of 'Shaft segments'.  [segments]`

**Enforced by** `F31-message-shape` (every literal message in a calculator's source and every notice its known-answer runs raise),
`F32-missing-input-phrasing`, `F33-one-idea-per-message`, `F34-severity-fits-the-condition`, `F35-emitted-severity-equals-declared` (declared severity equals the severity
of every `Warn`/`Block` call), `F35-limit-is-a-check-not-a-warning`, `F36-emitted-ids-declared`, `F36-no-input-prefix`, `F37-numbers-in-text`. The engine wraps its exceptions
with `CalcMessageShape.Engine` (Sheets tests `An_Engine_Failure_Is_Wrapped_In_The_Same_Shape`, `The_Validator_Messages_Follow_The_Shape`).

## G. Input order and grouping (rows 38 to 42)

**Rule.** A sheet with more than five inputs groups every input. The choice and table inputs that define the problem sit in a first group, `Setup`. Groups use one
vocabulary, in this order: `Setup`, `Geometry`, `Material`, `Loads`, `Conditions`, `Factors`, `Requirements`; a domain group (`Pump`, `Fan`, `NPSH`) may follow. A list
group is plural (`Loads`, not `Load`); a group is never numbered; the retired synonyms are `Load`, `Actions`, `Forces`, `Heat load`, `Limits`, `Spacing limits`, `Conductor
material`, `Conductor`, `Plate`, `Pressure`, `Pressures`. An optional input is last in its group. Every numeric input has a usual range (soft limits) inside its hard range;
a soft limit is left out only for a true physical bound.

**Good example.** `Setup` (type, bolts) → `Geometry` (d, t) → `Material` (f_y) → `Loads` (F_r, F_a) → `Factors` (a_1) → `Requirements` (L_req).

**Enforced by** `G38-groups-when-more-than-five-inputs`, `G39-setup-group-first`, `G40-group-names`, `G41-group-order`, `G42-soft-limits`.

## H. Output ports (rows 43 to 46)

**Rule.** A port is named `<area>.<quantity>`: the area is one lower-case word from the table below for the template, the quantity is lower-case words joined by `_`,
most general first (`bearing.life_modified`, `exchanger.temperature_hot_out`). The unit is in the unit field, not the name. A template declares ports for the headline results
only, and a pack binds to declared ports. A port description names its target by template id and input (`→ bearing-life F_r`), never by a legacy code (`→ M5`) or a calculator
that does not exist. A record kind's title repeats the noun of its id.

**Compatibility.** A port keeps its output symbol (`Symbol`), gains a public name (`Name`) and may list aliases (`Aliases`). Every place that resolves a port or an input
accepts any of its names: a saved link (`calc:<id>#<symbol>`), a pack binding or example, `BindAsync`, an inputs file, a CSV, a draft carried to a newer version. To rename an
input or output symbol in phases B and C: change the symbol, add the old one to `Aliases` (`CalcInputSpec.Aliases`, `CalcOutputPort.Aliases`), bump the template's minor version,
and add a test that loads an old-form instance. The registration refuses an alias that names another input or port. Phase A renames no symbol; it names the ports of the nine
calculators the audit lists (`bearing-life`, `shaft`, `weld-group`, `connection-ec3`, `load-combinations`, `load-budget`, `fault-protection`, `heat-exchanger`,
`sealed-system`).

Areas by template (`CalcPortNames.Areas`):

| Template | Area(s) | Template | Area(s) |
|---|---|---|---|
| `mech.tolerance-stack` | `tolerance` | `struct.lifting` | `lifting` |
| `mech.iso286-fit` | `fit` | `struct.column-buckling` | `column` |
| `mech.press-fit` | `press` | `struct.fillet-weld` | `weld` |
| `mech.thick-cylinder` | `cylinder` | `bs.pipe-pump` | `pipe`, `pump` |
| `mech.lifting-lug` | `lug` | `bs.psychrometrics` | `air` |
| `mech.bearing-life-basic`, `mech.bearing-life` | `bearing` | `bs.heat-exchanger` | `exchanger` |
| `mech.fatigue-miner` | `fatigue` | `bs.sealed-system` | `heating` |
| `mech.shaft-stress`, `mech.shaft` | `shaft` | `bs.room-heat-loss` | `room` |
| `mech.thermal-expansion` | `expansion` | `bs.duct-sizing` | `duct` |
| `mech.thin-vessel` | `vessel` | `bs.glaser` | `condensation` |
| `mech.material-margin` | `material` | `bs.pipe-expansion` | `piping` |
| `mech.bolted-joint` | `joint` | `bs.insulation` | `insulation` |
| `mech.bolt-group` | `boltgroup` | `bs.plane-wall` | `wall` |
| `mech.weld-group` | `weld` | `elec.motor-sizing` | `motor` |
| `mech.spring` | `spring` | `elec.enclosure-heat` | `enclosure` |
| `mech.snap-fit` | `snapfit` | `elec.voltage-drop` | `circuit` |
| `mech.mechanism` | `mechanism` | `elec.load-budget` | `load`, `battery` |
| `struct.section-properties` | `section` | `elec.fault-protection` | `fault`, `conductor` |
| `struct.frame-2d` | `frame` | `elec.conductor-sizing` | `conductor` |
| `struct.beam-formulas` | `beam` | `elec.pcb-trace` | `trace` |
| `struct.en1993-member` | `member` | `elec.containment` | `containment` |
| `struct.load-combinations` | `combination` | `elec.motor-start` | `start` |
| `struct.connection-ec3` | `bolt`, `weld` | `elec.thermal-chain` | `heatsink` |
| `struct.base-plate` | `baseplate` | `fp.cylinder` | `actuator` |
| `struct.ltb-interaction` | `ltb` | `fp.accumulator` | `accumulator` |
| `struct.plate-bending` | `plate` | `fp.oring-gland` | `gland` |

The nine calculators of the audit's port map, as named:

| Template | Symbol → public name |
|---|---|
| `mech.bearing-life` | `L_nmh` → `bearing.life_modified`, `L_10h` → `bearing.life_basic`, `P` → `bearing.load_dynamic`, `P_0` → `bearing.load_static`, `s_0` → `bearing.safety_static` |
| `mech.shaft` | `R_A` → `shaft.reaction_a`, `R_B` → `shaft.reaction_b`, `d_A` → `shaft.seat_diameter_a`, `d_B` → `shaft.seat_diameter_b`, `n_f_min` → `shaft.safety_fatigue_min`, `n_c` → `shaft.speed_critical` |
| `mech.weld-group` | `f_r_max` → `weld.force_per_length_max`, `tau` → `weld.stress_throat`, `s_req` → `weld.leg_required` |
| `struct.connection-ec3` | `F_v_Rd` → `bolt.resistance_shear`, `F_b_Rd` → `bolt.resistance_bearing`, `F_t_Rd` → `bolt.resistance_tension`, `f_vw_d` → `weld.strength_shear_design`, `A_s` → `bolt.area_stress`, `f_ub` → `bolt.strength_ultimate`, `f_yb` → `bolt.strength_yield` |
| `struct.load-combinations` | `E_d_max` → `combination.effect_uls_max`, `E_d_min` → `combination.effect_uls_min`, `E_k_max` → `combination.effect_sls_characteristic_max`, `E_qp_max` → `combination.effect_sls_quasi_permanent_max` |
| `elec.load-budget` | `P_dem` → `load.power_demand`, `S_dem` → `load.apparent_demand`, `I_3ph` → `load.current_three_phase`, `I_1ph` → `load.current_single_phase`, `I_DC` → `load.current_dc`, `Phi` → `load.heat_dissipated`, `U_L` → `load.voltage_line`, `pf_link` → `load.power_factor_linked`, `C_req` → `battery.capacity_required` |
| `elec.fault-protection` | `Z_s` → `fault.impedance_loop`, `I_f` → `fault.current_earth`, `S_min` → `conductor.area_adiabatic_min`, `I_k3` → `fault.current_three_phase`, `c_k3` → `fault.factor_voltage` |
| `bs.heat-exchanger` | `Q` → `exchanger.duty`, `T_h_out` → `exchanger.temperature_hot_out`, `T_c_out` → `exchanger.temperature_cold_out`, `A_req` → `exchanger.area_required`, `dT_c` → `exchanger.temperature_rise_cold`, `T_c_m` → `exchanger.temperature_cold_mean`, `cp_c` → `exchanger.specific_heat_cold` |
| `bs.sealed-system` | `Phi_tot` → `heating.heat_required`, `V_s` → `heating.volume_system`, `V_e` → `heating.volume_expansion_min`, `dT` → `heating.temperature_difference_emitter`, `dT_fr` → `heating.temperature_difference_loop`, `t_m` → `heating.temperature_mean` |

**Good example.** `{ Symbol: "L_nmh", Unit: "h", Name: "bearing.life_modified" }`; a pack binding `("shaft", "R_A", "bearing", "F_r")` and a link saved as `calc:<id>#R_A` still resolve.

**Enforced by** `H43-port-names`, `H44-ports-declared`, `H45-no-legacy-codes`, `H46-record-kinds`; `AliasCompatibilityTests` (Library: a link by old symbol, alias or public name, a link saved
before the rename updated after the upstream changes, a pack template and example written with old symbols, a draft upgrade carrying a renamed input, input and port aliases refused when they clash)
and `ConsistencyStandardTests` (Sheets: inputs.json, CSV, runner and sheet read an old input symbol; hashes unchanged for a template with no alias).

## I. Catalogue titles, categories, scope and method (rows 47 to 50)

**Rule.** The title is `<Subject> (<standard or method>)` in sentence case: the standard without its year, no version, no colon list (the list belongs in the scope), at most
one parenthesis, last. The category is one word from the discipline's list; a bolt, weld or snap-fit connection is `Connections` in mechanical as in structural.

| Discipline | Categories |
|---|---|
| Mechanical | Tolerancing, Fits, Vessels, Connections, Bearings, Shafts, Springs, Mechanisms, Strength, Thermal |
| Structural | Actions, Analysis, Sections, Members, Connections, Lifting |
| Building services | Hydraulics, Air, Thermal |
| Electrical | Drives, Power, Thermal, Electronics |
| Fluid power | Hydraulics, Seals |

A scope is at most 40 words in two sentences: sentence 1 begins `Calculates …`, sentence 2 gives the exclusions or the sign convention. A method is one relation per
line, label first (`Equivalent load: P = …`), at most 8 lines.

**Good example.** `Rolling bearing life (ISO 281)` in `Bearings`; `Limits and fits (ISO 286)` in `Fits`; `Sealed heating system` in `Hydraulics`.

**Enforced by** `I47-title-form` and `I48-category-vocabulary` (no exceptions: all 54 templates were retitled and recategorised in this phase), `I49-scope-length`, `I50-method-layout`
(`CalcCatalogueStandard` holds the form and the lists).

## J. Clause reference style (rows 51 to 54)

**Rule.** A standard is written in full, once, with the `BS` prefix where it applies and its year: `BS EN 1993-1-8:2005`; the same string wherever the standard appears. A locator
is `cl. x` for standards, `reg. x` for BS 7671 only, `Ch. n` or `§x` for books, `Table n`; never a bare number. A book is `<Author>, <Title>` with its edition in the locator
(`11th ed., Ch. 11`). A reference title holds no licence note (`cited, not copied`) and no brand name; a licence note is a separate field.

**Good example.** `BS EN 1993-1-8:2005` / `cl. 3.6.1` / `Design resistance of bolts`; `Budynas, Shigley's Mechanical Engineering Design` / `11th ed., Ch. 11`.

**Enforced by** `J51-standard-designation`, `J52-clause-locator`, `J53-book-citations`, `J54-no-notes-in-reference-titles`.

## K. Assumption wording (rows 55 to 57)

**Rule.** An assumption is one full sentence, subject first, at most 20 words, no semicolon; its basis is one sentence, a source or a reason. No internal code or section number
(`S4`, `§A5`) and no planned-calculator code: name a calculator by its title. A limitation is stated once, in the scope; a warning is raised only when the inputs make the exclusion matter.

**Good example.** `The joint is concentrically clamped and concentrically loaded.` — basis `VDI 2230 Part 1.`

**Enforced by** `K55-no-internal-codes`, `K56-one-short-sentence`, `K57-exclusion-stated-once`.

## L. Spec doc headings (rows 58 to 60)

**Rule.** A spec page (`docs/calcs/<discipline>/<id>.md`) has `##` headings in this order: Scope, Method, Inputs, Assumptions, Outputs, Ports, Checks, Limitations, References,
Test cases (Ports and Checks only where the template has them; the plural always). Inputs are given as a table. The title line is
`` # <template title> (`id@version`) `` with no legacy code. The Scope paragraph is the template's scope word for word (generated or linked, one source).

**Good example.** `` # Rolling bearing life (ISO 281) (`mech.bearing-life@1.2.0`) `` followed by `## Scope`, `## Method`, `## Inputs`.

**Enforced by** `L58-spec-headings`, `L59-spec-title-line`, `L60-spec-scope-is-the-template-scope`.

## Where the standard lives in code

| What | Where |
|---|---|
| Section headings, empty text, notice and check lines, result order | `Tempest.Calc.Sheets/CalcSheetStandard.cs` |
| Display precision, utilisation rounding, the noise rule | `Tempest.Calc.Sheets/CalcDisplay.cs` |
| Unit symbols | `Tempest.Calc.Sheets/CalcUnitText.cs` |
| One unit per quantity type | `Tempest.Calc.Sheets/CalcUnitStandard.cs` |
| Reserved and retired symbols | `Tempest.Calc.Sheets/CalcSymbolStandard.cs` |
| Message shape | `Tempest.Calc.Sheets/CalcMessageShape.cs` |
| Port names and areas | `Tempest.Calc.Sheets/CalcPortNames.cs`, `CalcLink.cs` (`CalcOutputPort`) |
| Input aliases | `Tempest.Calc.Sheets/CalcInputAliases.cs`, `Template.cs` (`CalcInputSpec.Aliases`) |
| Titles and categories | `Tempest.Calc.Sheets/CalcCatalogueStandard.cs` |
| The contract | `tests/Tempest.Calc.Tests.Calculators/Consistency/` |
