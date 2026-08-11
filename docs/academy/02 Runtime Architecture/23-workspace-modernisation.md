# Workspace Modernisation — Real Dispatch Behind a Modern Shell

## 1. Introduction

This is the Academy's own concept guide for `WP 10.2A` — the Work
Package that transformed `Tempest.Desktop`'s own working-but-minimal
`WP 10.0B`/`WP 10.1A` shell into a modern, professional engineering
application UI, while finding and closing a genuine, four-Work-Package
-old platform gap along the way.

## 2. Purpose

To explain why "modernise the UI" turned out to require a small, real
Workspace contract extension (`ADR-0096`) rather than being pure
presentation-layer work, and why that extension was the *smaller* risk,
not the larger one.

## 3. Background

`Tempest.Desktop` (`WP 10.0B`) shipped a real, working, but visually and
functionally minimal shell: a plain `TreeView`, plain grouped text in
the Property Inspector, a single-line Status Bar, one keyboard
shortcut. Every discipline (`WP 9.0A` onward) had, meanwhile, already
built a real, tested `Rename*Command`/`Delete*Command` pair per Kind —
`MechanicalWorkspaceRegistration`'s own remarks even named the exact
future caller: "a future context-menu action." No such caller had ever
been built.

## 4. The Problem

`WP 10.2A`'s own controlling instruction names "inline rename" and
"editable controls where appropriate" as required capabilities. Neither
can be built honestly as decorative UI — this platform's own "never
fabricate" discipline, applied consistently since the Engineering
Cockpit's own placeholder audits (`WP 10.1A`), extends naturally to not
building a text box that looks editable but silently does nothing.

## 5. The Design

`IWorkspaceManager` gained five new, additive members
(`RegisterRenameFactory`/`RegisterDeleteFactory`/`CanRename`/`CanDelete`/
`RenameObjectAsync`/`DeleteObjectAsync`), mirroring `RegisterFacetProvider`'s
own `ADR-0082` shape exactly. Dispatch reuses `CommandHandlerTable`'s
own existing, unmodified runtime-type-keyed lookup — the same primitive
`ICommandRegistry.InvokeAsync` already relies on. Every discipline's own
already-existing Rename/Delete command became a registered factory; no
new command class was written anywhere in this Work Package.

Around that real dispatch path, the whole shell was modernised: a
filterable, breadcrumbed, multi-select Project Explorer with a real
context menu and inline rename; a collapsible, sectioned Property
Inspector with a real editable Name field and two new, honestly-scoped
summary sections (Lifecycle, extracted from existing facets; Validation,
an honest placeholder); pinned, actively-highlighted Document Area tabs;
a six-segment Status Bar; five keyboard shortcuts; and a shared
`DesignTokens` spacing/typography system applied consistently
throughout.

## 6. Alternatives Considered

- **Build decorative rename/delete UI, disclosed as "not yet wired"** —
  rejected. Contradicts this project's own established "never
  fabricate" discipline; the real, working alternative was available
  and not materially harder.
- **A generic, untyped `ExecuteObjectCommandAsync(string verb, ...
  object[] args)`** — rejected (`ADR-0096`'s own Alternatives
  Considered): loses compile-time shape, reads as disguised reflection.
- **Expose `CommandHandlerTable` directly to `Tempest.Desktop`** —
  rejected: would bypass the Workspace's own established "composition
  root registers, presentation layer consumes" pattern for no benefit.

## 7. Why This Solution Was Chosen

It satisfied every one of this Work Package's own constraints at once:
genuinely required (no alternative honestly delivered "inline rename"),
minimally scoped (five additive members, zero existing member changed),
and precedented (mirrors `ADR-0082` exactly, the second time this exact
extensibility shape has been needed).

## 8. Architectural Principles

- **A "future context-menu action" named four Work Packages ago is
  still a real requirement when the time comes** — `WP 9.0A`'s own
  `createDefault`-omission comment was not decorative; it was a
  correctly-deferred design note, honoured here.
- **"Never fabricate" applies to interactive affordances, not only
  displayed data** — a text box that looks editable but silently does
  nothing is the UI equivalent of a fabricated Cockpit number.
- **A second use of an extensibility pattern confirms it, rather than
  merely repeating it** — `ADR-0096` is `ADR-0082`'s own pattern applied
  a second time, for a different concern (write dispatch vs. read
  sourcing), strengthening the case that Kind-keyed registration is
  genuinely this platform's own general Workspace extensibility idiom,
  not a one-off.

## 9. Benefits

A real, working Rename/Delete capability now exists for every
discipline that has one; two disciplines' own genuine incompleteness
(Requirements' rename, Calculations' synthetic Kind) is honestly
surfaced via `CanRename`/`CanDelete` rather than silently
always-enabled or fabricated. The whole shell reads as a coherent,
modern application, not a collection of independently-styled panels.

## 10. Trade-offs

Drag/drop is prepared, not implemented — reparenting needs a second,
larger, not-yet-uniform contract decision (`Move*Command`'s own
per-discipline shape differs, unlike Rename/Delete's own now-uniform
one) this Work Package's own proportionate scope did not justify.
"Current Project"/Notifications remain honest placeholders — no
platform capability backs either yet.

## 11. Common Mistakes

Treating "modernise the UI" as a purely visual task, missing that two
of the explicitly-required capabilities (inline rename, editable
controls) cannot be delivered honestly without real dispatch; building
a second, parallel dispatch mechanism instead of reusing the one
`ICommandRegistry.InvokeAsync` already proved out; extending a facet
group's own display logic (Lifecycle) without also removing the
extracted items from their own original group — a real defect this
Work Package's own Engineering Review found and fixed before sign-off,
not before it shipped.

## 12. Future Evolution

~~A uniform `Move*Command` shape across all six disciplines (mirroring
`ADR-0096`'s own Rename/Delete uniformity) would let a future Work
Package complete the drag/drop preparation architecture into real
reparenting.~~ **Implemented, `WP 10.7A`** — a lighter route than this
entry originally proposed: `ProjectExplorerView.OnTreeDrop` now raises a
new `ObjectMoveRequested` event (mirroring this class's own existing
`ObjectSelected`/`ObjectOpened` shape); `MainWindow` maps the dropped
object's own Kind to the correct discipline's own already-registered
`Move*Command` directly, never adding a `RegisterMoveFactory` member to
`IWorkspaceManager`. `ADR-0095` (floating/multi-monitor panels) remains
the next reserved contract question.

**The Property Inspector's own Validation section is now real,
`WP 10.8A`.** Previously a fixed "No automated validation is available
for this object type yet" message regardless of Kind — this class only
ever saw `PropertyFacet`s, never the real object `IValidatable.ValidateAsync`
needs. `EngineeringDomainContext` is now an optional, additive
constructor parameter (mirroring exactly how `ObjectEditorView` gained
`ICommandDispatcher` the same Work Package prior); when present, the
section performs the identical `IValidatable` read `ObjectEditorView`'s
own Validation section already does, reusing its `BuildSeverityRow`
helper directly (made `internal`, not duplicated). One disclosed,
honest exception remains: a Requirement never resolves through
`EngineeringDomainContext.Repository` (`TD-41`), so this section
reports "Real validation is not available for this object here" for
that one discipline rather than a fabricated result.

## 13. Key Takeaways

A UI modernisation Work Package that takes its own "never fabricate"
discipline seriously will sometimes need a small, real capability
extension underneath the visual work — the correct response is not to
avoid it, but to scope it as narrowly and precedentedly as the actual
requirement demands.

## Related Documents

- `ADR-0096`; `ADR-0082`; `ADR-0067`
- `docs/releases/v0.10.0/WP10.2A Implementation Report.md`
- `docs/academy/03 Work Packages/WP10.2A-workspace-modernisation.md`
