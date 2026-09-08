# WP 8.0A — Engineering Workspace — Navigation Specification

## Purpose

The detailed navigation model — global navigation, the Project
Explorer, and the engineering object hierarchy it presents — expanding
`WP8.0A Workspace Architecture Document.md` §4, §5, and §6.
Architecture only; no code.

## 1. Two Navigation Tiers

| Tier | Answers | Source | Existing Mechanism Reused |
|---|---|---|---|
| Global Navigation | "What areas exist?" | `INavigationProvider.Items` | Unchanged — read exactly as `TempestShell` already does |
| Project Explorer | "What objects exist within the selected area, and how are they related?" | Each area's own service (`IRequirementsService`, `IMaterialCatalog`, `ICalculationEngine`, `IVerificationService`) | New composition, zero new storage |

Global Navigation is deliberately left exactly as `WP 5.0A`/`WP 5.0B`
designed and implemented it — a flat-to-hierarchical registry of
top-level areas, each a `NavigationItem`. The Workspace does not
propose any change to `INavigationProvider`, `NavigationService`, or
`NavigationItem` itself. The Project Explorer is new *presentation*,
not a new *navigation platform service* — it is `Tempest.App`'s (or
whatever composition root replaces it) own read against services that
already exist, exactly as `TempestShell` itself is composition, not a
platform service.

## 2. Global Navigation — Areas

The Workspace's own Command Bar presents each `NavigationItem` at the
top level of `INavigationProvider.Items` as a switchable area. Selecting
an area does not close whatever is open in the Document Area (Workspace
Philosophy Point 2) — it changes what the Project Explorer displays.

Anticipated top-level areas, sourced from what already exists or is
architecturally certain to exist once discipline modules ship (none of
these requires a `NavigationItem` change — each already fits the
existing contract):

- Home (unchanged from today's placeholder)
- Requirements (`Tempest.Core.Requirements`)
- Materials (`Tempest.Core.Materials`)
- Calculations (`Tempest.Core.Calculations`)
- Verification (`Tempest.Core.Verification`)
- Settings (unchanged from today's placeholder)

A future Engineering Discipline Module registers its own area exactly
as any of the above would — no special-casing exists, or is designed
here, for "built-in" vs. "module-contributed" areas.

## 3. Project Explorer — Per-Area Tree Structure

### 3.1 Requirements Area

```
Requirements
├── Groups (RequirementGroup hierarchy, via parentGroupId)
│   ├── <Group A>
│   │   ├── <Group A.1>
│   │   │   └── REQ-0001, REQ-0002, ...
│   │   └── REQ-0003
│   └── <Group B>
│       └── REQ-0004
├── Ungrouped Requirements (no GroupedUnder relationship recorded)
│   └── REQ-0005, ...
└── Collections (RequirementCollection — a cross-cutting, filterable
    view, not a second parallel tree, since a Requirement may belong to
    many Collections but only one Group)
    ├── <Collection X>
    └── <Collection Y>
```

Source reads: `IRequirementsService.ListAsync` (every requirement),
`GetRelationshipsAsync` per group (to resolve `GroupedUnder` parentage
and membership), `FindCollectionAsync`/collection membership reads for
the Collections view. No new read method is required on
`IRequirementsService` — every fact above is already exposed by the
approved `WP7.2C Requirements Platform Contracts.md` surface, confirmed
directly against the shipped `IRequirementsService` (`WP 7.3A`).

### 3.2 Materials, Calculations, Verification Areas

None of `IMaterialCatalog`, `ICalculationEngine`, or
`IVerificationService` has a group/collection concept of its own today.
Each area is presented as a flat, sortable list (by identifier, by name,
by last-revised date) until a real need for hierarchy is identified —
per `VISION.md`'s own Product Principle 3 ("do not build ahead of real,
demonstrated need"), this architecture does not invent a grouping
concept for these three frameworks speculatively.

## 4. Engineering Object Hierarchy — Common Presentation Facets

Every engineering object the Project Explorer or Document Area presents
shares four facets, since every one is, underneath, an
`IEngineeringDocument`:

| Facet | Source | Present For |
|---|---|---|
| Identity | `Id` (Guid) + business identifier where one exists (a Requirement's own string `Identifier`; a Material's own `materialId`) | All |
| Revision History | `IDocumentRevision` history, via each framework's own read (`FindAsync` composing revision detail, or a dedicated history read where one exists) | All |
| Provenance | Creating/revising principal and timestamp | All |
| Relationships | Every `DocumentReference` with this object as source or target | All |

Discipline-specific facets layer on top, never replacing the four
above: a Requirement adds `RequirementStatus` and `Category`; a
Calculation Record adds its own assumptions, constraints, and validation
outcome; a Verification Record adds its own `VerificationOutcome`,
criteria, and evidence. The Workspace's own View for each object type
renders the shared four facets identically (the same Properties-panel
layout skeleton), then appends whatever discipline-specific facets that
`Kind` contributes — giving every future engineering object a
consistent presentation baseline without the Workspace needing to
special-case each `Kind` for the shared facets.

## 5. Navigation State and Selection

Selecting a node in the Project Explorer sets the Workspace's own
current selection, which:

1. Updates the Properties panel to that object's own shared + specific
   facets (§4).
2. Updates the Status Bar to reflect that object's own lifecycle state
   (e.g. a Requirement's own `RequirementStatus`).
3. Does **not** open a Document Area tab — opening requires an explicit
   "Open" interaction (`WP8.0A UI Architecture.md` §4), keeping
   selection (a cheap, frequent action) distinct from opening (a
   heavier action that adds a tab).

## Related Documents

`WP8.0A Workspace Architecture Document.md`; `WP8.0A UI Architecture.md`;
`WP8.0A Object Relationship Diagrams.md`;
`docs/architecture/Navigation Framework Architecture.md`;
`docs/academy/02 Runtime Architecture/16-requirements-engine.md`.
