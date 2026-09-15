# The Accessibility Baseline

**Release:** `v0.16.0` · **Work Package(s):** `WP 16.5A`, `WP 16.5A-R1` ·
**Debt:** `TD-65`, `TD-83`, `TD-128`, `TD-131` · **Code:**
`Tempest.Desktop.Views.DialogModality`, `Tempest.Desktop.Theming.ChromeStyles`,
`Tempest.Desktop.DigitalThread.DigitalThreadGraphView`

**In plain terms.** Some people cannot use a mouse — a temporary
injury, a permanent disability, or simply a preference for the keyboard
— and some cannot see the screen at all and rely on a screen reader,
software that speaks the interface aloud. An accessible application is
one both can actually operate: every button reachable and pressable
without a mouse, every control with a name a screen reader can
announce, text with enough contrast against its background to be read,
and a pop-up window that behaves like one — you cannot accidentally tab
past it into the screen behind it. None of this is decoration added
afterwards; it is part of whether the product works at all for the
people who need it that way. It is also, as this chapter's central
story shows, easy to build, test, and ship while still broken.

## The four gaps a fresh pair of eyes actually found

TempestOS had shipped a whole desktop application with no accessibility
baseline. An independent review on 2026-08-28 named the gaps
specifically enough to act on: six dialog classes that toggled
`IsVisible` on a `Border` and trapped no keyboard focus, icon-only
buttons and watermark-only inputs a screen reader could not name, and
Cockpit health-status text measured as low as 2.04:1 contrast — nowhere
near WCAG's (Web Content Accessibility Guidelines, the standard
reference for this) 4.5:1 floor for body text. This became `TD-65`.

`WP 16.5A` picked the top four of those findings, deliberately, rather
than a full sweep: real modal behaviour for six dialogs,
`AutomationProperties.Name` (the label a screen reader speaks) on named
controls, live regions (telling a screen reader "something changed, say
it" without the user hunting for it) for asynchronous feedback, and
keyboard operability for the Digital Thread graph — the node-and-line
diagram `27-digital-thread-visualisation.md` covers, until now entirely
mouse-driven. Naming the scope, rather than implying completeness, is
what let this Work Package be checked later against exactly what it
claimed.

## A dialog is not modal because it looks like one

Six overlays — `ConfirmationDialog`, `MessageDialog`, `InputDialog`,
`SettingsDialog`, `MacroManagerDialog`, `CommandPaletteOverlay` — looked
like dialogs: a card over a dimmed background. Underneath, each was an
ordinary `Border` with its visibility flipped on and off. Nothing
stopped keyboard Tab cycling into the window behind it. Two of the six
had no Escape-to-close and no initial focus at all.

The fix is a small static helper, `DialogModality.Install`
(`src/Tempest.Desktop/Views/DialogModality.cs`), called once per dialog:
it sets `KeyboardNavigation.SetTabNavigation(dialog, Cycle)` so Tab
cannot leave the dialog's own logical tree, and captures the focused
element the instant the dialog becomes visible, refocusing it the
instant the dialog is hidden again — keyed off `IsVisible`, which every
dialog already flips on open and close.

A shared base class was the more obvious shape, and was rejected —
again — for a reason already on record: an earlier architecture review
(`WP10.5B Architecture Review.md` §2) had found this dialog family's
layouts differ enough that a forced common base would cost more than it
saved. `DialogModality` shares the trap-and-restore logic without
forcing dissimilar dialogs into one inheritance tree. `MainWindow`
separately counts open dialogs and removes its own dock from Tab
navigation while any is open. Twelve dialogs call it today, not only
the original six: the pattern outlived the Work Package that introduced
it.

`TD-83` had asked for a real `MainWindow`-level test proving this.
`MainWindowKeyboardModalityTests.cs` opens each overlay over a real,
started window, walks Avalonia's own
`KeyboardNavigationHandler.GetNext` up to 25 steps, and asserts every
stop stays inside the dialog — confirmed by reflection to be a real,
public API on the referenced Avalonia version before the test was
written, rather than assumed from documentation.

## Naming, announcing, and a graph driven by keys

The rest of the baseline followed the same pattern: find the real
mechanism, verify it against the framework's actual behaviour, wire it.
`AutomationProperties.SetName` was added to six named controls — the
toast dismiss button, the graph's expand/collapse chevron (recomputed
every rebuild to announce the *next* action, not the last one), the
Command Palette query box, the Macro name box, the graph search box,
the Explorer filter — and `SetLiveSetting` went `Polite` on the status
bar's eight segments, `Assertive` on toasts, because a `Polite` toast
risks auto-dismissing before it is ever spoken.

The graph gained a keyboard: node borders became `Focusable` with a
deterministic tab order, `+`/`-` zoom at the mouse wheel's own 1.1×
factor, arrow keys pan in fixed 60px steps, and Enter/Space selects a
focused node and — only if it carries the expand/collapse chevron —
toggles it, deliberately not also opening the object, so a keyboard
user is never thrown out of the graph they are exploring. Every
relationship line gained an invisible, 8px-wide hit-test twin sharing
its click handler, rather than thickening the visible 1.4px stroke.

## The centre of this chapter: a test that could not fail

`ApplicationPalette`/`DesignTokens` already declared a
`FocusRingBrushKey` and a `FocusRingThickness` — the colour and width a
focus ring, the outline drawn around whatever a keyboard user has just
tabbed to, should use. Nothing consumed them. `WP 16.5A` fixed that in
`ChromeStyles`, adding a real `:focus-visible` style (Avalonia's own
pseudo-class, set only for genuine keyboard navigation, never a mouse
click) per button treatment.

Before shipping, the Work Package checked that `FocusRingBrushKey` had
not been accidentally swapped with `BrandPalette.AccentBrushKey`, the
Primary button's own fill colour. They matched — correctly read as
confirmation nothing had been swapped by mistake. Nobody asked the next
question: if ring and fill are *meant* to be the same colour, is a ring
identical to the button's own background a ring at all?

It is not. Both keys resolved to the exact same colour — `Indigo600` in
light mode, `Cyan500` in dark — on the single most common call-to-action
a keyboard user would tab to. A border painted the same colour as the
surface it sits on has a contrast ratio of 1.00:1: invisible.
`docs/releases/v0.16.0/Release Notes.md`, under "Defects this release
found in its own work", puts it plainly:

> The accessibility baseline shipped an invisible focus ring. The
> `:focus-visible` ring was painted in a brush that resolved to the
> same colour as the Primary button's own fill — 1.00:1 in both
> themes, on the shell's single call-to-action. The test written to
> prove the ring worked compared it only against its own token, never
> against the background it was drawn on, so it could not have failed.

That last sentence is the point of this chapter. The original
`FocusVisibleStyleTests.cs` asserted exactly one thing:
`presenter.BorderBrush == focusRing` — that the resolved brush equalled
the *token*. That is true whether the ring is visible or not, because
it never asks what colour the ring is drawn against. Wiring the right
token to the right property is real work worth testing — but it
answers "is the mechanism connected," not "does a person get the
experience the mechanism exists to produce." Those are different
questions, and only the second is the point of a focus ring.

## The review board, and a fix that failed its own first attempt

`WP 16.5A`'s own retrospective disclosed the invisible ring itself,
found by a later internal pass reading the shipped source directly,
before any outside review did. A review board then confirmed it
independently, rated it a **blocker**, and returned six findings: the
invisible ring; `Danger`-treatment buttons whose ring also failed once
hovered and focused together (2.33:1 light, 1.36:1 dark); Ribbon
command buttons and the graph's "Reset View" button, whose icon-plus-
label content announces to a screen reader as the literal type name
`Avalonia.Controls.StackPanel`; a status-bar "Hint" segment announcing
on every ribbon hover, drowning real messages in noise; and three
further measured contrast failures.

`WP 16.5A-R1` fixed all six. The Primary ring now draws in
`OnAccentBrushKey` — the treatment's own foreground colour, already
guaranteed to contrast with its own fill, because foreground and
background tokens are chosen to contrast with each other for text
legibility in the first place. Fixing `Danger` surfaced a genuine
framework surprise: the obvious fix, a more specific selector combining
`:pointerover:focus-visible`, assumes Avalonia resolves competing
styles the way a browser's CSS cascade does — by specificity. It does
not: two styles that both match, in the same `Styles` collection,
resolve by the order they were *added*. The "obvious" fix was written,
its own new test run, and the test failed — which is how the real rule
was found rather than assumed, and recorded in a code comment at the
point it matters. `FocusVisibleStyleTests.cs` itself was rewritten to
drive a real Tab keypress and pointer hover through Avalonia's actual
input pipeline and compute the genuine WCAG contrast ratio between the
resolved `BorderBrush` and `Background`, for all four treatments in
both themes. Twenty-one new tests closed the six findings; the Desktop
suite grew from 453 to 474 with no regressions.

## What remains open

Check `BACKLOG.md` for current status; it is authoritative over this
chapter for anything that has since moved.

- **`TD-65`**, the systemic gap, is still listed with a future Work
  Package as its planned owner. Neither `WP 16.5A` nor `WP 16.5A-R1`
  closed it; both narrowed it and published what remained (an
  `ObjectEditorView` input box, docking chrome buttons, sub-floor hit
  targets, the graph mini-map, drag-and-drop). This was always a
  baseline, not a conformance claim.
- **`TD-128`** — the graph's relationship (edge) lines are still
  keyboard-unreachable. Nodes became `Focusable`; the lines joining
  them remain plain, pointer-only `Line` elements. Reaching an edge
  needs a focus model for a non-control visual element that does not
  exist yet — a design decision for later, not a defect this round
  could absorb.
- **`TD-131`** — the rewritten contrast test still cannot see any
  `Flat`-treatment state whose own fill is fully transparent — the
  right instinct for a test that will not guess, but a real, disclosed
  gap in what that treatment has actually had verified.

## What was deliberately not built

No general accessibility sweep — only the top four findings in
`WP 16.5A`, and only the six the review board raised in `WP 16.5A-R1`.
A light-theme contrast failure found incidentally while measuring an
unrelated fix (4.37:1 against 4.5:1) was left unfixed and disclosed
rather than folded in opportunistically: it was not one of the six
findings this round was asked to close, and its on-screen placement
could not be traced with the same confidence as the pairs that were
fixed. Fixing an unlisted defect because the tools were already in hand
is the same scope creep as fixing none of the listed ones.

## What to take away

- **A test that a token is wired up correctly is not a test that the
  wiring produces a visible result** — the focus ring's own
  token-equality test could not fail whether or not a keyboard user
  could see anything.
- **When one item in a piece of work gets a rigorous check, every
  visually similar item in the same work needs the identical check**,
  or the asymmetry becomes the next reviewer's finding.
- **Do not trust a mental model borrowed from a different system** —
  verify empirically, by watching the "obviously correct" fix fail its
  own test, before relying on an assumption a framework never promised
  to honour.
- **Disclosing a defect in your own retrospective before an outside
  review finds it is worth more than a clean report** — it is what let
  the next Work Package fix exactly the right thing, fast.
