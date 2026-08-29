# Project Documents & Requirements

**Programme:** Product Convergence & Recovery, 2026-08-29 ·
**Debt:** `TD-102` (closed) · opens `TD-103` ·
**Code:** `Tempest.App.Projects`, `Tempest.Desktop.Views`

## The gap that was worse than a missing feature

The Project Workspace had nine areas. Seven of them were honestly marked
`Declared` and drew a card saying what was missing and which debt item
tracked it. Two — Documents and Requirements — were marked
`Implemented`.

They drew the same card.

`DeclaredCapabilityView` shows its "Not yet implemented" badge only when
the descriptor says `Declared`, so those two areas rendered a glyph, a
title, a paragraph of prose, and no badge to say anything was absent. The
descriptor table said the capability existed. The governance registers
said it existed. The surface looked like a finished page with nothing on
it.

> **The transferable lesson.** An honest placeholder is a good thing and
> this codebase is full of them. A placeholder that has been *marked
> complete* is worse than no feature at all: the missing feature is
> visible, and this is not. When a status field and a surface can drift,
> something has to fail if they do.

## Two registers, two different joins

The two areas look symmetrical and are not, because the domain underneath
them is not.

**A document is an engineering object.** `ProjectMembership` already
defines what belongs to a project — walk the durable `IHasParent` chain
upwards and see whether you reach a Project. So `ProjectDocumentRegister`
asks that question and nothing else. The consequence that matters is
transitivity: a drawing attached to a Part, inside a Sub-Assembly, inside
an Assembly, inside the project **is** a project document, because that is
how a real product structure is shaped and a direct-children-only rule
would find almost nothing.

**A requirement is not an engineering object.** Requirements live in
`IRequirementsService` over the document store, with their own identity,
revisions and status. `ProjectMembership` cannot reach one, and the
tempting fix — a `ProjectId` field on the requirements model — would be a
second, competing answer to a question the platform can already answer.

So the join is the link the platform already records: a requirement
belongs to a project when something it is **allocated to** is an
engineering object in that project. That reads the existing edge and
invents nothing.

It also has an honest consequence worth stating out loud rather than
engineering around: **an unallocated requirement belongs to no project.**
That is not a defect of the register. A requirement nobody has linked to
anything is not yet part of any project's work, and the empty state says
so — including what to do about it.

## Two statuses, because they disagree

Each requirement row shows its declared lifecycle status *and* what its
verification history records. These are different claims:

```
REQ-100   Status: Approved   Verified — passed
REQ-300   Status: Verified   Not verified          <- the interesting one
```

A requirement marked `Verified` with no verification record behind it is
exactly what a reviewer needs to find, and a surface showing one field
would hide it. The row says so outright rather than quietly preferring
either number.

The latest verification wins, not the worst ever recorded — a requirement
that failed, was fixed and passed is verified, and reporting the worst
outcome would leave it looking failed forever.

## What a permission check must not be allowed to say

`GetEvidenceAsync` is permission-gated, transitively, through the
verification history it composes. The first version of the register called
it for every requirement in the platform to decide project membership, and
threw `PermissionDeniedException` at a test.

That was the register's design being wrong twice over. Membership now
joins on the **ungated** relationship read, so it never depends on being
allowed to see verification — and it stops composing evidence for
requirements that turn out not to be in this project at all.

Where verification genuinely cannot be read, the state is `Unknown`, kept
distinct from `NotVerified`:

```
NotVerified   nothing was recorded
Unknown       you are not permitted to see what was recorded
```

Collapsing the second into the first would have the surface state
something **false about the user's engineering data** on the strength of a
permission check — the same reasoning that keeps `Missing`, `Corrupt` and
`Unsupported` apart in the document viewer.

That test failure also found a real gap: **the desktop shell establishes
no principal at all.** Only sample modules do, so what a session may read
depends on which sample initialised last. Recorded as `TD-103` and left
for the Administration module, not patched here.

## Opening a drawing, and saying where it went

The Documents area opens files through the same `AttachmentViewerLauncher`
the object editor uses — one entry point to the viewer, so a drawing
opened from the project register tabs, splits, floats and persists exactly
like one opened from an editor.

But the viewer is a panel in the *Engineering* workspace's layout, and the
user pressing Open is standing on the Project Workspace. Opening changes
no navigation state — the project, the module and the area are all exactly
as they were, which is the `TD-80` property worth keeping — and the
consequence is that nothing visibly happens where the user is looking.

**A button that appears to do nothing is indistinguishable from a broken
one.** So the row says where the document went. That is a smaller answer
than navigating the user somewhere they did not ask to go, and a more
honest one than silence.

## What the tests drive

Every acceptance test goes **project → project area →
document/requirement → action**, through the real `MainWindow`. None calls
a register or the launcher directly: a test that reached past the shell
would pass over exactly the defect class the `TD-80` visual audit found —
a working destination nobody can reach.

Five mutations, five killed: membership reduced to direct children,
project isolation removed from the requirement join, a file-less document
dropped, the earliest verification preferred over the latest, and the
Documents area's Open button unwired.

## Related

`ADR-0095` (the layout the viewer docks into) · `ADR-0114` (the durable
content) · `ADR-0115` (the viewer) · `TD-102` (closed here) · `TD-103`
(opened here) · `35-project-centric-convergence.md` ·
`38-document-and-drawing-viewer.md`
