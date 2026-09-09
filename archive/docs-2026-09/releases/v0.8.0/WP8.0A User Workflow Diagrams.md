# WP 8.0A — Engineering Workspace — User Workflow Diagrams

## Purpose

Step-by-step diagrams for the five user journeys named in `WP8.0A
Workspace Architecture Document.md` §2, each annotated with exactly
which existing (or reserved) mechanism carries out each step. No new
platform capability is assumed by any diagram below.

## Journey 1 — Browse and Inspect a Requirement

```mermaid
sequenceDiagram
    actor Engineer
    participant CB as Command Bar
    participant PE as Project Explorer
    participant PP as Properties Panel
    participant RS as IRequirementsService

    Engineer->>CB: Select "Requirements" area
    CB->>PE: Show Requirements tree
    PE->>RS: ListAsync() / GetRelationshipsAsync() per group
    RS-->>PE: Groups, Collections, Requirements
    Engineer->>PE: Select "REQ-0001"
    PE->>PP: Update selection
    PP->>RS: FindAsync(REQ-0001.Id)
    RS-->>PP: Requirement (statement, status, revisions)
    PP-->>Engineer: Displays facets (§4, Navigation Specification)
```

Zero writes. Every step is a direct read against the already-shipped
`IRequirementsService`.

## Journey 2 — Trace the Digital Thread

```mermaid
sequenceDiagram
    actor Engineer
    participant DA as Document Area
    participant DT as Digital Thread Panel
    participant RS as IRequirementsService
    participant VS as IVerificationService

    Engineer->>DA: Open REQ-0001 (new tab)
    Engineer->>DT: Open Digital Thread panel for REQ-0001
    DT->>RS: GetEvidenceAsync(REQ-0001.Id)
    RS->>VS: GetVerificationHistoryAsync (internal)
    RS->>RS: GetReferencesAsync (internal)
    RS-->>DT: Composed RequirementEvidence
    DT-->>Engineer: Verification history + linked references, listed
    Engineer->>DT: "Jump to" a linked CalculationRecord
    DT->>DA: Open CalculationRecord in a NEW tab
    Note over DA: REQ-0001's own tab remains open —<br/>Workspace Philosophy Point 2
```

`GetEvidenceAsync`'s own internal composition (`IVerificationService.
GetVerificationHistoryAsync` + `GetReferencesAsync`) is exactly what
`WP 7.3A` already ships — the Digital Thread panel adds no new read.

## Journey 3 — Revise a Requirement

```mermaid
sequenceDiagram
    actor Engineer
    participant DA as Document Area (REQ-0001 tab)
    participant CD as ICommandDispatcher
    participant RS as IRequirementsService

    Engineer->>DA: Edit statement text
    Engineer->>DA: Invoke "Revise Requirement" command
    DA->>CD: DispatchAsync(ReviseRequirementCommand)
    Note over CD: Command Framework handler<br/>(a future Work Package's own responsibility)
    CD->>RS: ReviseAsync(REQ-0001.Id, newStatement, changeSummary)
    RS-->>CD: Updated Requirement (new revision)
    CD-->>DA: Success
    DA->>DA: Refresh tab from RS.FindAsync (re-read, not cached)
```

The View never calls `ReviseAsync` directly (`ADR-0063`) — it dispatches
a Command, and the Command's own handler (not yet designed; a future
Work Package's own scope) calls the service.

## Journey 4 — Change Lifecycle Status

```mermaid
sequenceDiagram
    actor Engineer
    participant CM as Context Menu
    participant CR as ICommandRegistry
    participant CD as ICommandDispatcher
    participant RS as IRequirementsService

    Engineer->>CM: Right-click REQ-0001 (status: Draft)
    CM->>CR: GetDescriptors() filtered by Kind + current status
    CR-->>CM: ["Mark Reviewed", ...] (only valid transitions shown)
    Engineer->>CM: Select "Mark Reviewed"
    CM->>CD: DispatchAsync(SetRequirementStatusCommand)
    CD->>RS: SetStatusAsync(REQ-0001.Id, Reviewed)
    alt Transition permitted
        RS-->>CD: Success
    else Transition not permitted
        RS-->>CD: InvalidRequirementStatusTransitionException
        CD-->>CM: Surfaced as a command failure, not a silent no-op
    end
```

`RequirementStatusTransitions`'s own existing permitted-transition table
(`WP 7.3A`) is the single source of truth for which menu items even
appear — the Workspace does not duplicate that table, only queries
against it indirectly via what the Command Framework allows.

## Journey 5 — A Future Module Contributes Its Own Area

```mermaid
sequenceDiagram
    participant Module as Future Engineering<br/>Discipline Module
    participant NP as INavigationProvider
    participant CR as ICommandRegistry
    participant WS as Workspace (Command Bar,<br/>Project Explorer, context menus)

    Module->>NP: Register NavigationItem (new top-level area)
    Module->>CR: Register CommandDescriptor(s)
    Note over WS: Zero Workspace code change required
    WS->>NP: Items (already includes the new area)
    WS->>CR: GetDescriptors() (already includes the new commands)
    WS-->>WS: New area + commands appear automatically
```

This is the existing extensibility model, unchanged — the only reserved,
not-yet-designed extension point is the object-view contract itself
(`WP8.0A Workspace Architecture Document.md` §10, `ADR-0067` reserved),
since a module's own object `Kind` still needs a View to render it, and
that registration mechanism does not exist yet.

## Related Documents

`WP8.0A Workspace Architecture Document.md`; `WP8.0A UI Architecture.md`;
`WP8.0A Navigation Specification.md`; `WP8.0A Object Relationship
Diagrams.md`.
