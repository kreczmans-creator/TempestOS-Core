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
  `ConsistencyAllowList.json`, keyed by rule, template and the **number of offences** the template has today (C2-26). The allow-list **only shrinks**:
  a template that breaks a rule and is not on the list fails the build, so does a template that is on the list and gains an offence (its count
  rises), and so does one that has fixed an offence and not lowered its count. The write mode lowers or removes entries and refuses to add one or to raise a
  number. Phases B and C (the per-template work) empty the list; `docs/calcs/consistency-work-list.md` is generated from it and says what is wrong in each template.
- **Applicability rules** declare their effect on the result, `CalcRuleEffect.Conservative` or `Unconservative`, on every rule of every template (C2-25);
  an unconservative rule is a block, or is paired with a check that fails when it fires, or is on `UnconservativeAllowList.json` with its reason.
  `UnconservativeRuleTests` holds it. Nothing matches words.
- **The glossary** (`docs/calcs/glossary.md`) is generated from the catalogue; `GlossaryTests` fails when a template uses a symbol it lacks or
  lists under another quantity type, and when a use of a symbol that two or more templates share has no declared meaning id (`eta:efficiency`,
  `eta:perry-factor`; `GlossaryMeanings.json`), so two meanings in one quantity type are told apart (C2-27). `python3 scripts/generate-glossary.py --check`
  runs in CI.

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
| C2-06 | How are numbers rounded for display? | By quantity type, by the renderers and `CalcOutput`, from one table (`CalcDisplay.Rules`, the **least** decimals shown): decimals per dimension and unit, zero is `0`. A plain number that has no unit rule keeps five significant figures. A template may raise the decimals of one output (`c.Output(symbol, text, decimals: n)`) where it can justify it. **Amended by C2-20 (the noise rule), C2-21 (at least 4 and at most 5 significant figures, whole numbers exact, rounding that cannot cross a limit, utilisation by declaration), C2-30 and C2-31 (the direction follows the verdict of the check).** | The audit's bare decimals (no limit on significant figures), which print `321190.4 kN/m` and `0.0 MPa`. |
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
| C2-20 | Which outputs may be shown as `0` for being solver noise? | Only an output its template flags as noise-prone (`c.MarkNoiseProne(...)` or `c.Output(..., noiseProne: true)`): a solver residual, an equilibrium sum, a computed zero (the radial stress at a free surface, a reaction moment on a pinned support, a loop-closure residual). Such an output is shown as `0` when it is more than nine orders of magnitude below the largest working value of the same dimension and unit in the same run. The comparison is never with another output as such, and an unflagged output is never zeroed: `Z_x = 3.1595 cm³` beside `S_run = 13824 m³`, `0.4 ms` beside `150000 h` and `3 mN·m` beside `5 MJ` print as they are. The flagged outputs are `mech.thick-cylinder` (`sigma_r_a`, `sigma_r_b`), `mech.mechanism` (every kinematic scalar) and `struct.frame-2d` (reactions, their sum, member extremes). | The audit's grouping by dimension, which zeroed real values; a single absolute threshold; a scale symbol named by each template. |
| C2-21 | How many figures, and which way does a displayed number round? | A result is shown with at least four and at most five significant figures (the table's decimals are the least; `3.160 cm³`, `0.003400 m³`, `144.9 MPa`); a whole number prints exactly (`150000 h`, `13824 m³`, `2 kN`); zero is `0`. No displayed value appears on the passing side of a limit its check fails. A value that must be at least a limit (a safety factor, a margin, `n_*` against a target) is declared `AtLeast` and rounds **down** (1.4999 against 1.5 prints `1.4999`, never `1.50`). A utilisation rounds **up when its check fails** and to the nearest when it passes, and never prints above its limit when it passes (u 1.004 on a failing check prints `1.01`; on a check that passes within its tolerance it prints `1.00`; the direction is the verdict, C2-31); a negative utilisation rounds away from zero; one below 0.005 prints two significant figures (`0.00022`). A failing `Check` shows its demand rounded up and its capacity rounded down, in the check line and in the Results rows of the same two quantities (an angle of 40.004° against 40° prints `40.01°` against `40°`, C2-30). A utilisation is what a template **declares** (`c.MarkUtilisation(...)`, `kind: Utilisation`) or the symbol a utilisation check is made on, never what the symbol or the description says: `u_ratio`, "Factor applied to utilisation", is a plain number. | Two decimals for safety factors and efficiencies; finding utilisations by their names; rounding every value to nearest. |
| C2-22 | One formatter for the sheet, the PDF, the Desktop form, the governing banner and the register? | Yes. `CalcOutput.ValueText`, `CalcSheetStandard.GoverningBanner` and `FormCheckText` and `CalcDisplay.StoredUtilisation` are the only forms; the form showed a 3-figure utilisation in the banner and the register (`1.25` for a failing `1.2516`) and a yes/no check as `u = 0`. `docs/testing/v0.26.0-test-values.md` is regenerated, and its Expected column is written in the unit symbols its introduction promises (`kN·m`, `93.89°`). **For the guided test mode:** it assumed the form shows five significant figures (`GuidedTolerance.FormSignificantFigures = 5`, removed). The form shows 4 or 5 figures, a whole number exactly, and a utilisation to two decimals, so the allowance comes from the digits shown: `CalcDisplay.ShownResolution(shownText)` gives the size of the last shown digit (`144.9 MPa` is 0.1, `0.003400 N·m` is 0.000001, `150000 h` is 1) and half of it is the allowance (`ShownResolutionExponent` is the same as an exact integer power of ten, so the comparison stays exact). The release's cap (SEC-C-02) stays: the allowance is at most 1 % of the expected value but never less than half the form's last displayed digit. The auto-compare reads the governing utilisation as the banner prints it (`CalcCheck.UtilisationText`), not as three figures of its own. A whole number the form prints exactly (`150001 h` against a shown `150000 h`) is a mismatch where an assumed five figures allowed it; `3.1596 cm³` against a shown `3.160 cm³` is a match where five assumed figures refused it. | Keeping a second formatter in the Desktop. |
| C2-23 | What happens to calculations saved on an older template version when a template is bumped? | Nothing is released, so no migration now: the catalogue registers only the current version, and a calculation saved on an older one is carried forward by the draft upgrade plan (its inputs are kept) or re-entered. To keep that small, phases B and C bump a template at most once each beyond phase A and this fix: a phase bumps a template only when its version on that phase's branch is still the one phase A and this fix ship, and the integrator keeps the higher version where both branches bumped the same template and re-records its manifest line. **DECISIONS text:** before the first release the owner may want old template versions kept resolvable for re-running issued calculations. Option 1: keep a registry of superseded template versions (the catalogue then registers every released version, and an issued calculation re-runs on the version it was issued on). Option 2: exclude display-only text (titles, descriptions, messages, categories) from the content hash, so that a wording change does not make a new version. Neither is implemented. | Migrating saved calculations now. |
| C2-24 | The five checks that passed while their warning said the result was unsafe. | Fixed, each with a test that fails before: `struct.en1993-member` `k-default` (a k_yy or k_zz left at 1.0 under N_Ed/N_b,Rd above 0.3, not recorded as an assumption, is replaced in the check by the Annex A/B bound C_m(1 + 0.8n); a k entered with a recorded basis is the user's, with the warning kept); `elec.voltage-drop` `temperature` (the resistance and the drop are computed at the estimated conductor temperature when it is above the one entered); `mech.spring` `buckling` (a check in the verdict for a compression spring, and the status reads FAIL); `mech.shaft` `critical-speed` (a check that the critical speed is at least twice the operating speed); `elec.motor-sizing` `speed-above-rated` (a check that the motor speed is not above the rated speed). `net-section` and `no-ltb` of `struct.en1993-member` are blocks: a tension member needs `A_net` and `f_u` (and is checked for N_u,Rd = 0.9 A_net f_u/γ_M2, 6.2.3(2)(b)); a member bending about y with `ltb` = Not restrained is refused. Each template went up one minor version. | For the temperature, the insulation's maximum operating temperature; for `k-default`, failing the check instead of using the bound; keeping `net-section` and `no-ltb` as warnings. |
| C2-25 | How is an unconservative applicability rule found? | By a declaration, not by words: every `CalcApplicabilityRule` states its `CalcRuleEffect` (not part of the content hash). The contract: an unconservative rule is a block, or is paired with a check that fails when it fires (`UnconservativeRuleTests.Probes` runs the template on inputs that make it fire), or is on `UnconservativeAllowList.json` with the reason it is not yet one of those. The list has a ceiling and only shrinks. | The word-matching rule `F35-limit-is-a-check-not-a-warning`, which guessed from "exceeds" and "below". |
| C2-26 | What is the allow-list keyed by? | Rule, template and the number of offences. A template on the list that gains an offence fails; the write mode lowers or removes and never adds or raises. | A list of templates per rule, in which a new offence inside a listed template went unseen. |
| C2-27 | How are two meanings of one symbol in one quantity type told apart? | By a declared meaning id for every use of a symbol that two or more templates share (`GlossaryMeanings.json`; `eta:efficiency`, `eta:perry-factor`, `eta:shear-area-factor`; `n:active-coils`, `n:gas-exponent`, …). The glossary lists one row per symbol, meaning and quantity type, and marks a symbol with several `RESOLVE`. `python3 scripts/generate-glossary.py --check` runs in the CI step that checks the licences. | Grouping by quantity type alone. |
| C2-28 | Aliases. | The catalogue refuses a port name or alias equal to the symbol of any other output of the template (a run on the defaults and the known-answer expectations are searched). `CalcInputAliases.Canonicalise` refuses (`CalcInputAliasConflictException`) an input given under an old and a current symbol with different values, instead of dropping one; a run, an inputs file, a CSV file, `CreateAsync` and `UpdateInputsAsync` report it. The Desktop form canonicalises the saved inputs when it opens a calculation. | Letting the current symbol win. |
| C2-29 | What happens when the two lines of development bumped one template to the same or crossing versions? | Each template whose versions crossed gets a version above both, and its manifest line is **added**; the manifest never changes a line it has. `mech.spring` (both lines 1.4.0, different content) 1.4.1; `mech.shaft` (release 1.3.1, C-2 1.5.0) 1.5.1; `mech.iso286-fit` (release 1.5.0, C-2 1.4.1) 1.5.1; `elec.load-budget` (release 1.4.1, C-2 1.5.0) 1.5.1. A manifest key cannot hold two hashes: `mech.spring@1.4.0` keeps the release line's hash, since that is the content that shipped; the C-2 line's 1.4.0 content was never released and is not recorded. Every rule the release changed or added declares its effect: `elec.enclosure-heat` `limit` (now a warning: the walls' heat gain is in `P_cool`) conservative, `struct.base-plate` `alpha_bc` (now a block) conservative and `alpha_bc_profis` conservative (the anchor design's governing utilisation counts in `u_max`), `mech.spring` `hook-fatigue` unconservative (still on the allow-list: the hook bending is assessed only when `S_e_b` is entered). | Giving the merged template the higher of the two versions (it would print an id@version that names two contents on two branches). |
| C2-30 | Which Results rows follow a failing check? | The two quantities of a failing `Check`: the demand's row is rounded up (away from zero) and the capacity's row down (toward zero), as the check line is, so that no row prints on the passing side (`sigma_vm_hub` 350.00042 MPa against 350 MPa printed `350.0` against `350`; `y_cr` 11.99999 mm against `s_2` 12 mm printed `12.00`). `Check()` records the demand and capacity symbols; `CalcOutput.Rounded` carries the direction. A quantity that is the demand of one failing check and the capacity of another keeps the nearest text. | Rounding every output of a failing sheet; leaving the rows to the nearest. |
| C2-31 | What decides which way a utilisation rounds? | The check's verdict, not a threshold (`CalcDisplay.Utilisation(u, passed)`, `CalcCheck.UtilisationText`, `CalcDisplay.StoredUtilisation(stored, passed)`, the register, an output a utilisation check is made on). A check that fails rounds up and is never printed at or below its limit: the clean-number snap (1.25 held as 1.2500000000000002 prints 1.25) is refused where it would print a failing value at the limit (u = 1.0000000001 against a tolerance of 1e-12 is `1.01`, not `1.00`). A check that passes within its tolerance prints to the nearest and never above its limit (u = 1.006 with a tolerance of 0.01 prints `1.00`). A utilisation no check is made on is taken to pass when it is within its limit. `u_rated_speed` of `elec.motor-sizing` is declared a utilisation. | Rounding up above a fixed threshold of the limit plus 1e-9. |
| C2-32 | What does a yes/no governing check print in the register and in a worked-example README? | `PASS` or `FAIL` and no utilisation: `CalcCheckSnapshot` carries `HasUtilisation` (false for a yes/no check; a calculation saved before the field reads as a ratio check), and the register prints `PASS u = 0.83` only for a ratio check. The README's `Governing check:` line is written by the sheet's own formatter (`CalcCheck.UtilisationText`; no `(utilisation …)` for a yes/no check), so the guided test reads what the form shows. | Printing `u = 0.00`; a formatter of the README's own (three decimals, `0.000` for a yes/no check). |
| C2-33 | How are the old and the current symbol of one input compared, and how are port aliases checked? | A quantity is compared in SI (12000 N and 12 kN are one force; an input with no unit is in the input's default unit), so a set that gives both is kept once and not refused. The conflict message names only the two names actually given (`F: the input is given as 'F' and as 'F_old' with different values. Keep one.`), never the current symbol when only old ones were. The catalogue's check that a port name or alias is not the symbol of another output searches the outputs of a run of **every** known-answer case, so an alias that is an output of another mode (a name only the extension spring or the second section type gives) is refused too. | Comparing numbers and unit texts exactly; naming the current symbol; searching the default run only. |
| C2-34 | What are the glossary meanings of `rho` in the member check and of `S_e`? | `rho` of `struct.en1993-member` is the shear reduction factor of EN 1993-1-1 clause 6.2.8 (`shear-reduction-factor`; it was listed as a density ratio) and its output reads "Shear reduction factor ρ". `S_e` is two meanings: `bending-endurance-limit` (a shaft or a fatigue curve) and `spring-shear-endurance-strength` (the wire's shear endurance strength at N_c cycles). | One meaning for both endurances. |

## 2. Contract tests and the allow-list

`ConsistencyRules.All` holds one rule per audit row that a program can check. `ConsistencyContractTests` runs each rule over all 54 templates and compares
what it finds with `ConsistencyAllowList.json`, which is `{ rule: { template: offences } }`:

- a template that breaks a rule and has no entry fails the test;
- a template whose offences rise above its entry fails it (a new offence inside a listed template is a failure, not a quiet addition);
- a template that has fewer offences than its entry fails it too: lower the entry.

To lower the list and rewrite the work list after fixing templates:

```
TEMPEST_WRITE_CONSISTENCY_ALLOWLIST=1 dotnet test tests/Tempest.Calc.Tests.Calculators --filter ConsistencyContractTests
```

The write mode only lowers and removes: it refuses (the test fails and nothing is written) when a template has no entry or more offences than its entry, so it cannot be used to
grow the list. A reviewer rejects a diff that raises a number by hand. The rules with no entry hold for every template today: `E29`, `E30`, `F34`, `I47`, `I48`.

**Unconservative applicability rules.** Every applicability rule of every template declares its `CalcRuleEffect`. `UnconservativeRuleTests` requires an unconservative rule
(the sheet can still pass although the result may be on the unsafe side: a demand understated, a capacity overstated, a check not made, a method used outside its range) to be

1. a **block**, as `net-section` and `no-ltb` of `struct.en1993-member` now are; or
2. **paired with a check that fails when it fires**: `struct.en1993-member` `k-default`, `mech.shaft` `critical-speed`, `mech.spring` `buckling`, `elec.motor-sizing` `speed-above-rated`
   (each is a probe that runs the template on inputs that make the rule fire and requires the named check to fail); or
3. on `tests/Tempest.Calc.Tests.Calculators/Consistency/UnconservativeAllowList.json` (`{ template: { rule: reason } }`), each with the reason it is not yet one of those and what is left to do.
   The list has 91 entries (`UnconservativeRuleTests.Ceiling`); remove an entry and lower the ceiling in the same commit; adding one fails the build.

## 3. Versions in phases B and C, and the guided test mode

- Phase A and this fix bumped these templates (the manifest has the lines): the 20 of phase A (11 patch for a title, 6 minor for ports, 3 minor for ports and a title), and one minor
  version each for `struct.en1993-member` 1.4.0, `elec.voltage-drop` 1.3.0, `mech.spring`, `mech.shaft` and `elec.motor-sizing` 1.10.0 (C2-24). Four of those collided with a version the
  release line had already shipped under another content when the two lines were merged, and were given a version of their own above both (C2-29): `mech.spring` 1.4.1, `mech.shaft` 1.5.1,
  `mech.iso286-fit` 1.5.1 and `elec.load-budget` 1.5.1.
  Display-only changes (the precision, the unit symbols in table cells, the utilisation and noise declarations, the glossary) are not in the content hash and bump nothing.
- Phases B and C bump a template **at most once each** beyond that (C2-23). A phase bumps a template only when its version on that phase's branch is still the one this branch ships
  (check `git diff <this branch> -- <calculator file>` for the `Id, "x.y.z"` line before editing it); a second change to the same template on the same branch keeps the version
  already bumped. Where both phases bumped one template, the integrator keeps the higher version and re-records the manifest line
  (`TEMPEST_UPDATE_TEMPLATE_MANIFEST=1`, which only adds lines).
- The guided test mode takes its tolerance from the digits the form shows, `CalcDisplay.ShownResolution(shownText)` (C2-22), not from a fixed five figures (`GuidedToleranceTests`, `AddendumCSecurityRegressionTests`).

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
another quantity type, fails; every use of a symbol that two or more templates share declares a meaning id, `GlossaryMeanings.json`, so `eta` is `eta:efficiency`,
`eta:perry-factor` and `eta:shear-area-factor` even though all three are plain numbers; every symbol with several meanings is marked `RESOLVE` for phases B and C). Renames in B and C keep the old symbol as an alias
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
decimals"; its decimals are the **least** shown). A result is shown with at least four and at most five significant figures: a force 2194.8 N·m, a stress
144.9 MPa, 3.160 cm³, 0.003400 m³; a whole number is printed exactly (150000 h, 13824 m³, 2 kN); zero is `0`. No displayed value appears on the passing side of a limit
its check fails (C2-21): a value that must be at least a limit (a safety factor, a margin, `n_*` against a target) rounds **down**, so 1.4999 against 1.5 prints `1.4999`; a utilisation
rounds **up when its check fails** and to the nearest when it passes, by the verdict and not by a threshold (C2-31): u = 1.004 on a failing check prints `1.01` (a check against a limit of 0.5 prints 0.504 as `0.51`), and
1.006 on a check that passes within its tolerance prints `1.00`, never above its limit; a negative utilisation rounds away from zero; a utilisation below 0.005 prints two significant figures (`0.00022`); a failing check
line shows its demand rounded up and its capacity rounded down (`40.01°` against `40°`), and so do the Results rows of those two quantities (C2-30). A utilisation is what the template declares (`c.MarkUtilisation(...)`, or the symbol a utilisation check is made on), never what its symbol or description
says: `u_ratio`, "Factor applied to utilisation", prints as the plain number it is. A value is shown as `0` for being solver noise only when its template flags it noise-prone (C2-20),
and then only against the working values of its own dimension and unit in the same run (`1.8626e-15 MPa` beside `144.9 MPa` prints `0 MPa` for the flagged radial stress; an unflagged
`3.1595 cm³` beside `13824 m³` is `3.160 cm³`). The same rules apply in Results, Checks, the Desktop form, the governing banner and the register: one formatter (C2-22). Rounding is for display
only: no value, output, stored output, hash or export is rounded. What a reader sees differently from phase A is listed, old and new, in [`display-changes.md`](display-changes.md).

**Good example.** `sigma_t_a = 144.9 MPa`, `T = 2194.8 N·m`, `u_life = 0.94`, `sigma_r_b = 0 MPa` (flagged), `t_life = 150000 h`, `n_f = 1.4999` against a target of 1.5.

**Enforced by** `DisplayRulesTests` (Sheets): `A_Scalar_Is_Shown_At_The_Decimals_Of_Its_Quantity_Type_And_Never_With_Fewer_Than_Four_Figures`, `A_Result_Shows_At_Least_Four_And_At_Most_Five_Significant_Figures`,
`A_Whole_Number_Is_Printed_Exactly_Without_Decimals`, `Display_Rounds_The_Text_Only_And_Never_The_Value`,
`An_Output_Is_Never_Shown_As_Zero_For_Being_Small_Beside_Another_Output_Of_Its_Dimension` and `Only_An_Output_The_Template_Flags_As_Noise_Prone_Is_Shown_As_Zero_And_Only_When_It_Is_Noise_Beside_Its_Working_Values` (the three cases of the audit),
`A_Utilisation_Is_Two_Decimals_Two_Figures_Below_0_005_And_Rounded_Up_When_It_Fails_And_Away_From_Zero_When_Negative`, `A_Utilisation_Is_Rounded_Up_Whenever_It_Exceeds_Its_Own_Limit_Not_Only_Above_One`,
`A_Safety_Factor_That_Must_Be_At_Least_Its_Target_Is_Rounded_Down_And_Never_Reads_At_The_Target`, `A_Failing_Check_Shows_Its_Demand_Rounded_Up_And_Its_Capacity_Rounded_Down_And_A_Passing_One_Rounds_To_The_Nearest`,
`A_Utilisation_Is_Known_By_The_Declaration_Or_The_Check_Made_On_It_And_Not_By_Its_Symbol_Or_Description`, `The_Form_Banner_And_Register_Print_The_Utilisation_The_Sheet_Prints` (Sheets) and
`TheForm_ShowsEachResultCheckAndTheBanner_AsTheSheetDoes` (Desktop); `ConsistencyStandardTests.Every_Rule_Of_The_Table_Names_A_Unit_The_Engine_Parses_In_Its_Dimension_And_Is_Listed_Once`;
contract rules `E29-output-unit-has-a-precision` and `E30-utilisation-two-decimals` (no exceptions); `GlossaryTests.The_Quantity_Type_Table_States_The_Units_And_Decimals_Of_The_Code`.

## F. Warning and block wording (rows 31 to 37)

**Rule.** A message is `<symbol>: <one plain sentence>.` The symbol is the one input the user changes; there is one symbol, not `F_r/F_a`. A missing input reads
`<symbol>: enter <thing>.` A message states the condition in sentence 1 and, if it helps, the action in sentence 2; no semicolon; each sentence at most 20 words.
A rule's emitted severity equals its declared severity and every emitted rule id is declared once, without the engine's `input.` prefix. A block is for a method that
gives no result; a warning for a documented fallback, named in the message. A broken engineering limit is a check, or a block, never only a warning: an applicability rule declares its effect (`CalcRuleEffect`), and an unconservative rule is a block, is
paired with a failing check, or is on the allow-list with its reason (section 2, C2-25). A number in a message
has the decimals of its glossary entry; a range in a sentence is `<low> to <high> <unit>`; no comma as thousands separator.

**Good example.**
`Block  A: enter the heat transfer area.  [area-missing]` · `Warning  kappa: κ above 4 is treated as 4 when a_ISO is read.  [kappa-high]` ·
`Block  segments: enter at least one row of 'Shaft segments'.  [segments]`

**Enforced by** `F31-message-shape` (every literal message in a calculator's source and every notice its known-answer runs raise),
`F32-missing-input-phrasing`, `F33-one-idea-per-message`, `F34-severity-fits-the-condition`, `F35-emitted-severity-equals-declared` (declared severity equals the severity
of every `Warn`/`Block` call), `F36-emitted-ids-declared`, `F36-no-input-prefix`, `F37-numbers-in-text`. The engine wraps its exceptions
with `CalcMessageShape.Engine` (Sheets tests `An_Engine_Failure_Is_Wrapped_In_The_Same_Shape`, `The_Validator_Messages_Follow_The_Shape`). The declared effect of every applicability rule is held by
`UnconservativeRuleTests` (`An_Unconservative_Rule_Is_A_Block_Or_Is_Paired_With_A_Failing_Check_Or_Is_On_The_Allow_List_With_A_Reason`, `Each_Probe_Makes_Its_Rule_Fire_And_Fails_The_Check_It_Is_Paired_With`,
`The_Allow_List_Only_Shrinks_Its_Entry_Count_Is_The_Ceiling`), and the five checks that passed while their warning said the result was unsafe by `UnconservativePassTests` (C2-24).

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
and add a test that loads an old-form instance. The registration refuses an alias that names another input or port, and a port name or alias that is the symbol of any other output of the template (C2-28).
An input given under an old and a current symbol with different values is refused (`CalcInputAliasConflictException`), never resolved by dropping one; the Desktop form canonicalises the saved inputs when it opens a calculation. Phase A renames no symbol; it names the ports of the nine
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
before the rename updated after the upstream changes, a pack template and example written with old symbols, a draft upgrade carrying a renamed input, input and port aliases refused when they clash,
a port name or alias equal to another output refused, a conflicting old and current symbol refused on create and update)
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
| Display precision, rounding direction, utilisation, the noise rule, `ShownResolution` | `Tempest.Calc.Sheets/CalcDisplay.cs` |
| What an output is checked against (`CalcOutputKind`), `MarkUtilisation`, `MarkAtLeast`, `MarkNoiseProne` | `Tempest.Calc.Sheets/RunResult.cs` |
| What an applicability rule says about the result (`CalcRuleEffect`) | `Tempest.Calc.Sheets/Template.cs` |
| The governing banner and the form's check text | `Tempest.Calc.Sheets/CalcSheetStandard.cs` |
| Unit symbols | `Tempest.Calc.Sheets/CalcUnitText.cs` |
| One unit per quantity type | `Tempest.Calc.Sheets/CalcUnitStandard.cs` |
| Reserved and retired symbols | `Tempest.Calc.Sheets/CalcSymbolStandard.cs` |
| Message shape | `Tempest.Calc.Sheets/CalcMessageShape.cs` |
| Port names and areas | `Tempest.Calc.Sheets/CalcPortNames.cs`, `CalcLink.cs` (`CalcOutputPort`) |
| Input aliases | `Tempest.Calc.Sheets/CalcInputAliases.cs`, `Template.cs` (`CalcInputSpec.Aliases`) |
| Titles and categories | `Tempest.Calc.Sheets/CalcCatalogueStandard.cs` |
| The contract | `tests/Tempest.Calc.Tests.Calculators/Consistency/` (`ConsistencyRules.cs`, `ConsistencyAllowList.json`, `UnconservativeRuleTests.cs`, `UnconservativeAllowList.json`, `GlossaryMeanings.json`) |
