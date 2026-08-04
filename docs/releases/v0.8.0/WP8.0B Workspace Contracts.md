# WP 8.0B — Workspace Contracts

## Purpose

The complete public contract for the Engineering Workspace — full
proposed C# interface signatures for the twelve named concepts
`WP 8.0B`'s own controlling instruction requires, plus the small set of
supporting types those twelve genuinely need. **No implementation. No
compiled interface** — every signature below is proposed, documentation-
only C#, mirroring exactly how `WP7.2C Requirements Platform
Contracts.md` proposed the Requirements Engine's own contracts before
`WP 7.3A` ever compiled one of them. Design only follows from
`WP8.0A Workspace Architecture Document.md` and its four companion
deliverables — nothing here revisits an architectural decision already
made; every signature below exists to make an already-approved design
concrete enough to implement against.

## 0. Placement and Namespace

Every interface and supporting type below lives in a new namespace,
**`Tempest.App.Workspace`** — not `Tempest.Core`. This follows directly
from `ADR-0062`: the Workspace is `Tempest.App`'s own composition root,
not a Platform Service, so its own contracts belong where `TempestShell`
already lives (`Tempest.App`), never in the assembly every Platform
Service and Module depends on. See `WP8.0B Dependency Rules.md` for the
complete placement and layering analysis.

## 1. `IWorkspace`

The assembled, running Workspace instance — the aggregate root exposing
every sub-service a View or Command needs, and the read-only collection
of currently open views. Owns no lifecycle verbs of its own (creation
and shutdown belong to `IWorkspaceManager`, §2) — mirrors the
`ITempestHost`/`ITempestHostBuilder` split exactly.

```csharp
namespace Tempest.App.Workspace;

public interface IWorkspace
{
    IWorkspaceLayout Layout { get; }
    IWorkspaceState State { get; }
    INavigationService Navigation { get; }
    ISelectionService Selection { get; }
    IProjectExplorer ProjectExplorer { get; }
    IPropertyInspector PropertyInspector { get; }

    /// <summary>Every view currently open in the Document Area, in tab order.</summary>
    IReadOnlyList<IWorkspaceView> OpenViews { get; }

    /// <summary>The currently active (focused) view, or <see langword="null"/> if none is open.</summary>
    IWorkspaceView? ActiveView { get; }
}
```

**Rationale.** Read-only aggregate, exactly mirroring `ITempestHost`'s
own shape (`Services`, `State` — data a consumer reads, not verbs a
consumer calls to change what the Host itself is). Every mutating
operation (open a view, change selection, switch area) belongs to the
specific sub-service responsible for it, not to `IWorkspace` itself —
consistent with `FOUNDATION.md`'s own "one reason to change" principle,
applied here so that `IWorkspace` never needs to change when, say,
`INavigationService`'s own `OpenAsync` signature does.

## 2. `IWorkspaceManager`

Creates and owns the lifecycle of the one running `IWorkspace` instance
— the Workspace's own equivalent of `ITempestHostBuilder`/`ITempestHost`,
and the Workspace's own registration point for the extensibility
mechanisms that resolve `ADR-0067` (§11).

```csharp
public interface IWorkspaceManager
{
    /// <summary>The current running Workspace, or <see langword="null"/> before <see cref="StartAsync"/>.</summary>
    IWorkspace? Current { get; }

    /// <summary>Assembles and starts the Workspace — the composition-root entry point.</summary>
    Task<IWorkspace> StartAsync(CancellationToken cancellationToken = default);

    /// <summary>Persists current state (<see cref="IWorkspaceState.SaveAsync"/>) and shuts the Workspace down.</summary>
    Task ShutdownAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers the one <see cref="IWorkspaceViewFactory"/> responsible for
    /// presenting an engineering object of <paramref name="kind"/>.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="kind"/> is null, empty, or whitespace.</exception>
    /// <exception cref="DuplicateWorkspaceRegistrationException">A factory is already registered for <paramref name="kind"/>.</exception>
    void RegisterView(string kind, IWorkspaceViewFactory factory);

    /// <summary>
    /// Registers the one <see cref="IProjectExplorerNodeProvider"/> responsible
    /// for populating the Project Explorer's own tree for <paramref name="kind"/>.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="kind"/> is null, empty, or whitespace.</exception>
    /// <exception cref="DuplicateWorkspaceRegistrationException">A provider is already registered for <paramref name="kind"/>.</exception>
    void RegisterExplorerArea(string kind, IProjectExplorerNodeProvider provider);
}
```

**Rationale.** `RegisterView`/`RegisterExplorerArea` are this contract's
own answer to `ADR-0067` — a future Engineering Discipline Module's own
composition code calls both once, for its own `Kind`, exactly as it
already calls `INavigationProvider.Register`/`ICommandRegistry.
RegisterDescriptor` today. Neither `IWorkspace` nor `IProjectExplorer`
ever needs a compiled reference to `IRequirementsService`,
`IMaterialCatalog`, or any other Engineering Core service — every such
dependency lives inside whatever concrete `IWorkspaceViewFactory`/
`IProjectExplorerNodeProvider` a module registers, preserving
`WP8.0A Workspace Architecture Document.md` §1 Point 4 ("the Workspace
does not know what an Engineering Discipline Module is") at the
contract level, not only as a stated intention.

## 3. `IWorkspaceView`

Renders exactly one engineering object — never a relationship list or a
composed thread read, both of which are the Digital Thread panel's own
concern via `IPropertyInspector` (§11) and a dedicated panel, not a
`IWorkspaceView` responsibility (`WP8.0A UI Architecture.md` §3.1).

```csharp
public interface IWorkspaceView
{
    Guid Id { get; }
    string Title { get; }
    Guid ObjectId { get; }
    string ObjectKind { get; }

    /// <summary><see langword="true"/> if this view holds local edits not yet committed via a Command.</summary>
    bool IsDirty { get; }

    /// <summary>Re-reads <see cref="ObjectId"/> from its owning service and refreshes this view's own display.</summary>
    Task RefreshAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Requests this view close. Returns <see langword="false"/> if the
    /// caller should prompt the user about unsaved edits
    /// (<see cref="IsDirty"/>) before proceeding — this method itself never
    /// prompts, since prompting is a concrete rendering concern, not a
    /// contract-level one.
    /// </summary>
    Task<bool> CloseAsync(CancellationToken cancellationToken = default);
}
```

**Rationale.** `RefreshAsync` never accepts cached data — it always
re-reads from the owning service (`WP8.0A UI Architecture.md` §5's own
"never a cached copy of engineering data itself"). `CloseAsync`
returning `bool` rather than throwing or silently discarding edits keeps
the "what to do about unsaved edits" decision at the caller's own
level (a concrete rendering technology's own dialog/prompt mechanism,
deferred by `ADR-0066`, §10), not baked into this contract.

### Supporting: `IWorkspaceViewFactory`

```csharp
public interface IWorkspaceViewFactory
{
    /// <summary>The single <see cref="IWorkspaceView.ObjectKind"/> this factory constructs a view for.</summary>
    string Kind { get; }

    IWorkspaceView Create(Guid objectId, IWorkspaceContext context);
}
```

## 4. `IWorkspacePanel`

The dockable container base contract — Project Explorer, Property
Inspector, and (once built) the Digital Thread panel all implement this;
the always-present Document Area does not, since it is never
hideable/dockable away (`WP8.0A UI Architecture.md` §2).

```csharp
public interface IWorkspacePanel
{
    Guid Id { get; }
    string Title { get; }
    WorkspaceDockPosition DockPosition { get; }
    bool IsVisible { get; }

    Task ShowAsync(CancellationToken cancellationToken = default);
    Task HideAsync(CancellationToken cancellationToken = default);
}
```

### Supporting: `WorkspaceDockPosition`

```csharp
public enum WorkspaceDockPosition
{
    Left,
    Right,
    Bottom,
}
```

No `Floating` value exists — undocking a panel into its own top-level
window remains explicitly deferred (`WP8.0A Workspace Architecture
Document.md` §"Deliberately Out of Scope"); adding the enum value now,
ahead of the capability it would represent, would misrepresent this
contract as supporting something it does not.

## 5. `IWorkspaceLayout`

The docking arrangement — panel positions, sizes, and visibility.
Distinct from `IWorkspaceState` (§9): this is the structural arrangement
alone; `IWorkspaceState` is the complete session snapshot (layout, open
tabs, last selection) that gets persisted.

```csharp
public interface IWorkspaceLayout
{
    IReadOnlyList<WorkspacePanelPlacement> PanelPlacements { get; }

    WorkspacePanelPlacement GetPlacement(Guid panelId);

    /// <exception cref="ArgumentException"><paramref name="placement"/>'s own <c>PanelId</c> is not a known panel.</exception>
    void SetPlacement(Guid panelId, WorkspacePanelPlacement placement);

    /// <summary>Returns a new layout matching this Workspace's own documented default arrangement (`WP8.0A UI Architecture.md` §1).</summary>
    IWorkspaceLayout ResetToDefault();
}
```

### Supporting: `WorkspacePanelPlacement`

```csharp
public sealed record WorkspacePanelPlacement(
    Guid PanelId,
    WorkspaceDockPosition DockPosition,
    double Size,
    bool IsVisible);
```

An immutable value, mirroring `NavigationItem`/`CommandDescriptor`'s own
established shape for this platform's own registry-pattern data.

## 6. `INavigationService`

Workspace-scoped navigation — distinct from, and built on top of, the
existing `Tempest.Core.Navigation.INavigationProvider`. `INavigationProvider`
answers "what top-level areas exist"; `INavigationService` additionally
answers "open this specific object," "focus it if already open," and
"jump to a related object" — none of which `INavigationProvider` itself
is scoped to handle (it has no concept of a Document Area or a tab).

```csharp
public interface INavigationService
{
    /// <summary>Delegates directly to <see cref="Tempest.Core.Navigation.INavigationProvider.Items"/>.</summary>
    IReadOnlyList<Tempest.Core.Navigation.NavigationItem> Areas { get; }

    /// <summary>Switches the Project Explorer's own current top-level area. Delegates to <see cref="Tempest.Core.Navigation.INavigationProvider.Navigate"/>.</summary>
    /// <exception cref="Tempest.Core.Navigation.NavigationItemNotFoundException"><paramref name="areaId"/> is not registered.</exception>
    Task SwitchAreaAsync(string areaId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens <paramref name="objectId"/> in a new Document Area tab, or
    /// focuses its existing tab if already open — never opens a second tab
    /// for the same object (`WP8.0A UI Architecture.md` §4).
    /// </summary>
    /// <exception cref="WorkspaceViewFactoryNotFoundException">No <see cref="IWorkspaceViewFactory"/> is registered for <paramref name="kind"/>.</exception>
    Task<IWorkspaceView> OpenAsync(Guid objectId, string kind, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens <paramref name="targetObjectId"/> in a <b>new</b> tab, alongside
    /// whatever is already open — the Digital Thread panel's own "jump to"
    /// action (`ADR-0065`), never replacing the source's own tab.
    /// </summary>
    Task<IWorkspaceView> JumpToAsync(Guid targetObjectId, string targetKind, CancellationToken cancellationToken = default);

    Task CloseAsync(Guid viewId, CancellationToken cancellationToken = default);
}
```

**Rationale.** `OpenAsync` and `JumpToAsync` are two distinct methods,
not one with a "replace current tab?" boolean flag, because they answer
two different user intentions named separately in
`WP8.0A User Workflow Diagrams.md` (Journeys 1 and 2) — a boolean
parameter would let a caller silently pick the wrong behaviour by
mistake; two named methods cannot.

## 7. `ISelectionService`

Tracks the Workspace's own current selection and publishes its own
change through the **existing** `IEventBus` — not a plain C# event —
consistent with how `NavigationRequestedEvent` already publishes
navigation changes (`WP 5.0A`) rather than exposing a bespoke event on
`INavigationProvider` itself.

```csharp
public interface ISelectionService
{
    WorkspaceSelection? Current { get; }

    /// <summary>Publishes <see cref="WorkspaceSelectionChangedEvent"/> via <see cref="Tempest.Core.Events.IEventBus"/>.</summary>
    Task SelectAsync(Guid objectId, string kind, CancellationToken cancellationToken = default);

    Task ClearAsync(CancellationToken cancellationToken = default);
}
```

### Supporting: `WorkspaceSelection` and `WorkspaceSelectionChangedEvent`

```csharp
public sealed record WorkspaceSelection(Guid ObjectId, string Kind);

public sealed class WorkspaceSelectionChangedEvent : Tempest.Core.Events.IEvent
{
    public WorkspaceSelection? Previous { get; init; }
    public WorkspaceSelection? Current { get; init; }
}
```

**Rationale.** Reusing `IEventBus` rather than inventing a second
pub/sub mechanism means the Property Inspector, Status Bar, and any
future subscriber (a discipline module's own panel) each independently
implement `IEventHandler<WorkspaceSelectionChangedEvent>` exactly as
`TempestShell` already implements `IEventHandler<NavigationRequestedEvent>`
— zero new subscription infrastructure.

## 8. `IWorkspaceContext`

Ambient, read-only, DI-resolvable state — mirrors
`Tempest.Core.Identity.ICurrentPrincipalAccessor`'s own precedent
(`ADR-0044`) exactly, for the identical reason: a `IWorkspaceCommand`
handler or a `IWorkspaceViewFactory` should not need a constructor
dependency on `ISelectionService` merely to read what is currently
selected — that would make every future consumer depend on the whole
selection *service* (with its own mutation methods) just to read one
ambient fact.

```csharp
public interface IWorkspaceContext
{
    WorkspaceSelection? CurrentSelection { get; }
    Guid? ActiveViewId { get; }
}
```

**Rationale.** Deliberately minimal — two read-only properties, nothing
else. `IWorkspaceContext` is never a service locator: a component
needing `INavigationService` or `ISelectionService` itself still takes
it via ordinary constructor injection, exactly as every other component
in this platform does (`FOUNDATION.md`'s own dependency-injection
discipline, unmodified). See `WP8.0B Dependency Rules.md` §3 for the
complete rule.

## 9. `IWorkspaceState`

The complete, persistable session snapshot — layout, open tabs, and
last selection — backed by the existing `ISettingsProvider`
(`ADR-0064`).

```csharp
public interface IWorkspaceState
{
    IWorkspaceLayout Layout { get; }

    /// <summary>Every currently open view's own <see cref="IWorkspaceView.Id"/>, in tab order.</summary>
    IReadOnlyList<Guid> OpenViewIds { get; }

    WorkspaceSelection? LastSelection { get; }

    /// <summary>Writes current state via <see cref="Tempest.Core.Settings.ISettingsProvider.SetValueAsync{T}"/>.</summary>
    Task SaveAsync(CancellationToken cancellationToken = default);

    /// <summary>Reads persisted state via <see cref="Tempest.Core.Settings.ISettingsProvider.GetValueAsync{T}"/>; a missing value yields <see cref="IWorkspaceLayout.ResetToDefault"/> and no open tabs — never an exception.</summary>
    Task LoadAsync(CancellationToken cancellationToken = default);
}
```

**Rationale.** `LoadAsync` never throws for a first-run/missing-value
case — a brand-new user has no prior session to restore, and that is
the expected, ordinary case, not an error condition, mirroring
`ILicenseProvider`'s own established "a missing value is a valid
default" precedent (`ADR-0050`) rather than `TD-16`'s cautionary
opposite.

## 10. `IProjectExplorer`

```csharp
public interface IProjectExplorer : IWorkspacePanel
{
    Task<IReadOnlyList<ProjectExplorerNode>> GetRootNodesAsync(CancellationToken cancellationToken = default);

    /// <exception cref="ArgumentException"><paramref name="nodeId"/> does not identify a known node.</exception>
    Task<IReadOnlyList<ProjectExplorerNode>> GetChildrenAsync(Guid nodeId, CancellationToken cancellationToken = default);

    Task RefreshAsync(CancellationToken cancellationToken = default);
}
```

### Supporting: `ProjectExplorerNode`, `ProjectExplorerNodeType`, `IProjectExplorerNodeProvider`

```csharp
public sealed record ProjectExplorerNode(
    Guid Id,
    string Title,
    string? Kind,
    bool HasChildren,
    ProjectExplorerNodeType NodeType);

public enum ProjectExplorerNodeType
{
    /// <summary>A structural label with no backing engineering object — e.g. "Groups," "Collections" (`WP8.0A Navigation Specification.md` §3.1).</summary>
    Category,
    Group,
    Collection,
    /// <summary>A real engineering object — a Requirement, a Material, a Calculation Record, a Verification Record.</summary>
    Object,
}

public interface IProjectExplorerNodeProvider
{
    /// <summary>The single top-level area <see cref="Tempest.Core.Navigation.NavigationItem.Id"/> this provider populates.</summary>
    string Kind { get; }

    Task<IReadOnlyList<ProjectExplorerNode>> GetRootNodesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProjectExplorerNode>> GetChildrenAsync(Guid nodeId, CancellationToken cancellationToken = default);
}
```

**Rationale.** `IProjectExplorer` itself never calls
`IRequirementsService`/`IMaterialCatalog`/any Engineering Core service
directly — it delegates every read to whichever
`IProjectExplorerNodeProvider` `IWorkspaceManager.RegisterExplorerArea`
registered for the currently selected area (§2), resolving `ADR-0067`
for tree population exactly as `IWorkspaceViewFactory` resolves it for
object display.

## 11. `IPropertyInspector`

```csharp
public interface IPropertyInspector : IWorkspacePanel
{
    Task InspectAsync(Guid objectId, string kind, CancellationToken cancellationToken = default);

    Task ClearAsync(CancellationToken cancellationToken = default);

    IReadOnlyList<PropertyFacet> CurrentFacets { get; }
}
```

### Supporting: `PropertyFacet`, `PropertyFacetKind`

```csharp
public sealed record PropertyFacet(string Name, string Value, PropertyFacetKind FacetKind);

public enum PropertyFacetKind
{
    /// <summary>Identity — Id, business identifier (`WP8.0A Navigation Specification.md` §4).</summary>
    Identity,
    Revision,
    Provenance,
    Relationship,
    /// <summary>A facet only this object's own `Kind` contributes — e.g. `RequirementStatus`.</summary>
    DisciplineSpecific,
}
```

**Rationale.** `IPropertyInspector` presents the four shared facets
(`WP8.0A Navigation Specification.md` §4) plus whatever
`DisciplineSpecific` facets the selected object's own `IWorkspaceViewFactory`
(or a dedicated inspector contribution — not separately named, since
none of the twelve required interfaces name one, and no genuine need
for a fifth registration mechanism has been demonstrated) supplies.
Selection changes drive `InspectAsync` automatically — a subscriber to
`WorkspaceSelectionChangedEvent` (§7) calls it, the Property Inspector
never subscribes to selection itself, keeping the "who reacts to what"
wiring in one place (the Workspace composition, not scattered across
every panel's own constructor).

## 12. `IWorkspaceCommand`

Extends the **existing** `Tempest.Core.Commands.ICommand` — never a
second, parallel command contract. Every mutating Workspace action
still dispatches through the existing `ICommandDispatcher`
(`ADR-0063`) exactly as before; `IWorkspaceCommand` adds only the one
piece of metadata generic Workspace infrastructure needs to react
uniformly after any Workspace-originated command succeeds.

```csharp
public interface IWorkspaceCommand : Tempest.Core.Commands.ICommand
{
    /// <summary>The engineering object this command acts on.</summary>
    Guid TargetObjectId { get; }

    /// <summary>The <c>Kind</c> of <see cref="TargetObjectId"/> — e.g. <c>"Requirement"</c>.</summary>
    string TargetKind { get; }
}
```

**Rationale.** Deliberately does **not** duplicate `CommandDescriptor.
CanExecute`'s own existing applicability-predicate mechanism
(`Tempest.Core.Commands`) for context-menu filtering — a
Workspace-contributed `CommandDescriptor`'s own `canExecute` predicate
already closes over `IWorkspaceContext.CurrentSelection` (§8) directly,
exactly the same pattern every other `canExecute` predicate in this
platform already uses. `IWorkspaceCommand`'s own, narrower purpose:
after `ICommandDispatcher.DispatchAsync` succeeds for a command
implementing this interface, the Workspace automatically calls
`RefreshAsync` (§3) on whatever open `IWorkspaceView` matches
`TargetObjectId` — a generic, reusable behaviour that works for any
future Workspace command without that command's own handler needing to
know a view might be open and showing stale data.

## Exception Model

Three new exception types, mirroring the Requirements Engine's own
established shape (`ADR-0058`, an abstract base plus concrete leaves,
each carrying only identifiers the caller already knows):

```csharp
public abstract class WorkspaceException : Exception
{
    protected WorkspaceException(string message) : base(message) { }
}

public sealed class DuplicateWorkspaceRegistrationException : WorkspaceException
{
    public string Kind { get; }
}

public sealed class WorkspaceViewFactoryNotFoundException : WorkspaceException
{
    public string Kind { get; }
}
```

No new exception type is introduced for "object not found" —
`INavigationService.OpenAsync`/`JumpToAsync` and every
`IWorkspaceViewFactory`/`IProjectExplorerNodeProvider` implementation
surface whatever exception the underlying Engineering Core service
already throws (`RequirementNotFoundException`,
`EngineeringDocumentNotFoundException`, and siblings) — the Workspace
never wraps or re-throws these under a new type, preserving the
caller's own ability to catch the specific, already-documented
exception directly.

## Summary Table

| Interface | Namespace Role | Primary Dependency |
|---|---|---|
| `IWorkspace` | Aggregate root | Composes §2-§11 |
| `IWorkspaceManager` | Composition/lifecycle | Constructs `IWorkspace`; owns extensibility registries |
| `IWorkspaceView` | Object presentation | `IWorkspaceViewFactory`-constructed |
| `IWorkspacePanel` | Dockable container base | `IWorkspaceLayout` |
| `IWorkspaceLayout` | Docking arrangement | None (pure data) |
| `INavigationService` | Workspace-scoped navigation | `Tempest.Core.Navigation.INavigationProvider` |
| `ISelectionService` | Current selection | `Tempest.Core.Events.IEventBus` |
| `IWorkspaceContext` | Ambient read-only state | None (pure data, mutated internally) |
| `IWorkspaceState` | Persisted session snapshot | `Tempest.Core.Settings.ISettingsProvider` |
| `IProjectExplorer` | Object tree | `IProjectExplorerNodeProvider` (per area) |
| `IPropertyInspector` | Object facet display | `WorkspaceSelectionChangedEvent` subscriber |
| `IWorkspaceCommand` | Mutating action marker | `Tempest.Core.Commands.ICommand` (extends) |

## Related Documents

`WP8.0A Workspace Architecture Document.md` and its four companion
deliverables; `WP8.0B Sequence Diagrams.md`; `WP8.0B Lifecycle
Definitions.md`; `WP8.0B Dependency Rules.md`; `ADR-0062`–`ADR-0067`.
