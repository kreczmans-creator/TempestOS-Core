# Performance and stability soak

Addendum C, Track S, item C-7. Scripted runs of the real services over a real SQLite database, with time, memory and database size measured, and checks for leaks, stale-flag errors and immutability breaches. This page is written by the soak itself; see "Running it again" at the end.

| | |
|---|---|
| Result | **PASS**: 9 of 9 scenarios clean |
| Mode | full (`TEMPEST_SOAK=full` runs the specification's numbers) |
| Date | 2026-10-26 17:11 UTC |
| Commit | `32422f6fd4` on `c7-fix` |
| Machine | Ubuntu 24.04.5 LTS; X64; 4 logical processors (Intel(R) Xeon(R) Processor @ 2.80GHz); 10.0 GiB available to the runtime; .NET 10.0.13; Server GC off |
| Database | SQLite in WAL mode in a temporary folder, `PRAGMA synchronous = FULL (the product's setting)`, a fresh database for every pass |
| Composition | `WorkspaceHost` (the Desktop's own composition, headless): Evidence service, PDF sheet renderer, all 54 calculators, `CalcInstanceService` |
| Duration | 25.0 minutes for all 9 scenarios, each run twice |

## Summary

Pass 1 / pass 2 are the same scenario run twice in one process, each over a fresh host and database (a third pass is run only to tell a one-off step in the heap from a leak).

| Scenario | What ran | Time, s (pass 1 / 2) | Peak working set, MiB | Peak managed, MiB | DB + WAL, MiB | Heap left after disposal, last pass vs pass 1, MiB | Verdict |
|---|---|---|---|---|---|---|---|
| 500 calculation instances | Calculations created, run and saved: 500; Templates covered: 54; Database growth per calculation (KB): 19.3 | 25.7 / 30.9 | 288.0 | 71.6 | 23.9 | -2.3 | clean |
| Chains of depth 20 | Calculations in chains: 420; Steps reported stale after the head changed: 200; Steps updated in order: 200 | 14.7 / 15.6 | 258.8 | 51.7 | 21.3 | 1.5 | clean |
| 100 revise and supersede cycles | Revisions issued: 101; Edit attempts refused: 1,331; Database growth per issued revision (KB): 180.3 | 20.4 / 17.8 | 325.9 | 74.8 | 32.2 | -1.2 | clean |
| 1,000 Monte Carlo runs | Engine runs (default samples each): 1,000; Calculator evaluations: 10,000,000; Tornado sweeps: 1,000 | 341.4 / 351.6 | 303.9 | 61.7 | 14.6 | 0.0 | clean |
| Imports at their size caps | Largest scan table accepted (bytes): 5,242,880; CAD parameters accepted: 5,000; Refusals one past a cap: 16 | 23.4 / 23.1 | 1584.3 | 910.1 | 17.1 | 0.0 | clean |
| Record import at its caps | Templates imported: 7; Records committed at the caps: 1,000; Refusals one past a cap: 4 | 37.2 / 35.4 | 861.8 | 389.3 | 32.7 | -0.5 | clean |
| Demo project installed and removed | Install and remove cycles: 20; Install (mean ms): 3781; Database a removal leaves, first cycle (KiB): 780 | 84.7 / 86.6 | 347.9 | 86.3 | 31.2 | 0.0 | clean |
| Verification dashboard built repeatedly | Reference records in the store: 10,000; Finished guided runs in the store: 1,000; Build on the full store: mean (ms): 807 | 40.7 / 38.8 | 566.7 | 320.2 | 54.7 | 0.0 | clean |
| Guided test runs to the run cap | Runs made through the service (started, 110 steps recorded, finished): 1,000; Record one step (mean ms): 0.84; Delete older finished runs: deleted, kept (the newest 50 and those holding a template's latest result): 950, 50 | 99.3 / 96.4 | 468.0 | 225.1 | 39.3 | 0.0 | clean |

## 500 calculation instances

Create, run and save a calculation of every template, from its shipped worked example, in one project.

What ran and what it found (pass 1):

| | |
|---|---|
| Calculations created, run and saved | 500 |
| Templates covered | 54 |
| Database growth per calculation (KB) | 19.3 |
| Create (mean ms) | 19.3 |
| Run again (mean ms) | 22.6 |
| Save again (mean ms) | 8.6 |
| List the whole project (ms) | 47 |
| Staleness of the whole project (ms) | 135 |
| Expectations checked | 5,008 (pass 1), 5,008 (pass 2) |

Measurements:

| | Pass 1 | Pass 2 |
|---|---|---|
| Scenario time, s | 25.7 | 30.9 |
| Host start-up, s | 10.2 | 3.7 |
| Managed heap before, MiB (host running) | 29.1 | 28.0 |
| Managed heap after, MiB (host running) | 31.6 | 29.4 |
| Held for the work, MiB | 2.6 | 1.3 |
| Managed heap after the host is disposed, MiB | 29.5 | 27.1 |
| Peak managed heap while running, MiB | 71.6 | 65.9 |
| Peak working set while running, MiB | 269.1 | 288.0 |
| Process peak working set so far, MiB | 268.5 | 287.2 |
| tempest.db + WAL before the scenario (the seeded database), MiB | 14.5 | 14.5 |
| tempest.db, MiB | 19.6 | 19.6 |
| tempest.db-wal, MiB | 4.3 | 4.0 |
| tempest.db after shutdown, MiB | 19.7 | 19.7 |
| Open handles: before the host / before the scenario / after / after dispose | 193 / 259 / 259 / 260 | 260 / 267 / 267 / 260 |
| Handles on the database files: before / after / after dispose | 7 / 7 / 0 | 7 / 7 / 0 |
| Generation 2 collections | 4 | 4 |
| Allocated, GiB | 1.59 | 1.59 |

Leak check: the heap left after disposal changed by **-2.3 MiB** from pass 1 to pass 2 (bound +4.0 MiB); no leak, slowdown or handle growth found.

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
| Link a step (mean ms) | 10.4 |
| Update a step (mean ms) | 10.4 |
| Staleness query (mean ms) | 9.0 |
| Expectations checked | 2,490 (pass 1), 2,490 (pass 2) |

Measurements:

| | Pass 1 | Pass 2 |
|---|---|---|
| Scenario time, s | 14.7 | 15.6 |
| Host start-up, s | 3.5 | 3.5 |
| Managed heap before, MiB (host running) | 27.9 | 27.9 |
| Managed heap after, MiB (host running) | 29.4 | 29.4 |
| Held for the work, MiB | 1.5 | 1.5 |
| Managed heap after the host is disposed, MiB | 27.5 | 29.0 |
| Peak managed heap while running, MiB | 51.6 | 51.7 |
| Peak working set while running, MiB | 258.3 | 258.8 |
| Process peak working set so far, MiB | 287.2 | 287.2 |
| tempest.db + WAL before the scenario (the seeded database), MiB | 14.5 | 14.4 |
| tempest.db, MiB | 17.3 | 17.3 |
| tempest.db-wal, MiB | 4.0 | 4.0 |
| tempest.db after shutdown, MiB | 17.4 | 17.4 |
| Open handles: before the host / before the scenario / after / after dispose | 260 / 267 / 267 / 260 | 260 / 267 / 267 / 260 |
| Handles on the database files: before / after / after dispose | 7 / 7 / 0 | 7 / 7 / 0 |
| Generation 2 collections | 0 | 0 |
| Allocated, GiB | 2.30 | 2.30 |

Leak check: the heap left after disposal changed by **+1.5 MiB** from pass 1 to pass 2 (bound +4.0 MiB); no leak, slowdown or handle growth found.

No failed expectation, no stale-flag error, no immutability breach, no leak.

## 100 revise and supersede cycles

Revise, change, check, issue and supersede one calculation over and over; every issued revision stays byte-identical and refuses edits.

What ran and what it found (pass 1):

| | |
|---|---|
| Revisions issued | 101 |
| Database growth per issued revision (KB) | 180.3 |
| Revise → supersede (mean ms) | 42.1 |
| Check (mean ms) | 14.6 |
| Issue incl. PDF sheet (mean ms) | 108.2 |
| Edit attempts refused | 1,331 |
| Immutability checks of issued revisions | 6,468 |
| Longest revision label (characters) | 76 |
| PDF sheet (mean KB) | 142.3 |
| Expectations checked | 6,468 (pass 1), 6,468 (pass 2) |

Measurements:

| | Pass 1 | Pass 2 |
|---|---|---|
| Scenario time, s | 20.4 | 17.8 |
| Host start-up, s | 5.4 | 3.3 |
| Managed heap before, MiB (host running) | 29.8 | 30.9 |
| Managed heap after, MiB (host running) | 31.3 | 31.1 |
| Held for the work, MiB | 1.5 | 0.3 |
| Managed heap after the host is disposed, MiB | 30.4 | 29.2 |
| Peak managed heap while running, MiB | 70.2 | 74.8 |
| Peak working set while running, MiB | 320.4 | 325.9 |
| Process peak working set so far, MiB | 319.7 | 325.1 |
| tempest.db + WAL before the scenario (the seeded database), MiB | 14.4 | 14.5 |
| tempest.db, MiB | 28.1 | 27.9 |
| tempest.db-wal, MiB | 4.2 | 4.2 |
| tempest.db after shutdown, MiB | 28.6 | 28.6 |
| Open handles: before the host / before the scenario / after / after dispose | 260 / 267 / 267 / 260 | 260 / 267 / 267 / 260 |
| Handles on the database files: before / after / after dispose | 7 / 7 / 0 | 7 / 7 / 0 |
| Generation 2 collections | 9 | 9 |
| Allocated, GiB | 2.39 | 2.38 |

Leak check: the heap left after disposal changed by **-1.2 MiB** from pass 1 to pass 2 (bound +4.0 MiB); no leak, slowdown or handle growth found.

No failed expectation, no stale-flag error, no immutability breach, no leak.

## 1,000 Monte Carlo runs

The Monte Carlo service (engine and through a calculator) at its default sample count, its caps, and a tornado sweep.

What ran and what it found (pass 1):

| | |
|---|---|
| Engine runs (default samples each) | 1,000 |
| Engine samples drawn | 10,000,000 |
| Engine runs, total seconds | 4.4 |
| Engine run at its 1,000,000-sample cap (ms) | 438 |
| Calculator runs | 1,000 |
| Samples per calculator run | 10,000 |
| Calculator evaluations | 10,000,000 |
| Calculator runs, total seconds | 333.1 |
| Calculator evaluation (mean µs) | 33.3 |
| Calculator run at its 100,000-sample cap (s) | 3.3 |
| Tornado sweeps | 1,000 |
| Tornado sweeps, total seconds | 0.18 |
| Expectations checked | 7,022 (pass 1), 7,022 (pass 2) |

Measurements:

| | Pass 1 | Pass 2 |
|---|---|---|
| Scenario time, s | 341.4 | 351.6 |
| Host start-up, s | 2.8 | 3.7 |
| Managed heap before, MiB (host running) | 29.7 | 29.7 |
| Managed heap after, MiB (host running) | 29.2 | 29.2 |
| Held for the work, MiB | -0.5 | -0.5 |
| Managed heap after the host is disposed, MiB | 28.8 | 28.8 |
| Peak managed heap while running, MiB | 61.7 | 61.7 |
| Peak working set while running, MiB | 303.9 | 301.3 |
| Process peak working set so far, MiB | 325.1 | 325.1 |
| tempest.db + WAL before the scenario (the seeded database), MiB | 14.6 | 14.5 |
| tempest.db, MiB | 10.7 | 11.1 |
| tempest.db-wal, MiB | 4.0 | 0.0 |
| tempest.db after shutdown, MiB | 11.1 | 11.1 |
| Open handles: before the host / before the scenario / after / after dispose | 260 / 267 / 267 / 260 | 260 / 267 / 265 / 260 |
| Handles on the database files: before / after / after dispose | 7 / 7 / 0 | 7 / 5 / 0 |
| Generation 2 collections | 3 | 3 |
| Allocated, GiB | 90.14 | 90.14 |

Leak check: the heap left after disposal changed by **+0.0 MiB** from pass 1 to pass 2 (bound +4.0 MiB); no leak, slowdown or handle growth found.

No failed expectation, no stale-flag error, no immutability breach, no leak.

## Imports at their size caps

Scan tables, CAD expression files and parameter CSVs, and reference records, each at its cap and one past it.

What ran and what it found (pass 1):

| | |
|---|---|
| Scan import: stored record after the import (KB) | 9 |
| Scan import at the row cap (s) | 3.3 |
| Refused: scan table one byte over the 5 MB cap (through the reader) | The file is larger than 5 MB and was not read. |
| Refused: scan table one byte over the 5 MB cap (straight to the importer) | A file is larger than 5 MB and was not read. |
| Refused: scan table of 1,000,001 rows, the header among them (one over the row cap) | The file has more than 1000000 rows. |
| Refused: 65 scan files at once | At most 64 files are imported at once. |
| Refused: five 5 MB scan files together (over the 20 MB total) | The tables together are 25 MB; at most 20 MB are imported at once. |
| Refused: expression file of 5,001 parameters (one over the parameter cap) | The file holds more than 5000 parameters. |
| Refused: parameter CSV of 5,001 parameters (one over the parameter cap) | The file holds more than 5000 parameters. |
| Refused: expression file one byte over the 5 MB cap (through the reader) | The file is larger than 5 MB and was not read. |
| Refused: expression file one byte over the 5 MB cap (straight to the importer) | The file is larger than 5 MB and was not read. |
| Inputs CSV at the cap: 10,000 rows, 1,000,000 bytes, read in (ms) | 102 |
| Refused: inputs CSV of 1,000,001 bytes (one over the 1 MB cap) | The file is larger than 1000 kB and was not read. |
| Refused: inputs CSV of 10,001 rows with a final line break (one over the row cap) | The file has more than 10000 rows; only the first 10000 were read. |
| Refused: inputs CSV of 10,001 rows with no final line break (one over the row cap) | The file has more than 10000 rows; only the first 10000 were read. |
| Imported table saved and read back, calculation run (ms) | 136 |
| Frame of 2,000 nodes, 3,910 members, 20,000 loads: run (s) | 5.6 |
| Frame at its caps: outcome | 13 outputs, 0 checks |
| Frame at its caps: saved or refused | refused after 5.4 s: Calculation 'Soak frame at its caps' is larger than 4 MB and was not saved; take some of its table rows out and save it again. |
| Reference record at its caps: saved in (ms) | 25 |
| Refused: reference record with 201 values | A reference record holds between 1 and 200 values. |
| Refused: reference record with 501 table rows | A table in a reference record holds at most 500 rows. |
| Refused: reference record with 51 cells in a row | A row of a reference record holds at most 50 cells of at most 200 characters. |
| Refused: reference record with a 201-character text | A reference record's symbols, texts and units are at most 200 characters. |
| Record of 200 tables of 500 × 50 cells | refused: Reference record 'dd0e7e0581da416688550d5d52d4a01e' is larger than 4 MB and was not written. |
| Largest scan table accepted (bytes) | 5,242,880 |
| Largest scan table accepted (rows) | 136,188 |
| Scan table at the row cap (rows, header included) | 1,000,000 |
| Largest CAD expression file accepted (bytes) | 5,242,880 |
| CAD parameters accepted | 5,000 |
| Refusals one past a cap | 16 |
| Import timing | scan 5,242,880 bytes, 136,188 rows: preview 0.9 s |
| Import timing | scan apply 0.8 s |
| Import timing | scan 4,000,000 bytes, 1,000,000 rows: preview 3.3 s |
| Import timing | CAD expression file 5,242,880 bytes, 5,000 parameters: preview 0.2 s |
| Import timing | CAD parameter CSV 85,015 bytes, 5,000 parameters: preview 0.1 s |
| Expectations checked | 79 (pass 1), 79 (pass 2) |

Measurements:

| | Pass 1 | Pass 2 |
|---|---|---|
| Scenario time, s | 23.4 | 23.1 |
| Host start-up, s | 3.3 | 3.1 |
| Managed heap before, MiB (host running) | 29.6 | 44.0 |
| Managed heap after, MiB (host running) | 44.0 | 44.0 |
| Held for the work, MiB | 14.4 | 0.0 |
| Managed heap after the host is disposed, MiB | 43.6 | 43.6 |
| Peak managed heap while running, MiB | 909.8 | 910.1 |
| Peak working set while running, MiB | 1204.7 | 1584.3 |
| Process peak working set so far, MiB | 1204.1 | 1583.9 |
| tempest.db + WAL before the scenario (the seeded database), MiB | 14.4 | 14.7 |
| tempest.db, MiB | 11.9 | 12.8 |
| tempest.db-wal, MiB | 4.1 | 4.3 |
| tempest.db after shutdown, MiB | 12.8 | 12.8 |
| Open handles: before the host / before the scenario / after / after dispose | 260 / 267 / 269 / 262 | 262 / 269 / 269 / 262 |
| Handles on the database files: before / after / after dispose | 7 / 7 / 0 | 7 / 7 / 0 |
| Generation 2 collections | 13 | 3 |
| Allocated, GiB | 10.14 | 10.12 |

Leak check: the heap left after disposal changed by **+0.0 MiB** from pass 1 to pass 2 (bound +4.0 MiB); no leak, slowdown or handle growth found.

No failed expectation, no stale-flag error, no immutability breach, no leak.

## Record import at its caps

Track P record templates imported at the 5 MB, 1,001-row, 64-column and 4,096-character caps, one past each cap refused, all or nothing, and the licence lock after many revisions.

What ran and what it found (pass 1):

| | |
|---|---|
| File at the caps: preview (ms) | 433 |
| File at the caps: commit (ms) | 494 |
| Refused: a file one byte over the 5 MB cap | The file is larger than 5 MB and was not read. |
| Refused: a file of 1,002 rows (one over the 1,001-row cap, the header among them) | The file has more than 1001 rows. |
| Refused: a file of 65 columns (one over the 64-column cap) | Line 1 has more than 64 columns. |
| Refused: a field of 4,097 characters (one over the 4,096-character cap) | A field on line 2 is longer than 4096 characters. |
| Also refused: a 4,096-character designation (past the 200 characters a record's text may hold) | Error, line 4, designation: designation: the text is longer than 200 characters. |
| Also refused: an empty file | The file is empty. |
| Also refused: a header with no data rows | Error: The file holds a header row and no data rows. |
| All or nothing: one bad row (last of 1,000) saved | 0 |
| All or nothing: one bad row (first of 1,000) saved | 0 |
| All or nothing: batch of 201 with its last record refused, saved | 0 |
| Licensed record: revisions saved | 101 |
| Licensed record: attempts to release it accepted | 0 |
| Licensed record: revisions that exported | 0 |
| Licensed record: seconds for the revisions | 27.1 |
| Templates imported | 7 |
| Records committed at the caps | 1,000 |
| Refusals one past a cap | 4 |
| Database growth per record (KB) | 3.4 |
| Import timing | template 24, 1,000 rows, 1,000 record(s): preview 76 ms, commit 110 ms |
| Import timing | template 12, 1,000 rows, 1,000 record(s): preview 83 ms, commit 167 ms |
| Import timing | template 06, 1,000 rows, 1,000 record(s): preview 164 ms, commit 176 ms |
| Import timing | template 04, 60 rows, 1 record(s): preview 137 ms, commit 83 ms |
| Import timing | template 02, 40 rows, 1 record(s): preview 140 ms, commit 79 ms |
| Import timing | template 45, 12 rows, 1 record(s): preview 152 ms, commit 68 ms |
| Import timing | template 62, 300 rows, 300 record(s): preview 219 ms, commit 224 ms |
| Import timing | file at the caps (5,242,880 bytes, 1,001 rows, 64 columns, 4,096-character fields): preview 433 ms, commit 494 ms |
| Expectations checked | 357 (pass 1), 357 (pass 2), 357 (pass 3) |

Measurements:

| | Pass 1 | Pass 2 | Pass 3 |
|---|---|---|---|
| Scenario time, s | 37.2 | 35.4 | 36.4 |
| Host start-up, s | 3.1 | 3.4 | 2.7 |
| Managed heap before, MiB (host running) | 44.1 | 30.1 | 30.1 |
| Managed heap after, MiB (host running) | 29.7 | 29.7 | 29.7 |
| Held for the work, MiB | -14.4 | -0.4 | -0.4 |
| Managed heap after the host is disposed, MiB | 29.3 | 29.3 | 28.8 |
| Peak managed heap while running, MiB | 385.9 | 389.3 | 372.3 |
| Peak working set while running, MiB | 861.8 | 657.6 | 649.4 |
| Process peak working set so far, MiB | 1583.9 | 1583.9 | 1583.9 |
| tempest.db + WAL before the scenario (the seeded database), MiB | 14.4 | 14.5 | 14.6 |
| tempest.db, MiB | 24.8 | 24.8 | 24.7 |
| tempest.db-wal, MiB | 4.6 | 4.5 | 7.9 |
| tempest.db after shutdown, MiB | 25.0 | 25.0 | 25.0 |
| Open handles: before the host / before the scenario / after / after dispose | 262 / 269 / 269 / 262 | 262 / 269 / 269 / 262 | 262 / 269 / 269 / 262 |
| Handles on the database files: before / after / after dispose | 7 / 7 / 0 | 7 / 7 / 0 | 7 / 7 / 0 |
| Generation 2 collections | 43 | 44 | 45 |
| Allocated, GiB | 9.51 | 9.51 | 9.51 |

Leak check: the heap left after disposal changed by **-0.5 MiB** from pass 1 to pass 3 (bound +4.0 MiB); no leak, slowdown or handle growth found.

A third pass was run because pass 2 left the heap 0.0 MiB above pass 1. Pass 3 left it -0.5 MiB above pass 2, so the heap did not keep growing: a one-off step (buffers the runtime keeps for a while), not a leak.

No failed expectation, no stale-flag error, no immutability breach, no leak.

## Demo project installed and removed

The demonstration project installed and removed over and over, a real project beside it byte for byte unchanged, nothing leaked and the database growing only by what a removal keeps by design.

What ran and what it found (pass 1):

| | |
|---|---|
| Install and remove cycles | 20 |
| Calculations and packs per install | 60 and 13 |
| Install (mean ms) | 3781 |
| Install (first / last ms) | 3883 / 3916 |
| Remove (mean ms) | 210 |
| Remove (first / last ms) | 229 / 191 |
| Database an install adds (KiB) | 1276 |
| Calculation and pack documents an install writes and a removal deletes (KiB) | 524 |
| Database a removal gives back (KiB) | 496 |
| Database a removal leaves, first cycle (KiB) | 780 |
| Database a removal leaves per soft-deleted object (KiB) | 5.8 |
| Database a removal leaves, after the last cycle (KiB, whole) | 14600 |
| Bound on what the cycles leave (KiB) | 19564 |
| Soft-deleted objects kept per removal | 134 |
| Real project documents compared after every install and removal | 9 |
| Removal with an issued calculation | refused: The demo project was not removed: 'DEMO — Blank-stack tamper: Tamper cam' is issued, and an issued calculation is never deleted. |
| Expectations checked | 296 (pass 1), 296 (pass 2) |

Measurements:

| | Pass 1 | Pass 2 |
|---|---|---|
| Scenario time, s | 84.7 | 86.6 |
| Host start-up, s | 2.8 | 2.6 |
| Managed heap before, MiB (host running) | 29.6 | 30.5 |
| Managed heap after, MiB (host running) | 35.3 | 35.3 |
| Held for the work, MiB | 5.7 | 4.7 |
| Managed heap after the host is disposed, MiB | 29.8 | 29.8 |
| Peak managed heap while running, MiB | 80.9 | 86.3 |
| Peak working set while running, MiB | 347.9 | 347.1 |
| Process peak working set so far, MiB | 1583.9 | 1583.9 |
| tempest.db + WAL before the scenario (the seeded database), MiB | 14.5 | 14.5 |
| tempest.db, MiB | 26.9 | 26.8 |
| tempest.db-wal, MiB | 4.4 | 4.5 |
| tempest.db after shutdown, MiB | 27.1 | 27.1 |
| Open handles: before the host / before the scenario / after / after dispose | 262 / 269 / 270 / 262 | 262 / 269 / 270 / 262 |
| Handles on the database files: before / after / after dispose | 7 / 8 / 0 | 7 / 8 / 0 |
| Generation 2 collections | 5 | 5 |
| Allocated, GiB | 16.11 | 16.11 |

Leak check: the heap left after disposal changed by **+0.0 MiB** from pass 1 to pass 2 (bound +4.0 MiB); no leak, slowdown or handle growth found.

No failed expectation, no stale-flag error, no immutability breach, no leak.

## Verification dashboard built repeatedly

The verification dashboard built over and over on a store with thousands of reference records and hundreds of guided runs; the time per build.

What ran and what it found (pass 1):

| | |
|---|---|
| Reference records in the store | 10,000 |
| Seconds to import them | 5.4 |
| Finished guided runs in the store | 1,000 |
| Steps in each run | 110 |
| Run documents (KB each) | 20.7 |
| Reading all the records (ms) | 370 |
| Reading all the runs for the manual status (ms) | 346 |
| Dashboard rows | 66 |
| Builds | 48 |
| Build on the seeded database (mean ms) | 7 |
| Build on the full store: mean (ms) | 807 |
| Build on the full store: first / median / 95th percentile / slowest (ms) | 802 / 797 / 912 / 989 |
| Dashboard export: markdown (ms), PDF (ms) | 6, 76 |
| Markdown export (KB), PDF export (KB) | 18, 155 |
| Expectations checked | 69 (pass 1), 69 (pass 2) |

Measurements:

| | Pass 1 | Pass 2 |
|---|---|---|
| Scenario time, s | 40.7 | 38.8 |
| Host start-up, s | 3.1 | 2.8 |
| Managed heap before, MiB (host running) | 30.6 | 31.0 |
| Managed heap after, MiB (host running) | 30.9 | 30.9 |
| Held for the work, MiB | 0.3 | -0.2 |
| Managed heap after the host is disposed, MiB | 30.5 | 30.5 |
| Peak managed heap while running, MiB | 320.2 | 298.2 |
| Peak working set while running, MiB | 566.7 | 558.5 |
| Process peak working set so far, MiB | 1583.9 | 1583.9 |
| tempest.db + WAL before the scenario (the seeded database), MiB | 14.5 | 14.5 |
| tempest.db, MiB | 49.5 | 49.1 |
| tempest.db-wal, MiB | 5.2 | 5.2 |
| tempest.db after shutdown, MiB | 51.5 | 51.0 |
| Open handles: before the host / before the scenario / after / after dispose | 262 / 269 / 269 / 262 | 262 / 269 / 269 / 262 |
| Handles on the database files: before / after / after dispose | 7 / 7 / 0 | 7 / 7 / 0 |
| Generation 2 collections | 73 | 82 |
| Allocated, GiB | 7.38 | 7.37 |

Leak check: the heap left after disposal changed by **+0.0 MiB** from pass 1 to pass 2 (bound +4.0 MiB); no leak, slowdown or handle growth found.

No failed expectation, no stale-flag error, no immutability breach, no leak.

## Guided test runs to the run cap

Guided runs started, filled in (110 steps), finished and repeated to the 1,000-run cap, then the older ones deleted; the time to read the run store as it grows.

What ran and what it found (pass 1):

| | |
|---|---|
| Run 1,001 | There are already 1000 test runs on file, which is the most this build keeps; save the results sheets you need, then use "Delete older finished runs" on the Guided test screen (a run that is not finished has to be finished first). |
| Runs made through the service (started, 110 steps recorded, finished) | 1,000 |
| Runs on file when the cap was tried | 1,000 |
| Steps recorded | 110,000 |
| Start a run (mean ms) | 1.1 |
| Record one step (mean ms) | 0.84 |
| Record one step (first 10 runs / last 10 runs, ms) | 0.85 / 0.77 |
| Finish a run (mean ms) | 1.4 |
| One run's document (KB) | 22.4 |
| Database the runs take at the cap (MiB) | 23.5 |
| Delete older finished runs: deleted, kept (the newest 50 and those holding a template's latest result) | 950, 50 |
| Delete older finished runs (ms) | 730 |
| Database freed by the deletion (MiB, pages on the free list) | 22.4 |
| Run-store read time | 1 runs: overview 0 ms, newest 100 0 ms, status of every template 0 ms, plan to delete 4 ms |
| Run-store read time | 10 runs: overview 0 ms, newest 100 5 ms, status of every template 2 ms, plan to delete 4 ms |
| Run-store read time | 50 runs: overview 2 ms, newest 100 19 ms, status of every template 17 ms, plan to delete 13 ms |
| Run-store read time | 100 runs: overview 6 ms, newest 100 18 ms, status of every template 33 ms, plan to delete 19 ms |
| Run-store read time | 250 runs: overview 9 ms, newest 100 59 ms, status of every template 79 ms, plan to delete 59 ms |
| Run-store read time | 500 runs: overview 23 ms, newest 100 149 ms, status of every template 151 ms, plan to delete 173 ms |
| Run-store read time | 750 runs: overview 37 ms, newest 100 226 ms, status of every template 305 ms, plan to delete 303 ms |
| Run-store read time | 1,000 runs: overview 50 ms, newest 100 336 ms, status of every template 346 ms, plan to delete 414 ms |
| Run-store read time | 50 runs after deleting the older runs: overview 1 ms, newest 100 9 ms, status of every template 8 ms, plan to delete 8 ms |
| Expectations checked | 2,052 (pass 1), 2,052 (pass 2) |

Measurements:

| | Pass 1 | Pass 2 |
|---|---|---|
| Scenario time, s | 99.3 | 96.4 |
| Host start-up, s | 2.9 | 2.7 |
| Managed heap before, MiB (host running) | 31.0 | 30.3 |
| Managed heap after, MiB (host running) | 29.9 | 29.9 |
| Held for the work, MiB | -1.1 | -0.4 |
| Managed heap after the host is disposed, MiB | 29.5 | 29.5 |
| Peak managed heap while running, MiB | 225.1 | 169.6 |
| Peak working set while running, MiB | 464.5 | 468.0 |
| Process peak working set so far, MiB | 1583.9 | 1583.9 |
| tempest.db + WAL before the scenario (the seeded database), MiB | 14.6 | 14.5 |
| tempest.db, MiB | 34.7 | 34.6 |
| tempest.db-wal, MiB | 4.1 | 4.7 |
| tempest.db after shutdown, MiB | 34.7 | 34.6 |
| Open handles: before the host / before the scenario / after / after dispose | 262 / 269 / 270 / 262 | 262 / 269 / 270 / 262 |
| Handles on the database files: before / after / after dispose | 7 / 8 / 0 | 7 / 8 / 0 |
| Generation 2 collections | 20 | 20 |
| Allocated, GiB | 10.86 | 10.86 |

Leak check: the heap left after disposal changed by **+0.0 MiB** from pass 1 to pass 2 (bound +4.0 MiB); no leak, slowdown or handle growth found.

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
- **Imports of max-size files**: scan deviation tables at the 5 MB byte cap and at the one-million-row cap, a CAD expression file at the 5 MB cap with 5,000 parameters (the parameter cap), a CAD parameter CSV at the parameter cap, and a reference record at every one of its caps; then one past each cap, which must be refused with a plain sentence and leave the calculation byte for byte as it was. Record import from CSV is soaked below.
- **Record import at its caps** (Addendum C, C-3): seven Track P templates of different shapes (one record per row, a table, a table with scalars read from its first row, twelve fixed rows, materials) are imported with data sized to the template; then one file at every cap at once, the column map leaving its padding columns out: 1,001 rows with the header among them, 64 columns, 4,096-character fields and 5,242,880 bytes. One past each cap (a byte, a row, a column, a character) is refused in a sentence with nothing saved, at the real caps in both modes. A file with one bad row among a thousand good ones saves nothing, and a batch whose last record cannot be written, on the real database, leaves none of the others. A licensed record is revised 100 times, each import first trying to say it is not licensed, and every revision must still refuse to export.
- **Demo project** (C-6): installed and removed 20 times with a real project (calculations, one issued, a pack, a scan profile, a reference record) beside it, compared document for document after every install and removal. The database is counted in its own pages, not its file size, because SQLite never shortens its file and folds its log in at moments the soak does not choose.
- **Verification dashboard** (C-4): built 40 times on a store holding 10,000 reference records (imported through the record import) and 1,000 finished guided runs of the whole script, after a fifth as many builds on the seeded database. The first three runs are made through the service and the rest written in the service's own form (checked by reading the service's back), because the point is what the build reads, not how the runs got there.
- **Guided runs** (C-5): 1,000 runs started, filled in step by step (every one of the script's 110 steps) and finished through the service, which is the run cap of 1,000; the next start is refused; "Delete older finished runs" then keeps the newest 50 (and any run holding a template's latest result). The time to read the run store is measured as it grows.

## Notes

<!-- soak:notes:begin -->
### Result

Everything the specification asks for was run at its numbers, and so were the four scenarios added for what the release gained after the soak was first written (record import C-3, the dashboard C-4, guided runs C-5, the demo project C-6), in 25.0 minutes (the brief allowed about 40). Nothing was scaled down in this full run; the scaled-down run (an ordinary test) is. No scenario found a leak, a stale-flag error, an immutability breach, a cap that is not enforced or a document that changed when it should not. The first soak found two defects while it was being built and noted a third (C-7h, a calculation too large to store); all three are fixed and each has a test that failed before the fix. The four new scenarios found nothing wrong in the record import, the demonstration, the dashboard or the guided runs.

### Defects found and fixed

1. **Inputs CSV row cap off by one** (`CalcCsv.ImportInputs`, cap 10,000 rows with the header counted). The cap was checked only as each line ended, so a file of 10,001 rows whose last line had no line break after it was read in full, and the row that crossed the cap was kept even when it was refused. Now the last line is checked too and the row that goes over is not read. Pinned by `CsvRowCapTests` (the 10,001-row file without a final line break was accepted before the fix).
2. **The 4 MB stored-document cap was checked after the whole document had been built** (`CalcInstanceStore`: instances, reference records, import and scan profiles). A reference record that is within every shape cap (200 values, 500 rows, 50 cells to a row) is some 250 million characters of JSON, so one refused save took about 1.5 GiB of managed heap and a 1.8 GiB working set (measured in the scaled-down run before the fix). The document is now written through a buffer that refuses at the cap, so a save past it builds at most the cap's worth; the text written, the cap and the refusal sentence are unchanged. Pinned by `StoreDocumentCapTests` (a record four times the cap allocated 62 MiB before the fix against a bound of 32 MiB).
3. **A calculation past the 4 MB store cap threw from Create but was refused by Update, and named itself by its identifier (C-7h).** `CalcInstanceService.CreateAsync` threw an `InvalidOperationException` ("Calculation instance '7d1c...' is larger than 4 MB and was not written.") where `UpdateInputsAsync` returned a refusal, the worksheet screen showed "Could not save that calculation: " and the exception's text, and the Evidence record and register entry made for the calculation before the save were left behind. Now `CreateAsync` builds and runs the record first and refuses before anything is made (`CalcInstanceTooLargeException`), `TryCreateAsync` gives that refusal as a result the way `UpdateInputsAsync` does, the worksheet screen shows it, and every act that meets the cap (create, update, issue) says "Calculation 'Pump base frame' is larger than 4 MB and was not saved; take some of its table rows out and save it again." This run's frame at its table caps (2,000 nodes, 3,910 members, 20,000 loads) is that case: it is refused after 5.4 s in that sentence and nothing is left behind. Pinned by `CreateSizeCapTests` (6) and `WorksheetCreateSizeCapTests`.

### Observations, nothing changed

- **Revision labels after Z.** The 27th revision of a calculation is labelled `Z+`, the 28th `Z++`, and so on: the 101st is a 76-character label. Everything works with it (Evidence, the register, file names, the PDF sheet; there is no cap on the label), but it reads badly on a sheet. An owner decision, not a fault.
- **Scan row cap.** The 1,000,000-row cap counts every row of the file, header included, as the inputs CSV's does; a table of 1,000,000 data rows is refused ("more than 1000000 rows"). The importer took 3.7 s for the largest table that is allowed.
- **Worked examples that cite a shipped material** (for example `struct.plate-bending`) are reported stale ("the reference record no longer exists") on a database whose library has not been seeded, and fresh as soon as it is. That is the stale flag being right; the library seeds itself the first time a screen asks for a material, and the soak does the same first, after checking the flag in both states.
- **First pass opens 67 more handles than the process had** (assemblies and libraries, loaded once); the second pass ends with exactly as many as the first. No handle is left open on the database's files after any host is disposed (0 in every pass).
- **One-off step in the heap.** Reading a very large file can leave the settled heap 4 to 10 MiB higher after the next pass (a buffer the runtime keeps for a while); the check runs a third pass when it sees a step and calls it a leak only if the heap is still rising. In this run no scenario needed a third pass.
- **What the dashboard reads grows with the store.** Building it takes 7 ms on the seeded database and 0.8 s on a store with 10,000 reference records and 1,000 finished guided runs (the run cap), about 0.37 s of which is reading every record (the "Records not entered" column lists them all) and 0.35 s reading every run (the "Manual test" column deserialises each run's whole 21 KB document, 110 results, to find the latest result of each worked example). Both are linear and neither grows with repeated builds (the first and the 48th take the same time). At the cap it is still under a second; "Delete older finished runs" takes it back to a few milliseconds. If the Dashboard ever needs to be faster the record side wants a per-kind read and the run side a small index of latest results; neither is needed now.
- **The demo project's removal leaves a fixed 780 KiB behind, every time, by design.** Each install adds 1.25 MiB (60 calculations, 13 packs, two scan profiles, their Evidence records and register entries, the search index). The removal deletes the calculation, pack and scan-profile rows and gives their pages back (496 KiB of the database against the 524 KiB of documents it deleted, the difference being what its own writes took), and keeps the soft-deleted project, Evidence and register objects (134 of them, 5.8 KiB each) and the audit rows, because an engineering object is never erased (ADR-0074). So the database does not return to its size before the first install; it holds 780 KiB more per install-and-remove, and 14.6 MiB after 20 (the bound the soak enforces is 20 times the first cycle's residual plus 25 % and 64 KiB, 19.1 MiB, which is a statement that it is the same set of rows every cycle, not more; the objects kept are 134 every cycle). If the owner wants the database to return to its size, that is a separate decision (a purge of soft-deleted demonstration objects), not a fault.
- **The 1,001st guided run is refused with its cure named** ("... use "Delete older finished runs" on the Guided test screen (a run that is not finished has to be finished first)."). The deletion at the cap took 950 of the 1,000 runs in 0.7 s and freed 22 MiB of pages; it kept every template's manual status.
- **Record import caps.** The caps are read exactly: 5,242,880 bytes, 1,001 rows with the header, 64 columns and 4,096 characters in a field are accepted (the whole at once, in a file of 1,000 records: preview 0.43 s, commit 0.49 s); a byte, a row, a column or a character more is refused in one sentence ("The file is larger than 5 MB and was not read.", "The file has more than 1001 rows.", "Line 1 has more than 64 columns.", "A field on line 2 is longer than 4096 characters.") with nothing saved. A 4,096-character text in a column the record keeps is read by the importer and refused on its row, because a record's own text cap is 200.

### Reading the numbers

- **Time.** 500 calculations (create, run, save, run again, save again) take 26 s, about 52 ms each; a step of a 20-link chain links in about 11 ms and updates in about 11 ms; an issued revision with its PDF sheet takes about 108 ms to issue and 42 ms to revise. The Monte Carlo service draws 10,000,000 samples through a real calculator in about 5.7 minutes and 10,000,000 through the engine in under 6 seconds. A record import of 1,000 records takes 0.2 to 0.35 s end to end (preview and commit); 101 revisions of a licensed record, each preceded by a refused attempt to release it, take 27 s. Installing the demonstration takes 3.8 s and removing it 0.2 s, the same at the twentieth cycle as the first. A guided run (start, 110 results, finish) takes about 95 ms, 0.84 ms a result, the same for the last runs as the first; 1,000 of them took 99 s.
- **Memory.** The working set peaks at 259 to 348 MiB in the first four scenarios and the demonstration, and higher in the others: the imports 1.6 GiB (the frame at its caps and the largest tables; managed peak 910 MiB), the record import 0.86 GiB (a 5 MB file of 64,000 fields and the 1,000 records planned from it), the dashboard 0.57 GiB (10,000 records and 1,000 runs read whole) and the guided runs 0.47 GiB (the whole run store read at the cap). After a scenario the application holds only 0 to 2.5 MiB more than before it: the data lives in the database, not in memory.
- **Database.** A seeded, empty database is 14.5 MiB. 500 calculations add 9 MiB (19 KB each); 101 issued revisions add 18 MiB (180 KB each, of which the PDF sheet is 142 KB); 420 chained calculations add 7 MiB; a reference record takes 3.4 KB (1,000 of them, 3.4 MiB); 1,000 guided runs take 23.5 MiB (22.4 KB each); a demonstration install adds 1.25 MiB and a removal leaves 0.76 MiB (above). The write-ahead log stays at about 4 MiB. The database is counted in its own pages (the pages in use, and those on the free list) for the demonstration and the guided runs, because SQLite never shortens its file when rows are deleted.
- **The scaled-down run** (an ordinary test, about 150 s for all nine scenarios) uses `PRAGMA synchronous = NORMAL` like the rest of the Desktop suite; this full run used `FULL`, the product's setting. In the scaled-down run the record import, the demonstration, the dashboard and the guided runs use a tenth or less of the numbers (rows and bytes at a tenth of the caps, two demonstration cycles, 300 records and 20 runs, 12 guided runs through the service topped up directly to the real run cap), but every refusal is tried at the real cap.
- **How the dashboard's and the cap's runs were made.** The guided runs of the dashboard scenario are three through the service and 997 written in the service's own form (the soak reads one back and writes it again, byte for byte, to prove the form); the dashboard's cost is in what it reads, not in how the runs got there. The 1,000 runs of the guided scenario are all made through the service.
- **This machine is shared.** Other builds and test runs ran on it at the same time, so times are an upper bound (the load average was 20 or more at the start of this run); the memory, size and handle figures do not depend on it.

### Not soaked

- The Desktop screens for the new features (the records screen, Settings → Demo project, the Guided test screen, the dashboard screen) are not driven by the soak: it talks to the services under them. Their own tests drive the screens.
- The packs, the Evidence screens and the PDF viewer are not part of this item.
<!-- soak:notes:end -->

## Running it again

```
# the scaled-down soak (about a tenth), part of the ordinary Desktop test run:
dotnet test tests/Tempest.Desktop.Tests --filter "FullyQualifiedName~Tempest.Desktop.Tests.Soak"

# the full soak, which rewrites this page (about 25 minutes):
TEMPEST_SOAK=full dotnet test tests/Tempest.Desktop.Tests --filter "FullyQualifiedName~Tempest.Desktop.Tests.Soak.SoakFull"
```

`TEMPEST_SOAK_LOG=<file>` appends one line per step to a file, to watch a long run.

`TEMPEST_SOAK_ONLY=6,7` runs only the scenarios with those numbers at full scale and writes `TEMPEST_SOAK_REPORT=<file>` instead of this page (it is for looking at one scenario's full-scale numbers without the others' time).
