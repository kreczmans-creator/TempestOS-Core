<!--
  WP 17.0B — Governance reset. A Work Package is a branch and a PR;
  this description IS the retrospective — there is no separate report.
  See CONTRIBUTING.md for the full rules this template enforces.
  Delete this comment before submitting; keep every section below.
-->

## Work Package / Change

<!-- e.g. "WP 17.1A — SQLite persistence" -->

## What changed

<!-- What changed, in a few sentences. -->

## Why

<!-- Why this, why now — the problem it closes or the capability it adds. -->

## Evidence

- **Build:** 0 warnings, 0 errors, both configurations, under
  `TreatWarningsAsErrors`. <!-- link the CI run -->
- **Tests:** every test passing in both configurations, including the
  architecture invariants (`DependencyDirectionTests`). <!-- link the CI run -->
- **`PHYSICAL_REVIEW.md` §7 row:** <!-- link the row, or write "no new
  user-facing surface" -->
- **ADR:** <!-- link it, or write "no decision in this PR constrains
  future code" -->

## Findings filed to `BACKLOG.md`

<!-- Anything the reviewer or author found that isn't fixed here, filed
     as a row in BACKLOG.md, or "none". A finding is Release Blocking
     only if it is data loss a user can reproduce from the running UI —
     see CONTRIBUTING.md. -->

## Markdown vs. code line count

<!-- This PR must not add more Markdown lines than code lines
     (scripts/governance-healthcheck.ps1's own check enforces this).
     State the two figures, or "n/a — no Markdown changed". -->
