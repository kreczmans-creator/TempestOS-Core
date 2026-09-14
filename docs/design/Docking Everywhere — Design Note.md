# Docking Everywhere — Design Note

For the Product Owner, alongside `ADR-0153`. Two pages: what it feels
like to use, and what happens at the edges.

## What you can already do today

Four panels — Project Explorer, the Documents area as one block,
Property Inspector, Output — drag onto each other to tab, drag onto an
edge to split, and drag off the window entirely to float into their own
small window, inside the Engineering workspace only (whether that is
standalone at Home, or inside a project's *Structure* tab). Nothing
else moves: a Quote tab, a Requirements list, the Projects dashboard,
an individual document tab inside the Documents area — all fixed in
place.

## What changes

**Tear a Quote tab out beside a drawing.** You open a project, its Quote
tab is showing, and you have a drawing open in the Structure tab's own
document viewer. Grab the Quote tab's header and drag — not onto
another tab, but out past the window's own edge, toward the drawing's
window. As your pointer crosses into that window, a translucent
highlight appears over wherever you are about to land, exactly like the
highlight that already shows when you drag one engineering panel onto
another today. Release over the drawing's right half: the Quote tab
docks there, side by side with the drawing, in the same window. Release
somewhere with no window at all — an empty stretch of a second monitor —
and the Quote tab becomes its own small window instead, exactly where
you let go of it. Either way, the project you were in does not change;
only where the Quote tab lives changed. It carries its own small header
now — the project's name, and whether it is Archived — so it still
makes sense once it is no longer sitting under the project workspace's
own banner.

**Drag a dashboard to a second monitor, dock it back.** The Projects
area's dashboard is a full pane, not a tab, but it drags the same way:
grab its own header strip and pull it off the main window entirely. It
becomes its own window; drop it onto your second monitor and leave it
there — a live dashboard, on its own screen, updating as your data
changes, while the main window gets on with whatever project you are
actually working in. Change your mind and drag it back: as it crosses
back over the main window, the same highlight shows where it will land,
and releasing over the Projects tree re-docks it exactly like putting
any other panel back.

**What the highlight shows, always.** Wherever your pointer is during a
drag, a soft accent-coloured overlay covers exactly the space the thing
you are dragging will occupy if you let go right now — the edge of a
pane if you are about to split it, the whole of a tab strip if you are
about to tab into it. Nothing ever docks silently; you always see it
coming, before you commit to it. Let go somewhere that resolves to
nothing sensible and the thing you dragged simply becomes its own small
floating window at the point you released it — dragging something out
is never a way to lose it.

## What persists

Everything you arrange survives closing and reopening TempestOS: which
window each thing lives in, how that window's own contents are split
and tabbed, and — new here — which monitor each window was on and
roughly where. Restart with your monitors exactly as you left them, and
every window comes back exactly where you put it. Rail navigation still
works the way it always has: clicking Projects still shows you the
Projects area's default arrangement if you have not moved anything, and
your own arrangement once you have — the rail chooses a starting point,
never a cage.

## What happens on a laptop with the second monitor unplugged

Every window that was on your second monitor still opens — nothing is
lost, and nothing throws an error — but since the monitor it remembers
is not there, that window opens on your primary screen instead, sized
and positioned sensibly rather than off in space you cannot see or
reach. Plug the second monitor back in later and drag the window back
out to it whenever you like; TempestOS does not try to guess that you
want it moved back automatically, since the next time you unplug, moving
it back automatically would be exactly as unwelcome. If two windows
both remembered the missing monitor, both land on the primary screen,
stacked rather than hidden behind one another, so you always have
something visible to grab and rearrange rather than a window you cannot
find.

## What stays exactly as it is

The rail, the header, and the ribbon never tear out — they are the one
constant, wherever your other windows end up. The Engineering
workspace's own internal arrangement — Explorer, the discipline panels,
Inspector, Output — keeps working exactly as it does today, including
inside a torn-out Structure tab; tearing the whole Structure tab out to
its own monitor is one drag, and rearranging what is inside it once it
is there is the same drag-and-dock you already know from Engineering
today.
