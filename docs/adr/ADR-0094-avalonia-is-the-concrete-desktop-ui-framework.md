# ADR-0094: Avalonia Is the Concrete Desktop UI Framework

## Status

Accepted — `v0.10.0` "User Experience & Desktop Application", `WP 10.0B`
(Desktop Application Framework), 2026-08-07. Resolves the question
`ADR-0092` (`WP 10.0A`) deliberately reserved: which concrete
cross-platform .NET desktop UI framework realises the graphical
paradigm that ADR decided on.

## Context

`ADR-0092` decided the Workspace's presentation paradigm — a graphical
desktop application — but explicitly deferred the concrete framework
choice as an implementation-phase evaluation, mirroring exactly how
`ADR-0066` once deferred "the specific TUI library." `WP 10.0B`'s own
controlling instruction now asks this Work Package to "select and
justify the concrete desktop UI framework (`ADR-0094`)."

Every existing Workspace contract (`IWorkspaceView`, `IWorkspacePanel`,
`IWorkspaceLayout`, `WorkspacePanelPlacement`, `WorkspaceDockPosition`)
is confirmed rendering-agnostic (`WP10.0A Engineering Review.md`
§F2-F4) — no contract signature constrains this choice. What does
constrain it: TempestOS targets `net10.0` across every project
(`global.json`), has zero prior GUI dependency of any kind
(`ADR-0066`'s own Context), and `ADR-0092`'s own Negative Consequences
name cross-platform reach as a genuine, disclosed risk this decision
must account for, not assume away.

## Decision

**Avalonia (11.2.3) is the concrete desktop UI framework.**
`Tempest.Desktop`, a new project, references `Avalonia`,
`Avalonia.Desktop`, and `Avalonia.Themes.Fluent` — three focused
package references, restored and build-verified directly against
`net10.0` before this decision was finalised, not assumed compatible.

Considered against three real alternatives (§Alternatives Considered):
WPF, .NET MAUI, and a browser-based (Blazor Hybrid/Electron-style)
presentation. Avalonia is chosen because it is the only candidate that
satisfies all four of TempestOS's own real, disclosed constraints at
once:

1. **Cross-platform**, preserving the reach `ADR-0066` once flagged a
   graphical framework would put at risk (`ADR-0092` §Negative) —
   Avalonia runs unmodified on Windows, Linux, and macOS from one
   codebase, via its own Skia-based rendering backend, not a
   platform-specific wrapper.
2. **A genuine desktop windowing/docking/panel model** — multi-window,
   resizable/dockable panel layouts, and a real menu/toolbar/status-bar
   composition are all first-class Avalonia concepts, directly
   satisfying `WP 10.0B`'s own named implementation list (Main window,
   Docking framework, Panel host, Document host, Menu system).
3. **Zero categorically new dependency risk** — Avalonia is MIT-licensed,
   has no native-toolchain build step beyond the ordinary .NET SDK
   (unlike MAUI, which requires platform-specific mobile/desktop
   workloads installed), and — confirmed directly, not assumed —
   restores and builds cleanly against this repository's own `net10.0`
   target with zero additional SDK installation.
4. **A real, first-class headless testing mode** (`Avalonia.Headless`,
   `Avalonia.Headless.XUnit`) — letting this Work Package's own
   "Demonstrate" requirements (multi-panel layout, docking, resizing,
   tabbed documents, persistent layouts, theme switching, Command
   Palette opening, workspace restoration) be proven as real, automated,
   passing xUnit tests, exactly as this project's own testing culture
   requires (`FOUNDATION.md`, "disclose, don't invent" applied to UI
   behaviour: a claim of "it demonstrates docking" is only as credible
   as the test that exercises it). No alternative candidate offers an
   equivalently mature headless/automated UI testing story compatible
   with this project's own existing `dotnet test`-driven verification
   discipline.

## Consequences

**Positive:**

- Genuinely cross-platform, satisfying `ADR-0092`'s own disclosed risk
  directly rather than accepting it unmitigated.
- `Avalonia.Headless.XUnit` lets `WP 10.0B`'s own "Demonstrate" list
  become real, `dotnet test`-verified behaviour, not asserted prose —
  the same standard every prior implementation Work Package's own
  Workspace Integration test suite already holds itself to.
- Every existing Workspace contract is consumed entirely unchanged —
  `Tempest.Desktop` references `Tempest.App` and binds to
  `IWorkspaceManager`/`IWorkspace`/`IProjectExplorer`/`IPropertyInspector`
  directly, proving `ADR-0092`'s own central claim (a rendering-agnostic
  contract layer pays for itself the moment the paradigm changes) a
  second time, this time against real, running framework code rather
  than only a documentation-stage prediction.
- MIT license, no per-seat or commercial cost, consistent with
  `ADR-0050`'s (Licensing Framework) own existing platform-wide posture.

**Negative:**

- This platform's first-ever GUI framework dependency, now real and not
  merely decided in principle (`ADR-0092`'s own disclosed cost, paid
  here). `Tempest.Desktop.csproj` is the first project in this
  repository with a `PackageReference` outside the ASP.NET Core
  `FrameworkReference` (`ADR-0049`) and the test-only packages already
  present.
- `TempestShell`'s own console-based test infrastructure
  (`[Collection("Console output capture")]`, `TextReader`/`TextWriter`
  redirection) does not apply to `Tempest.Desktop` — a new,
  Avalonia-headless-based test project (`tests/Tempest.Desktop.Tests`)
  was introduced instead, named here as the disclosed new test strategy
  `WP10.0A Architecture Review.md` §5 (Risk 2) flagged as an open
  question this Work Package would need to answer.
- Avalonia's own XAML/Fluent visual vocabulary is a new authoring
  surface this project has no prior convention for — `WP10.0A Visual
  Design System.md`'s own theme/iconography/colour architecture
  (concept-level) is realised here at only the first, minimal fidelity
  this Work Package's own scope requires (§Deliberately Out of Scope,
  Implementation Report); a richer visual system remains future work.

## Alternatives Considered

**WPF** — considered and rejected. Windows-only; would directly
contradict the cross-platform reach `ADR-0066`'s own Negative
Consequences named as a real cost of moving to a graphical framework,
turning a disclosed risk into a certainty rather than mitigating it.

**.NET MAUI** — considered and rejected. Primarily a mobile-first
framework with desktop support as a secondary target; requires
per-platform workloads (`maui-windows`, `maui-maccatalyst`, etc.)
installed via `dotnet workload install`, a materially heavier and more
fragile build-environment dependency than a plain `PackageReference` —
confirmed by direct comparison against Avalonia's own zero-workload
restore, verified in this repository's own build environment before
this decision was finalised. Its docking/multi-panel desktop story is
also less mature than Avalonia's, which was designed desktop-first.

**A browser-based (Blazor Hybrid) presentation** — reconsidered from
`ADR-0066`'s own prior rejection of a browser-based Workspace, and
rejected again for the same reason that decision gave: it introduces a
hosting/serving concern this platform's own `VISION.md`
self-description ("not a general-purpose application platform") sits
awkwardly beside, with no clearer benefit than a native framework for
`WP 10.0A`'s own named journeys, and a genuinely native desktop
application — not a browser wrapper — is what the Product Owner's own
Programme 10 title and `WP 10.0B`'s own explicit "concrete desktop UI
framework" instruction both name directly.

## Related Documents

`ADR-0092` (reserved this question); `ADR-0066` (the TUI-era precedent
this decision's own reasoning mirrors in structure); `ADR-0049` (the
closest prior precedent — a specific, narrow technology choice made
once a real need was clear); `WP10.0B Implementation Report.md`;
`WP10.0A UX Architecture Document.md`; `WP10.0A Architecture
Review.md` §5.
