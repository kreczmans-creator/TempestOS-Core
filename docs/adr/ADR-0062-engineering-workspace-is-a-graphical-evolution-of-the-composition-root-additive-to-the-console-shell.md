# ADR-0062: The Engineering Workspace Is a Graphical Evolution of the Composition Root, Additive to the Console Shell

## Status

Accepted — `v0.8.0` "Engineering Workspace", `WP 8.0A` (Engineering
Workspace Architecture), 2026-07-30. Resolves the question `WP 8.0A`'s
own controlling instruction raises implicitly: what *is* the Engineering
Workspace, architecturally — a new Platform Service, a new Module
category, or something else?

## Context

Every real, user-facing capability TempestOS has shipped through
`v0.7.0` lives one layer beneath what a user actually sees:
`TempestShell` (`ADR-0033`–`ADR-0035`, `WP 5.0D`) is a minimum-viable,
text-console composition root — a Navigation Region, a Content Region,
and a reserved Status Bar, driven by a `TextReader`/`TextWriter` loop.
It renders `PlaceholderPage` stubs; no Engineering Core object has ever
been displayed. `NavigationItem`'s own design (`WP 5.0A`) already
anticipated this would not be the platform's last word on presentation:
its own `Icon` property is documented as "a symbolic key only...
resolving what, if anything, an `Id` or `Icon` actually looks like on
screen is entirely `Tempest.App`'s (or any future UI shell's) own
responsibility." `WP 8.0A` must decide what that future UI shell
actually is, architecturally, before designing its own internal
structure.

Three candidate shapes exist, by direct analogy to `ADR-0033`'s own
reasoning for the Shell: a new Platform Service (rejected there, for the
Shell, on the grounds that a blocking, interactive presentation loop has
no natural completion point inside either the module or hosted-service
lifecycle); a new Module category; or a composition root, evolved from
the one that already exists.

## Decision

**The Engineering Workspace is `Tempest.App`'s own composition root,
evolved from a console-based Shell into a graphical, multi-panel
desktop presentation — additive to, not a replacement of,
`TempestShell`'s own architectural role.** It introduces zero new
Platform Service and zero new Module category. It consumes exactly the
Platform Services and Engineering Core services that already exist
(`INavigationProvider`, `ICommandDispatcher`/`ICommandRegistry`,
`IDiagnosticsProvider`, `ISettingsProvider`, `IEngineeringDocumentStore`
and every framework built on it), exactly as `TempestShell` already
does, at greater presentational depth.

This decision locks in the Workspace's own **shape** — a windowed,
multi-panel, docking desktop experience — as an architectural boundary
decision, made now because every other section of `WP8.0A Workspace
Architecture Document.md` depends on it (docking strategy, view
architecture, and the main window layout all presuppose a graphical,
not console, presentation). It deliberately does **not** lock in the
concrete rendering technology (a specific .NET desktop UI framework) —
that is an implementation-phase evaluation, reserved as `ADR-0066`, not
an architectural boundary this document needs to cross to complete its
own scope.

## Consequences

**Positive:**

- Zero new Platform Service, zero new authorization model, zero new
  storage mechanism — the Workspace inherits every governance guarantee
  (permission gating, audit trail once wired, revision history) the
  services it presents already provide.
- `TempestShell` itself need not be deleted or deprecated — it remains
  a valid, minimal, always-available presentation for any future
  headless, scripted, or constrained-environment use case, exactly as a
  console fallback commonly coexists alongside a graphical shell in
  comparable platforms.
- The four-layer platform model (`ADR-0023`) requires no amendment —
  the Workspace sits exactly where `TempestShell` already sits, above
  the Runtime Host, consuming Platform APIs.

**Negative:**

- A graphical, docking, multi-panel desktop application is a
  substantially larger presentation surface than a console loop —
  implementation effort for `Tempest.App`'s own composition root will be
  materially larger than `TempestShell`'s own ~280 lines, a cost this
  ADR accepts as the necessary price of the platform's first real
  engineering-domain product surface (`VISION.md`'s own Long-Term
  Objective 2).
- Two presentation layers (console `TempestShell`, graphical Workspace)
  now coexist in the repository, a maintenance surface `WP 5.0D` did not
  anticipate — accepted, not treated as debt, since `TempestShell`
  remains genuinely useful on its own terms (see Positive, above), not
  merely legacy code left in place by inertia.

## Alternatives Considered

**A new Platform Service** — considered and rejected, for the identical
reason `ADR-0033` rejected it for the Shell: a graphical, interactive
presentation surface has no natural completion point inside either the
module or hosted-service lifecycle, and forcing one would gain nothing
over composition-root placement.

**Replacing `TempestShell` outright** rather than adding a second
presentation layer — considered and rejected. `TempestShell` remains a
valid, minimal, dependency-light presentation with no real cost to
retain; replacing it would require deciding, prematurely, that no future
consumer will ever want the lighter option, a claim this ADR does not
make.

**Deferring the graphical-vs-console question entirely to Contract
Review** — considered and rejected. Every subsequent section of
`WP8.0A Workspace Architecture Document.md` (docking, view
architecture, main window layout) is meaningless without first deciding
the Workspace is graphical — this is a genuine architecture-phase
boundary decision, not an implementation detail deferrable without
stalling the rest of this Work Package's own scope.

## Related Documents

`ADR-0023` (four-layer platform model); `ADR-0033`–`ADR-0035` (Shell &
Composition Framework); `ADR-0031`/`ADR-0032` (Navigation); `WP8.0A
Workspace Architecture Document.md`; `WP8.0A UI Architecture.md`;
`VISION.md`.
