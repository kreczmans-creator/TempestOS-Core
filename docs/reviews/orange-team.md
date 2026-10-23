# 🟠 Orange team — developer education

## How to author a calculator in TempestOS (v0.26.0)

1. **Pick the assembly.** `Tempest.Calc.Mechanical`, `.Structural`, `.BuildingServices` or `.Electrical`. They reference `Tempest.Calc.Sheets` only (ADR-0165 §6); never add a reference to Core, Workspace or Desktop.
2. **Declare the template** (`CalcTemplate`): id `discipline.topic`, semantic version, discipline, title, scope, method (formulas in your own notation), inputs (`CalcInputSpec` with default unit, soft `Min`/`Max` that warn and `HardMin`/`HardMax` that block; `IsOptional` when a shape or route does not need it; tables with `CalcColumnSpec` and `MaxRows`), assumptions, clause references (number and title only), applicability rules (ids reused by `Warn`/`Block`), and at least two `CalcTestCase`s with a `CalcReferenceSource` (title, edition, location) and `CalcExpectedValue`s with explicit tolerances; mark a case `Unconfirmed` when you could not open the source.
3. **Write `Run(CalcRunContext c)`.** Call `c.Input(symbol)` for every input the sheet uses; prefer `c.Assign(symbol, "expression", unit, description, clause)` so the sheet shows formula → substitution → result; use `c.Record(...)` for native steps (solvers, tables) with a formula text and a substitution text; `c.Output` what the user needs; `c.Check*` for pass/fail with the governing check first; `c.Warn`/`c.Block` with a message that starts with the input symbol and one plain sentence. Never throw for bad input: block. Expressions: `*` for every product (`2*t`, not `2 t` — `2 t` is two tonnes), `mod(a,b)` not `%`, `sqrt((x)^3)` not `(x)^1.5` on dimensioned values, `in` converts units.
4. **Caps.** Every loop driven by user data has a cap (table `MaxRows`, solver sizes, iteration counts) and a test that proves the cap bites. No recursion on user data.
5. **Register it** in the assembly's `*Calculators.All` list (the Desktop composes that list statically; nothing is discovered from paths).
6. **Tests** in `tests/Tempest.Calc.Tests.Calculators/<Discipline>/`: the common DoD theories run automatically (known answers, template hygiene, defaults run, golden sheet under `Golden/<id>.txt` — delete the golden to regenerate after an intentional change); add property tests (CsCheck) for invariants, robustness tests for blocks, and an independent cross-check where one exists (closed form vs solver).
7. **Worked examples** (two): add to `<Discipline>WorkedExamples.All` with provenance on every input (`Drawing`, `Specification`, `Assumed`, `FromReference`; `Manual` cannot issue); run `TEMPEST_WRITE_WORKED_EXAMPLES=1 dotnet test tests/Tempest.Calc.Tests.Calculators` to write `inputs.json`, `README.md`, `sheet.txt`, then the same variable on `tests/Tempest.Desktop.Tests --filter CalcSheets` to render `sheet.pdf` (< 200 KB).
8. **Docs**: one lean spec at `docs/calcs/<discipline>/<id>.md` (scope, method, inputs/outputs/checks, test cases), a CHANGELOG line, a yellow-team entry if you took a shortcut. Mind the Markdown budget (a PR may not add more Markdown lines than code lines).
9. **Standards**: cite clause numbers and titles; never paste text, notes, figures or tables. Tabulated data becomes an editable input with provenance (imperfection factors, partial factors, loss coefficients, heat-transfer coefficients) seeded only from published formulae or textbooks.

## Secure-coding checklist for calculator authors

- [ ] Every user string (names, provenance, table text) reaches the sheet only through the renderer (`CalcSheetTextRenderer.Clean`); never build HTML, SQL or file paths from it.
- [ ] Every numeric input has hard limits; every table has `MaxRows`; every vector is bounded by `CalcLimits.MaxArrayElements`.
- [ ] Solvers: fixed iteration caps (bisection ≤ 200, fixed point ≤ 100), size caps with a plain block message, singular/mechanism detection that blocks rather than returns NaN.
- [ ] No file, network, reflection, environment or process access inside `Run`.
- [ ] No `double.Parse` on user text; units go through `UnitCatalog.Parse`, numbers come from `CalcCell.Number`.
- [ ] A hostile-string case in `HostileInputTests` passes for your template (free-text inputs and text columns).
- [ ] Issue-path invariants are the Library's; a calculator never touches persistence.
- [ ] Reference values: cite the source; if you cannot verify it, mark the case `Unconfirmed` and say so in STATUS.
