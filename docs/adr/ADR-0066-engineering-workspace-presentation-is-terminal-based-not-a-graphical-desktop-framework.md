# ADR-0066: Engineering Workspace Presentation Is Terminal-Based, Not a Graphical Desktop Framework

## Status

Accepted — `v0.8.0` "Engineering Workspace", `WP 8.0B` (Workspace
Contracts), 2026-07-30. Resolves the question `ADR-0062` deliberately
reserved: what concrete rendering technology presents the Workspace to
a user?

**Superseded 2026-08-07 by `ADR-0092`** (`v0.10.0` "User Experience &
Desktop Application", `WP 10.0A`) — the Product Owner's own Programme
10 commissioning is the real, demonstrated need for a graphical desktop
experience this ADR's own Consequences §Negative named as its
reversal condition. This decision's own reasoning remains historically
correct for the evidence available at the time it was made — see
`ADR-0092` for the full disclosure and the reversal's own reasoning.
This record is left otherwise unedited, per this project's own "never
silently modify historical records" discipline.

## Context

`ADR-0062` locked in the Workspace's own *shape* — a windowed,
multi-panel, docking desktop experience — while explicitly reserving
the concrete rendering technology as `ADR-0066`, "an implementation-
phase evaluation." `WP 8.0B`'s own controlling instruction now asks
this Work Package to resolve `ADR-0066` "where appropriate." It is
appropriate here: `IWorkspaceView`'s and `IWorkspacePanel`'s own
contracts (`WP8.0B Workspace Contracts.md` §3-4) are already written to
be rendering-agnostic (no pixel, window-handle, or graphics-API concept
appears in any signature) — but the *category* of technology chosen
still affects real, near-term decisions (what `Tempest.App`'s own
project file references, whether a new platform-wide dependency is
introduced, whether the existing console-based test infrastructure
remains usable) that a future implementation Work Package should not
have to re-derive from scratch.

TempestOS's own entire history to date has zero graphical UI framework
dependency of any kind. `TempestShell` (`WP 5.0D`) is a `TextReader`/
`TextWriter`-driven console loop; every project in the repository
targets `net10.0` with exactly one `FrameworkReference`
(`Microsoft.AspNetCore.App`, `ADR-0049`, confined to the REST API's own
hosting boundary) and zero UI-framework `PackageReference` of any kind
(`WP7.4.0 Release Readiness Report.md` §10). A full graphical desktop
framework (WPF, Avalonia, MAUI, or similar) would be this platform's
first-ever GUI dependency — a materially larger commitment than any
dependency this project has taken on before, including ASP.NET Core
itself (which added networking capability to an already-headless
platform, not a new interaction paradigm to what a user sees).

## Decision

**The Engineering Workspace's own presentation is terminal-based — a
rich, multi-panel Terminal User Interface (TUI), not a pixel-based
graphical desktop GUI framework.** Docking panels, tabs, a Command Bar,
and a Status Bar are all achievable within a terminal — a TUI framework
provides window/pane management, focus, and input handling inside the
same terminal surface `TempestShell` already runs in, without
introducing this platform's first GUI dependency. `WP8.0A Workspace
Architecture Document.md`'s own use of "graphical" is clarified, not
reversed, by this decision: it described the Workspace's own *visual
richness* (multiple panels, docking, tabs) relative to `TempestShell`'s
own single-region console loop, not a commitment to a pixel-based
rendering paradigm specifically.

The **specific TUI library** (for example, a `Terminal.Gui`-shaped
dependency, a `Spectre.Console`-shaped dependency, or a narrower,
hand-rolled ANSI-escape renderer) remains an implementation-phase
choice, genuinely reserved — none of the twelve contracts in
`WP8.0B Workspace Contracts.md` depends on which one is eventually
chosen, since every signature is already rendering-agnostic. This
narrower question does not need its own ADR number: it is a library
selection within an already-decided paradigm, the same class of
decision `WP 6.3`'s own choice of ASP.NET Core/Kestrel specifically
(rather than "some HTTP framework, TBD") made at implementation time,
not architecture time.

## Consequences

**Positive:**

- Zero new dependency category is introduced by this decision alone —
  a TUI library remains a single, focused `PackageReference` (or
  `FrameworkReference`, depending on the specific library chosen),
  comparable in scope to `ADR-0049`'s own `ASP.NET Core` decision, not a
  categorically larger commitment.
- `TempestShell`'s own existing console-based test infrastructure
  (`[Collection("Console output capture")]`, `TextReader`/`TextWriter`
  redirection) remains directly applicable to Workspace testing —
  no parallel, GUI-specific test harness needs designing.
- Cross-platform reach is preserved automatically — a terminal exists
  on every platform TempestOS already targets, with no OS-specific
  windowing toolkit to evaluate per platform.
- Consistent with `VISION.md`'s own Product Principle 3 ("do not build
  ahead of real, demonstrated need") — no real, demonstrated need for a
  pixel-based GUI has been identified; every user journey in
  `WP8.0A User Workflow Diagrams.md` is satisfiable within a rich
  terminal interface.

**Negative:**

- A terminal interface is a genuine capability ceiling relative to a
  full graphical framework — no custom fonts, no embedded images, no
  pixel-precise layout, and generally coarser interaction fidelity
  (mouse support varies by terminal and TUI library, keyboard-first
  interaction is the reliable baseline). Accepted, disclosed, not
  treated as a defect: no engineering journey named in this Work
  Package's own scope requires graphical-only capability.
- Should a real, demonstrated need for a graphical desktop experience
  emerge later (a customer requirement, a rendering need no terminal
  can satisfy), this decision would need formal revisiting — not a
  minor library swap, since `ADR-0062`'s own "additive to console
  Shell" framing and this decision's own rendering-paradigm choice are
  now both premised on staying within one broad presentation category
  (text/terminal), not two.

## Alternatives Considered

**A full graphical desktop framework (WPF, Avalonia, MAUI)** —
considered and rejected. This would be the platform's first-ever GUI
dependency, a categorically larger commitment than any dependency this
project has taken on before, with no real, demonstrated need behind it
— the same "do not build ahead of a demonstrated need" reasoning
`ADR-0065` already applied to Digital Thread traversal, applied here to
an entire presentation paradigm.

**A browser-based (Blazor/web) presentation** — considered and
rejected. This would introduce a hosting/serving concern (a local web
server, a browser dependency) with no clearer benefit than a rich TUI
for this Work Package's own named user journeys, and would sit awkwardly
alongside `TempestShell`'s own console-native precedent and this
platform's own "not a general-purpose application platform" self-
description (`VISION.md`).

**Deferring the paradigm question entirely, resolving only individual
interface signatures** — considered and rejected. `WP 8.0B`'s own
controlling instruction explicitly invites resolving `ADR-0066` "where
appropriate," and leaving the paradigm undecided would force every
future reader of `WP8.0B Workspace Contracts.md` to guess whether "main
window," "docking," and "panel" describe a terminal or a desktop
window — this ADR removes that ambiguity without over-committing to a
specific library.

## Related Documents

`ADR-0062`; `ADR-0049` (the closest prior precedent — a specific,
narrow technology choice made once a real need was clear);
`WP8.0A Workspace Architecture Document.md`; `WP8.0B Workspace
Contracts.md`; `WP7.4.0 Release Readiness Report.md` §10 (Dependency
Consistency — the zero-GUI-dependency baseline this decision preserves).
