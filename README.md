# TempestOS

TempestOS is a modular runtime platform: a Runtime Host
(`TempestHost`/`TempestHostBuilder`) that discovers, registers, and
orchestrates modules through a deterministic startup/shutdown sequence,
built on six independently-designed platform services (Configuration,
Logging, Discovery, Registration, Dependency Injection, Lifecycle), and
extended with Plugin loading, an Event Bus, and Host-orchestrated
Background Services.

**Where the project stands right now** (current release, current branch,
current Work Package, repository metrics): see
[`PROJECT_STATUS.md`](PROJECT_STATUS.md). **Why it's built this way, and
what must never change**: see
[`docs/releases/FOUNDATION.md`](docs/releases/FOUNDATION.md). **New to
this repository?** Start with
[`docs/academy/Contributor Learning Path.md`](docs/academy/Contributor%20Learning%20Path.md).

## Current Implementation

TempestOS is implemented in C# on .NET 10. This is the sole canonical,
actively developed codebase.

## Architecture

The solution is organised as follows:

```
src/
├── TempestOS.slnx           # Solution file
├── Tempest.Core/            # The platform itself: Configuration, Logging,
│                            # Discovery, Registration, Lifecycle, Dependency
│                            # Injection, Runtime (the Host), Events (Event
│                            # Bus), Plugins, BackgroundServices (hosted
│                            # services), Commands, Versioning — plus legacy,
│                            # pre-module-pipeline bootstrap/project code
│                            # (Bootstrap, Hosting, Projects, Repositories)
├── Tempest.App/             # TempestOS's Internal Engineering Harness
│                            # (ADR-0101) — a console presentation
│                            # (WorkspaceShell) over the shared Engineering
│                            # Workspace domain layer, plus the domain layer
│                            # itself (WorkspaceManager, all six Engineering
│                            # Disciplines' commands/node providers).
│                            # Tempest.Desktop depends on this project's
│                            # shared domain layer; not a shipped product of
│                            # its own — a fast, scriptable verification
│                            # tool, not TempestOS's application.
├── Tempest.Desktop/         # TempestOS's shipped desktop application
│                            # (ADR-0092, ADR-0094) — Avalonia 11.2.3,
│                            # the graphical Engineering Workspace: Ribbon,
│                            # Object Editors, Docking, Digital Thread graph,
│                            # Command Palette, Undo/Redo, Macros. This is
│                            # how TempestOS is actually run and used.
├── Samples/Tempest.Samples/ # ClockModule, ClockLifecycleObserverModule, and
│                            # the six real Engineering Discipline sample
│                            # modules — the living reference modules every
│                            # Work Package extends
├── Templates/                # `dotnet new` module project template
└── Plugins/                  # Empty by design — no real plugin ships yet

tests/
├── Tempest.Core.Tests/       # xUnit tests, mirroring src/'s own namespace
│                             # structure directory-for-directory
└── Tempest.Desktop.Tests/    # xUnit tests against real Avalonia headless
                               # rendering — no display attached, no mocks
```

For the full platform picture — every platform service, module, hosted
service, and plugin that exists, what's implemented versus contract-only,
and how it's all cross-referenced — see
[`docs/governance/Governance Index.md`](docs/governance/Governance%20Index.md)
and [`docs/architecture/Platform Service Map.md`](docs/architecture/Platform%20Service%20Map.md).
For why `Tempest.App` and `Tempest.Desktop` both exist and what each one
is for, see [`ADR-0101`](docs/adr/ADR-0101-tempest-app-workspaceshell-is-tempestos-internal-engineering-harness-not-a-shipped-product.md).

## Documentation

This repository documents itself in five places, each with one stated job:

- **`docs/adr/`** — Architecture Decision Records: what was decided, and why.
- **`docs/architecture/`** — standing architecture reference (Runtime Host,
  Host Lifecycle, failure model, and more).
- **`docs/academy/`** — teaching material: principles, patterns, case
  studies, and a retrospective for every Work Package. Start at
  [`docs/academy/Academy Index.md`](docs/academy/Academy%20Index.md).
- **`docs/releases/`** — `FOUNDATION.md` (the permanent constitution) plus
  one subtree per release.
- **`docs/governance/`** — the governance register suite: every ADR,
  platform service, module, risk, and piece of technical debt indexed and
  cross-referenced. Start at
  [`docs/governance/Governance Index.md`](docs/governance/Governance%20Index.md).

## Build Instructions

Requires the .NET SDK version pinned in [global.json](global.json).

```
dotnet build src/TempestOS.slnx
```

**Run TempestOS** (the shipped desktop application):

```
dotnet run --project src/Tempest.Desktop/Tempest.Desktop.csproj
```

**Run the Internal Engineering Harness** (`Tempest.App`/`WorkspaceShell`
— a console verification tool, not a second application; see
[`ADR-0101`](docs/adr/ADR-0101-tempest-app-workspaceshell-is-tempestos-internal-engineering-harness-not-a-shipped-product.md)):

```
dotnet run --project src/Tempest.App/Tempest.App.csproj
```

Run tests:

```
dotnet test src/TempestOS.slnx
```

TempestOS also has a CI pipeline (`.github/workflows/ci.yml`) that builds
Debug and Release and runs the complete test suite on every push, pull
request, and manual dispatch — see
[`docs/academy/06 Engineering Standards/04-continuous-integration.md`](docs/academy/06%20Engineering%20Standards/04-continuous-integration.md).

## Archive

A prior Python prototype (Build 0008.3, "Foundation Alpha Rev A") has been
retired and is retained for historical reference only, under
[archive/](archive/README.md). It is not part of the active codebase and
receives no further development.

## License

See [LICENSE.md](LICENSE.md).
