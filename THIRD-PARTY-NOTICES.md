# Third-Party Notices

TempestOS is built on the following third-party packages. This list is
generated from the direct `PackageReference` entries in `src/**/*.csproj`
plus each package's own runtime-native dependencies, cross-checked against
the license metadata each package publishes in its own `.nuspec` (read
directly from the locally restored NuGet cache, not re-typed by hand).
Regenerate this file whenever a package reference is added, removed, or
has its version changed.

| Package | Version | License |
|---|---|---|
| Avalonia | 11.3.20 | MIT |
| Avalonia.Desktop | 11.3.20 | MIT |
| Avalonia.Fonts.Inter | 11.3.20 | MIT |
| Avalonia.Themes.Fluent | 11.3.20 | MIT |
| Microsoft.Data.Sqlite | 10.0.11 | MIT |
| Microsoft.Extensions.Configuration | 10.0.11 | MIT |
| Microsoft.Extensions.Configuration.CommandLine | 10.0.11 | MIT |
| Microsoft.Extensions.Configuration.EnvironmentVariables | 10.0.11 | MIT |
| Microsoft.Extensions.Configuration.Json | 10.0.11 | MIT |
| Microsoft.Extensions.Logging.Abstractions | 10.0.11 | MIT |
| PDFtoImage | 4.1.1 | MIT |
| System.Security.Cryptography.ProtectedData | 10.0.11 | MIT |
| Tmds.DBus.Protocol | 0.21.3 | MIT |

## Runtime-native dependencies (pulled in transitively)

`PDFtoImage` bundles the following native libraries; none is a direct
`PackageReference` in this repository, so each is listed separately here
rather than in the table above:

| Package | Version | License |
|---|---|---|
| SkiaSharp | 2.88.8 | MIT |
| SkiaSharp.NativeAssets.Win32 | 2.88.8 | MIT |
| SkiaSharp.NativeAssets.Linux.NoDependencies | 2.88.8 | MIT |
| SkiaSharp.NativeAssets.macOS | 2.88.8 | MIT |
| bblanchon.PDFium.Win32 | 130.0.6721 | Apache-2.0 |
| bblanchon.PDFium.Linux | 130.0.6721 | Apache-2.0 |
| bblanchon.PDFium.macOS | 130.0.6721 | Apache-2.0 |

`bblanchon.PDFium.*` packages Google's own PDFium project (the PDF engine
Chromium ships); `bblanchon` is the packager, not the upstream author —
see [PDFium's own project page](https://pdfium.googlesource.com/pdfium/)
for the upstream license and source.

## Notes

- Every package above uses a permissive licence (MIT or Apache-2.0); none
  imposes a copyleft/share-alike obligation on TempestOS's own source.
- This list covers direct dependencies and PDFtoImage's own declared
  native dependencies only — not the full transitive closure of every
  `Microsoft.Extensions.*`/`Microsoft.Data.Sqlite` package's own
  dependencies (themselves further MIT-licensed `Microsoft.Extensions.*`/
  `System.*` packages from the same first-party source). A complete,
  automated transitive scan (e.g. via a `dotnet-project-licenses`-style
  tool run in CI) is a reasonable follow-up if the dependency set grows
  materially, but is not required for the current, small, entirely
  first-party-and-MIT/Apache-2.0 dependency set this audit found.
