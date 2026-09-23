# Tempest OS — operations console (UI kit)

Tempest OS is the operations console named by `tempest-os-logo-dark.png` / `-light.png` in the brand pack.

**Provenance note.** The brand pack contained no product screenshots, code, or Figma file, so these screens are *constructed* from the Tempest visual foundations (navy ground, blueprint grid, Chakra Petch labels, cyan single-accent rule, squared 3–5px corners, Space Mono for machine data) rather than recreated from a source. Treat layout and content as a plausible reference implementation, not ground truth — replace with real views when a source is available.

## Screens
| File | Screen |
|---|---|
| `LoginScreen.jsx` | Sign-in over the brand wallpaper |
| `OverviewScreen.jsx` | Fleet overview: readouts, site table, event log |
| `TelemetryScreen.jsx` | Single-array telemetry with channel list and chart band |
| `AlertsScreen.jsx` | Alert queue with severity filters and detail drawer |
| `SplashScreen.jsx` | Launch splash: royal-blue ripple, layered fade construction of the mark, lift into the vertical Tempest OS lockup |
| `AppShell.jsx` | Sidebar rail + top bar + toast layer |

## Launch splash
Motion follows the approved reference (`uploads/TempestOS_launch_8_second_ROYAL_BLUE_OSCILLATING_RIPPLE.gif`): void navy throughout → system-charge pulse (energy gathers to the centre point, a white core flares, then indigo/cyan shockwaves and a pressure glow travel out and exit the frame) → hub fades in → indigo, then cyan, then violet stroke groups fade in at full canonical geometry (opacity only — no growth, no translation, no rotation, no lock pulse) → the single icon lifts → the TEMPEST OS product wordmark fades in below (vertical lockup, no “Engineering”). The mark is never redrawn: layers are vector splits of the supplied mark SVG (`assets/logo/derived/mark-{hub,indigo,cyan,violet}.svg`) — resolution-independent, remapped to the corrected brand tokens.

`tempest-boot.js` (`window.TempestBoot`) is the startup lifecycle: RUNTIME HOST → DESIGN SYSTEM → TYPE SYSTEM → BRAND ASSETS → MODULE SYSTEM → UI SHELL. Each phase checks a real condition (script globals, `document.fonts`, image decode) — no fake progress, no artificial 8-second sleep: startup runs concurrently with the animation, the splash exits only when both the sequence and the runtime are ready, and a failed phase surfaces STARTUP HALTED with Retry. The status line under the lockup is that real state; `SplashScreen.jsx` is presentation only and subscribes to it. Respects `prefers-reduced-motion` (no ripple or lift — fades at final positions). `splash.html` is a standalone card with a replay control.

## Interactions in `index.html`
Splash (real boot lifecycle) → sign in (any credentials) → console. Sidebar switches screens, tabs switch panels, alert rows open the detail drawer, "Take offline" opens a confirm Dialog and fires a Toast.

Components come from the compiled design system via `window.TempestEngineeringDesignSystem_1d4355`.
