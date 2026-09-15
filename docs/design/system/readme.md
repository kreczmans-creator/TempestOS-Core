# Tempest Engineering — Design System

Tempest Engineering builds control-system software for grid-scale wind: dispatch control, telemetry, and safety/compliance tooling. Its product surface is **Tempest OS**, the operations console named by the product lockups in the brand pack. The identity reads like instrumentation — near-black navy ground, a blueprint grid, one cyan accent, squared corners cut from the logotype, and machine data always set in monospace.

## Sources given

| Source | What it contained |
|---|---|
| `uploads/Tempest Engineering Brand Pack/` (from `Tempest Engineering Brand Pack.zip`) | Logo lockups (horizontal + vertical, navy and paper), app icons (navy, white), round social avatar, Tempest OS product lockups (dark, light), 3840×2160 desktop wallpaper |
| `uploads/*.ttf` | Chakra Petch (10 styles), Inter (variable), Space Mono (4 styles) |

No codebase, repository, Figma file, slide deck, product screenshot, or written copy was supplied. **Everything in this system that is not a colour, a font, or a supplied asset is a construction from those foundations, not a recreation of an existing product.** The two UI kits carry provenance notes saying so; their copy is placeholder written in the brand's voice. No slide templates were created because no deck was provided.

The logo mark was **not** redrawn — every appearance of it in this system is one of the supplied PNGs from `assets/logo/`.

## Index

| Path | What |
|---|---|
| `styles.css` | Global entry point — `@import` list only. Consumers link this. |
| `tokens/` | `fonts.css` `colors.css` `typography.css` `spacing.css` `radius.css` `elevation.css` `motion.css` `semantic.css` `base.css` |
| `components/core/` | `Icon` `Button` `IconButton` `Badge` `Tag` `Card` |
| `components/forms/` | `Input` `Textarea` `Select` `Checkbox` `Radio` `Switch` |
| `components/navigation/` | `Tabs` |
| `components/feedback/` | `Dialog` `Toast` `Tooltip` |
| `ui_kits/tempest-os/` | Operations console: launch splash, login, fleet overview, telemetry, alert queue |
| `ui_kits/tempest-web/` | Marketing site: header, hero, capabilities, numbers band, footer |
| `guidelines/` | Foundation specimen cards (Colors, Type, Spacing, Brand) |
| `assets/logo/`, `assets/imagery/`, `assets/fonts/` | Supplied brand assets and webfonts |
| `SKILL.md` | Agent-skill wrapper for use outside this project |

**Intentional additions.** No source defined a component inventory, so `components/` is a standard primitive set sized to what the two kits need. `Icon` is a wrapper around the Lucide glyph set (see ICONOGRAPHY) — the brand pack shipped no glyphs of its own.

---

## CONTENT FUNDAMENTALS

The pack contained no written copy, so this section is a **prescription** derived from the identity (engineering-first, instrument-like, no ornament) rather than an observation of existing copy. Follow it; correct it when real copy exists.

**Voice.** Plain engineering register. State the fact, then the consequence. No hype adjectives ("revolutionary", "seamless", "cutting-edge"), no metaphor, no rhetorical questions, no "this, not that" constructions.

- Yes: "Shear reached 18.4 m/s against a 16.0 m/s limit. Units 04-03 and 04-04 feathered automatically."
- Yes: "One number that matters, and the path to change it."
- No: "Unleash the power of your fleet with next-generation intelligence."

**Person.** "We" for the company ("We build the dispatch, telemetry and safety software…"). Second person for instructions to the operator ("Assign to me", "Take offline"). Never first-person singular. Never "our platform empowers you to…".

**Numbers are the argument.** Every claim carries a figure and a unit: "Nine sites, 144 units, one control loop." Units are always spelled in the SI form and set in Space Mono when they sit next to a value (`12.4 MW`, `18.4 m/s`, `50.02 Hz`, `41ms`). Timestamps are UTC with a trailing Z (`14:02:11Z`). Identifiers are lowercase kebab in mono (`north-ridge`, `array-04`, `unit 09-14`, `ALR-4192`).

**Casing.**
- Headlines and prose: sentence case. "Control systems for grid-scale wind."
- UI labels, buttons, tabs, eyebrows, table headers, badges: UPPERCASE with wide tracking, two words maximum. "TAKE OFFLINE", "SYSTEM STATUS", "OPEN ALERTS".
- Log levels: `INFO` `WARN` `ERR` `OK`, four characters or fewer.

**Length.** Headline ≤ 8 words. Sub-headline one sentence, ≤ 30 words. Card body 1–2 sentences. Alert detail 2–3 sentences, always ending in what happened as a result. Empty states say what is absent and what to do about it, in one line.

**Emoji: never.** Not in product UI, not in marketing, not in release notes. Status is carried by a coloured dot, a Badge, or a log level — never a 🟢. Unicode symbols are limited to `●` for a status dot, `→` inside a log line, and `·` as a separator in mono metadata.

**Punctuation.** No exclamation marks. Em dashes sparingly, in prose only — never in UI strings. Serial commas. Ranges use an en dash (`08:00–14:00Z`). Don't end UI labels or table cells with periods; do end prose sentences.

**Vibe.** A control room at 03:00: calm, legible, factual, slightly cold. The system should feel like it is telling you the truth about machinery.

---

## VISUAL FOUNDATIONS

**Ground.** Dark first. `--navy-800 #0b0e1e` is the page; `--navy-700` is a panel; `--navy-900` is sunken (inputs, rails, log surfaces). The paper theme (`.t-light`, `#f5f6fa`) exists for documents, decks, print, and the site footer — never mix the two on one screen except as a deliberate full-width band (see the marketing footer).

**Colour.** Three brand hues, all sampled from the mark: indigo `#1c2d97`, cyan `#40a2ce`, violet `#6c29d9`. Cyan is the interactive accent on dark and carries *one* meaning per screen: this is where you act, or this is the live value. Indigo is the accent in the paper theme. Violet is a secondary/brand tint only (badges, second data series, category rules) — never the primary CTA. Status hues are green/amber/red and are reserved for machine state; never decorative. Maximum two background colours per surface. No gradients as decoration; the only gradient-like fills in the system are the 5.5%-opacity blueprint grid and a faint scanline overlay.

**Type.** Chakra Petch for anything structural — headings, UI labels, tab titles, numeric readouts. Inter for all running prose. Space Mono for machine data: IDs, units, timestamps, log lines, tags. Display sizes run 18 → 84px with tight tracking (`-.015em` to `-.03em`); labels run 10 → 12px with wide tracking (`.14em` to `.28em`) and uppercase. Prose 14–18px at 1.5–1.65 line-height, measure capped near 60ch. Never set prose in Chakra Petch; never set a heading in Inter.

**Spacing & layout.** 4px grid; 2px exists only for hairline insets. Control heights are exactly 28 / 36 / 44px. Page padding 32px, gutters 24px, content max 1240px, marketing section rhythm 96px. Panels are laid out as flex/grid with gap, never margin chains. Fixed elements: the marketing header is sticky with a blurred translucent fill; in Tempest OS the 60px sidebar rail, 56px top bar and 28px status strip are fixed and only the main column scrolls. Toasts stack bottom-right above the status strip.

**Corners.** Squared system: 2px (badges, checkboxes), 3px (buttons, inputs, tags), 5px (cards, panels, dialogs), 8px max. Only two round things exist: radio buttons and the Switch track. The **notch** — a 10px cut on the top-right corner, borrowed from the E/S/P of the logotype — is the one expressive shape, allowed on hero CTAs and hero panels, at most twice per screen.

**Borders and elevation.** On dark, elevation is a hairline plus optional glow, never a drop shadow: `--paper-a08` for subtle, `--paper-a14` for default, `--navy-400` for strong, cyan for accent/focus. Modal panels are the single exception (`--shadow-panel`, a 50%-black 64px blur) because they float over content. Cards carry a flat fill, one hairline, no shadow, and optionally a 2px status rule on the top edge (cyan default, amber/red for state, violet for category). Left rules (2px) mark selection in lists, rails and toasts. In the paper theme, soft shadows return (`--shadow-sm/md/lg`) and hairlines drop to `--ink-a08`.

**Transparency and blur.** Used in exactly three places: the sticky marketing header (86% navy + 10px blur), the dialog backdrop (72% ink + 3px blur), and the login panel (82% navy + 8px blur over the wallpaper). Nowhere else — panels are opaque so telemetry stays legible.

**Animation.** Mechanical. `cubic-bezier(.2,0,.2,1)` for everything; 80ms for press, 120ms for hover/focus/toggle, 200ms for panels and tabs, 320ms for route change. No bounce, no overshoot, no spring, no scale-in. Values that update live cross-fade rather than count up. Charts do not animate on load.

**Hover / press / focus / disabled.**
- Hover: a 5% paper wash (`--bg-hover`) plus a colour step on text or border; filled buttons move to the *lighter* cyan (`--cyan-400`). Never a shadow lift, never a transform.
- Press: the *darker* cyan (`--cyan-600`) or a 9% wash. No shrink, no translate.
- Focus: 2px page-coloured gap then a 2px cyan ring (`--glow-focus`); inputs additionally take a cyan border plus a 3px 12%-cyan halo.
- Disabled: 40% opacity, `not-allowed`, no colour change.
- Selected in lists: 12%-cyan fill plus a 2px cyan left rule.

**Backgrounds and imagery.** One texture: the 64px blueprint grid at 5.5% cyan (`--bg-grid`), taken from the desktop wallpaper. Use it on marketing heroes, the OS main column and numeric bands; never behind body text. Imagery is cool, near-monochrome, near-black, centred, with generous void — the wallpaper is the reference. No photography was supplied; if photos are added they should be cool-toned, low-key, hardware-focused, and never warm or lifestyle-flavoured. No illustrations exist in the brand and none should be invented.

**Data display.** Numeric readouts are Chakra Petch 28–48px with the unit trailing in mono at 12px. Tables use uppercase micro headers, hairline row separators, right-aligned numbers in mono, no zebra striping, no vertical rules. Charts are bar/band forms in cyan at varying opacity with the latest sample in paper white; amber when the channel is in warning. Gridlines at `--paper-a08`, horizontal only.

---

## ICONOGRAPHY

The brand pack contains **no icon set, icon font, or sprite** — only logo artwork. Iconography is therefore a flagged substitution: **Lucide** (`lucide-static` via unpkg CDN), chosen because its round caps and round joins match the terminals of the logo mark's six strokes, and its ~1.75px stroke at 16px sits correctly against Chakra Petch labels.

- Access it only through `components/core/Icon.jsx`: `<Icon name="activity" size={16} />`. It masks the CDN SVG so the glyph inherits `currentColor` — that is why icons pick up hover and status colours for free.
- Sizes: 14 (table rows, tags, small buttons), 16 (buttons, menu items, inline), 18–20 (rails, toolbars), 22–24 (feature marks, empty states). Never scale a glyph above 24 for decoration.
- Colour: inherit by default; cyan only when the icon is the active/interactive signal; status hues only for status.
- **Never hand-draw an SVG icon** for this brand, and never rebuild the logo mark in code — use `assets/logo/*.png`.
- **Emoji are never used.** Unicode is limited to `●` `→` `·` as described in CONTENT FUNDAMENTALS.
- Vendor logos, if ever needed, come from the vendor and sit at 60% opacity in the footer.

Open question for the brand owner: if Tempest has a real glyph set, drop it into `assets/icons/` and `Icon.jsx` should be repointed at it.
