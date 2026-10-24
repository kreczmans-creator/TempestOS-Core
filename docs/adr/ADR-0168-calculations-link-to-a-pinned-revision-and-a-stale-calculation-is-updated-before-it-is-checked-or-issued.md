# ADR-0168: Calculations Link to a Pinned Revision, and a Stale Calculation Is Updated Before It Is Checked or Issued

## Status

Accepted (v0.26.0, Addendum A, L1). Extends ADR-0165 (Evidence governs) and ADR-0166 (a checked calculation is never a dead end).

## Context

The addendum's twelve calculators feed one another (motor torque into a shaft, shaft reactions into bearings, a cable into its fault protection). v0.26.0 had a first form of linking: an input whose provenance was `reference: calc:<id>` named another instance, and a changed upstream outputs hash made it stale before issue. It did not say *which output* fed the input, did not check units, did not see a revision as a change (a superseded revision keeps its outputs), allowed a stale calculation to be checked, and stranded a checked one whose upstream moved on.

## Decision

1. **A link is a provenance kind.** `CalcProvenanceKind.Link`, reference `calc:<instance id>#<output symbol>`, revision = the upstream revision. An instance is one revision, so the link is pinned to it, never to "latest".
2. **Output ports.** A template may declare `OutputPorts` (symbol, unit, description); declared ports enter the content hash. Templates that declare none offer the outputs a default run produces.
3. **Binding is checked.** `BindAsync` refuses a different dimension, a link to itself, a cycle and a chain longer than 20 links; the value is copied into the input's own unit.
4. **Stale is transitive.** A linked input is stale when its upstream is superseded, no longer gives the output, gives a different value, or is itself stale. A stale calculation cannot be checked, issued, or checked or issued as a pack member. Changes publish `CalcStalenessChanged` on the event bus with every affected downstream calculation.
5. **Update, never strand.** `UpdateLinksAsync` ("Update to revision n") re-pins every link to the current upstream revision and recalculates: a draft in place; a checked or issued calculation as a new revision (the old one superseded), so it is checked and issued again.
6. **Pack templates.** A saved chain (steps, links) instantiates a linked set of calculations in one pack; the pack is checked once and issued as one Evidence bundle (v0.26.0 packs). A chain built by hand can be saved as a template.

## Consequences

Imported files may state links; a forged link that does not match its upstream is simply stale and cannot be checked. Staleness reads the project's instances, so it costs one listing per question; at the 20-link cap and today's project sizes that is well inside the form's budgets.

## Amendment (2026-10-24, after the Opus review of Addendum A)

Decision 3's checks now run on **every path that can set a link**, not only `BindAsync`. That covers `UpdateInputsAsync` (the form's provenance field and CSV/JSON imports) and `UpdateLinksAsync` (re-pinning to a newer revision). One validator applies them all: the upstream exists, gives the output in the input's dimension, closes no cycle through any revision of either calculation, and keeps the chain within 20 links. A temperature difference (K) and an absolute temperature (°C/°F) are also refused, instead of being converted with a 273.15 offset. A forged looping link in an import is therefore refused when it is entered, not merely left stale. The test `Forged_Links_From_An_Import_That_Form_A_Cycle_Finish_And_Cannot_Be_Checked` is replaced by `Forged_Links_That_Would_Loop_Are_Refused_On_Entry` to match.
