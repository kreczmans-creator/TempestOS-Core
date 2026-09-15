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
| [System.Security.Cryptography.ProtectedData](https://www.nuget.org/packages/System.Security.Cryptography.ProtectedData) | 10.0.11 | MIT |
| [Tmds.DBus.Protocol](https://www.nuget.org/packages/Tmds.DBus.Protocol) | 0.21.3 | MIT |
| [Velopack](https://www.nuget.org/packages/Velopack) | 1.2.0 | MIT |
| [xunit](https://www.nuget.org/packages/xunit) | 2.9.3 | Apache-2.0 |
| [xunit.runner.visualstudio](https://www.nuget.org/packages/xunit.runner.visualstudio) | 3.1.4 | Apache-2.0 |

## Notes

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
