# TempestOS v0.20.0 — Release Notes

**Status: in progress on `release/v0.19.1`, built up Work Package by Work
Package on individual `wp/*` worktrees before merge. Nothing in this
document is certification.**

## What shipped, by Work Package

| Work Package | Delivered | Merged |
|---|---|---|
| `WP 20.3B` Small P3 closures: `TD-20`, `TD-18`, `TD-21`, `TD-03`, `TD-05`, `TD-84`, `TD-170` | `TD-20`: `IEngineeringDocumentStore.GetLatestRevisionAsync` (new) gives `ReferenceDataCatalog<TDefinition>`'s latest-only lookups a single-revision fetch, replacing the whole-history read `ReadDtoAsync`/`ReadRecordAsync` used to make (twice, in `ReadRecordAsync`'s case). `TD-18`: `LinkAsync` concurrency is now proven by a parallel battery over twenty sources linking to one shared object and it linking back to all of them at once — no lost link, no duplicate, no cycle confusion between a reciprocal pair; no defect found, the domain write lock already serialised it. `TD-21`: `ICalculationDefinition.Calculate` now carries a default-able `CancellationToken`, threaded from `ICalculationEngine.ExecuteAsync` through the six dormant definitions and the Double Length sample, proven against a definition that loops observing it until cancelled mid-run | — |
