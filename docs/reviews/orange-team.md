# 🟠 Orange team — developer education

## How to author a calculator in TempestOS (v0.26.0)

1. **Pick the assembly.** `Tempest.Calc.Mechanical`, `.Structural`, `.BuildingServices` or `.Electrical`. They reference `Tempest.Calc.Sheets` only (ADR-0165 §6); never add a reference to Core, Workspace or Desktop.
2. **Declare the template** (`CalcTemplate`): id `discipline.topic`, semantic version, discipline, title, scope, method (formulas in your own notation), inputs (`CalcInputSpec` with default unit, soft `Min`/`Max` that warn and `HardMin`/`HardMax` that block; `IsOptional` when a shape or route does not need it; tables with `CalcColumnSpec` and `MaxRows`), assumptions, clause references (number and title only), applicability rules (ids reused by `Warn`/`Block`), and at least two `CalcTestCase`s with a `CalcReferenceSource` (title, edition, location) and `CalcExpectedValue`s with explicit tolerances; mark a case `Unconfirmed` when you could not open the source.
3. **Write `Run(CalcRunContext c)`.** Call `c.Input(symbol)` for every input the sheet uses; prefer `c.Assign(symbol, "expression", unit, description, clause)` so the sheet shows formula → substitution → result; use `c.Record(...)` for native steps (solvers, tables) with a formula text and a substitution text; `c.Output` what the user needs; `c.Check*` for pass/fail with the governing check first; `c.Warn`/`c.Block` with a message that starts with the input symbol and one plain sentence. Never throw for bad input: block. Expressions: `*` for every product (`2*t`, not `2 t` — `2 t` is two tonnes), `mod(a,b)` not `%`, `sqrt((x)^3)` not `(x)^1.5` on dimensioned values, `in` converts units.
4. **Caps.** Every loop driven by user data has a cap (table `MaxRows`, solver sizes, iteration counts) and a test that proves the cap bites. No recursion on user data.
5. **Register it** in the assembly's `*Calculators.All` list (the Desktop composes that list statically; nothing is discovered from paths).
6. **Tests** in `tests/Tempest.Calc.Tests.Calculators/<Discipline>/`: the common DoD theories run automatically (known answers, template hygiene, defaults run, golden sheet under `Golden/<id>.txt` — delete the golden to regenerate after an intentional change); add property tests (CsCheck) for invariants, robustness tests for blocks, and an independent cross-check where one exists (closed form vs solver). Changing a template changes its content hash, so bump its `Version` (an `id@version` must never name two contents) and record the new line with `TEMPEST_UPDATE_TEMPLATE_MANIFEST=1 dotnet test tests/Tempest.Calc.Tests.Library --filter TemplateVersionManifestTests` (add-only; DI-10).
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

## Addendum B

Materials, fluid power, imports and gated calculators. This extends "How to author a calculator" and the Addendum A section above. Read ADR-0170 (Fluid Power), ADR-0171 and ADR-0172 (imports) and DB-02…DB-08 in `docs/decisions/v0.26.0-DECISIONS.md` first. Paths: `Library/` is `src/Tempest.Calc/Tempest.Calc.Library/`; `Sheets/` is `src/Tempest.Calc/Tempest.Calc.Sheets/`; `Import/` is `src/Tempest.Calc/Tempest.Calc.Import/`.

### Where a calculator goes
1. **Five discipline assemblies.** `Tempest.Calc.FluidPower` joins the four (ADR-0170). Register the calculator in its `*Calculators.All`. A new discipline must also be listed in `AllCalculators.ByDiscipline` (a test fails until it is) and in the Desktop `CalcCalculatorComposition`.
2. **Known answers.** Mark a case `Unconfirmed` when you could not open the source. Set `CalcReferenceSource(..., Secondary: true)` when the value comes from a calculator website, a forum, course notes or the brief itself. List both in STATUS and in the spec's "Definition of done" section, with title, edition and example number. A number you could not pin goes in the spec's "not pinned" list, never into a test.

### Take material properties from W3
1. On each input that is a material property, set `MaterialProperty: "E"` (or `G`, `nu`, `rho`, `alpha`, `lambda`, `f_y`, `f_u`, `Rm_A`, `Rm_m`, the permissible strains). The keys are in `Library/MaterialProperties.cs`. Also list the input in a record kind named `material.properties` (`MaterialLibrary.Kind`).
2. `MaterialLibrary.ApplyAsync(instance, material, revision, temperature, unit)` fills the inputs in their own units and pins `record:<id>@<n>`. A later revision makes the calculation stale. Typing over a value drops its pin.
3. A temperature always comes with its unit; none is refused. A property tabulated against temperature (`E(T)`) is read linearly between rows and refused outside them. It is never extrapolated.
4. Tables (`Es_curve`, `mu_pairs`) are checked against the input's columns and row limit.
5. Limits:
   - One material per sheet. A second metal's α is a typed input (F3 `alpha_s`).
   - A table column cannot carry a `MaterialProperty`. Use a record kind instead (B7 layer λ, `material.conductivity`).
6. Seeds are generic textbook values flagged `indicative`. Never seed a grade, a catalogue value or a table.
7. Test: copy `tests/Tempest.Calc.Tests.Library/Materials/PipeExpansionMaterialTests.cs` (apply, pin, run, stale on a new revision).

### Record kinds and the licensed_source lock
1. Each value of a record can carry a `CalcValueSource(Source, Edition, Reference, Licence, EnteredBy, Date, EvidenceSha256)`. `Licence` is `public` or `licensed_source`.
2. One licensed value makes the whole record licensed (`CalcDataRecord.IsLicensed()`). The lock follows links at any depth, into inputs and table cells: export withholds the value, and the sheet prints the link with "withheld" (`LicensedLinkTests`).
3. Licensed quantities have **no default**. A blank one blocks with what to enter: M7 R_m(d) and S_e; M8 E_s and ε_perm; B8 S_a and K1; F3 extrusion limit.
4. A generic textbook default must say so in its description ("generic default … replace with …"): B9 h_se, M7 stress fractions.
5. Invented test numbers say "illustrative, not from any standard". A number that happens to equal a published factor is a §B5 breach even when invented (E8 used 0.45, now 0.42).
6. The lock belongs to the record, not to one revision: flagging any revision licensed locks every revision (SEC-B-02, fixed in `051647fb`).

### Table-cell links in packs
1. Bind a scalar output into one cell with `CalcPackBinding(from, output, to, input, Row: 0, Column: "D")`. Examples: F1 `d_prs` → B1 `segments[0].D`; E7 loss → an E6 load row.
2. The step's example must create the row; instantiating never does.
3. The pack README's chain line is rendered from the bindings by one renderer (DB-06). Do not hand-write it.

### Ports that cannot mislead
1. When a value cannot be computed, withhold the port; never publish a sentinel or an unbounded peak. M9 cuts the sweep at a toggle and withholds its ports. A text output where a number is expected refuses the link (M8 self-locking).
2. Give a port a unit that blocks a wrong binding (M9 `output_motion` is m/s in every mode). State the basis of a pressure: F2 warns when a linked gauge pressure meets the Absolute basis.
3. A value typed in two steps of a chain needs a run-time guard, either a link or a mismatch warning (M7 `travel-mismatch`). A chain test alone is not enough.
4. Model the duty, not only the formula. A sheet's "Emergency stroke" must use the charge and discharge the duty implies (Green G-AB-01).

### Imports (W1, W2)
1. A calculator never reads a file. `Import/` parses untrusted files under one set of limits:
   - 5 MB per file;
   - a declared number format, never guessed;
   - strict CSV;
   - xlsx with zip, depth and size budgets;
   - no formula, macro or CAD expression evaluated.
2. Imported values carry provenance kind `CadImport` or `ScanImport`, with file SHA-256, profile id@version and time. Only the importers set it. `CadImportVerifier` and `ScanImportVerifier` run on every input update and turn a claim they cannot verify into `Manual`. A user cannot pick it by hand.
3. A re-import is a diff: it updates the draft and makes linked calculations stale. A checked or issued calculation is never changed in place.
4. For a template to take a W2 push, keep the target schema the mapper writes: M1 contributor rows, F3 measured rows, a single input.
5. Prove each import chain in `tests/Tempest.Calc.Tests.Library/Chains/` (`CadDrivenToleranceChainTests`, `ScanToSealChainTests`), through stale **and** Issue refused.

### Feature flags (gated calculators)
Written from the S8 branch (ADR-0173, `065fe64c`, not merged).
1. Set `CalcTemplate.FeatureFlag` to a name in `CalcFeatures.All`.
2. The catalogue validates a gated template but never lists, creates, links or issues it while the flag is off.
3. Flags come only from `appsettings.json` in the install folder (`Calc:Features:<Name>: true`), never from the environment, the command line or a user setting. Nothing at run time can turn one on.
4. Governance (competent-person attestation, second Checked signature) is in `Library/CalcInstanceService.Governance.cs`.
5. Test that the flag is off by default, cannot be enabled any other way, and that Issue refuses without both signatures.

S8: reviewed separately in three Opus rounds (Security and Red sections, S8 lifting); no open Critical, High or Medium finding; the flag stays off by default.

### Checklist for an Addendum B calculator
- [ ] Material inputs carry `MaterialProperty` and are listed in `material.properties`; a test applies, pins and stales a material.
- [ ] Licensed quantities have no default; generic defaults say "generic"; invented numbers say "illustrative".
- [ ] `Unconfirmed` and `Secondary` set and listed in STATUS and the spec.
- [ ] Every check has a real ratio: a lower bound is limit/value, an upper bound value/limit. No "utilisation 0" and no sentinel (Yellow Y-B-02, Y-B-03).
- [ ] Soft limits catch a ×1000 unit slip of each modulus, pressure and thickness (Black B-B-02, B-B-03). Hard limits match §B6 or a decision says why not.
- [ ] Sign conventions and pressure bases are stated on the input and checked where a slip is plausible (Black B-B-01, B-B-05).
- [ ] No "§", "Addendum", "DA-", "DB-" or review id in printed text (Yellow Y-B-04).
- [ ] Ports declared with units; withheld when not computable; chain guards at run time.
- [ ] `IndependentCheck: true` when the result protects people or structure. For Addendum B this is open: none set it (O-B-01).
- [ ] Two worked examples, each with `inputs.json`, `sheet.txt`, `sheet.pdf` and README (the Desktop `WorkedExamplePdfTests` fails without the PDF).
- [ ] Template version raised when inputs, outputs or formulas change.

### Findings

| ID | Sev | Location | Description | Fix | Status |
|---|---|---|---|---|---|
| O-B-01 | Low | Addendum B templates | The guidance says `IndependentCheck: true` for safety and structural results; none of the 14 sets it (S7, F1, F2 included). | Set it for S7 and F2, or narrow the guidance and list the templates that need it. | Open: decide (set for S7, F2, or narrow the guidance) and record |
| O-B-02 | Low | This guide | The Addendum B review lessons (withheld ports, run-time chain guards, real check ratios, slip-catching soft limits, no internal references) were not written down. | Added above. | Closed by this section |
| O-B-03 | Info | Feature flags | Written from the unmerged S8 branch. | Recheck when B-5 merges. | Open |

### Verification at `6a189078`

- O-B-01 is still open: `IndependentCheck: true` is set only on E4, E5 and S6. Decide and record.
- SEC-B-02 is fixed (`051647fb`); item 6 of "Record kinds" above says so.
- F2 1.3.0 is the reference for "model the duty": a `process` input defaulting to the duty, the process printed as an output, and a `process` warning when the choice does not suit the duty.
- O-B-03 is open until B-5 merges. S8: reviewed separately in three Opus rounds (Security and Red sections, S8 lifting); no open Critical, High or Medium finding; the flag stays off by default.

## Addendum C

Records, dashboards, test runs and installers. This extends the sections above to features that are not calculators: a screen over the store, an importer, a run log and a seeded project. Read ADR-0174 (record import), ADR-0175 (guided test), ADR-0176 (demo project) and DC-10 … DC-14 first. Paths: `Library/` is `src/Tempest.Calc/Tempest.Calc.Library/`; `Desktop/` is `src/Tempest.Desktop/`.

### Read the store once per screen
1. A screen that lists every kind, template or row reads its collection **once** and groups in memory. Asking per kind or per row reads the whole collection again each time. The records screen did this, taking 68 reads of every record and two more reads per unlicensed record, and needed 2.2 s to open at 1,000 records; it now reads once and opens in 0.18 s (O-C-01).
2. When a rule spans revisions (a licence on any revision locks every revision, SEC-B-02), compute it from the same read (`CalcInstanceStore.ListRecordsWithLicenceAsync`). Do not call `IsLicensedInAnyRevisionAsync` per record.
3. Prove it with the `CountingProxy` in `tests/Tempest.Calc.Tests.Library/BlueRoundTests.cs`: assert the number of `ReadAllAsync` calls, not a time (`AddendumCColourRegressionTests.Orange_O_C_01_…`).

### Cap what a person can grow
1. Every collection or file a person can grow has a cap. Each cap has a sentence that says what to do, and a test that shows it bites:
   - record files: 1,000 data rows and 64 columns (`RecordImportService.MaxFileRows`, `MaxColumns`), 5 MB;
   - guided runs: 1,000 (`ManualTestRunService.DefaultMaxRuns`), refused with the way out ("Delete older finished runs");
   - per run: 1,000 results, a 2,000-character note (refused, never cut), 4,000 characters of values;
   - dashboard: 500 rows and an 8 MB export;
   - the embedded script: its size, steps and field lengths.
2. Measured costs at the caps (this review, Linux, 4 cores):

| What | Size | Time |
|---|---|---|
| Record import preview / commit | 1,000 rows (19 KB), one record per row | 83–254 ms / 125–132 ms (one transaction) |
| Records screen list (`ListTypesAsync`) | 1,000 records, 68 kinds | 2,216 ms before O-C-01, 176 ms after |
| Worksheet missing-records panel (`RequirementsAsync`) | 1,000 records | 157–269 ms (one read after O-C-02) |
| Dashboard build | 1,000 records / 1,000 runs × 110 results | 84–133 ms / 762–890 ms |
| Guided run store on SQLite | 1,000 runs × 110 results with 300-character notes | 53 MB database (and 54 MB WAL until a checkpoint); overview 113–280 ms, latest per template 0.8–1.5 s, listing 0.7–1.1 s, deleting 949 old runs 1.4 s |
| Guided script parse at start-up | 0.5 MB | 71 ms |
| Demo install / remove (in-memory store) | 60 calculations, 13 packs, 2 profiles | 6.1 s on a pool thread with progress lines / 88 ms |

### Write all or nothing, and delete in one place
1. A set that belongs together (an import's records, a demo's rows) is written or deleted in **one** transaction (`CalcInstanceStore.SaveRecordsAsync`, `DeleteDemonstrationRowsAsync`). Re-judge it inside the commit against the store as it is then, because the preview may be stale.
2. The calculation store deletes in one method only, guarded by a key prefix and a name prefix, and refuses any issued row. Do not add a second one.
3. A write that expects a stored row must refuse when the row is gone (`WriteGuardedAsync`, `WriteGuardedPackAsync`). Otherwise a stale save brings a deleted row back.
4. Write the audit row for a delete as soon as the delete commits, before any step that can fail.

### New stored documents
1. A new collection needs no migration: it appears on its first write. Give each document a `Schema` number.
2. A reader leaves out a document it cannot read or that a newer build wrote. It must also leave out a damaged **part** of a readable document (a null list element, a missing id, an enum value it does not know), never count one as a good result, and never throw (B-C-01).
3. Hand a reader that must not write the read-only interface (`IManualTestStatusSource`), never the service.

### Generated data that ships inside the build
1. When `docs/` is the source, generate a JSON file with a script, embed it as a resource and add three guards: a staleness test that re-reads the docs, the script's `--check` in CI, and a reader that caps sizes and never throws (`verification-snapshot.json`, `guided-test-script.json`).
2. A test must read counts from the artefact, not hard-code them (Yellow Y-C-06).
3. A version that hashes the whole artefact changes when any part changes; key per item when results should survive an unrelated change (Grey GR-C-02).

### Screens
1. Every handler that reaches the store, a file or the clipboard runs through the view's guard and turns a failure into one sentence that names the action ("Could not import the file: …"). Never fire and forget without a guard (Black B-C-05, Yellow Y-C-03).
2. Long work (the demo install) runs on the pool and reports progress lines. The view model keeps the UI thread.
3. Do not reuse a type name in a sibling namespace of the same assembly (Green G-C-01).
4. Name things on screen in the user's words, not the project's ("Independent re-derivation", not "Track P re-derivation"; Yellow Y-C-01).

### Checklist for an Addendum C–style feature
- [ ] The collection is read once per screen; a `CountingProxy` test pins it.
- [ ] Every growable set has a cap, a sentence and a test at the cap and one over it.
- [ ] Writes and deletes of a set are one transaction, re-judged at commit; stale writes to a removed row are refused.
- [ ] A new document type has a `Schema`; readers skip whole and partial damage without throwing.
- [ ] Generated embedded data has a generator, a staleness test, a CI `--check` and a capped, non-throwing reader.
- [ ] Every UI handler is guarded and names its action; no unguarded fire-and-forget.
- [ ] No internal names (work streams, review ids) on screen.
- [ ] An ADR, a DECISIONS row, an ADR Register row (the CI health check fails without it, W-C-01) and a CHANGELOG line.

### Findings

| ID | Sev | Location | Description | Fix | Status |
|---|---|---|---|---|---|
| O-C-01 | Medium | `Library/RecordImport/RecordImportService.cs` (`ListTypesAsync`) | The records screen read every record once per record kind (68) and twice more per unlicensed record. It took 2.2 s at 1,000 records, growing as kinds × records. | One read with the any-revision licence (`CalcInstanceStore.ListRecordsWithLicenceAsync`); test `AddendumCColourRegressionTests.Orange_O_C_01_The_Records_Screen_List_Reads_The_Records_Once_Not_Once_Per_Kind_Or_Per_Record`. | Fixed |
| O-C-02 | Low | `RecordImportService.RequirementsAsync` | The worksheet's missing-records panel read every record once per record kind of the calculator, on every form open. | One read per call; the same test, second part. | Fixed |
| O-C-03 | Low | `Library/ManualTesting/ManualTestRunService.cs` (`LatestByTemplateAsync`, `ListAsync`, `PlanTrimAsync`) | Each call deserialises every run. At the 1,000-run cap that is 0.7–1.5 s, and the dashboard pays it on every refresh. `OverviewAsync` already avoids it for finished runs. | Keep a small summary document (latest counted result per example) updated in the same transaction as `RecordAsync` and `FinishAsync`, or read only finished runs of the current script. Accepted for now: "Delete older finished runs" keeps the store small (RR-C-05). | Open |
| O-C-04 | Info | `RecordRequirementsRecordsSource`, `StoreRequiredRecordsSource` | The dashboard reads the whole record collection once per build (84–133 ms at 1,000). This is fine at desktop scale; RR-B-04 still describes the per-record cost of the export paths. | None. | Accepted |
| O-C-05 | Info | `Desktop/WorkspaceHost.cs` | The 0.5 MB guided script is parsed at start-up (71 ms), even when the guided test is never opened. | It could be read on first use; the cost is small. | Accepted |

### 2026-10-27: C-2, DC-21 builds and DI-08

Lessons from the SVG reader (DC-22) and the frame caps (DC-15), merged after the sections above, up to `08315a20`.

#### Read a repeatable list as a set
1. A list in a file (a class list, a selector list, a family list) can name one item many times. Make it distinct before anything multiplies by it. The SVG reader took one class's rule once per name. 480,000 names under one rule of 200 declarations exhausted 512 MB in 1.3 s (SEC-C-11).
2. Read the tokens as spans, and keep a string only when it is new (`HashSet<string>.GetAlternateLookup<ReadOnlySpan<char>>()`). Millions of repeats then cost one string.
3. Test membership by lookup. Never scan the list once per candidate.

#### Bound the work inside one step
1. A deadline checked between elements bounds only the number of elements. Work inside one element needs its own bound.
2. For each loop inside a step, name what bounds it: a cap, the input's length, or a look at the token.
3. Prove it by allocation, not by time. `GC.GetAllocatedBytesForCurrentThread` around the call gives a stable figure: 101 MB before the fix, under 16 MB after.

#### Size a stored document from its rows
1. The frame caps come from the stored bytes of a row: 364 per node, 340 per member and 526 per load. At 1,200, 2,400 and 2,400 the worst frame stores in 3.21 MB, 80 % of the 4 MB cap.
2. Pin the row size with the real serialiser in a test (`FrameSizeCapTests`). A wider row then fails the test before it fails a save.
3. Count what rides on a row. Cell provenance adds about 290 bytes a cell, so a fully provenanced frame can still pass 4 MB (B-C-07).

#### Measured costs (this review, Linux, 4 cores)
The SVG figures come from the shipped runner. It ran under the CI harness's limits: a 512 MB heap and 2 GiB of address space.

| What | Size | Time / peak |
|---|---|---|
| 1,500 class rules, 60 rects of 1,500 classes each | 505 KB | 0.51 s / 83 MB |
| The same rules, one class per rect | 31 KB | 0.17 s / 67 MB |
| One class named 480,000 times, 200 declarations (before the fix) | 961 KB | out of memory at 1.3 s / 427 MB |
| One text run of 900,000 characters | 900 KB | build 0.23 s / 83 MB |
| A frame at the caps, stored | 3.21 MB (computed) | — |

#### Checklist additions
- [ ] Every list read from a file is made distinct before it multiplies work.
- [ ] Every loop inside one step has a named bound. A deadline between steps is not one.
- [ ] A stored document's caps come from a row size that the real serialiser pins in a test.

#### Findings (2026-10-27)

| ID | Sev | Location | Description | Fix | Status |
|---|---|---|---|---|---|
| O-C-06 | Low | `DocumentViewerView` (render on zoom); `SvgDocumentPageSource.RenderPage` | The page is drawn again on the UI thread at each zoom step. The caps bound an SVG picture's replay, but a text run is not counted (SEC-C-13). | Draw off the UI thread, as the build is, or count glyphs into the verb budget. | Open |
| O-C-07 | Info | `SvgBoundedProbeTests` harness | Under 2 GiB of address space a 4,000 px bitmap could not be allocated here. CI's probe children abort at start-up (SEC-C-12). So the raster probes' memory figures are unproven. | Size the limit to the runtime's reservations, then rerun. | Open |
