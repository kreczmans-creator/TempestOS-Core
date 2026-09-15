# Third-Party Notices

TempestOS is built with the following third-party software. This file lists
every direct dependency this solution's own projects reference — package,
version pinned in the relevant `.csproj`, and licence — plus the embedded
font assets shipped inside `Tempest.Desktop`.

Added `WP 21.4A` (2026-09-15), per the Product Owner's rule (decision §5,
2026-09-15): a renderer's licence must be stated explicitly. Extended, not
created from nothing — every package below predates this Work Package
except `Svg.Skia`; the file itself did not exist until this Work Package
needed somewhere to state that one package's licence and found none.

## NuGet packages

| Package | Version | Licence | Used by |
|---|---|---|---|
| `Avalonia` | 11.3.20 | MIT | `Tempest.Desktop` — the desktop UI framework (`WP 10.0B`, `ADR-0094`). |
| `Avalonia.Desktop` | 11.3.20 | MIT | `Tempest.Desktop` — the desktop windowing backend. |
| `Avalonia.Fonts.Inter` | 11.3.20 | MIT | `Tempest.Desktop` — the design system's own prose typeface (`WP 14.1A`). |
| `Avalonia.Themes.Fluent` | 11.3.20 | MIT | `Tempest.Desktop` — the base theme this platform's own styling extends. |
| `Microsoft.Data.Sqlite` | 10.0.11 | MIT | `Tempest.Core` — the transactional persistence store. |
| `Microsoft.Extensions.Configuration` | 10.0.11 | MIT | `Tempest.Core`/`Tempest.Desktop` — configuration binding (`ADR-0146`). |
| `Microsoft.Extensions.Configuration.CommandLine` | 10.0.11 | MIT | Configuration from command-line arguments. |
| `Microsoft.Extensions.Configuration.EnvironmentVariables` | 10.0.11 | MIT | Configuration from environment variables. |
| `Microsoft.Extensions.Configuration.Json` | 10.0.11 | MIT | Configuration from `appsettings.json`. |
| `Microsoft.Extensions.Logging.Abstractions` | 10.0.11 | MIT | Logging interfaces this platform's own `ILogger` builds on. |
| `PDFtoImage` | 4.1.1 | MIT | `Tempest.Desktop` — the PDF rasteriser behind `PdfDocumentPageSource` (`TD-80`), built on PDFium and SkiaSharp. Pinned deliberately below latest — see the package reference's own remarks in `Tempest.Desktop.csproj`. |
| `Svg.Skia` | 2.0.0.8 | MIT | `Tempest.Desktop` — the SVG rasteriser behind `SvgDocumentPageSource` (`TD-99`, `WP 21.4A`). Pinned to the last release on the 2.x line, the only one whose own `SkiaSharp` dependency floor (2.88.9) matches the version this solution already resolves through `PDFtoImage` — see the package reference's own remarks in `Tempest.Desktop.csproj`. |
| `System.Security.Cryptography.ProtectedData` | 10.0.11 | MIT | Local credential protection. |
| `Tmds.DBus.Protocol` | 0.21.3 | MIT | `Tempest.Desktop` — Linux clipboard/IME integration, pulled in transitively by `Avalonia.Desktop`'s own `Avalonia.FreeDesktop`; pinned explicitly for a security floor (`TD-116`, `WP 16.5B`) — see the package reference's own remarks in `Tempest.Desktop.csproj`. |

Every direct package this solution references resolves to the MIT licence.
Transitive dependencies (PDFium, SkiaSharp, and `Svg.Skia`'s own `Svg.Model`/
`Svg.Custom`/`Svg.Animation`/`Svg.SceneGraph` split packages among them) are
not restated here individually; none introduces a licence obligation beyond
what its own direct parent package already carries, per each publisher's own
NuGet listing.

## Embedded font assets

Not NuGet packages — font files embedded directly under
`src/Tempest.Desktop/Assets/Fonts/`, each under the SIL Open Font License
1.1, with the licence text shipped alongside the font itself (recovered
during the Companion brand alignment, `WP 14.1A`):

| Font | Licence | File |
|---|---|---|
| Chakra Petch | OFL-1.1 | `Assets/Fonts/OFL-ChakraPetch.txt` |
| Space Mono | OFL-1.1 | `Assets/Fonts/OFL-SpaceMono.txt` |

Inter, the design system's third typeface, ships through the `Avalonia.Fonts.Inter`
package above rather than as an embedded asset.
