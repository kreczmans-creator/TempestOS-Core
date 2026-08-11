# ADR-0092: Engineering Workspace Presentation Moves to a Graphical Desktop Application, Superseding ADR-0066

## Status

Accepted — `v0.10.0` "User Experience & Desktop Application", `WP 10.0A`
(User Experience Architecture), 2026-08-07. **Supersedes `ADR-0066`.**
This is the first supersession of any Architecture Decision Record in
this project's history — 91 ADRs existed before this Work Package,
none previously marked Superseded (`docs/governance/Architecture/ADR
Register.md`).

## Context

`ADR-0066` (`WP 8.0B`, 2026-07-30) decided, deliberately and with full
reasoning, that the Engineering Workspace's presentation would be a
rich Terminal User Interface (TUI) — not a pixel-based graphical
desktop GUI framework — because no real, demonstrated need for a
graphical experience had been identified at that time, and a TUI
satisfied every user journey named in `WP8.0A User Workflow
Diagrams.md` without introducing this platform's first-ever GUI
dependency. That ADR named its own reversal condition explicitly, in
its own Consequences §Negative: *"Should a real, demonstrated need for
a graphical desktop experience emerge later... this decision would
need formal revisiting — not a minor library swap."*

That condition is now met. The Product Owner has commissioned
**Programme 10 — User Experience & Desktop Application**, opening with
`WP 10.0A`'s own controlling instruction naming, as explicit, required
scope: multi-monitor behaviour, theme architecture, iconography
strategy, an engineering colour language, resizable and dockable
panels beyond a terminal's own coarse-grained window management, and a
full object-relationship visualisation. None of these is achievable
inside `ADR-0066`'s own terminal paradigm at the fidelity the
controlling instruction's own topic list implies — a terminal has no
concept of a monitor boundary to reason about, no iconography beyond a
constrained glyph/Unicode set, and no pixel-precise docking geometry.
The Programme's own name, "Desktop Application," is itself the
demonstrated Product Owner decision `ADR-0066` said would be needed —
not an inference this Work Package draws on its own initiative.

This Work Package's own controlling instruction is explicit: "This is
an architecture and specification Work Package only. No implementation.
No code changes. No contract changes." Deciding the presentation
*paradigm* — exactly the class of decision `ADR-0066` itself was — sits
squarely inside that scope, mirroring how `ADR-0066` was itself an
architecture-only decision with zero code attached to it. What this
Work Package explicitly does **not** do is choose a concrete UI
framework or touch any `Tempest.App.Workspace` contract — both are
named below as genuinely reserved, implementation- and
contract-review-phase questions, mirroring exactly how `ADR-0066` itself
once reserved "the specific TUI library" without designing it.

## Decision

**The Engineering Workspace's presentation moves from a Terminal User
Interface to a pixel-based graphical desktop application, for `v0.10.0`
onward.** `ADR-0062`'s own framing — a windowed, multi-panel, docking
experience, additive to (not replacing) `TempestShell`'s own console
loop — is **not** reversed by this decision; it is, for the first time,
realised as originally read literally, rather than reinterpreted
narrowly the way `ADR-0066` itself did ("`WP8.0A Workspace Architecture
Document.md`'s own use of 'graphical' is clarified, not reversed... it
described the Workspace's own visual richness... not a commitment to a
pixel-based rendering paradigm specifically"). This Work Package
resolves that same word, "graphical," back to its plain meaning.

Four concrete consequences follow directly from the paradigm choice,
each elaborated in this Work Package's own companion documents, and
each explicitly **not** designed at the contract or implementation
level here:

1. **A concrete, cross-platform .NET desktop UI framework** must
   eventually be chosen (a library selection, not an architecture
   decision — mirroring `ADR-0066`'s own identical treatment of "the
   specific TUI library"). **Reserved as `ADR-0094`**, an
   implementation-phase evaluation, not designed here.
2. **`WorkspaceDockPosition`/`WorkspacePanelPlacement` need a genuine
   contract extension** — a graphical desktop application supports
   undocking a panel into its own top-level window (`WorkspaceDockPosition`
   deliberately has no `Floating` value today, per its own XML
   documentation) and placing that window on a specific monitor in a
   multi-monitor arrangement (no monitor concept exists in either type
   today). **Reserved as `ADR-0095`**, a Contract Review Work Package's
   own question — explicitly out of this Work Package's own "no
   contract changes" scope. `WorkspacePanelPlacement.Size`'s own
   existing "deliberately unitless... for example, a column count in a
   terminal" documentation already anticipated this exact moment: a
   pixel- or device-independent-unit interpretation is now the correct
   one, requiring no signature change, only a documentation update
   `ADR-0095`'s own eventual Work Package should make.
3. **Zero change to any Engineering Core, Platform Service, or existing
   Workspace contract.** `IWorkspaceView`, `IWorkspacePanel`,
   `IWorkspaceLayout`, `IProjectExplorer`, `IPropertyInspector`,
   `ICommandRegistry`, `ISettingsProvider`, and every Engineering
   Discipline service already consumed by the six real Workspace
   disciplines (`Tempest.App.Workspace.Mechanical`/`.Requirements`/
   `.Calculations`/`.Verification`/`.Documents`/`.Manufacturing`) are
   already rendering-agnostic, confirmed directly by `ADR-0066`'s own
   Context ("no pixel, window-handle, or graphics-API concept appears
   in any signature") — a claim this ADR re-verifies still holds by
   direct read of the current contracts, unchanged since `WP 8.0B`.
   **This is why this decision requires zero contract change**, exactly
   as reversing `ADR-0066` warned it would *not* be a minor swap only
   where the reversal touches rendering-*specific* concepts (docking
   geometry, monitor placement) — those two are named above and
   deferred to `ADR-0095`, not silently assumed away.
4. **`TempestShell` (console) is not removed or deprecated by this
   decision.** `ADR-0062`'s own "additive to the console Shell" framing
   is explicitly retained — the Workspace becomes the graphical desktop
   experience `Tempest.App` presents by default (`ADR-0068`, unchanged);
   `TempestShell` remains available and untouched, exactly as it has
   been additive rather than replaced since `v0.8.0`.

## Consequences

**Positive:**

- Resolves the single reversal condition `ADR-0066` itself named, on
  the exact evidentiary basis that ADR required (a real, demonstrated
  Product Owner decision, not a speculative "might be nice").
- Every existing Workspace contract survives this decision unchanged —
  `WP 8.0B`'s own rendering-agnostic contract design (itself a
  deliberate choice, confirmed correct by this ADR) means six full
  Engineering Disciplines' worth of Workspace implementation
  (`v0.9.0`, `WP 9.0A`–`WP 9.5A`) requires zero rework to remain valid
  under a graphical desktop paradigm. This is the single strongest
  piece of evidence in this project's history that `ADR-0066`'s own
  original design ("every signature already rendering-agnostic") was
  the correct call even though the paradigm itself has now changed.
- Multi-monitor behaviour, theming, iconography, and pixel-precise
  docking — all named in `WP 10.0A`'s own controlling instruction —
  become achievable in principle for the first time; none was
  achievable inside a terminal regardless of implementation effort.

**Negative:**

- This is now this platform's first-ever commitment to a graphical UI
  dependency category — exactly the "materially larger commitment"
  `ADR-0066` itself flagged as the reason to defer the decision until a
  real need existed. That cost is accepted now, on the strength of the
  Product Owner's own explicit Programme commissioning, not waived or
  minimised.
- `TempestShell`'s own console-based test infrastructure
  (`[Collection("Console output capture")]`, `TextReader`/`TextWriter`
  redirection) does **not** carry forward to graphical Workspace
  testing the way `ADR-0066` noted it would for a TUI. A future
  implementation Work Package will need a new, disclosed test strategy
  for a graphical presentation layer — named here as a real, open
  question for that future Work Package, not answered by this
  architecture-only one.
- Cross-platform reach, which `ADR-0066` noted a terminal preserves
  "automatically," now depends on the concrete framework `ADR-0094`
  eventually selects — a genuine, disclosed risk deferred to that
  decision, not resolved by this one.

## Alternatives Considered

**Continue under `ADR-0066`'s own TUI paradigm, satisfying `WP 10.0A`'s
own topic list within a terminal's own capability ceiling** —
considered and rejected. A terminal-native reading of "multi-monitor
behaviour," "theme architecture," "iconography strategy," and an
"engineering colour language" would require stretching each term past
what a terminal can actually express (ANSI 256-colour approximations of
a theme; glyph-only "icons"; no real per-monitor window placement) —
producing a specification that only nominally answers the controlling
instruction's own explicit topic list rather than actually satisfying
it. `ADR-0066` itself anticipated and rejected exactly this kind of
strained accommodation when it chose a TUI in the first place *because*
a TUI satisfied `WP8.0A`'s own journeys without stretching; the
symmetry holds in reverse here.

**A browser-based (Blazor/web) presentation** — reconsidered from
`ADR-0066`'s own prior rejection, and rejected again for the same
reason: it would introduce a hosting/serving concern this platform's
own `VISION.md` self-description ("not a general-purpose application
platform") sits awkwardly alongside, with no clearer benefit than a
native desktop framework for the same named journeys, and a genuine
desktop application is what the Product Owner's own Programme title
names directly.

**Designing the concrete UI framework choice now, inside this same
Work Package** — considered and rejected. `WP 10.0A`'s own controlling
instruction is explicit ("No implementation. No code changes. No
contract changes."), and `ADR-0066` already established the precedent
that a specific rendering library is an implementation-phase decision,
not an architecture-phase one — the identical reasoning applies in
reverse now (§Decision, Point 1; reserved `ADR-0094`).

## Related Documents

`ADR-0066` (superseded by this decision); `ADR-0062` (unchanged,
reaffirmed — its own "graphical, multi-panel, additive to console
Shell" framing is realised literally by this decision, not reversed by
it); `ADR-0065` (superseded separately, `ADR-0093`); `ADR-0067`/
`ADR-0068`/`ADR-0069`/`ADR-0070`/`ADR-0071` (all unchanged, reaffirmed —
presentation-technology-agnostic); `WP10.0A UX Architecture
Document.md`; `WP10.0A Visual Design System.md`; `WP8.0A Workspace
Architecture Document.md`; `WP8.0B Workspace Contracts.md`; `WP8.0C UX
Specification.md`.
