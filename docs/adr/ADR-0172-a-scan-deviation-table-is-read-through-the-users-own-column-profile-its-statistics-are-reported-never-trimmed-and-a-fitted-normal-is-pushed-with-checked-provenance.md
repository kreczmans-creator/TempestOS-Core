# ADR-0172: A Scan Deviation Table Is Read Through the User's Own Column Profile, Its Statistics Are Reported and Never Trimmed, and a Fitted Normal Is Pushed With Checked Provenance

## Status

Accepted (v0.26.0, Addendum B, B-4, W2). Builds on `ADR-0171` (the hardened import primitives and the CAD parameter import), `ADR-0168` (linking and staleness) and `ADR-0170` (the M1 stack engine and the F3 measured groove rows).

## Context

Addendum B §B6 W2 reads scan deviation tables — ZEISS and GOM Inspect tables, multi-stage tables, PolyWorks and Geomagic Control X text — computes the statistics of each feature over N parts (mean, σ, Cp, Cpk, Pp, Ppk, a normality check) and pushes a fitted distribution into M1 tolerance-stack contributors, F3 groove inputs and M2 fits (the scan-to-seal chain, W2 → M1 → F3). The tables are untrusted files. Their layout is the layout the person chose in the scan software, so no column order can be assumed. Four questions had to be settled:

1. How is a layout that the person defined read, and what is done with a cell that is blank, not a number, or text that could carry a link?
2. Which σ do Cp/Cpk and Pp/Ppk use, how is normality judged, and what happens to outliers and to small samples?
3. What does "push a fitted distribution into M1 / F3" write, and in what unit and under which checks?
4. What does the provenance of a fitted value promise, and what stops it promising too much?

## Decision

1. **A scan profile names the columns; there is no default layout.** `ScanProfile` (named, versioned, immutable once stored, JSON) maps header texts (or positions `#3`) to feature, nominal, actual, deviation, upper and lower tolerance, serial and stage, and states the delimiter (auto, comma, semicolon, tab, whitespace), the decimal mark, the header row, whether the tolerance columns are deviations from nominal or absolute limits, the unit, and the feature-to-input map. Header spellings for the four named products are offered as *suggestions* (`ScanColumnSuggestions`, unconfirmed against real exports), never applied silently. A header the profile names that the file lacks is a **profile mismatch**: the whole import is refused with the headers the file does have. Files are read with `BoundedInputReader` (5 MB, strict UTF-8, no control character), `CsvReader`, `LocaleNumber` and, for `.xlsx`, `XlsxReader` — the ADR-0171 primitives, none duplicated. Row cap 1e6 (`ImportLimits.Scan`), 64 files. A data row with more fields than the header is **refused** (a number split by the other decimal mark or a thousands separator would shift every value after it); a short row is reported.

2. **A cell is a number or it is reported; text is plain text.** A blank cell is *missing* (never zero) and counted; text that is not a plain finite number — NaN, Infinity, `0x10`, `1_000`, a unit suffix, a number in the other decimal notation, a value beyond ±1e9 — is *not a number* and counted. A row falls back from the actual value to nominal + deviation, and is otherwise not used and reported. Every count and the first examples (file, line, feature) are kept per problem kind and column. Names, stages and serials are reduced to plain text: markup tags and link markers (URL schemes, `=HYPERLINK(`) are removed and reported, invisible characters dropped. The statistics and push reports are written with `CsvSafeWriter`.

3. **The statistics are from first principles and never trim silently.** Mean and sample σ (N − 1, corrected two-pass). *Overall σ* (Pp, Ppk) is that s. *Within σ* (Cp, Cpk) is the pooled σ of consecutive subgroups of a stated size; with no subgrouping (size 1, a table of one measurement per part) there is nothing to pool, so **Cp = Pp and Cpk = Ppk** — documented and tested — and no moving-range estimate is made because a scan table does not carry production order. Cp needs both limits; one limit gives Cpk/Ppk for that side; σ = 0 gives no index (never Infinity). Normality is the Anderson–Darling test implemented from the published formula, A² with the modification A*² = A²(1 + 0.75/N + 2.25/N²) and the D'Agostino–Stephens p-value approximation (Anderson and Darling 1954; D'Agostino and Stephens, *Goodness-of-Fit Techniques*, 1986) — cited, no table shipped — with ln Φ evaluated by Marsaglia's series and a log-domain continued fraction so the tails do not cancel. Outliers are flagged by Tukey's fences (1.5 IQR; 3 IQR "far out"), reported with line and serial, **kept by default**, and left out only when the person asks (and then listed). Fewer than 30 parts (adjustable) warns that capability is uncertain; fewer than 8 says normality is not assessed; fewer than 2 parts cannot be fitted. A rejected normal fit warns and still pushes.

4. **A push writes a normal in the target's own schema, by dimension.** `ScanMapper` maps a feature at a stage (a feature at several stages must name one) to: a single input and statistic (`D`, `D:sigma`); an existing row of a tolerance-stack table (`contributors[2]`: nominal from the scan, mean shift = fitted mean − nominal, band ±kσ with the stack's own k so the stack reads back exactly σ, distribution normal; a position or profile contributor is refused); or the row of a measured-dimension table for a key (`measured{D_g}`: mean, σ, normal; added or replaced, within the table's row cap). Units convert through the engine (a spread by scale only, never an offset); a unit of another dimension is refused; while anything is refused nothing is applied; a feature the profile does not list is reported as unmapped and snapshotted, never dropped. M2 takes a statistic through the single-input form.

5. **Provenance is a new kind, `CalcProvenanceKind.ScanImport`, appended at the end of the enum, and it is a claim that is checked.** The reference follows W1's shape — software and version, stage, serials, alignment (as the person entered it), the SHA-256 of the file (of the *set* of files when a feature's parts came from several), import time, feature — and the revision is the profile `id@version`; text is shortened to fit 200 characters, the hash never. The instance keeps a `ScanImportRecord` per profile with every feature and **the exact values the import wrote**. A value carrying `ScanImport` provenance is accepted on save only if it is unchanged from the stored instance or equals what a record says that file and profile wrote at that place; a created calculation cannot cite one. (This closes for W2 the gap the W1 security review found for CAD provenance: an edited value cannot keep its claim, and the kind chosen by hand with copied text is refused.)

6. **A re-import is an ordinary input update**, as in `ADR-0171`: `CalcInstanceService.ApplyScanImportAsync` updates the draft through `UpdateInputsCoreAsync` with the record committing in the same write; linked calculations go stale through `ADR-0168` because the draft's outputs changed; a checked, issued or superseded calculation is revised first. The diff lists features added, removed and changed.

## Consequences

- No new package and no new assembly: W2 lives in `Tempest.Calc.Import` (namespace `Scan`), `CalcInstanceService.ScanImport.cs` and the worksheet form.
- **The PDF table path (PdfPig, time-limited worker) is not built** — dropped first under the §B2 drop order ("W2 PDF path, keep CSV"). A PDF table has to be exported to CSV or `.xlsx` by the scan software.
- Two lines are added to W1-owned `CalcInstanceService.cs` (the provenance check on update and on create) and the worksheet form/coordinator gain a second action and panel; nothing else of W1 changes.
- The `.xlsx` path inherits W1's `XlsxReader` and so its fixes.
- Cp = Pp for the usual table. A within-subgroup σ needs the person to state a subgroup size, and only consecutive file order defines a subgroup.
- A fitted normal is only as good as the sample and the alignment named for it; the provenance says which, it does not say the scan is right.

## Follow-ups

1. Owner checks, in the owner test pack: the header spellings of real ZEISS/GOM/PolyWorks/Geomagic exports, and one real inspection table end to end.
2. The PDF path, if wanted, as its own reviewed item (a time-limited worker, no hyperlink or HTML passed through).
3. A moving-range within σ if a table ever carries production order.
