# TempestOS v0.20.0 — Release Notes

**Status: in progress, on `wp/20.3D` off `release/v0.19.1` head `4f5ee83a`, 2026-09-15.
This document carries only the Work Packages that have reported into it so
far; it is not a release candidate and nothing in it is certification.**
Later Work Packages in this tranche extend the "What shipped, by Work
Package" table below rather than replacing it — see each Work Package's
own report for what it found.

## Summary

**v0.20.0's first landed change closes `B6`** — the CI Build & Test job's
ceiling had been raised twice (30 to 45 minutes at v0.17.0, 45 to 90 at
`WP 19.9.1`) because a single `dotnet test` invocation ran the whole
solution's tests, Core and Desktop together, in series, and the Desktop
suite kept growing. `WP 20.3D` shards the Desktop assembly across three
parallel legs per configuration, alongside an unsharded Core leg, and
brings the ceiling back to 45.

## What shipped, by Work Package

| Work Package | Delivered | Merged |
|---|---|---|
| `WP 20.3D` CI: shard the Desktop test suite so the Build & Test ceiling could come back down | `.github/workflows/ci.yml`'s `build-and-test` matrix gains a `shard` dimension (`core`, `desktop-1`, `desktop-2`, `desktop-3`): `Tempest.Core.Tests` runs once per configuration, unsharded; `Tempest.Desktop.Tests` runs as three `--filter`-selected shards, split by the first letter of each class's own namespace segment (A-D / E-P / Q-Z, 195 / 219 / 214 of 628 Desktop tests measured by `--list-tests` on this branch head) so a future test class always lands in exactly one shard with no filter list to maintain; `Category=LayoutWalk` (5 tests) stays entirely inside the `desktop-2` shard, and the existing dedicated Release-only screenshot rerun is pinned to that one shard rather than every Release shard. Coverage collection (`WP 17.0C`/`WP 19.9.1`) stays Release-only, now per shard; the step summary is scoped to each shard's own `TestResults/<configuration>/<shard>` directory; the shipped-application and Internal Engineering Harness build-output artefacts are published from the `core` shard only, not duplicated four times, to avoid re-hitting the artefact storage quota named in that upload step's own remark; build logs and test-result artefacts do carry a shard suffix, since their content genuinely differs per shard. `timeout-minutes` returns to 45 (from 90); the CI Gate job's `needs:` already covered every matrix combination without a code change (documented, not altered). Not yet re-measured on a hosted runner — no push access from this worktree — so the first real per-shard timings belong in this table once the lead records them on the candidate. | (not yet merged — `wp/20.3D`) |

## Figures

Desktop and Core test counts below are `--list-tests` counts taken on
`wp/20.3D` at branch head `4f5ee83a`, not a gate run (the brief for this
Work Package permits `--list-tests` only, not running the suites).

| Figure | Value |
|---|---|
| Core tests (`tests/Tempest.Core.Tests`) | 4,441 |
| Desktop tests (`tests/Tempest.Desktop.Tests`) | 628 |
| Desktop shard `desktop-1` (A-D) | 195 |
| Desktop shard `desktop-2` (E-P, includes `Category=LayoutWalk`, 5 tests) | 219 |
| Desktop shard `desktop-3` (Q-Z) | 214 |

## Gate on this Work Package's own change

- Build: not required by this Work Package's brief (no test class or
  product code was touched) and not run, other than a plain local
  `dotnet build` used only to produce a `--list-tests`-capable Debug
  assembly.
- Each shard's `--filter` expression selects a non-empty, correctly
  bounded set locally (`dotnet test tests/Tempest.Desktop.Tests -c Debug
  --no-build --list-tests --filter "<expr>"`, counted per shard); the
  three shards' counts sum to the whole Desktop assembly's own
  unfiltered `--list-tests` count (628) with no gap and no overlap.
- YAML: `.github/workflows/ci.yml` parses cleanly under
  `ConvertFrom-Yaml` (`powershell-yaml`, installed locally for this
  check — no `python` or `pwsh` was present in this worktree's
  environment) after every edit in this Work Package.
- Governance health check: see this Work Package's own report for the
  result recorded at the time it ran.

## Warnings

- **Shard timings are estimated by test count, not measured wall-clock
  time per class** (`WP 20.3D`): no per-class timing data exists to
  split on instead, so `desktop-1`/`2`/`3` are balanced to within about
  12% of each other by count, not by the actual, unequal cost of a
  journey test versus a small unit test. The lead should record real
  per-shard times on the first hosted-runner run of the candidate and
  rebalance a boundary letter if one shard runs materially longer than
  the other two.
- **This document is a single Work Package's own report, not a release
  summary** (`WP 20.3D`): it will need consolidating once the rest of
  the v0.20.0 tranche lands, the way `docs/releases/v0.19.1/Release
  Notes.md` was consolidated across its own twelve Work Packages.
