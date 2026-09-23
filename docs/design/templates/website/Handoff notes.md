# Tempest Engineering website — handoff notes

## Canonical design
The DARK theme is the main site. Light (-Light) pages are an alternative treatment; keep them only if you want a print/paper look somewhere.

- Desktop pages: Home / About / Capabilities / Work / Contact / Privacy (.dc.html)
- Mobile pages (-Mobile): same five + Privacy-Mobile — these show the layout below ~768px
- Offline export: `export-resp/` holds single RESPONSIVE sources (one file per page, desktop + mobile via media queries at 1180/1080/960/768px, fluid headings via clamp). These compile to `site-export/*.html`; re-run the bundler on them after edits.

## Rebuilding in Squarespace
Work page-by-page, top-to-bottom. Every section maps to a standard Squarespace block:

1. Copy: select text in the preview and copy it — all copy is final, including the Privacy Notice.
2. Fonts: Chakra Petch (headings, UPPERCASE labels), Inter (body), Space Mono (email, credentials, numbers). All three are free on Google Fonts and available in Squarespace's font picker.
3. Colors: page background #0b0e1e (near-black navy), panel #151a33, body text light grey, accent cyan #40a2ce (links, buttons, "01/02/03" step numbers). Buttons: cyan fill, dark text, square corners.
4. Layout rules: max content width 1240px, big section spacing (~96px), thin 1px dividers between sections, square corners everywhere (2–5px radius max), no drop shadows.
5. Footer: light grey band with the company line (company no. 17349874) and the Privacy Notice link. The registered office address is deliberately NOT published on the site.
6. Images: About headshot + four Work images still to be produced (renders/drawings from Creo).
7. Contact: plain mailto link (StevenK@Tempest-Engineering.com) and tel link (+44 7761 073202) — deliberately no contact form.

## Logo files
- assets/logo/derived/logo-horizontal-transparent-light.svg — for dark backgrounds (header)
- assets/logo/derived/logo-horizontal-transparent-ink.svg — for light backgrounds
