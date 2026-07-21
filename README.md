# TempestOS

TempestOS is a project engineering platform providing structured project
creation, tracking, and lifecycle management for engineering work.

## Current Implementation

TempestOS is implemented in C# on .NET 10. This is the sole canonical,
actively developed codebase.

## Architecture

The solution is organised as follows:

```
src/
├── TempestOS.slnx        # Solution file
├── Tempest.Core/         # Domain logic: configuration, hosting, logging,
│                         # project management and repositories
└── Tempest.App/          # Console application entry point

tests/
└── Tempest.Core.Tests/   # xUnit test project for Tempest.Core
```

`Tempest.Core` defines the platform's services (bootstrap, configuration,
hosting, logging) and project management (`ProjectService`,
`ProjectNumberGenerator`, JSON-backed `IProjectRepository`). `Tempest.App`
is a thin console front end over `Tempest.Core`.

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
