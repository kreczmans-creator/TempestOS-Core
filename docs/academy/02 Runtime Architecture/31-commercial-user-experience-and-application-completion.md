# Commercial User Experience & Application Completion

## 1. Introduction

`WP 10.5C`'s own concept guide — how TempestOS ran its first required,
methodologically-disclosed runtime UX audit of its own prior work, why
that audit found the platform's own reachability stronger than its own
controlling instruction implied, and how the two real defects it did
find (both hardcoded, wrong-in-one-theme colours) were closed by
extending a single, coherent, cross-surface "engineering colour
language" rather than five disconnected patches.

## 2. Purpose

Explains why "launch the application and audit it first" was taken
literally rather than skipped in favour of implementation, how a GUI
audit was performed honestly in an environment with no way to render or
screenshot a window, and why `DisciplineColors` — a genuinely new
cross-cutting colour identity — was judged not to need its own ADR.

## 3. Background

`WP 10.6A` had just closed, itself closing with "await Product Owner
instruction before `WP 10.6B`." The Product Owner's own next
instruction named this Work Package `WP 10.5C` — a number that sorts
*before* `WP 10.6A`, despite being commissioned and completed *after*
it — the identical class of out-of-sequence numbering this project has
disclosed and recorded plainly before, never silently renumbered
(`WP 9.3A` completed after `WP 9.4A`, `PROJECT_STATUS.md`). This Work
Package's own instruction asked for something genuinely different from
every prior Work Package this session: not "add capability X," but
"verify what was already claimed, then polish what verification finds
genuinely thin" — an audit-first discipline explicit in its own first
paragraph.

## 4. The Problem

No Work Package this session had ever launched the real, compiled
`Tempest.Desktop.exe` and compared its own running behaviour against
its own prior documentation's claims — every prior "Verification"
section relied on headless Avalonia tests and direct source reading,
both real evidence, but neither is the same thing as the application
actually running end to end as a real user would experience it. This
Work Package's own instruction required exactly that gap closed first,
before any implementation change.

## 5. The Design

**A two-part audit methodology, honestly bounded to what this
environment can actually prove.** No screenshot tool, no accessibility
tree inspector exists here — a real limitation, disclosed directly
rather than worked around with a fabricated substitute. The audit
instead combines (1) a real `dotnet run` process launch, observed for a
fixed window, proving the application starts and composes its full
dependency graph without exception, and (2) direct source-level
reachability tracing — the same "confirmed directly" discipline this
project's own Engineering Reviews already apply to *behaviour*, applied
here specifically to *reachability*.

**The audit's own real finding: reachability was already strong.**
Every named `WP 10.0B`–`WP 10.5B` feature traced to a real, wired
control. The two real defects found were not missing wiring but wrong
colours — `CockpitCardControl`/`RibbonView` both carried a hardcoded
`Brushes.Gray`, the identical `TD-39` risk class two prior Work
Packages had already found and closed elsewhere, simply not yet swept
in these two particular, text-heavy files.

**One new, real, cross-cutting colour identity.** `DisciplineColors` —
keyed on the real `CommandDescriptor.Category` string, confirmed
exhaustive against all six real registered disciplines by direct
`grep` — joins `HealthColors`/`SeverityColors`/`LifecycleColors`/
`CategoryColors` as this platform's own fifth colour-language mapping,
applied consistently across the Ribbon (tab accents), reusing the
*existing* `LifecycleColors` mapping newly applied to two further
surfaces (Project Explorer node dots, Property Inspector lifecycle
rows) for cross-surface consistency, and a real progress-bar rendering
for the Cockpit's own existing coverage KPIs.

## 6. Alternatives Considered

- **A Kind→Discipline colour strip on `ObjectEditorView`** — considered,
  rejected; `DisciplineColors` is keyed on `Category`, not `Kind`, and
  most Mechanical/Manufacturing Kinds do not contain their own
  discipline's name as a substring. A second, separate lookup table was
  judged a real risk of encoding a wrong mapping with no way to
  visually verify the result here.
- **Force-mapping `RequirementStatus` onto `LifecycleState`** for the
  Project Explorer's own new lifecycle dot — considered, rejected; the
  two enums' own real members do not correspond cleanly, and a lossy
  mapping would misrepresent real status data.
- **A screenshot-based or accessibility-tree-based audit** — not
  possible in this environment; disclosed directly rather than silently
  substituting a weaker method and calling it equivalent.

## 7. Why This Solution Was Chosen

The alternatives each risked shipping a plausible-looking but genuinely
wrong visual claim (a guessed Kind colour, a guessed status mapping) in
an environment with no way to catch the mistake by looking at it. The
chosen scope extends only what could be verified correct by direct,
re-derivable evidence — a real grep against the real registry, a real
enum member list — and discloses plainly everywhere it chose not to
guess.

## 8. Architectural Principles

- **Consistency is prioritised over completeness where the two
  genuinely conflict** — the Object Editor and Requirements' own tree
  nodes stay undecorated rather than risk a colour language a user
  cannot trust everywhere it appears.
- **A required audit is performed with the same evidentiary standard as
  a required review** — no verdict in the Traceability Matrix rests on
  "a prior document said so"; every one traces to a real file/line/test.
- **A fifth colour-mapping class is an application of an already-
  decided pattern, not a new architectural decision** — the same
  reasoning `WP 10.4A` already applied to its own two new colour
  classes.

## 9. Benefits

Every future Work Package touching the Cockpit/Explorer/Inspector/
Ribbon now has one, real, already-proven colour-language pattern to
extend rather than invent again. The audit methodology itself (real
launch + direct reachability tracing) is now a reusable template for
any future "verify what we claimed" Work Package.

## 10. Trade-offs

- No physical GUI rendering verification exists in this environment —
  the same, already-repeatedly-disclosed boundary every prior `WP10.x`
  Accessibility/UX Review has named.
- The lifecycle dot's own exact state is only available via tooltip
  hover, not to a pure-keyboard workflow.
- Requirements' own tree nodes remain visually inconsistent with the
  other five disciplines — a disclosed, deliberate absence, not an
  oversight.

## 11. Common Mistakes

- Assuming an audit "confirmed" a claim because the prior document
  asserted it, rather than re-deriving the evidence independently — the
  identical discipline this project's own Engineering Reviews already
  guard against for *behaviour* claims, extended here to *reachability*
  claims.
- Building a second, parallel classification (Kind→Discipline) when an
  existing one (Category→Discipline) almost, but not quite, fits — the
  gap between "almost fits" and "correct" is exactly where a plausible-
  looking wrong answer hides, especially with no way to visually verify
  the result.

## 12. Future Evolution

- Real, structural, per-discipline Object Editor layouts (`FCR-0068`,
  unchanged, still open).
- A visually-verified `ObjectEditorView` discipline accent, once a real
  Kind→Discipline mapping can be authored and checked against actual
  rendering.
- A capped, animated transition when a KPI's own progress bar value
  changes on refresh (not attempted — animations remain subtle,
  per this Work Package's own instruction).

## 13. Key Takeaways

An instruction to "audit first, then polish what the audit finds
genuinely thin" is best honoured by treating the audit itself as a real
verification activity with its own disclosed methodology and re-
derivable evidence — not a formality before the "real" implementation
work. Doing so here found the platform's own reachability already
strong, redirecting effort correctly toward the real gap (visual
richness, not wiring) rather than manufacturing reachability fixes that
were not actually needed.

## Related Documents

- `WP10.5C Implementation Report.md`, `WP10.5C Runtime UX Traceability
  Matrix.md`, `WP10.5C Technical Debt Review.md`.
- Future Capability Register — `FCR-0068` (still open, reconfirmed).
- `30-command-execution-and-productivity-experience.md` — the Work
  Package this one's own out-of-sequence numbering follows
  chronologically, despite sorting before it.
