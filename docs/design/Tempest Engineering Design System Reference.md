# Tempest Engineering Design System — Repository Reference

**Provenance.** The authoritative Tempest Engineering Design System was
supplied by the Product Owner at `WP 14.1A` (2026-08-28) as a complete
pack (tokens, guidelines, brand assets, fonts, web component kit). This
document is the repository's condensed reference to the rules TempestOS
surfaces implement; the pack itself is the source of truth. The assets
the products ship are copied verbatim into each head's `Assets/`
(`src/Tempest.Desktop/Assets/`, and `src/Tempest.Companion/Assets/` on
the Companion branch: `Brand/` — the supplied lockup PNGs and app icons;
`Fonts/` — the supplied Chakra Petch, Space Mono faces under SIL OFL
1.1). The mark and logotype vector coordinates are transcribed verbatim
from the pack's derived SVGs into each head's
`Branding/TempestMarkGeometry.cs` — the pack's "never rebuild the logo
mark in code" rule is honoured by carrying the supplied geometry
unchanged, never redrawing it.

## Identity in one paragraph

Tempest Engineering builds control-system software; its product surface
is **Tempest OS**. The identity reads like instrumentation: near-black
navy ground, a faint blueprint grid, one cyan accent, squared corners,
and machine data always in monospace. Dark ("instrument") is the home
ground; the paper (light) theme exists for documents and daylight
reading. Calm, factual, no ornament — "a control room at 03:00."

## Colour tokens (verbatim)

| Token | Value | Role |
|---|---|---|
| navy-900 | `#070915` | Sunken surfaces — chrome bars, rails, inputs |
| navy-800 | `#0b0e1e` | The page ground |
| navy-700 | `#111527` | Cards/panels |
| navy-600 | `#181d33` | Raised surfaces |
| paper-050 | `#f5f6fa` | Headings on dark; the paper page |
| slate-400/500/600/700 | `#a2a5af` `#82848e` `#4b5160` `#31343f` | Muted/faint/body text tiers |
| ink-900 | `#16181d` | Headings on paper; text on cyan fills |
| indigo-600 | `#1c2d97` | Brand indigo — the paper theme's accent |
| cyan-500/400/600 | `#40a2ce` `#68bde2` `#2b7fa5` | THE interactive accent on dark; hover lighter, press darker |
| violet-500 | `#6c29d9` | Strictly secondary — badges, category rules; never the primary CTA |
| green/amber/red | `#12b981` `#f5a524` `#e5484d` | Machine state only, never decoration |

Hairlines: 8%/14% paper alpha on dark; 8%/14% ink alpha on paper.
Selection: 12% cyan fill plus a 2px cyan rule.

## Type

Chakra Petch — structure: headings, UPPERCASE labels (10–12px, `.14em`–
`.28em` tracking, two words where possible), numeric readouts (28–48px).
Inter — running prose (14–18px), never headings. Space Mono — machine
data: identifiers (lowercase kebab), units beside values, timestamps
(UTC with a trailing `Z`), log levels (`INFO WARN ERR OK`). Never prose
in Chakra Petch; never a heading in Inter.

## Shape, texture, motion

Squared corners: 2px badges, 3px buttons/inputs, 5px cards/panels (8px
max); only radios and switch tracks are round. Cards: flat fill, one
hairline, no shadow, optional 2px status rule on the TOP edge (cyan
default, amber/red state, violet category); 2px LEFT rules mark
selection in lists and rails. One texture: the 64px blueprint grid at
5.5% cyan, on page grounds only, never behind body text. Motion is
mechanical: `cubic-bezier(.2,0,.2,1)`, 80/120/200/320ms, no bounce or
scale; live values cross-fade.

## Interaction states

Hover: 5% paper wash, filled buttons step to the LIGHTER cyan. Press:
the DARKER cyan or a 9% wash; no shrink or translate. Focus: 2px cyan
ring. Disabled: 40% opacity. Filled accent buttons carry ink text.

## Content rules

Sentence case for prose; UPPERCASE wide-tracked labels for UI chrome;
figures carry units in mono; no hype adjectives; **no emoji, ever** —
status is a coloured dot, a badge, or a log level; Unicode is limited to
`●` `→` `·`; no exclamation marks; no hand-drawn icon glyphs (the pack
ships no glyph set — its web kit substitutes Lucide, a substitution
TempestOS's native surfaces do not adopt: they use text labels and the
three permitted symbols instead).

## Logo

The mark: eighteen 24-unit round-capped strokes in three layers —
indigo (outer), cyan (middle), violet (inner) — around a paper hexagonal
core. Lockups: TEMPEST logotype in paper (dark grounds) or ink (paper
grounds), OS always cyan. Use supplied artwork only.

## Where this is implemented

**Desktop (`Tempest.Desktop`).** `Theming/` (`BrandPalette` — the tokens
and semantic keys; `TempestTheme` — the Fluent palette and resource
overrides that recolour every stock control; `ChromeStyles` — the flat,
subtle, primary and danger button treatments; `DesignTokens` — the type
roles, tracking, squared radii and shell geometry), `Branding/`
(`TempestMarkGeometry`, `TempestLogoControl`, `TempestLockupControl` —
the same verbatim artwork transcription the Companion carries),
`Icons/IconGeometry` (the monochrome vector chrome set that replaced the
shell's colour emoji), and `Assets/` (`Brand/` — the lockup PNGs, app
icons and the `.ico` the executable carries; `Fonts/` — Chakra Petch and
Space Mono under OFL; Inter via `Avalonia.Fonts.Inter`). The shell's
header, rail, status bar, ribbon, docking chrome, Cockpit and every
empty state paint from these. This closes `FCR-0092` for the shell; the
Object Editor and Digital Thread graph bodies still inherit only the
control theme and are the remaining realignment surface.

**Companion (`Tempest.Companion`, on its own branch).**
`Theming/` (`BrandPalette`, `CompanionTokens`, `CompanionStatusColors`),
`Branding/` (`TempestMarkGeometry`, `TempestLogoControl`,
`TempestLockupControl`), `Views/` (cards, state views, buttons, shell).
Conformance is test-guarded by
`tests/Tempest.Companion.Tests/BrandConformanceTests.cs`.

## Related Documents

`docs/architecture/TempestOS Companion Architecture.md` §6; `ADR-0113`
(Status correction note, `WP 14.1A`);
`docs/academy/03 Work Packages/WP14.1A-brand-alignment-to-the-tempest-engineering-design-system.md`.
