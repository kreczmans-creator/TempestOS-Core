# TempestOS v0.20.0 — Release Notes

**Status: in progress on `release/v0.19.1`, ahead of a cut. This document
grows one row per Work Package as each lands; the Summary, Figures and
Gate sections below are completed by whichever Work Package cuts the
release candidate.** Nothing in this document is certification.

## Summary

*(Completed at release-candidate cut.)*

## What shipped, by Work Package

| Work Package | Delivered | Merged |
|---|---|---|
| `WP 20.1A2` TD-38: a business identifier is unique within its project | `IEngineeringObject.BusinessIdentifier` — a read-only per-Kind projection, no new stored field: a Part's or a Calculation's name, a Document's number if it has one else its name, unchanged for Requirement (already correct); `IBusinessIdentifierIndex`, maintained by `EngineeringObjectFactory<T>.CreateAsync` at creation and by `EngineeringObjectBase.RenameAsync` at rename, both under the domain write lock — a duplicate in the same project is refused naming the clash ("A Part named 'Bracket' already exists in project P-001 (id …)."), the same identifier in another project is accepted, a soft-deleted holder's claim is treated as free, and the index is rebuilt wholesale after rehydration; enforced for the five factory registries' own Kinds (Mechanical, Calculations, Documents, Manufacturing, Verification) and Evidence — Requirement and the Tasks/commercial Kinds are unchanged. A Ribbon create of a duplicate Part is refused on the status bar, not an unhandled exception. | *(pending)* |

## Figures

*(Completed at release-candidate cut.)*

## Gate on the candidate head

*(Completed at release-candidate cut.)*

## Related

- `docs/releases/v0.19.1/Release Notes.md`
