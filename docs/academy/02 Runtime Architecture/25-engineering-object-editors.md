# Engineering Object Editors

## 1. Introduction

`WP 10.3A`'s own concept guide — how TempestOS replaced the Document
Area's own three-line placeholder with one generic, real editor
engine, applied uniformly across all six Engineering disciplines,
without duplicating six bespoke implementations and without reopening
any of the twelve frozen `WP8.0B` Workspace contracts beyond one
precedented, additive extension.

## 2. Purpose

Explains why "one engine, six disciplines" is the architecturally
correct answer once every real Engineering Object already composes
the identical `EngineeringObjectBase` facet set (`ADR-0075`), and how
`ADR-0063` ("Workspace Views read directly; every mutation dispatches
through the Command Framework") shaped every design decision in the
Object Editor Framework, from Content editing down to why Validation
never blocks Save.

## 3. Background

`DocumentAreaView.BuildBody` (`WP 10.0B`) rendered exactly three
read-only lines — Title, Kind, Id — for every open tab, since
`WP10.0A UX Architecture Document.md` §8's own Object Editors were
explicitly named as future work at the time. Four Work Packages later
(`WP 10.1A` through `WP 10.2B`), that placeholder was still the whole
of what a user saw when opening any Engineering Object. This Work
Package builds the real thing.

## 4. The Problem

A user could select an object (Property Inspector), see its facets
(read-only), and open it (a tab) — but never actually edit it, never
see its own relationships in context, never see its own lifecycle
history, and never navigate from one related object to another without
returning to the Project Explorer tree each time.

## 5. The Design

**One generic engine** (`ObjectEditorView`), not six per-discipline
classes. Every real Engineering Object, regardless of discipline,
already implements `IHasBusinessIdentifier`/`IHasRevisions`/
`IHasLifecycle`/`IHasRelationships`/`IValidatable` unconditionally
(`EngineeringObjectBase`) — the generic engine reads all five directly
(`ADR-0063` permits direct reads), rendering Identity, Content,
Lifecycle, Relationships, and Validation sections identically
regardless of which discipline the object belongs to. Discipline
differentiation comes from the *data* (a Requirement's own Content
reads differently from a Mechanical Component's own Content), not from
six different class hierarchies.

**Two real, dispatched editable fields**: Name (`IWorkspaceManager.RenameObjectAsync`,
`ADR-0096`) and Content (`ReviseObjectAsync`, `ADR-0097`, this Work
Package's own new, third Kind-keyed `IWorkspaceManager` extension,
mirroring `ADR-0096`'s exact shape). Both are gated per-Kind by an
honest `CanRename`/`CanRevise` pre-check — never a field that looks
editable but silently fails.

**Dirty-state as a new, Desktop-local concept**: `IWorkspaceView.IsDirty`
has meant "always false, every mutation commits immediately" since `WP
8.1A` — a genuinely different, correct meaning for a View that never
buffers. `ObjectEditorView.IsDirty` is a new, separate, buffered
concept, reflected in the Document Area's own tab header
(`DocumentAreaView.MarkDirty`) *alongside*, never *instead of*, the
frozen contract member.

**Validation, informational only**: `ValidateAsync()` already existed
at the Domain layer since `ADR-0075`, never previously reachable from
any Workspace/Desktop surface. This Work Package surfaces it, real,
for the first time — but deliberately never blocks Save on a
pre-existing finding, since this platform's `IValidationRuleSet` has
no notion of "which errors this specific edit caused."

## 6. Alternatives Considered

- **Six separate, per-discipline editor classes.** Rejected — would
  duplicate the identical field-population logic six times, precisely
  the anti-pattern `ADR-0075`'s own composition-over-inheritance
  reasoning already rejected at the Domain layer, reapplied here at
  the presentation layer for the same reason.
- **Direct calls to `IHasRevisions.ReviseAsync`/`IHasLifecycle.TransitionAsync`
  from the View.** Rejected outright — a direct violation of
  `ADR-0063`'s own explicit decision.
- **Blocking Save on any Validation finding.** Rejected — this
  platform's validation model has no per-edit attribution; blocking on
  unrelated, pre-existing findings would be surprising, not helpful.

## 7. Why This Solution Was Chosen

Every alternative considered would have either duplicated logic the
Domain layer already unifies, violated an already-Accepted
architectural decision, or introduced surprising behaviour without a
real capability to back it — resolving to the simplest design that
genuinely serves all six disciplines through the platform's own
already-uniform Domain-object shape.

## 8. Architectural Principles

- **Composition Over Inheritance** — one engine composes five already-
  uniform facets, rather than six subclasses each reimplementing the
  same composition.
- **Separation of Concerns** — reads are direct (View responsibility);
  writes dispatch through Commands (Workspace-layer responsibility).
- **Fail Fast / Honest Disclosure** — `CanRename`/`CanRevise` gate
  editability per-Kind; `TryCreate` returns `null` rather than
  fabricating an editor for an object that does not really exist.
- **Single Responsibility** — `ObjectEditorView` owns rendering and
  buffered edit state; `IWorkspaceManager` owns dispatch; neither
  duplicates the other's concern.

## 9. Benefits

- Six named "editor" scope items satisfied by one, real, working
  class — not six thinner, less-complete ones.
- `Content` becomes genuinely editable for the first time on every
  Kind across all six disciplines, including Mechanical, the one
  discipline that had no Revise command of any kind before this Work
  Package.
- Validation feedback becomes real for the first time at the Desktop
  layer, closing a gap `PropertyInspectorView` had only ever disclosed,
  never closed.

## 10. Trade-offs

- No bespoke per-discipline layout yet (a BOM-fields section for
  Mechanical, an Execute button for Calculations) — disclosed,
  `FCR-0068`.
- Closing a dirty tab does not prompt for confirmation — disclosed,
  `TD-40`.
- Relationship rows show raw `RelationshipKind` strings, not a
  human-friendly phrase — the same presentation the Property Inspector
  already uses since `WP 10.2A`, not a new inconsistency.

## 11. Common Mistakes

- **Assuming six discipline names in the scope list meant six
  classes.** They meant six real, working editors — satisfied by one
  class applied to six sets of real Kinds.
- **Assuming `ObjectEditorView.IsDirty` is `IWorkspaceView.IsDirty`
  finally becoming real.** It is a distinct, new, Desktop-local
  concept; every concrete `IWorkspaceView.IsDirty` still returns
  `false`, unchanged.
- **Assuming Validation blocks Save.** It does not, deliberately —
  see §6.

## 12. Future Evolution

- ~~`FCR-0068` — Discipline-Specific Object Editor Enhancements (BOM
  fields, Owner/Priority, Execute, Record Result, Attachments).~~
  **Implemented, `WP 10.7A`** — five real, Kind-gated sections
  (`Expander.IsVisible` toggled by `PopulateFrom`'s own gate), each
  dispatching an already-existing, already-registered command via
  `ICommandDispatcher` (newly threaded through as a constructor
  parameter): Mechanical BOM (`SetBomLineCommand`), Requirements Owner/
  Priority (`SetRequirementOwnerCommand`/`SetRequirementPriorityCommand`),
  Calculations Execute/Recalculate (`ExecuteCalculationCommand`/
  `RecalculateCalculationCommand`), Verification Record Result
  (`RecordVerificationResultCommand`), Documents Attachments
  (`AttachDocumentCommand`, metadata only — `TD-31` unchanged, no file
  bytes fabricated). See `WP10.7A Implementation Report.md`.
- **One genuine, disclosed gap found while implementing the above,
  registered as `TD-41`**: `TryCreate` never resolves a real Requirement
  via `EngineeringDomainContext.Repository` — a pre-existing gap since
  this very Work Package (`WP 10.3A`), not caused by `WP 10.7A`. The new
  Requirements Owner/Priority section is correctly implemented but
  reachable only via the Ribbon for that one discipline, not this class.
- `TD-40` — a real "unsaved changes" confirmation on tab close.
  **Resolved, `WP 10.5A`.**
- A richer, human-friendly `RelationshipKind` label mapping, if a
  future Work Package finds the raw string presentation genuinely
  confusing in practice.

## 13. Key Takeaways

Six named "Object Editor" requirements were satisfied by one generic
engine, not six duplicated ones — made possible because every real
Engineering Object across all six disciplines already shares one
uniform Domain-layer shape (`ADR-0075`). Editable properties (Name,
Content) are both real and dispatched through Commands, never a direct
Domain call, honouring `ADR-0063` throughout. One genuine, precedented,
additive contract extension (`ADR-0097`) closes the one remaining gap
(`ADR-0063` requires Commands; Mechanical had no Revise command) —
zero redesign of any of the twelve frozen `WP8.0B` contracts.

## Related Documents

`WP10.3A Implementation Report.md`; `WP10.3A Architecture Review.md`;
`WP10.3A UX Review.md`; `docs/adr/ADR-0063-...md`; `docs/adr/ADR-0096-...md`;
`docs/adr/ADR-0097-...md`; `24-docking-and-workspace-layouts.md` (the
prior concept guide this one follows).
