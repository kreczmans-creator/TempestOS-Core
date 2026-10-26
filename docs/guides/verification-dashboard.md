# Verification dashboard

Addendum C, item C-4. One read-only screen that shows, for every worksheet calculator and every chain, how far it can be trusted today. Open it from **Engineering → Modules → Calculations → Verification**. It changes nothing: no calculation, no Evidence status, no issued record, no reference record.

## What each column says

| Column | Meaning | Where it comes from |
|---|---|---|
| Calculator or chain | The title; a chain says `(chain)` and lists its calculators when you hover the row. | The template catalogue and the built-in chains. |
| Discipline | Mechanical, Structural, Electrical, Building services, Fluid power, or Chains. | The template. |
| Version | The template version, as printed on every sheet. A chain has none. | The template. |
| Known answers | For a calculator: the known-answer cases (KATs) and how many passed in the last recorded run, for example `3 of 3 pass`. `not run (…)` when no results exist for this version of the template. For a chain: its issued worked example (`1 of 1 pass` means the example exists and the chain's tests generate it). | Catalogue for the count; `docs/testing/kat-results.json` for the result. |
| Unconf. and Second. | How many known-answer cases are **Unconfirmed** (the reference value could not be checked against its source) and how many come from a **Secondary** source (a solutions manual, a website, course notes, a vendor check). For a chain: the sums over its calculators. | The cases declared in the template. |
| Track P re-derivation | The result of the independent re-derivation (Match, Match with findings, Values transfer correctly, Finding, or Not re-derived) and how many findings of Medium or above it raised, fixed or not. | `docs/verification/rederivation/*.md`. |
| Records not entered | Record kinds the calculator can draw on that have no record in this database yet (for example the cable ampacity table). A chain shows the kinds any of its calculators lack. The generic starting materials are not counted. Enter the missing records from your own licensed copy (Reference data, or the record entry screens). | The template's record kinds and the records store. |
| Open defects | Track P findings that `docs/testing/v0.26.0-known-defects.md` does not mark **Fixed**: still open, or waiting for your decision against a licensed standard. Hover the number to see the ids. | The known-defects document, through the snapshot. |
| Manual test | The outcome of your own test of the calculator from the guided test runs. `Not tested` until a run is recorded. | The guided test run records (Addendum C, C-5). |

A row is **Clean** when no case fails, it has no open defect, no Unconfirmed or Secondary case, no record kind missing and its results are current. The manual test is not part of Clean.

## Filtering

The **Discipline** box shows one discipline or every one (Chains are their own). The **Status** box shows the rows that have one condition: Failing, Open defects, Unconfirmed or Secondary, Records not entered, Not run, Not manually tested, or Clean. Both boxes apply together, and the line under them says how many rows are shown and why.

## Exporting for your records

**Export markdown…** and **Export PDF…** save the rows currently shown, with the filter, the date and the application version at the top, the open defects row by row, and a note on each column. Pick the file name in the dialog; nothing is saved if you cancel. If you filter to no rows, the buttons are disabled.

## Where the data comes from, and why it is embedded

The screen must work in an installed build, where there is no `docs` folder. So the facts that live in documents are read once, at build time, into `src/Tempest.Calc/Tempest.Calc.Library/Dashboard/verification-snapshot.json`, which is embedded in the program. The rest (version, known-answer cases and their flags, record kinds, the records you have entered) is read live.

When a document changes, a test fails until the snapshot is regenerated:

1. If a calculator's known-answer cases, version or results changed: `TEMPEST_WRITE_KAT_RESULTS=1 dotnet test tests/Tempest.Calc.Tests.Calculators --filter KatResultsTests` rewrites `docs/testing/kat-results.json` from a real run of every case and every worked example.
2. Then `python3 scripts/generate-verification-snapshot.py` rewrites the embedded snapshot from the re-derivation reports, the known-defects document, the KAT results and the worked examples. `--check` says whether it is current.
3. Commit both files. `VerificationSnapshotStaleTests` hashes the same documents and fails with this list when they differ from what the snapshot was made from.

If the embedded data cannot be read, the screen says so in one sentence and shows only what the catalogue gives.

## Limits

The screen lists at most 500 rows, the exports stop at 500 rows (and the markdown at about 1 MB), and the embedded data is read only up to 4 MB, 1,000 items and 500 findings per item. A chain's open defects are the ones filed under the chain itself (`chain.…` ids); the defects of its calculators are on their own rows.
