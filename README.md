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
├── Tempest.App/             # A console front end — still the legacy
│                            # bootstrap/project-management app; does not
│                            # yet run through TempestHost (see
│                            # docs/governance/Engineering/Platform Services
│                            # Register.md)
├── Samples/Tempest.Samples/ # ClockModule and ClockLifecycleObserverModule —
│                            # the living reference modules every Work
│                            # Package extends
└── Plugins/                 # Empty by design — no real plugin ships yet

tests/
└── Tempest.Core.Tests/      # xUnit tests, mirroring src/'s own namespace
                              # structure directory-for-directory
```

For the full platform picture — every platform service, module, hosted
service, and plugin that exists, what's implemented versus contract-only,
and how it's all cross-referenced — see
[`docs/governance/Governance Index.md`](docs/governance/Governance%20Index.md)
and [`docs/architecture/Platform Service Map.md`](docs/architecture/Platform%20Service%20Map.md).

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

Run the console application:

```
dotnet run --project src/Tempest.App/Tempest.App.csproj
```

Run tests:

```
dotnet test src/TempestOS.slnx
```

## Archive

A prior Python prototype (Build 0008.3, "Foundation Alpha Rev A") has been
retired and is retained for historical reference only, under
[archive/](archive/README.md). It is not part of the active codebase and
receives no further development.

## License

See [LICENSE.md](LICENSE.md).
