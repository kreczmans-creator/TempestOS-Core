# Performance and stability soak

Addendum C, Track S, item C-7. Scripted runs of the real services over a real SQLite database, with time, memory and database size measured, and checks for leaks, stale-flag errors and immutability breaches. This page is written by the soak itself; see "Running it again" at the end.

| | |
|---|---|
| Result | **PASS**: 5 of 5 scenarios clean |
| Mode | full (`TEMPEST_SOAK=full` runs the specification's numbers) |
| Date | 2026-10-26 15:49 UTC |
| Commit | `03d3cce008` on `c7-soak` |
| Machine | Ubuntu 24.04.5 LTS; X64; 4 logical processors (Intel(R) Xeon(R) Processor @ 2.80GHz); 10.0 GiB available to the runtime; .NET 10.0.13; Server GC off |
| Database | SQLite in WAL mode in a temporary folder, `PRAGMA synchronous = FULL (the product's setting)`, a fresh database for every pass |
| Composition | `WorkspaceHost` (the Desktop's own composition, headless): Evidence service, PDF sheet renderer, all 54 calculators, `CalcInstanceService` |
| Duration | 17.3 minutes for all five scenarios, each run twice |

## Summary

Pass 1 / pass 2 are the same scenario run twice in one process, each over a fresh host and database (a third pass is run only to tell a one-off step in the heap from a leak).

| Scenario | What ran | Time, s (pass 1 / 2) | Peak working set, MiB | Peak managed, MiB | DB + WAL, MiB | Heap left after disposal, last pass vs pass 1, MiB | Verdict |
|---|---|---|---|---|---|---|---|
| 500 calculation instances | Calculations created, run and saved: 500; Templates covered: 54; Database growth per calculation (KB): 18.7 | 28.4 / 30.5 | 283.8 | 76.1 | 23.7 | -0.1 | clean |
| Chains of depth 20 | Calculations in chains: 420; Steps reported stale after the head changed: 200; Steps updated in order: 200 | 15.2 / 21.9 | 256.2 | 49.5 | 21.3 | 0.1 | clean |
| 100 revise and supersede cycles | Revisions issued: 101; Edit attempts refused: 1,331; Database growth per issued revision (KB): 179.7 | 20.7 / 20.5 | 317.5 | 68.2 | 32.3 | -0.1 | clean |
| 1,000 Monte Carlo runs | Engine runs (default samples each): 1,000; Calculator evaluations: 10,000,000; Tornado sweeps: 1,000 | 390.3 / 399.2 | 294.3 | 58.9 | 14.5 | 0.0 | clean |
| Imports at their size caps | Largest scan table accepted (bytes): 5,242,880; CAD parameters accepted: 5,000; Refusals one past a cap: 16 | 25.9 / 24.3 | 1422.3 | 908.1 | 15.9 | 0.1 | clean |

## 500 calculation instances

Create, run and save a calculation of every template, from its shipped worked example, in one project.

What ran and what it found (pass 1):

| | |
|---|---|
| Calculations created, run and saved | 500 |
| Templates covered | 54 |
| Database growth per calculation (KB) | 18.7 |
| Create (mean ms) | 21.5 |
| Run again (mean ms) | 24.6 |
| Save again (mean ms) | 9.7 |
| List the whole project (ms) | 64 |
| Staleness of the whole project (ms) | 112 |
| Expectations checked | 5,008 (pass 1), 5,008 (pass 2) |

Measurements:

| | Pass 1 | Pass 2 |
|---|---|---|
| Scenario time, s | 28.4 | 30.5 |
| Host start-up, s | 8.3 | 3.7 |
| Managed heap before, MiB (host running) | 24.9 | 25.9 |
| Managed heap after, MiB (host running) | 27.3 | 27.3 |
| Held for the work, MiB | 2.4 | 1.3 |
| Managed heap after the host is disposed, MiB | 25.1 | 25.0 |
| Peak managed heap while running, MiB | 76.1 | 75.2 |
| Peak working set while running, MiB | 275.1 | 283.8 |
| Process peak working set so far, MiB | 274.8 | 283.2 |
| tempest.db + WAL before the scenario (the seeded database), MiB | 14.5 | 14.4 |
| tempest.db, MiB | 19.7 | 19.7 |
| tempest.db-wal, MiB | 4.0 | 4.0 |
| tempest.db after shutdown, MiB | 19.7 | 19.7 |
| Open handles: before the host / before the scenario / after / after dispose | 193 / 257 / 257 / 260 | 260 / 267 / 267 / 260 |
| Handles on the database files: before / after / after dispose | 5 / 5 / 0 | 7 / 7 / 0 |
| Generation 2 collections | 4 | 4 |
| Allocated, GiB | 1.56 | 1.55 |

Leak check: the heap left after disposal changed by **-0.1 MiB** from pass 1 to pass 2 (bound +4.0 MiB); no leak, slowdown or handle growth found.

No failed expectation, no stale-flag error, no immutability breach, no leak.

## Chains of depth 20

Link calculations 20 deep; change the head; every step goes stale; update in order; every step is current.

What ran and what it found (pass 1):

| | |
|---|---|
| Chains | 20 |
| Links per chain | 20 |
| Calculations in chains | 420 |
| Steps reported stale after the head changed | 200 |
| Steps updated in order | 200 |
| Link a step (mean ms) | 10.7 |
| Update a step (mean ms) | 10.8 |
| Staleness query (mean ms) | 9.5 |
| Expectations checked | 2,490 (pass 1), 2,490 (pass 2) |

Measurements:

| | Pass 1 | Pass 2 |
|---|---|---|
| Scenario time, s | 15.2 | 21.9 |
| Host start-up, s | 6.7 | 7.0 |
| Managed heap before, MiB (host running) | 25.8 | 25.8 |
| Managed heap after, MiB (host running) | 27.2 | 27.2 |
| Held for the work, MiB | 1.4 | 1.5 |
| Managed heap after the host is disposed, MiB | 25.3 | 25.4 |
| Peak managed heap while running, MiB | 49.5 | 49.1 |
| Peak working set while running, MiB | 255.9 | 256.2 |
| Process peak working set so far, MiB | 283.2 | 283.2 |
| tempest.db + WAL before the scenario (the seeded database), MiB | 14.6 | 14.5 |
| tempest.db, MiB | 17.4 | 17.3 |
| tempest.db-wal, MiB | 4.0 | 4.0 |
| tempest.db after shutdown, MiB | 17.4 | 17.4 |
| Open handles: before the host / before the scenario / after / after dispose | 260 / 267 / 267 / 260 | 260 / 267 / 267 / 260 |
| Handles on the database files: before / after / after dispose | 7 / 7 / 0 | 7 / 7 / 0 |
| Generation 2 collections | 0 | 0 |
| Allocated, GiB | 2.27 | 2.27 |

Leak check: the heap left after disposal changed by **+0.1 MiB** from pass 1 to pass 2 (bound +4.0 MiB); no leak, slowdown or handle growth found.

No failed expectation, no stale-flag error, no immutability breach, no leak.

## 100 revise and supersede cycles

Revise, change, check, issue and supersede one calculation over and over; every issued revision stays byte-identical and refuses edits.

What ran and what it found (pass 1):

| | |
|---|---|
| Revisions issued | 101 |
| Database growth per issued revision (KB) | 179.7 |
| Revise → supersede (mean ms) | 37.3 |
| Check (mean ms) | 14.3 |
| Issue incl. PDF sheet (mean ms) | 115.0 |
| Edit attempts refused | 1,331 |
| Immutability checks of issued revisions | 6,468 |
| Longest revision label (characters) | 76 |
| PDF sheet (mean KB) | 142.3 |
| Expectations checked | 6,468 (pass 1), 6,468 (pass 2) |

Measurements:

| | Pass 1 | Pass 2 |
|---|---|---|
| Scenario time, s | 20.7 | 20.5 |
| Host start-up, s | 5.5 | 5.0 |
| Managed heap before, MiB (host running) | 25.8 | 26.9 |
| Managed heap after, MiB (host running) | 27.3 | 27.1 |
| Held for the work, MiB | 1.5 | 0.3 |
| Managed heap after the host is disposed, MiB | 26.4 | 26.2 |
| Peak managed heap while running, MiB | 66.5 | 68.2 |
| Peak working set while running, MiB | 317.5 | 317.3 |
| Process peak working set so far, MiB | 317.0 | 317.1 |
| tempest.db + WAL before the scenario (the seeded database), MiB | 14.6 | 14.4 |
| tempest.db, MiB | 28.1 | 28.0 |
| tempest.db-wal, MiB | 4.2 | 4.1 |
| tempest.db after shutdown, MiB | 28.6 | 28.6 |
| Open handles: before the host / before the scenario / after / after dispose | 260 / 267 / 267 / 260 | 260 / 267 / 267 / 260 |
| Handles on the database files: before / after / after dispose | 7 / 7 / 0 | 7 / 7 / 0 |
| Generation 2 collections | 11 | 11 |
| Allocated, GiB | 2.39 | 2.38 |

Leak check: the heap left after disposal changed by **-0.1 MiB** from pass 1 to pass 2 (bound +4.0 MiB); no leak, slowdown or handle growth found.

No failed expectation, no stale-flag error, no immutability breach, no leak.

## 1,000 Monte Carlo runs

The Monte Carlo service (engine and through a calculator) at its default sample count, its caps, and a tornado sweep.

What ran and what it found (pass 1):

| | |
|---|---|
| Engine runs (default samples each) | 1,000 |
| Engine samples drawn | 10,000,000 |
| Engine runs, total seconds | 5.7 |
| Engine run at its 1,000,000-sample cap (ms) | 609 |
| Calculator runs | 1,000 |
| Samples per calculator run | 10,000 |
| Calculator evaluations | 10,000,000 |
| Calculator runs, total seconds | 379.7 |
| Calculator evaluation (mean µs) | 38.0 |
| Calculator run at its 100,000-sample cap (s) | 4.0 |
| Tornado sweeps | 1,000 |
| Tornado sweeps, total seconds | 0.19 |
| Expectations checked | 7,022 (pass 1), 7,022 (pass 2) |

Measurements:

| | Pass 1 | Pass 2 |
|---|---|---|
| Scenario time, s | 390.3 | 399.2 |
| Host start-up, s | 3.8 | 3.5 |
| Managed heap before, MiB (host running) | 26.7 | 26.7 |
| Managed heap after, MiB (host running) | 26.2 | 26.2 |
| Held for the work, MiB | -0.6 | -0.5 |
| Managed heap after the host is disposed, MiB | 25.8 | 25.8 |
| Peak managed heap while running, MiB | 58.9 | 58.7 |
| Peak working set while running, MiB | 292.8 | 294.3 |
| Process peak working set so far, MiB | 317.1 | 317.1 |
| tempest.db + WAL before the scenario (the seeded database), MiB | 14.5 | 14.5 |
| tempest.db, MiB | 10.5 | 10.5 |
| tempest.db-wal, MiB | 4.0 | 4.0 |
| tempest.db after shutdown, MiB | 11.1 | 11.1 |
| Open handles: before the host / before the scenario / after / after dispose | 260 / 267 / 267 / 260 | 260 / 267 / 266 / 260 |
| Handles on the database files: before / after / after dispose | 7 / 7 / 0 | 7 / 6 / 0 |
| Generation 2 collections | 3 | 3 |
| Allocated, GiB | 90.15 | 90.15 |

Leak check: the heap left after disposal changed by **+0.0 MiB** from pass 1 to pass 2 (bound +4.0 MiB); no leak, slowdown or handle growth found.

No failed expectation, no stale-flag error, no immutability breach, no leak.

## Imports at their size caps

Scan tables, CAD expression files and parameter CSVs, and reference records, each at its cap and one past it.

What ran and what it found (pass 1):

| | |
|---|---|
| Scan import: stored record after the import (KB) | 9 |
| Scan import at the row cap (s) | 3.7 |
| Refused: scan table one byte over the 5 MB cap (through the reader) | The file is larger than 5 MB and was not read. |
| Refused: scan table one byte over the 5 MB cap (straight to the importer) | A file is larger than 5 MB and was not read. |
| Refused: scan table of 1,000,001 rows, the header among them (one over the row cap) | The file has more than 1000000 rows. |
| Refused: 65 scan files at once | At most 64 files are imported at once. |
| Refused: five 5 MB scan files together (over the 20 MB total) | The tables together are 25 MB; at most 20 MB are imported at once. |
| Refused: expression file of 5,001 parameters (one over the parameter cap) | The file holds more than 5000 parameters. |
| Refused: parameter CSV of 5,001 parameters (one over the parameter cap) | The file holds more than 5000 parameters. |
| Refused: expression file one byte over the 5 MB cap (through the reader) | The file is larger than 5 MB and was not read. |
| Refused: expression file one byte over the 5 MB cap (straight to the importer) | The file is larger than 5 MB and was not read. |
| Inputs CSV at the cap: 10,000 rows, 1,000,000 bytes, read in (ms) | 135 |
| Refused: inputs CSV of 1,000,001 bytes (one over the 1 MB cap) | The file is larger than 1000 kB and was not read. |
| Refused: inputs CSV of 10,001 rows with a final line break (one over the row cap) | The file has more than 10000 rows; only the first 10000 were read. |
| Refused: inputs CSV of 10,001 rows with no final line break (one over the row cap) | The file has more than 10000 rows; only the first 10000 were read. |
| Imported table saved and read back, calculation run (ms) | 61 |
| Frame of 2,000 nodes, 3,910 members, 20,000 loads: run (s) | 5.6 |
| Frame at its caps: outcome | 13 outputs, 0 checks |
| Frame at its caps: saved or refused | refused after 6.0 s: Calculation instance '842927db-4d69-46f2-afc9-5d6526296ca0' is larger than 4 MB and was not written. |
| Reference record at its caps: saved in (ms) | 40 |
| Refused: reference record with 201 values | A reference record holds between 1 and 200 values. |
| Refused: reference record with 501 table rows | A table in a reference record holds at most 500 rows. |
| Refused: reference record with 51 cells in a row | A row of a reference record holds at most 50 cells of at most 200 characters. |
| Refused: reference record with a 201-character text | A reference record's symbols, texts and units are at most 200 characters. |
| Record of 200 tables of 500 × 50 cells | refused: Reference record '58d51ea408b74e88be89ebc610579ca3' is larger than 4 MB and was not written. |
| Largest scan table accepted (bytes) | 5,242,880 |
| Largest scan table accepted (rows) | 136,188 |
| Scan table at the row cap (rows, header included) | 1,000,000 |
| Largest CAD expression file accepted (bytes) | 5,242,880 |
| CAD parameters accepted | 5,000 |
| Refusals one past a cap | 16 |
| Import timing | scan 5,242,880 bytes, 136,188 rows: preview 1.0 s |
| Import timing | scan apply 1.2 s |
| Import timing | scan 4,000,000 bytes, 1,000,000 rows: preview 3.7 s |
| Import timing | CAD expression file 5,242,880 bytes, 5,000 parameters: preview 0.2 s |
| Import timing | CAD parameter CSV 85,015 bytes, 5,000 parameters: preview 0.1 s |
| Expectations checked | 77 (pass 1), 77 (pass 2) |

Measurements:

| | Pass 1 | Pass 2 |
|---|---|---|
| Scenario time, s | 25.9 | 24.3 |
| Host start-up, s | 9.3 | 3.5 |
| Managed heap before, MiB (host running) | 26.6 | 41.0 |
| Managed heap after, MiB (host running) | 40.9 | 41.1 |
| Held for the work, MiB | 14.3 | 0.0 |
| Managed heap after the host is disposed, MiB | 40.5 | 40.6 |
| Peak managed heap while running, MiB | 908.1 | 907.0 |
| Peak working set while running, MiB | 1164.4 | 1422.3 |
| Process peak working set so far, MiB | 1188.7 | 1421.6 |
| tempest.db + WAL before the scenario (the seeded database), MiB | 14.5 | 14.5 |
| tempest.db, MiB | 11.9 | 11.9 |
| tempest.db-wal, MiB | 3.9 | 4.0 |
| tempest.db after shutdown, MiB | 12.8 | 12.8 |
| Open handles: before the host / before the scenario / after / after dispose | 260 / 267 / 269 / 262 | 262 / 269 / 269 / 262 |
| Handles on the database files: before / after / after dispose | 7 / 7 / 0 | 7 / 7 / 0 |
| Generation 2 collections | 12 | 10 |
| Allocated, GiB | 10.13 | 10.11 |

Leak check: the heap left after disposal changed by **+0.1 MiB** from pass 1 to pass 2 (bound +4.0 MiB); no leak, slowdown or handle growth found.

No failed expectation, no stale-flag error, no immutability breach, no leak.

## Method

- **Time** is the wall-clock time of the scenario itself on a started host; the host's start-up is measured separately and shown per scenario.
- **Managed heap** is `GC.GetTotalMemory` after three forced, compacting collections and a wait for finalisers: once before the scenario, once after it with the host still running (what the application holds for that data), and once after the host is disposed (what is left that nothing owns).
- **Peak working set and peak managed heap** are the highest values a sampler thread saw (every 100 ms) while the scenario ran, not the values at its end. The process's own peak working set is also shown (it includes every pass before it).
- **Database size** is `tempest.db` and its write-ahead log at the end of the scenario with the host still running, and `tempest.db` again once the host is shut down and the log has been folded in.
- **Handles** are the process's open file descriptors (`/proc/self/fd`), and, separately, those open on the database's own files, which is the footprint of the pooled SQLite connections. The process's handles after the second pass's host is disposed are compared with those after the first's (the first pass also opens what loading the application's assemblies opens, once); the database's handles must not grow during the scenario and must be none once the host is disposed.
- **Leak check.** Each scenario runs twice in the same process. The managed heap left after the second pass's host is disposed may not exceed the first pass's by more than 4 MiB (a leak worth the name is tens of kilobytes per item over hundreds of items, many times that, and keeps growing). If it does, a third pass is run: the heap is a leak only if it is still rising by more than half the bound; a one-off step (buffers the runtime keeps for a while after a large file is read) that then stays flat is reported as such and is not. The process may end the second pass with no more than 3 more handles than the first, the database's handles may not grow by more than 3 during a scenario and must be none after the host is disposed, and the second pass may not take more than 2 times the first plus five seconds.
- **Stale-flag errors** are both kinds: a calculation reported stale when nothing it depends on changed, and one not reported stale when something did.
- **Immutability breaches** are any change to an issued revision after its issue: its stored record (with only its status and time set aside, the two fields supersession changes), its outputs, its sheet text, its PDF sheet in the content store, or its Evidence; and any attempt to edit one that is not refused.

### How the specification's numbers were read

- **500 calc instances**: 500 calculations created round the 54 templates from their shipped worked examples, each run again and saved again, in one project.
- **Chains of depth 20**: chains of 20 links (21 calculations) of the ISO 286 fit calculator, each the nominal size of the next; a link past the depth cap is refused. Two chains share a project so that one chain's change can be seen to leave the other fresh.
- **100 revise/supersede cycles**: 100 times over one calculation: revise (the issued revision becomes Superseded), change an input, check by someone else, issue with its PDF sheet; 101 revisions in all.
- **1,000 Monte Carlo runs**: 1,000 runs of the Monte Carlo service, each at its default sample count (10,000), which is inside the service's own cap (1,000,000 for the engine). The service over a whole calculator re-runs the calculator once per sample and has a lower cap (100,000); it is run 1,000 times at 10,000 samples each, the default, and once at its cap. 1,000 tornado sweeps are run as well.
- **Imports of max-size files**: scan deviation tables at the 5 MB byte cap and at the one-million-row cap, a CAD expression file at the 5 MB cap with 5,000 parameters (the parameter cap), a CAD parameter CSV at the parameter cap, and a reference record at every one of its caps; then one past each cap, which must be refused with a plain sentence and leave the calculation byte for byte as it was. Record import from CSV (Addendum C, C-3) is not in this build, so it is not soaked.

## Notes

<!-- soak:notes:begin -->
### Result

Everything the specification asks for was run at its numbers, in 17.3 minutes (the brief allowed about 30), and nothing was dropped or scaled down. No scenario found a leak, a stale-flag error or an immutability breach. The soak found two defects while it was being built; both are fixed and each has a test that failed before the fix.

### Defects found and fixed

1. **Inputs CSV row cap off by one** (`CalcCsv.ImportInputs`, cap 10,000 rows with the header counted). The cap was checked only as each line ended, so a file of 10,001 rows whose last line had no line break after it was read in full, and the row that crossed the cap was kept even when it was refused. Now the last line is checked too and the row that goes over is not read. Pinned by `CsvRowCapTests` (the 10,001-row file without a final line break was accepted before the fix).
2. **The 4 MB stored-document cap was checked after the whole document had been built** (`CalcInstanceStore`: instances, reference records, import and scan profiles). A reference record that is within every shape cap (200 values, 500 rows, 50 cells to a row) is some 250 million characters of JSON, so one refused save took about 1.5 GiB of managed heap and a 1.8 GiB working set (measured in the scaled-down run before the fix). The document is now written through a buffer that refuses at the cap, so a save past it builds at most the cap's worth; the text written, the cap and the refusal sentence are unchanged. Pinned by `StoreDocumentCapTests` (a record four times the cap allocated 62 MiB before the fix against a bound of 32 MiB).

### Observations, nothing changed

- **Revision labels after Z.** The 27th revision of a calculation is labelled `Z+`, the 28th `Z++`, and so on: the 101st is a 76-character label. Everything works with it (Evidence, the register, file names, the PDF sheet; there is no cap on the label), but it reads badly on a sheet. An owner decision, not a fault.
- **Saving a calculation past 4 MB.** `CalcInstanceService.CreateAsync` throws an `InvalidOperationException` (the screen's guard turns it into a message) where `UpdateInputsAsync` returns a refusal, and the sentence names the calculation by its identifier, not its name. The soak met it with a frame at its table caps (2,000 nodes, 3,910 members, 20,000 loads): the calculator runs it in about 6 s (the imports scenario that contains it peaks at about 900 MiB managed and 1.4 GiB working set), and the calculation cannot then be saved because its stored record is past 4 MB.
- **Scan row cap.** The 1,000,000-row cap counts every row of the file, header included, as the inputs CSV's does; a table of 1,000,000 data rows is refused ("more than 1000000 rows"). The importer took 3.7 s for the largest table that is allowed.
- **Worked examples that cite a shipped material** (for example `struct.plate-bending`) are reported stale ("the reference record no longer exists") on a database whose library has not been seeded, and fresh as soon as it is. That is the stale flag being right; the library seeds itself the first time a screen asks for a material, and the soak does the same first, after checking the flag in both states.
- **First pass opens 67 more handles than the process had** (assemblies and libraries, loaded once); the second pass ends with exactly as many as the first. No handle is left open on the database's files after any host is disposed (0 in every pass).
- **One-off step in the heap.** Reading a very large file can leave the settled heap 4 to 10 MiB higher after the next pass (a buffer the runtime keeps for a while); the check runs a third pass when it sees a step and calls it a leak only if the heap is still rising. In this run no scenario needed a third pass.

### Reading the numbers

- **Time.** 500 calculations (create, run, save, run again, save again) take 28 s, about 57 ms each; a step of a 20-link chain links in about 11 ms and updates in about 11 ms; an issued revision with its PDF sheet takes about 115 ms to issue and 37 ms to revise. The Monte Carlo service draws 10,000,000 samples through a real calculator in about 6.3 minutes (38 µs a sample, one whole calculator run each) and 10,000,000 through the engine in under 6 seconds.
- **Memory.** The working set peaks at 256 to 318 MiB in every scenario except the imports, which peak at 1.4 GiB (the frame at its caps and the largest tables; managed peak 908 MiB). After a scenario the application holds only 1 to 2.5 MiB more than before it, for 500 calculations, 420 chained ones or 101 issued revisions: the data lives in the database, not in memory. (The imports hold 14 MiB more after their first pass and nothing more after the second: buffers kept once.)
- **Database.** A seeded, empty database is 14.5 MiB. 500 calculations add 9 MiB (18.7 KB each); 101 issued revisions add 18 MiB (180 KB each, of which the PDF sheet is 142 KB); 420 chained calculations add 7 MiB. The write-ahead log stays at about 4 MiB.
- **The scaled-down run** (an ordinary test, about 100 s for all five scenarios) uses `PRAGMA synchronous = NORMAL` like the rest of the Desktop suite; this full run used `FULL`, the product's setting.
- **This machine is shared.** Other builds and test runs ran on it at the same time (the load average was 4 to 10 on 4 processors), so times are an upper bound; the memory, size and handle figures do not depend on it.

### Not soaked

- Record import from CSV (Addendum C, C-3) is not in this build. The reference-record caps are soaked through the service instead.
- The packs, the Evidence screens and the PDF viewer are not part of this item.
<!-- soak:notes:end -->

## Running it again

```
# the scaled-down soak (about a tenth), part of the ordinary Desktop test run:
dotnet test tests/Tempest.Desktop.Tests --filter "FullyQualifiedName~Tempest.Desktop.Tests.Soak"

# the full soak, which rewrites this page (about 20 minutes):
TEMPEST_SOAK=full dotnet test tests/Tempest.Desktop.Tests --filter "FullyQualifiedName~Tempest.Desktop.Tests.Soak.SoakFull"
```

`TEMPEST_SOAK_LOG=<file>` appends one line per step to a file, to watch a long run.
