# Workspace Visual Polish & Engineering User Experience

## 1. Introduction

`WP 10.5A`'s own concept guide — how TempestOS built its first real
theme-reactive brush infrastructure, closed two long-disclosed
Technical Debt items as a natural consequence of a polish pass rather
than a dedicated fix, and learned two genuine lessons about Avalonia's
own resource-resolution and headless-test-application lifecycle along
the way.

## 2. Purpose

Explains why "no ADR required" was independently re-verified rather
than assumed, why `ThemeReactiveBrush` exists as its own small,
reusable primitive rather than eight repeated call sites, and documents
two real implementation-time defects — both found by a failing test,
both root-caused, neither papered over.

## 3. Background

`WP 10.5A`'s own controlling instruction named an enormous, almost
whole-application scope — every panel, every interaction state, every
theme, every named feedback mechanism. Every prior Work Package this
session established the same discipline for a scope this size:
build a real, cohesive foundation, apply it to the highest-value
surfaces first, and disclose directly what received lighter treatment,
rather than claim exhaustive coverage that was never actually built.
This guide follows that same discipline.

## 4. The Problem

Two real, pre-existing, disclosed Technical Debt items — `TD-39`
(`PanelHostControl`/`CommandPaletteOverlay`'s own fixed-brush overlay
backgrounds, wrong in Light theme) and `TD-40` (a dirty Object Editor
tab closing without confirmation, silently discarding unsaved edits) —
each already named its own precise revisit trigger. Separately, a
direct `grep` sweep found `Tempest.Desktop` had **zero** controls using
`DynamicResource`/theme-reactive resource binding anywhere, and
`IconRegistry`'s own Kind glyphs mixed full-colour emoji with
monochrome symbols inconsistently.

## 5. The Design

**`ApplicationPalette` + `ThemeReactiveBrush`.** Five explicit
`ResourceDictionary.ThemeDictionaries` brush keys, resolved via a
small, shared helper rather than the conventional `GetResourceObservable`/
`Bind` code-behind pattern — which, discovered directly by a failing
test, does not reliably re-push a value once a control that subscribed
while unattached is later attached to a real visual tree.
`ThemeReactiveBrush.Bind` instead calls `Application.Current.TryGetResource`
directly, on both `AttachedToVisualTree` and `ActualThemeVariantChanged`.

**Four new, small, reusable controls** — `ToastHost`, `BusyOverlay`,
`ConfirmationDialog`, `EmptyStateView` — each a real, working,
Desktop-local class, each wired to at least one genuine, real consumer,
never built speculatively with zero usage.

**Icon consistency, two tiers.** `IconRegistry`'s own Kind glyphs move
to a uniform, monochrome, text-default Unicode vocabulary (Geometric
Shapes/Mathematical Operators blocks) — real, working, but still
text-glyph-based. `IconGeometry` is a small, real, hand-authored vector
set for the new controls' own interactive chrome — a genuinely
different, higher-fidelity tier, deliberately not yet extended to every
Kind glyph.

**`TD-39`/`TD-40` closed as a direct consequence, not a side quest.**
Both fixes are the most natural application of this Work Package's own
core theme/dialog infrastructure to already-named, already-understood
gaps — closing them here, rather than in a dedicated future Work
Package, avoided building the exact same infrastructure twice.

## 6. Alternatives Considered

- **A global `:focus-visible` style override**, using the new
  `FocusRingBrush` token — considered, deliberately deferred.
  `FluentTheme` already provides real, working focus indication for
  every stock control; overriding it blind, with no way to visually
  verify the result in this environment, was judged a real risk of
  shipping something worse than the existing default.
- **A comprehensive, hand-authored vector icon library** replacing
  every `IconRegistry` glyph — considered, scoped down to four chrome
  icons plus a Unicode-based Kind-glyph refresh; a full vector library
  is real, disclosed future work (`FCR-0071`).
- **Wrapping every `LoadAsync` refresh in `BusyOverlay`** — considered,
  rejected for single-object refreshes (fast enough that a busy overlay
  would only flicker); applied only to the substantially slower
  whole-area switch.

## 7. Why This Solution Was Chosen

Every alternative either risked shipping an unverifiable regression
(the focus-ring override) or expanded scope well beyond what a single
Work Package can realistically deliver to a genuinely high standard (a
full vector icon library, wrapping every refresh in a busy indicator).
The chosen scope delivers a real, cohesive, tested foundation and
applies it honestly, rather than a thin, unverified layer spread across
everything named in the instruction.

## 8. Architectural Principles

- **A shared primitive earns its own abstraction after the second real
  failure, not the first guess** — `ThemeReactiveBrush` exists because
  the naive pattern failed empirically twice, not because it was
  designed defensively in advance.
- **Closing a named debt item is the natural, not the incidental,
  outcome of building the infrastructure that trigger names** — `TD-39`/
  `TD-40` were always going to be closed by exactly this kind of Work
  Package; this one simply arrived.
- **A built-but-unwired capability is disclosed as exactly that** —
  `IconGeometry` is real and tested, but this Work Package says plainly
  that no end-user-visible surface renders it yet, rather than implying
  broader adoption than actually shipped.

## 9. Benefits

Every future custom-drawn overlay/panel has a real, proven, three-call
pattern for theme-correct colours. Every future feedback need (a
success message, a destructive-action confirmation, an empty list) has
a real, tested control to reuse rather than a new one-off.

## 10. Trade-offs

- No cap on simultaneous toasts.
- No platform-wide tab-order audit.
- No automated contrast-ratio verification — direct value inspection
  only.
- No physical High-DPI/multi-monitor verification — inherited,
  unverified trust in Avalonia's own DPI-aware pipeline, the same
  boundary `WP 10.0B`/`WP 10.2B` already accepted.

## 11. Common Mistakes

- Assuming `GetResourceObservable`/`Bind` "just works" for a
  theme-reactive brush in code-behind — it does not reliably survive
  attachment timing in this codebase's own topology; verify with a real
  attached-and-shown `Window`, not a bare unattached control.
- Guarding a registration method with a process-wide static flag when
  the underlying host (here, Avalonia's own headless test `Application`)
  does not guarantee one instance per process — a subtle, genuinely
  hard-to-anticipate class of bug, found only by a flaking test.
- Writing a test that touches Avalonia rendering APIs
  (`StreamGeometry.Parse`) as a plain `[Fact]` rather than
  `[AvaloniaFact]` — a failed static constructor from this mistake can
  silently poison unrelated tests for the rest of the process.

## 12. Future Evolution

- A comprehensive, hand-authored vector icon library (`FCR-0071`).
- A global, visually-verified `:focus-visible` override using
  `FocusRingBrush` once real rendering verification is available.
- Split/tiled document view (`FCR-0072`).
- A capped, queued toast stack.

## 13. Key Takeaways

A "polish" Work Package this large is best served by building a real,
small, well-tested foundation and applying it to the highest-value
surfaces honestly — not by spreading thin, unverified effort across
every named bullet. Two genuine, non-obvious Avalonia lessons (resource
binding across attachment timing; per-test-run `Application` lifecycle)
were found only because the work was real enough to fail realistically,
and both are now reusable knowledge for every future Work Package that
touches theming or headless tests.

## Related Documents

- `WP10.5A Implementation Report.md`, `WP10.5A Engineering Review.md`,
  `WP10.5A Technical Debt Review.md`.
- Technical Debt Register — `TD-39`, `TD-40` (both Resolved here).
- `27-digital-thread-visualisation.md` — the Work Package whose own
  hardcoded node colours this one found and fixed.
