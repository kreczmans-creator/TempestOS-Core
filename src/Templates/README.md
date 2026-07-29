# TempestOS Templates

`dotnet new` template **sources** — content the .NET templating engine
copies and renames when a contributor scaffolds a new module, never
built as part of `TempestOS.slnx` directly (see Engineering Governance
§11). Introduced by `WP 5.3`.

## Available templates

| Directory | Short name | Produces |
|---|---|---|
| `Tempest.Templates.Module/` | `tempest-module` | A single-file TempestOS module, shaped exactly as `docs/academy/02 Runtime Architecture/03-building-a-module.md` describes. |

## Using a template

Install it once, from the repository root:

```
dotnet new install ./src/Templates/Tempest.Templates.Module
```

Then generate a new module (also from the repository root, so the
generated project's own relative `ProjectReference` to `Tempest.Core`
resolves correctly):

```
dotnet new tempest-module -n MyModule --ModuleId my.module.id -o src/Samples/MyModule
```

`-n` sets both the class name and the generated project's own folder
name; `--ModuleId` sets the value passed to `[ModuleMetadataAttribute]`
and the base constructor (defaults to a placeholder if omitted — see the
template's own `template.json`). `--ModuleDisplayName`/`--ModuleVersion`
are also available, both optional.

To remove the template again:

```
dotnet new uninstall ./src/Templates/Tempest.Templates.Module
```

## Why a local-folder template, not a NuGet package

Considered and rejected — see `docs/architecture/Rejected Designs.md`,
`RD-0045`. In short: this repository has no NuGet publishing pipeline yet,
and building one solely to distribute one small template would be
disproportionate to this template's own scope.
