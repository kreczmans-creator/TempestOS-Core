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

## Addendum A

Linked chains, reference records and independent checks. This extends "How to author a calculator" above. Read ADR-0168 (linking, with amendments 1–3) and DA-05, DA-06, DA-12, DA-21 and DA-22 in `docs/decisions/v0.26.0-DECISIONS.md` first. Paths: `Library/` is `src/Tempest.Calc/Tempest.Calc.Library/`; `Sheets/` is `src/Tempest.Calc/Tempest.Calc.Sheets/`.

### Make a calculator linkable

1. **Declare output ports** on the template: `OutputPorts: [new CalcOutputPort("I_b", "A", "Design current")]` (symbol, unit, description). Where a template declares ports, only those can be linked. A factor you echo from a licensed record must not be a port (R-A-09). A template that declares none offers every numeric output, so declare them. See `Elec/ConductorSizingCalculator.cs`.
2. **Name the unit of each port.** Binding is refused if the dimensions differ, and a K output never links to °C (DA-17).
3. **Do not read another calculation's value from inside `Run`.** Take it as an ordinary input. The link lives in the input's provenance, so the sheet shows where the number came from.

### Add a linked chain (pack template)

1. **Write the template** in `Library/CalcPackTemplates.cs`: a `CalcPackTemplate` with an id `pack.<name>`, a title, a plain description, the steps (`CalcPackStep(key, templateId, name, example)`) and the bindings (`CalcPackBinding(fromStep, output, toStep, input)`). Steps name calculators by template id. The Library does not reference the calculator assemblies. Add the template to `CalcPackTemplates.All`. The Desktop composes that list in `src/Tempest.Desktop/CalcSheets/CalcPackTemplateComposition.cs`.
2. **Bind only declared ports.** A binding to a table cell sets `Row` and `Column` (ADR-0168 amendment 2). The step must be given its rows as inputs, because instantiating never creates rows. Work out the order from the links: a cycle is refused, and so is a chain deeper than 20.
3. **Write example inputs** for every step in `Library/CalcPackExamples.cs` (`CalcPackStep.Example`; `Omit` and `Hint` are optional). They are laid over the sheet's opening inputs, and each is recorded as "assumption: example input — replace with your own". Use invented numbers and invented drawing numbers. A step that needs the user's own licensed tables (the E4 cable step) carries a `Hint` and no values, and the pack is refused at that step with the step's name and block message.
4. **Write the chain test** in `tests/Tempest.Calc.Tests.Library/Chains/<Name>ChainTests.cs`, in the style of `CoolingChainTests.cs`. Use `ChainKit`:
   - `ChainKit.InstantiateAsync` creates the pack and asserts that nothing was refused, blocked or stale.
   - `Si` and `InputSi` read outputs and inputs in SI, so assert each link value equals its upstream value to nine places, and assert at least one hand-checked number.
   - `ReviseAndUpdateAsync` revises an upstream input and asserts that every downstream instance is stale, that Check and Issue are refused, and that "Update to revision n" clears it in order.
   - `AuthoredByAnotherAsync` makes the steps look as if someone else wrote them, so a template that demands an independent check can be checked.
   - `CheckAndIssueAsync` checks and issues the pack as one bundle and returns the issue sheet text. Assert that no licensed value is in it.
5. **Assert what the steps agree on**, not only what each step computes. The worst chain defect so far (G-B-01) was a device type C sized in one step and type B checked in the next, and every step test passed. Link a code or choice when two steps must share it.
6. **Write the pack worked example.** Add the chain to `ChainPackWorkedExampleTests.cs`, then run `TEMPEST_WRITE_WORKED_EXAMPLES=1 dotnet test tests/Tempest.Calc.Tests.Library --filter ChainPackWorkedExample` to write `docs/worked-examples/packs/<name>/` (`inputs.json`, the pack issue sheet, each step's sheet, `README.md`). Read the README. Every FAIL in it needs a reason in the text, or it is a defect (B-A-04). Render the PDFs as for a single sheet.
7. **Run the dry run.** `InstantiatePackTemplateAsync` builds the chain in memory first, so a refusal stores nothing (`PackDryRunTests`). A name clash in a project is a refusal before anything is written (R-A-13).

### Reference records and the licensed flag

1. **Declare the kinds** the template draws on: `RecordKinds: [new CalcRecordKind("cable.ampacity", "Cable current-carrying capacities", ["ampacity"], "From your own licensed table…")]` (kind, title, the input symbols it fills, a note on where the user gets it). See `Elec/ConductorSizingCalculator.cs`.
2. **Never ship a tabulated standard value.** Tests, goldens, examples and seeds use invented numbers, and the example record is titled SYNTHETIC and saved unlicensed (DA-19). Formula-derived constants and small factor sets are allowed (§A5 of the addendum; DA-07, DA-15).
3. **A record** (`Library/CalcDataRecord.cs`) has a kind, title, source title, edition and amendment, table or clause, licence holder, who entered it, a `LicensedSource` flag and its origin (user-entered, derived, manufacturer example). Revisions are never edited. An input pins `record:<id>@<n>`. The service calls are `SaveRecordAsync`, `SaveRecordFromInputsAsync` (from a form), `ApplyRecordAsync` and `RecordsForAsync` in `Library/CalcInstanceService.Records.cs`. The form has "apply record" and "save as record".
4. **Set `LicensedSource` when the values come from the user's own copy of a standard.** Then:
   - every export path withholds the values (CSV, pack, any new one): call `WithholdLicensedAsync`, and add a test for any export you add (DA-06);
   - the issued sheet prints the pin, the citation and "rows withheld (licensed record); the values used appear in the working". A scalar from a licensed record stays, because it is the value used;
   - a revision of a licensed record stays licensed.
5. **A value that is not the record's cannot cite it.** The held value is compared with the pinned revision, a bare `record:` prefix is not a pin, and a pin to a missing revision is stale (R-A-06). A tabulated value typed without a record runs with a warning.

### Table-cell links

A number or quantity cell of a table can link to another calculation's output (`BindCellAsync(instance, table, row, column, upstream, output)`). It is draft-only and needs an existing row. It has the same pinning, staleness, cycle and dimension rules as an input link. A table pinned to a reference record takes no cell links. Test with `tests/Tempest.Calc.Tests.Library/CellLinkTests.cs`. Cell provenance is in no hash. The CSV export writes `name[r].column` rows, so cell links round-trip.

### Independent check

Set `IndependentCheck: true` on a template whose result is a safety or structural claim (E4, E5, S6 do). Check and CheckPack then refuse the calculation's author, everyone who changed its inputs (`EditedBy`, kept across revisions), and nobody signed in. This applies whatever the global `Evidence:IndependentCheck` setting says (DA-12, DA-23). The flag is part of the template hash. In tests, use `ChainKit.AuthoredByAnotherAsync` or see `IndependentCheckTests.cs`. A one-person practice signs in a second person to check these.

Watch one trap: pressing "Update to revision n" or "Revise" adds you to `EditedBy`, so in a two-person team the designer should press Update, not the checker (P-A-03).

### External evidence

Anchorage and similar designs done in other software (a Hilti PROFIS report) are filed as Evidence, not recomputed. Use `AttachExternalEvidenceAsync(instanceId, fileName, contentType, content, inputSymbol)` in `Library/CalcInstanceService.ExternalEvidence.cs`, or the attach button on the worksheet form. It attaches the file to the draft's Evidence and, if you name a text input, records the file name there with the attachment as its source. It is refused for a member of a checked pack, and it checks the citing input before it attaches anything. S6 reads `profis_ref` (`Structural/BasePlateCalculator.cs`). Typed text alone only warns; a PASS needs an attachment (G-A-03). Known gap: the Library does not yet check that "evidence" provenance resolves to a real attachment (P-A-02), so do not rely on that PASS for a real design.

### Checklist for a new chain or record-backed calculator

- [ ] Output ports declared, with units.
- [ ] Every tabulated value is a record input or derived; none copied from a standard.
- [ ] `IndependentCheck: true` if the result protects people or structure.
- [ ] Chain test: link values, cross-step agreement, stale propagation, check and issue as one bundle, no licensed value on the sheet.
- [ ] Example inputs invented and recorded as assumptions; pack README read for unexplained FAILs.
- [ ] A new sheet opens on declared defaults only (`CalcTemplateHasher.NewSheetInputs`). A required table with no default must give a block message that says what to enter next (P-A-05).
- [ ] No review ids, "§A" or "DA-" text in scope, assumption or method text. It prints on every sheet (B-A-18).
- [ ] Template version raised when inputs, outputs or formulas change. A stored instance of the old version stops running and its owner must revise it (see `grey-team.md`).
