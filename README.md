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
│                            # services), Commands, Versioning. The legacy,
│                            # pre-module-pipeline bootstrap/project code
│                            # this line used to name (Bootstrap, Hosting,
│                            # Projects, Repositories) was deleted in full
│                            # by WP-C (TD-110) — it is gone, not retired
│                            # in place
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

For why `Tempest.App` and `Tempest.Desktop` both exist and what each one
is for, see [`ADR-0101`](docs/adr/ADR-0101-tempest-app-workspaceshell-is-tempestos-internal-engineering-harness-not-a-shipped-product.md).
The wider governance register suite this section used to point to — every
platform service, module and piece of technical debt indexed and
cross-referenced — was archived by `WP 17.0B` (below); `docs/adr/` and
`BACKLOG.md` are now the live sources for the two questions it answered
most often: what was decided, and what still needs doing.

## Documentation

`WP 17.0B` ("Governance reset") archived the Work Package retrospective
and governance-register suite this repository used to carry live —
archive/docs-2026-09/. What remains live is deliberately smaller:

- **`docs/adr/`** — Architecture Decision Records: what was decided, and
  why. Frozen in place; new ADRs are still written here.
- **`docs/architecture/`** — the seventeen standing architecture documents
  the running code still depends on (Runtime Host, Host Lifecycle,
  Startup/Shutdown Sequence, Command Framework, Event Bus, and more).
- **`docs/security/`** — the threat model and security principles the
  platform is designed against.
- **`docs/releases/`** — `FOUNDATION.md` (the permanent constitution),
  `docs/releases/v1.0.0/WorkPackages.md` (the current programme), and one
  `Release Notes.md` per shipped release.
- **`BACKLOG.md`** (repository root) — the live technical-debt list a user
  could still notice, each row mapped to the Work Package that owns it.
- **`CONTRIBUTING.md`** (repository root) — how a Work Package becomes a
  branch, a PR, and a merge.
- **`archive/`** — everything superseded by this reset, moved with `git mv`
  so its full history is intact: Work Package retrospectives, review-board
  dispositions, superseded registers and architecture documents. Start at
  [`archive/docs-2026-09/README.md`](archive/docs-2026-09/README.md).

## Build Instructions

Requires the .NET SDK version pinned in [global.json](global.json). It is
the only mandatory install — no workloads, no external services, no
secrets.

**Taking this onto a workstation to run and review it?**
[`PHYSICAL_REVIEW.md`](PHYSICAL_REVIEW.md) is the complete clean-machine
guide: minimum environment, exact build/test commands, launch procedure,
where runtime data is written, how to reset it, a 10–15 minute smoke test,
and the known platform limitations.

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
