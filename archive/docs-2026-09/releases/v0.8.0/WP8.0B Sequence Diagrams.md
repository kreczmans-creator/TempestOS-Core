# WP 8.0B — Workspace Contracts — Sequence Diagrams

## Purpose

Sequence diagrams showing exactly how `WP8.0B Workspace Contracts.md`'s
own twelve interfaces interact, for the six operations that exercise
every contract at least once. Each diagram cites the exact method
signature it calls — no diagram below invents an interaction the
contracts document does not already define.

## 1. Workspace Startup

```mermaid
sequenceDiagram
    participant App as Tempest.App entry point
    participant WM as IWorkspaceManager
    participant WS as IWorkspaceState
    participant WL as IWorkspaceLayout
    participant SP as ISettingsProvider (existing)

    App->>WM: StartAsync()
    WM->>WS: LoadAsync()
    WS->>SP: GetValueAsync<WorkspaceStateDto>(...)
    alt First run — no persisted state
        SP-->>WS: null
        WS->>WL: ResetToDefault()
    else Prior session exists
        SP-->>WS: persisted layout + open tabs + last selection
    end
    WM-->>App: IWorkspace (assembled, Layout/State populated)
```

## 2. Select a Requirement in the Project Explorer

```mermaid
sequenceDiagram
    actor Engineer
    participant PE as IProjectExplorer
    participant NP as IProjectExplorerNodeProvider<br/>(Requirements, module-registered)
    participant SS as ISelectionService
    participant EB as IEventBus (existing)
    participant PI as IPropertyInspector

    Engineer->>PE: (UI) selects "REQ-0001" node
    PE->>SS: SelectAsync(REQ-0001.Id, "Requirement")
    SS->>EB: Publish(WorkspaceSelectionChangedEvent)
    EB-->>PI: HandleAsync(WorkspaceSelectionChangedEvent)
    PI->>PI: InspectAsync(REQ-0001.Id, "Requirement")
    Note over PI: Calls IRequirementsService.FindAsync internally,<br/>via whatever facet-mapping this Kind's own<br/>registration supplies (§11, Workspace Contracts)
    PI-->>Engineer: Facets displayed
```

Note `IProjectExplorer` itself never calls `IRequirementsService`
directly — `GetRootNodesAsync`/`GetChildrenAsync` (§10, Workspace
Contracts) delegate to the registered `IProjectExplorerNodeProvider` for
the current area.

## 3. Open an Object (with View-Factory Extensibility)

```mermaid
sequenceDiagram
    actor Engineer
    participant PE as IProjectExplorer
    participant NS as INavigationService
    participant WM as IWorkspaceManager
    participant VF as IWorkspaceViewFactory<br/>(Requirements, module-registered)
    participant W as IWorkspace

    Engineer->>PE: (UI) double-clicks "REQ-0001"
    PE->>NS: OpenAsync(REQ-0001.Id, "Requirement")
    NS->>W: Check OpenViews for existing ObjectId match
    alt Already open
        NS-->>Engineer: Focus existing tab (no new IWorkspaceView created)
    else Not yet open
        NS->>WM: (internal) resolve registered factory for "Requirement"
        WM-->>NS: IWorkspaceViewFactory
        NS->>VF: Create(REQ-0001.Id, IWorkspaceContext)
        VF-->>NS: IWorkspaceView
        NS->>W: Add to OpenViews, set ActiveView
        NS-->>Engineer: New tab opened and focused
    end
```

If no factory is registered for `"Requirement"`,
`OpenAsync` throws `WorkspaceViewFactoryNotFoundException` (§Exception
Model, Workspace Contracts) — surfaced to the engineer as "this object
type cannot be displayed," never a silent no-op.

## 4. Digital Thread — Jump to a Linked Calculation Record

```mermaid
sequenceDiagram
    actor Engineer
    participant DT as Digital Thread panel<br/>(an IWorkspaceView, per ADR-0065)
    participant RS as IRequirementsService (existing)
    participant NS as INavigationService

    Engineer->>DT: Open Digital Thread for REQ-0001
    DT->>RS: GetEvidenceAsync(REQ-0001.Id)
    RS-->>DT: RequirementEvidence (verification history + references)
    DT-->>Engineer: Listed, each with a "Jump to" action
    Engineer->>DT: "Jump to" CALC-0004
    DT->>NS: JumpToAsync(CALC-0004.Id, "CalculationRecord")
    Note over NS: New tab — REQ-0001's own tab remains open<br/>(never OpenAsync, which would only focus an existing tab)
    NS-->>Engineer: CALC-0004 opened in a new tab, alongside REQ-0001
```

## 5. Revise a Requirement (Command Dispatch + Auto-Refresh)

```mermaid
sequenceDiagram
    actor Engineer
    participant V as IWorkspaceView (REQ-0001 tab)
    participant CD as ICommandDispatcher (existing)
    participant RS as IRequirementsService (existing)
    participant W as IWorkspace

    Engineer->>V: Edits statement text
    Note over V: IsDirty becomes true
    Engineer->>V: Invokes "Revise Requirement"
    V->>CD: DispatchAsync(ReviseRequirementCommand)
    Note over CD: ReviseRequirementCommand : IWorkspaceCommand<br/>TargetObjectId = REQ-0001.Id, TargetKind = "Requirement"
    CD->>RS: ReviseAsync(REQ-0001.Id, newStatement, changeSummary)
    RS-->>CD: Updated Requirement (new revision)
    CD-->>V: Success
    Note over W: Generic post-dispatch hook (§12, Workspace Contracts):<br/>command implements IWorkspaceCommand →<br/>find open view matching TargetObjectId
    W->>V: RefreshAsync()
    V->>V: IsDirty = false (re-read from RS.FindAsync)
```

## 6. Layout Change and Persistence

```mermaid
sequenceDiagram
    actor Engineer
    participant P as IWorkspacePanel (Project Explorer)
    participant WL as IWorkspaceLayout
    participant WS as IWorkspaceState
    participant SP as ISettingsProvider (existing)

    Engineer->>P: Resizes the Project Explorer panel
    P->>WL: SetPlacement(ProjectExplorer.Id, newPlacement)
    Note over WS: Debounced or on-shutdown — exact trigger<br/>is an implementation-phase choice, not fixed here
    WS->>SP: SetValueAsync(workspaceStateSettingKey, currentSnapshot)
```

## Related Documents

`WP8.0B Workspace Contracts.md`; `WP8.0B Lifecycle Definitions.md`;
`WP8.0B Dependency Rules.md`; `ADR-0063`; `ADR-0064`; `ADR-0065`;
`ADR-0067`.
