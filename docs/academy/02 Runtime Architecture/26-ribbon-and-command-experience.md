# Ribbon & Command Experience

## 1. Introduction

`WP 10.3B`'s own concept guide — how TempestOS built a professional
Engineering Ribbon entirely as a *view* over the existing
`ICommandRegistry` (`ADR-0070`), and how discovering that zero
registered commands anywhere in this platform's history had ever set
`CreateDefault` reshaped the entire dispatch design, including finding
and fixing a genuine, previously-invisible defect in the Command
Palette itself.

## 2. Purpose

Explains why "no new command framework" and "do not bypass `ADR-0070`"
together point toward reusing the three already-Kind-keyed dispatch
verbs (`ADR-0096`/`ADR-0097`) rather than generalising
`ICommandRegistry.InvokeAsync`, and documents the two genuine,
disclosed defects found while building this class — one pre-existing,
one in this Work Package's own new code — both fixed before commit.

## 3. Background

Every prior `v0.10.0` Work Package through `WP 10.3A` explicitly
excluded a ribbon ("No ribbon"). `WP 10.3B`'s own controlling
instruction reverses that exclusion directly, naming "Engineering
ribbon" as its own first scope item — a genuine, disclosed change of
direction from the Product Owner, not a contradiction this Work
Package silently resolves; no prior ADR ever formally forbade a
ribbon (`ADR-0092`/`ADR-0094` decided the desktop *paradigm* and
*framework*, neither commits to or against any specific chrome style),
so no ADR supersession is needed here — the prior Work Packages'
own "No ribbon" was each a disclosed, per-Work-Package scope boundary,
not a standing architectural decision.

## 4. The Problem

`Tempest.Desktop` had a Menu, a two-button Toolbar, and a Navigation
Framework button row (`WP 10.0B`) — none of them discipline-command-
aware. A user reaching a real discipline command (Rename, Delete,
Create, ...) had exactly two paths: the Project Explorer's own context
menu, or the Command Palette. Neither surfaced commands grouped by
discipline, with icons, with tooltips, or with a "recently used"
memory — the professional command-centre experience a ribbon provides.

## 5. The Design

**One generic engine, over `ICommandRegistry.Items`.** `RibbonView`
groups every registered `CommandDescriptor` by `Category` into tabs —
zero per-discipline Desktop code, mirroring `ObjectEditorView`'s own
"one engine, six disciplines" precedent (`WP 10.3A`) a second time.

**A genuine, load-bearing discovery**: a direct `grep` across this
platform's entire history found zero `CommandDescriptor` registrations
anywhere ever set `CreateDefault`. `ICommandRegistry.InvokeAsync`
therefore cannot invoke a single real command by Id alone — a fact
that shaped the entire dispatch design. Rather than generalising the
Command Framework (explicitly excluded), the Ribbon reuses
`IWorkspaceManager`'s own three real, Kind-keyed verbs for the two
operations that genuinely need no more input than a click (Delete) or
that already have a real input-collection surface elsewhere (Rename/
Edit, routed to the Object Editor). Everything else honestly reports
it cannot be dispatched from a button click alone.

**Two genuine defects found, both fixed before commit.** Building the
Ribbon's own honest "cannot dispatch" case exposed the identical,
pre-existing problem already latent in `CommandPaletteOverlay`: since
no command has ever had `CreateDefault`, pressing Enter on any real
command in the Palette has silently done nothing, with zero feedback,
since `WP 8.1A`. Fixed at its own source (`CommandUnavailable`, a new
event). Separately, this Work Package's own new `RibbonView.Rebuild()`
was found, by its own test suite, to never call `RefreshEnablement()`
— every selection-aware button defaulted to enabled regardless of
whether a selection existed. Fixed at its own source too.

## 6. Alternatives Considered

- **A fourth `IWorkspaceManager` Kind-keyed extension, generalising
  dispatch to any command.** Rejected — "No new command framework"
  reads most naturally as excluding exactly this generalisation, even
  though it would technically be additive like `ADR-0096`/`ADR-0097`.
- **A ribbon-local text box for Rename/Revise input.** Rejected —
  would duplicate the Object Editor's own already-real Name/Content
  fields (`WP 10.3A`), the identical "don't rebuild what already
  exists" reasoning that shaped the Object Editor Framework itself.
- **Keeping the old Toolbar and Navigation Framework alongside the new
  Ribbon.** Rejected — both would become genuinely redundant with QAT/
  Ribbon content respectively, not complementary.

## 7. Why This Solution Was Chosen

Every alternative would have either violated "no new command
framework" in spirit, duplicated an already-real UI surface, or left
redundant, unmaintained controls alongside their own direct
replacements — resolving to the design that reuses the most existing
capability and introduces the least new surface.

## 8. Architectural Principles

- **Composition Over Inheritance** — one Ribbon engine composes
  Category-grouped, verb-classified data; never six hand-built tabs.
- **Fail Fast / Honest Disclosure** — a command with no real dispatch
  route says so, immediately, rather than silently doing nothing (the
  exact discipline whose absence caused the Palette's own defect).
- **Single Responsibility** — `RibbonView` renders and classifies;
  `IWorkspaceManager` dispatches; neither duplicates the other.
- **Fix Defects At Their Own Source** — both disclosed defects were
  fixed inside the class that owned them, never worked around one
  layer up.

## 9. Benefits

- A real, professional, discipline-grouped command surface, achieved
  with zero new Workspace/Domain/Runtime surface.
- A genuine, six-years-latent Command Palette defect (informally: since
  `WP 8.1A`) finally found and fixed, closing a real gap in `ADR-0070`'s
  own "reachable from the palette" promise.
- Recently-Used commands and status-bar hints give the Ribbon real,
  live feedback a static toolbar never had.

## 10. Trade-offs

- Command icons/grouping are verb-derived heuristics, not authored
  per-command data — `FCR-0069`.
- No discipline command has a real keyboard shortcut yet — an honest,
  pre-existing gap, not fabricated here.
- Rename/Edit routes to a tab switch, not an immediate inline edit —
  a real, disclosed behavioural difference from the Project Explorer's
  own `F2` shortcut for the "same" action.

## 11. Common Mistakes

- **Assuming the Ribbon can invoke any command by Id.** It cannot —
  `CreateDefault` is `null` everywhere; only Delete/Rename/Edit have a
  real dispatch route today.
- **Assuming the Command Palette's own silent no-op was always
  intentional.** It was a genuine, undisclosed defect, not a design
  choice — fixed this Work Package.
- **Assuming Ribbon tabs and Navigation areas are two independent
  concepts that happen to look similar.** They are the same concept,
  deliberately consolidated — a Ribbon tab click *is* an area switch.

## 12. Future Evolution

- `FCR-0069` — Real, Authored Per-Command Icons.
- A fourth `IWorkspaceManager` extension generalising selection-aware
  dispatch beyond Delete/Rename/Edit, if a real, demonstrated need for
  more ribbon-dispatchable verbs emerges (deliberately not built here
  — see §6).
- Persisting Recently-Used commands across restarts, mirroring
  `DesktopPanelUiState`'s own established pattern, if a real need
  emerges.
- ~~`FCR-0075` — Uniform Create/Duplicate Wiring Across All Six
  Disciplines.~~ **Implemented, `WP 10.7A`** — `ObjectCreationHandlers`
  (§5's own extensibility seam, deliberately not a fourth
  `IWorkspaceManager` member) now carries a real handler for Create/
  Duplicate/status-transition verbs across all six disciplines, not
  Mechanical alone; `WP 10.8A` added Manufacturing's own "Record
  Inspection Result" (a disclosed cross-Work-Package reuse of
  `Verification.RecordVerificationResultCommand`, the identical command
  the Object Editor's own Verification section dispatches). **The one
  remaining unwired verb, Copy, needs a destination-parent picker
  dialog that does not exist anywhere in this platform** — `FCR-0073`,
  unchanged, genuinely out of scope until that dialog exists.
- **Honest-fallback wording corrected, `WP 10.8A`.** The final fallback
  message (§5, step 5) previously claimed a command "not yet collected"
  by the Ribbon was "available via Project Explorer's own context menu,
  or the Command Palette (Ctrl+K)" — confirmed by direct investigation
  that **neither** is actually true for any command still reaching that
  branch (the Command Palette cannot invoke any real discipline command
  by Id at all, §11; the Project Explorer's own context menu offers only
  Open/Rename/Delete/Favourite). The message now names no false
  alternative — a direct instance of this platform's own "the UI must
  honestly communicate capability" discipline.

## 13. Key Takeaways

A professional Engineering Ribbon was built entirely as a view over
the existing `ICommandRegistry`/`IWorkspaceManager`, introducing zero
new Workspace/Domain/Runtime surface and zero new command framework —
made possible by reusing the exact two dispatch verbs (`ADR-0096`/
`ADR-0097`) that structurally need no more input than a button click.
A genuine, six-Work-Package-latent defect in the Command Palette was
found and fixed as a direct consequence of building the Ribbon's own
honest failure-reporting, closing a real gap in `ADR-0070`'s own
central promise.

## Related Documents

`WP10.3B Implementation Report.md`; `WP10.3B Architecture Review.md`;
`WP10.3B UX Review.md`; `docs/adr/ADR-0070-...md`; `docs/adr/ADR-0063-...md`;
`25-engineering-object-editors.md` (the prior concept guide this one
follows).
