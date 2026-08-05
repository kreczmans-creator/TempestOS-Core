# ADR-0082: Property Inspector Facet Sourcing Is a Third Kind-Keyed Provider Category, Added to the Frozen `IWorkspaceManager` Contract as a New, Additive Member

## Status

Accepted — `v0.9.0` "Mechanical Foundation", `WP 9.0A` (Mechanical Product Structure), 2026-08-05. Recorded separately from `ADR-0080` because it deviates from a different frozen contract (`IWorkspaceManager`, `WP8.0B`) than that ADR's own Domain-layer facets.

## Context

`ADR-0067` establishes Kind-keyed provider registration as this platform's own Workspace extensibility mechanism, realised twice by `WP8.1B`: `IProjectExplorerNodeProvider` (populates the tree) and `IWorkspaceViewFactory` (presents one object). `PropertyInspector`'s own `WP8.1B`-era XML documentation states plainly that no third, matching provider exists yet for real facets — "every displayed facet is derived purely from the selection tuple itself (Id, Kind), no Engineering Core service is ever consulted" — and that this holds only "for this Work Package," i.e., until a real discipline needed it.

`WP 9.0A`'s own Workspace Integration scope names "Properties panel" as a required capability, and this Work Package is the first to have real Engineering Domain data (Identifier, Name, Revision, Status, Owner, Discipline, Classification, Tags, Notes — precisely `WP 9.0A`'s own named Engineering Metadata scope) for a facet to come from. `IWorkspaceManager` is one of the twelve `WP8.0B Workspace Contracts.md` interfaces — frozen, same standing as `IProjectExplorerNodeProvider`'s own contract.

## Decision

**A new interface, `IPropertyFacetProvider`** (`Kind { get; }`, `GetFacetsAsync(Guid objectId, ...)`), mirrors `IProjectExplorerNodeProvider`'s own shape exactly. **`IWorkspaceManager` gains one new member, `RegisterFacetProvider(string kind, IPropertyFacetProvider provider)`** — additive only: every existing member (`Current`, `StartAsync`, `ShutdownAsync`, `RegisterView`, `RegisterExplorerArea`) is untouched, and `WorkspaceManager`'s own existing `RegisterView`/`RegisterExplorerArea` implementation pattern (a `Dictionary<string, T>`, `TryAdd`, `DuplicateWorkspaceRegistrationException` on collision) is reused verbatim, not reinvented. `PropertyInspector.InspectAsync` consults the registered provider for the selection's own Kind when one exists; when none is registered (every Kind besides this Work Package's own five), it falls back to exactly the Id/Kind-only facets `WP8.1B` already shipped — so every `Workspace/Samples` selection, and every future Kind without a registered provider, behaves identically to before this change.

## Consequences

**Positive:**

- Proves `ADR-0067`'s own stated principle — Kind-keyed registration is *the* Workspace extensibility mechanism — generalises to a third concern without inventing a fourth, different pattern.
- `WP8.1B`'s own disclosed limitation is closed exactly as its own documentation anticipated it eventually would be, by the first Work Package with real data to supply.
- Zero behavioural change for any already-shipped Kind: the fallback path is byte-for-byte what `PropertyInspector` already did.

**Negative:**

- `IWorkspaceManager` is a second frozen `WP8.0B` contract `WP 9.0A` extends (alongside the Domain facets `ADR-0080` adds) — a real, disclosed pattern of this Work Package needing more from "frozen" contracts than any prior Work Package did, worth watching for a future Work Package rather than treating as a one-off. Named in the Technical Debt Assessment.
- A third provider category means a future reader of `WP8.0B Workspace Contracts.md` alone, without also reading this ADR, undercounts `IWorkspaceManager`'s own real surface by one member.

## Alternatives Considered

**A generic `Dictionary<string, object>` "facet provider registry" reachable without a new `IWorkspaceManager` member (for example, a static/ambient registry)** — considered and rejected. Would abandon the composition-root-registers, dependency-rules-respecting model `ADR-0071` already establishes for the other two provider categories, introducing an inconsistent, harder-to-discover extension mechanism for no benefit.

**Compute facets inside `PropertyInspector` itself via a hard-coded `switch` on Kind, referencing `Tempest.Core.EngineeringDomain` directly** — considered and rejected. Would violate `WP8.0B Dependency Rules.md`'s own layering (`IPropertyInspector`'s implementation staying free of any specific discipline's own Core dependency), and would not generalise to a second Engineering Discipline Module the way a registered provider does.

## Related Documents

`ADR-0067`; `ADR-0071`; `ADR-0080`; `WP8.0B Workspace Contracts.md`; `WP8.1C Implementation Report.md`; `src/Tempest.App/Workspace/IPropertyFacetProvider.cs`; `src/Tempest.App/Workspace/IWorkspaceManager.cs`; `src/Tempest.App/Workspace/PropertyInspector.cs`.
