# TempestOS v0.20.0 — Release Notes

**Status: in preparation on `release/v0.20.0`, cut from the `v0.19.1`
candidate. Nothing in this document is certification.**

## Summary

**v0.20.0 begins closing the technical debt the v0.19.1 candidate
disclosed rather than fixed.** `WP 20.1B` closes two of the seven
questions the Product Owner answered on 2026-09-15: `TD-180`, a real
per-client payment-terms field replacing the Finance bucket's thirty-day
guess, and `TD-181`, a calculation that is a task from the moment it is
created, with its own Complete action. Both close a disclosed Warning
from the `v0.19.1` notes.

## What shipped, by Work Package

| Work Package | Delivered | Merged |
|---|---|---|
| `WP 20.1B` Payment terms per client; a calculation is a task from creation | `TD-180`: `Organisation` gains a closed-vocabulary `PaymentTerms` (Up front / 30 days / 60 days, default Up front), set through the reference-data catalogue's existing `ReviseAsync` edit path; `InvoiceRequest` gains `PaymentTerms` (copied from the client and frozen at raise) and `DueOn` (computed once, at Send, from `SentAtUtc` plus the term's own days; a pre-`WP 20.1B` request backfills as Up front, due the day it was sent); the Finance task bucket, the Invoicing area's Outstanding/Overdue grouping and the Business dashboard's receivable split all read the request's own `DueOn` in place of the old thirty-day-since-Sent constant, which is removed; the Commercial section's client picker (`OrganisationPicker` — the one place a client's own fields are edited today) shows and changes the selected organisation's own terms. `TD-181`: `Calculation` gains a minimal `Completed`/`CompletedOn` flag and a `calculations.complete` command (Calculation only, never a Calculation Set); `TaskBucket.Calculations` lists every live Calculation under a project (one with no project ancestor at all is not shown — confirmed with the Product Owner), from creation, leaving the bucket on completion or when live, Issued evidence citing it is recorded, whichever first; Engineering → Tasks and the Engineering dashboard both show the Calculations heading with rows, each opening the calculation right up and offering Complete. | pending |

## Figures

Not yet re-derived — figures are taken on the release candidate head,
once the tranche is complete.

## Warnings

- **A calculation carries no due date of its own** (`WP 20.1B`): the
  Calculations bucket orders by title, not by date, and the Home
  dashboard's Overdue/Due today/Due this week/Later tiles never count a
  calculation — only `TaskBucket.Calculations`'s own count does, mirroring
  how Reviews/Approvals/Finance already sit outside that scheme. Adding a
  due date to the Kind was not in this Work Package's own brief; the read
  model already threads `TaskItem.DueDate` through if one is ever added.
- **A calculation's own project is found by walking its parent chain**
  (`WP 20.1B`): a Calculation may sit directly under a Project, or nested
  under an Assembly, Part, Component or Calculation Set, so
  `TasksReadModelService` walks up from its own direct parent (bounded to
  64 hops, defensively) until it finds one Kind "Project" or runs out —
  the latter is not shown, rather than shown with no project to open it
  from.

## Related

- `docs/releases/v0.19.1/Release Notes.md`
- `docs/releases/v0.19.1/Product Owner Decisions 2026-09-15.md`
