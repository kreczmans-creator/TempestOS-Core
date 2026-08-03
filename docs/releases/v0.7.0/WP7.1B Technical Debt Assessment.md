# WP 7.1B — Units & Quantities Framework — Technical Debt Assessment

## Purpose

Discloses every new debt item or trade-off this Work Package's own
implementation introduces, and confirms which existing debt items (if
any) it touches — mirroring `WP7.1A Technical Debt Assessment.md`'s own
format.

## Existing Debt: What Actually Happened

**No existing Technical Debt Register item (`TD-01` through `TD-18`) is
touched by this Work Package.** `Tempest.Core.UnitsAndQuantities`
depends on nothing `TD-01`–`TD-18` concern — no Persistence, no Logging,
no Plugin/Command/Navigation registration path, no REST API, no
Licensing.

## New Debt Disclosed by This Work Package

### TD-19 — No Affine/Offset Unit Conversion Support (Temperature Deferred)

**What.** `Unit<TDimension>` supports only a single multiplicative
`ToBaseUnitFactor`. Temperature (Celsius↔Fahrenheit) requires an affine
(scale-and-offset) conversion this shape cannot express, and is
therefore deliberately absent from this Work Package's own starting
catalogue.

**Why this is debt, not merely a limitation.** Nearly every future
Mechanical, HVAC, or Materials capability will eventually need
Temperature — this gap will need resolving before any of them can fully
proceed, unlike a limitation with no foreseeable future consumer.

**Revisit trigger.** A real, demonstrated need for a Temperature
dimension (any future Mechanical/HVAC/Materials capability naming it).

## New Accepted Trade-off Disclosed by This Work Package

### AT-14 — Compile-Time Dimension Safety Verified by Inspection, Not an Automated Test

**What.** The compile-time guarantee that a `Quantity<Length>` cannot be
combined with a `Quantity<Mass>` is documented and verified by directly
attempting the invalid code and observing the compiler reject it
(`CompileTimeDimensionSafetyTests.cs`), rather than by an automated
Roslyn-scripting-based "assert this does not compile" test.

**Why this is a trade-off, not debt.** No new test-only dependency was
judged worth adding for a single guarantee — the deliberate choice
matches this project's own "don't build infrastructure ahead of a real,
demonstrated need" discipline (Security Principle 7's own reasoning,
applied here to test infrastructure rather than security).

**Revisit trigger.** A second, independent framework needing the same
kind of compile-time guarantee verified, making shared Roslyn-scripting
infrastructure worth its own dependency cost.

## A Genuine, Disclosed Engineering-Review Finding (Not Debt)

**The `[JsonConstructor]` requirement for correct `System.Text.Json`
deserialization of a hand-written value-type constructor** — found and
resolved during implementation, not left outstanding. Included here
only to confirm it was considered and correctly classified as a
resolved implementation detail, not unaddressed debt.

## Summary Table

| # | Item | Status | Revisit Trigger |
|---|---|---|---|
| TD-19 | No affine/offset unit conversion — Temperature deferred | New, Open | A real, demonstrated need for a Temperature dimension |
| AT-14 | Compile-time dimension safety verified by inspection, not an automated test | New, Accepted Trade-off | A second, independent need for the same kind of test |

**Total: 1 new debt item disclosed, 1 new accepted trade-off disclosed,
0 existing items worsened.**

## Related Documents

`docs/governance/Quality/Technical Debt Register.md` (updated with
`TD-19`/`AT-14` in this same Work Package); `ADR-0054`; `WP7.1B
Implementation Report.md`.
