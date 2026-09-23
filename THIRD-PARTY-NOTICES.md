# Third-Party Notices

TempestOS is built on the open-source packages below. This file lists
every package **directly** referenced by a `PackageReference` in any
`.csproj` under `src/` or `tests/` (`WP 21.5E` / `WP RC.0C`'s own
"a renderer's licence must be stated explicitly" rule, Product Owner
decision 2026-09-15 §5, generalised here to every direct dependency), the
exact version this solution pins, and its licence — read from each
package's own NuGet package metadata (`<license type="expression">`) in
this repository's local package cache, not from memory or a web search.

This file does not enumerate transitive dependencies exhaustively; the one
exception is noted under `PDFtoImage` below, because its own native
rendering dependency is directly relevant to what this platform ships.

| Package | Version | Licence |
|---|---|---|
| [Avalonia](https://www.nuget.org/packages/Avalonia) | 11.3.20 | MIT |
| [Avalonia.Desktop](https://www.nuget.org/packages/Avalonia.Desktop) | 11.3.20 | MIT |
| [Avalonia.Fonts.Inter](https://www.nuget.org/packages/Avalonia.Fonts.Inter) | 11.3.20 | MIT |
| [Avalonia.Headless.XUnit](https://www.nuget.org/packages/Avalonia.Headless.XUnit) | 11.3.20 | MIT |
| [Avalonia.Skia](https://www.nuget.org/packages/Avalonia.Skia) | 11.3.20 | MIT |
| [Avalonia.Themes.Fluent](https://www.nuget.org/packages/Avalonia.Themes.Fluent) | 11.3.20 | MIT |
| [coverlet.collector](https://www.nuget.org/packages/coverlet.collector) | 6.0.4 | MIT |
| [CsCheck](https://www.nuget.org/packages/CsCheck) | 4.8.0 | Apache-2.0 |
| [Microsoft.Data.Sqlite](https://www.nuget.org/packages/Microsoft.Data.Sqlite) | 10.0.11 | MIT |
| [Microsoft.Extensions.Configuration](https://www.nuget.org/packages/Microsoft.Extensions.Configuration) | 10.0.11 | MIT |
| [Microsoft.Extensions.Configuration.CommandLine](https://www.nuget.org/packages/Microsoft.Extensions.Configuration.CommandLine) | 10.0.11 | MIT |
| [Microsoft.Extensions.Configuration.EnvironmentVariables](https://www.nuget.org/packages/Microsoft.Extensions.Configuration.EnvironmentVariables) | 10.0.11 | MIT |
| [Microsoft.Extensions.Configuration.Json](https://www.nuget.org/packages/Microsoft.Extensions.Configuration.Json) | 10.0.11 | MIT |
| [Microsoft.Extensions.Logging.Abstractions](https://www.nuget.org/packages/Microsoft.Extensions.Logging.Abstractions) | 10.0.11 | MIT |
| [Microsoft.NET.Test.Sdk](https://www.nuget.org/packages/Microsoft.NET.Test.Sdk) | 17.14.1 | MIT |
| [PDFtoImage](https://www.nuget.org/packages/PDFtoImage) | 4.1.1 | MIT |
| [Svg.Skia](https://www.nuget.org/packages/Svg.Skia) | 2.0.0.8 | MIT |
| [System.Security.Cryptography.ProtectedData](https://www.nuget.org/packages/System.Security.Cryptography.ProtectedData) | 10.0.11 | MIT |
| [Tmds.DBus.Protocol](https://www.nuget.org/packages/Tmds.DBus.Protocol) | 0.21.3 | MIT |
| [Velopack](https://www.nuget.org/packages/Velopack) | 1.2.0 | MIT |
| [xunit](https://www.nuget.org/packages/xunit) | 2.9.3 | Apache-2.0 |
| [xunit.runner.visualstudio](https://www.nuget.org/packages/xunit.runner.visualstudio) | 3.1.4 | Apache-2.0 |

## Notes

- **Svg.Skia** (`WP 21.4A`) renders SVG attachments in the viewer through SkiaSharp; pinned to the last 2.x release because its SkiaSharp floor matches the version this solution resolves. Untrusted SVG is sanitised before parsing (external references blanked, DOCTYPE and script stripped).
- **Velopack** (`WP 21.5A`, `WP RC.0A`) is referenced by `Tempest.Desktop` only: the Windows installer, the in-place update check (off by default, HTTPS feed) and installed-versus-development-run detection. `Tempest.Harness` carries no installer.
- **PDFtoImage** (used by `Tempest.Desktop`'s PDF page source) pulls in
  `SkiaSharp` (MIT) and, for its native rendering engine,
  `bblanchon.PDFium.*` — a packaging of Google's **PDFium** (BSD-3-Clause)
  — as transitive dependencies. Stated here because the rendered-page
  surface (`WP 20.2B`, `WP 21.4A`) is exactly the kind of "renderer whose
  licence must be stated explicitly" the Product Owner's rule names, even
  though PDFium itself is transitive rather than a direct
  `PackageReference` of this solution.
- Every package above resolves through `https://api.nuget.org/v3/index.json`,
  the one NuGet source this solution's `NuGet.Config` (or its absence,
  falling back to the default feed) uses — no private or unverified feed
  is configured.
- This list reflects `src/TempestOS.slnx`'s direct `PackageReference`s as
  of `WP 21.5E` (2026-09-15). A future Work Package that adds a package
  should add a row here in the same commit — `docs/security/Security
  Posture.md`'s own dependency-scanning section names this file as part of
  the same discipline the CI dependency scan enforces mechanically for
  known vulnerabilities; licence drift is not itself machine-checked.

## Licence texts

Every licence above is a standard, unmodified OSI-approved licence
(MIT, Apache-2.0). Their full texts are published at:

- MIT: <https://opensource.org/license/mit>
- Apache-2.0: <https://www.apache.org/licenses/LICENSE-2.0>
- BSD-3-Clause (PDFium, transitive): <https://opensource.org/license/bsd-3-clause>

No package above ships its licence text embedded in this repository; each
is reproduced by reference to its own canonical, unmodified text rather
than copied here, to avoid this file drifting out of sync with the
original as licences are (rarely, but occasionally) revised.

## Fonts

`WP 21.2A` embedded the Tempest Engineering Design System's three type
families as resources of `Tempest.Desktop` (`src/Tempest.Desktop/Documents/Assets/Fonts/`),
loaded directly through `SKTypeface.FromStream` for document rendering
(`Tempest.Desktop.Documents.DocumentFonts`) — the same three families
`src/Tempest.Desktop/Assets/Fonts/` already carries for the application's
own UI chrome (`WP 14.1A`).

- **Chakra Petch** — Copyright 2018 The Chakra Petch Project Authors
  (<https://github.com/m4rc1e/Chakra-Petch.git>). Licensed under the SIL
  Open Font License, Version 1.1. Full text:
  `src/Tempest.Desktop/Assets/Fonts/OFL-ChakraPetch.txt`.
- **Inter** — Copyright 2020 The Inter Project Authors
  (<https://github.com/rsms/inter>). Licensed under the SIL Open Font
  License, Version 1.1 (<https://openfontlicense.org>). Also shipped,
  under the identical licence, via the `Avalonia.Fonts.Inter` NuGet
  package this project already references for the application's own UI
  text.
- **Space Mono** — Copyright 2016 The Space Mono Project Authors
  (<https://github.com/googlefonts/spacemono>). Licensed under the SIL
  Open Font License, Version 1.1. Full text:
  `src/Tempest.Desktop/Assets/Fonts/OFL-SpaceMono.txt`.

The SIL Open Font License permits embedding, use, modification and
redistribution of the fonts themselves, bundled with software, free of
charge; it does not permit selling the fonts on their own. See either
vendored `OFL-*.txt` file, or <https://openfontlicense.org>, for the full
licence text.

## Brand assets

The horizontal navy Tempest wordmark lockup
(`src/Tempest.Desktop/Documents/Assets/Logo/tempest-logo-horizontal-navy.png`,
embedded `WP 21.2A`) is Tempest Design Engineering Ltd's own asset — not
third-party — carried here only because this file collects every embedded
asset's provenance in one place.
