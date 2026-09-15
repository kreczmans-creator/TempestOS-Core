# Third-party notices

TempestOS embeds the following third-party assets. Each is used under the
licence named below; the full licence text is vendored alongside the asset
it covers.

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
