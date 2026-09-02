# Workspace Layout & Docking

**Programme:** Product Convergence & Recovery, 2026-08-29 ·
**Debt:** `TD-72` (resolved) · **Decision:** `ADR-0095` ·
**Code:** `Tempest.App.Workspace.Layout`, `Tempest.Desktop.Docking`

## The feature that was not missing — it was unrepresentable

TempestOS needed drag-to-dock, tabbed groups, arbitrary splitting and
floating windows. It had a docking surface: a five-column, three-row
`Grid`, with panels assigned to named slots at composition time.

The instinct is to add to it. Put drag handles on the splitters, add a
`Floating` value to the enum, find somewhere for tabs.

None of that works, and it is worth being precise about why. **There were
three places a panel could be, and they were all occupied.** "Drag this
panel there" has no *there*. "Tab these two together" has no
representation. "Split this pane again" has no fourth slot. The features
were not missing from the implementation; they were missing from the
*vocabulary*.

The controlling instruction said it outright: *"Do not merely add drag
handles to the existing 5×3 DockingGrid; replace the underlying
abstraction properly."*

> **The transferable lesson.** When several requested features all turn out
> to be awkward in the same place, stop costing them individually. Ask what
> the model cannot say. If the answer is "any of them", you have an
> abstraction problem wearing a feature backlog's clothes.

## The whole design, in one shape

```
Split(Horizontal)
├── Tabs[Explorer]
├── Split(Vertical)
│   ├── Tabs[Document, Drawing]
│   └── Tabs[Output]
└── Tabs[Inspector, Materials]
```

A tree of splits and tab groups. That is it. Everything the brief asked
for falls out of it rather than being added to it:

- **Arbitrary splitting** — splits nest, either orientation, any depth.
- **Tabbed groups** — the only leaf *is* a tab group.
- **Drag-to-dock** — a dock is `Remove` then `Insert`, five relations.
- **Floating** — a subtree in a window, with screen coordinates.

The single most useful decision was making a lone docked panel **a tab
group of one**. It sounds like a technicality. It means "drag a panel onto
another panel to tab them together" is the ordinary insert operation
rather than a special case that has to convert a panel into a group first.

> **The transferable lesson.** Look for the representation where your
> special case is already the general case. A leaf that is always a
> collection removes an entire branch of logic you would otherwise write,
> test and get wrong.

## Immutable, because docking edits are multi-step

Every operation returns a **new** tree:

```csharp
tree = tree.Dock(inspectorId, targetGroupId, DockRelation.Right);
```

A dock is remove-then-insert-then-normalise. With mutation, an exception
halfway leaves a corrupt arrangement on screen and no way back. With pure
functions, a failed operation is a no-op by construction.

The bigger payoff was testability. **The entire docking system is provable
with no UI in the process**: 81 tests over the model, the drop-zone
geometry, serialisation, presets and migration, running in a fifth of a
second. The renderer is then a separate, much smaller question — is it a
faithful function of the model? — proven by 38 more.

> **The transferable lesson.** UI-heavy features are not inherently
> hard to test; they are hard to test when the decisions live in the
> controls. Move the decisions into data and the tests get boring, which
> is what you want.

## The normalisation nobody asks for

After every edit, the tree collapses its own debris: splits reduced to one
child, and nested splits sharing their parent's orientation.

Skip it and nothing looks wrong on day one. Dock and undock a panel twenty
times and you have twenty one-child wrappers nested around a single pane,
each one a `Grid` in the visual tree. It degrades quietly, over a session,
which is the worst way for anything to degrade.

There is now a test that docks and undocks ten times and asserts the tree
depth is unchanged.

> **The transferable lesson.** For any structure users edit repeatedly, ask
> what accumulates. Then assert that it does not.

## What the mutation testing found

Eleven deliberate defects were introduced and eleven were caught — but one
of them survived the first attempt.

The mutation was *"stop collapsing a split reduced to one child"*. Every
test still passed. Working through why: the existing tests all removed a
panel from a **three**-pane split, leaving two, so no collapse was needed.
Nothing exercised the two-to-one case, which is precisely the case that
accumulates wrappers.

Three tests were added, including the ten-cycle depth test above. Then the
mutation died.

> **The transferable lesson.** A surviving mutant is worth more than a
> dozen dead ones. This one did not say "add an assertion" — it said "your
> tests only ever exercise the interior of the range, never the boundary
> where the interesting logic lives."

## Preserving what people already had

Replacing an abstraction is easy to justify and easy to make someone else
pay for. A returning user has no layout tree — they have widths, a
collapsed Explorer, an auto-hidden Inspector, a hidden Output panel.

`WorkspaceLayoutMigration` turns those into the equivalent tree, once. It
has its own tests, because *"does my workspace still look like my
workspace"* is the only question an existing user will actually ask on
upgrade day.

One of those tests found a genuine bug. A 320 px Explorer in a 1280 px
window should migrate to 25%; substituting the weight and letting
normalisation rescale gave 23.8%, because the other panes still carried
their original shares and the total exceeded one. Small, invisible, and
wrong. Recorded fractions are now preserved exactly, with the remainder
shared among the panes that had none.

> **The transferable lesson.** Write the migration test as the user's
> question, not the developer's. "Round-trips correctly" would have passed.
> "My Explorer is the width I left it" did not.

## Two things fixed on the way

**A guarantee that was never wired up.** `TD-70` gave the workspace a
responsive rule: side panels give way so the working pane stays usable.
It existed, it was tested — and *nothing in the running application ever
called it*. Only the tests did (`TD-83` had recorded the smell). The new
host subscribes to its own size changes, so the guarantee is now real for
a user resizing a window.

> **And the same smell came straight back, one level up.** The closure
> pass mutated that subscription away — deleted the one line — and all 260
> Desktop tests stayed green. Every responsive test called
> `ApplyResponsiveLayout` directly, so the suite proved the rule was
> *correct* and nothing proved it was *connected*. That is exactly the
> shape `TD-83` had recorded, moved from the method to its wiring, and it
> survived a work package written specifically to close it.
>
> The fix is a test that resizes and never names the method:
> `ShrinkingTheWindow_AppliesTheResponsiveRule_WithoutAnyoneInvokingItDirectly`.
> Its first draft failed against correct code — it drove `Window.Width`,
> which the headless harness does not propagate, so the host's bounds
> never moved. A test that fails for its own reasons is no better than one
> that passes for them; it now drives the host's own layout pass, which is
> what a resize actually is.
>
> **The transferable lesson.** "Is this behaviour correct?" and "does
> anything invoke it?" are two questions, and a test suite that only ever
> asks the first will answer the second wrong for years. Mutating the
> *wiring* — not the logic — is what asks the second one.

**A comment that had become a lie.** `DigitalThreadGraphView` explained
that it was a document tab rather than a panel because *"there are exactly
three physical dock slots, all already occupied."* That was true and is
not any more. The comment now says so, and says the view stays a document
tab because that is where it belongs in the workflow — not because the
layout forces it.

> **The transferable lesson.** When you remove a constraint, grep for the
> decisions that cited it. Some of them are now unjustified, and a stale
> rationale is worse than none: it will be quoted back at you.

## What we did not do, and said so

- Focus is not preserved across a re-render (`TD-90`).
- `IWorkspaceLayout`'s projection cannot express tabbing or floating —
  lossy by construction, not by oversight (`TD-91`).
- Drag has no live preview adorner following the pointer; the drop target
  resolves and applies on release (`TD-92`).

Each is bounded, disclosed, and cheap to close. None is a workaround.

## Related

`ADR-0095` · `ADR-0092` (which reserved that number for this decision,
four weeks earlier) ·
`docs/architecture/Workspace Layout & Docking Architecture.md` ·
`35-project-centric-convergence.md`
