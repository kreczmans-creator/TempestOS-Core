# Working with TempestOS's Governance Registers

## What This Article Is

A first-read guide to `docs/governance/` — why it exists, how to keep it
current, and the mistakes most worth avoiding. The full engineering
philosophy behind this suite lives in `docs/governance/Governance
Philosophy.md`; this article is the shorter, teaching-depth companion,
mirroring the relationship *Working with the TempestOS Host* already has
to `Runtime Host Architecture.md` and its five companion documents. Read
this first; read the Philosophy document when you need the fuller
reasoning behind a specific rule.

## Why the Register Exists

TempestOS already had, before `WP 4.5A`, a strong per-document discipline:
every ADR explains one decision well; every retrospective explains one
Work Package well. What none of them could answer efficiently, on their
own, is an *aggregate* question — "how many ADRs exist, and does every
one of them still hold?", "does every platform service have a test, an
ADR, and an Academy article, or did something slip through unnoticed?"
A register exists to answer exactly that second kind of question:
enumeration and cross-reference across many subjects at once, which prose
resists by nature, however well the prose is written.

This is why a register is deliberately *thin* wherever a fuller document
already exists — see, for example, `Risk Register.md`'s own Source of
Truth field pointing back to `Risks.md`. The register's job is to index
and cross-check, not to compete with the original for a reader's trust by
half-repeating it.

## How to Maintain a Register

- **Update it in the same Work Package that changes what it tracks**, not
  as a follow-up task. Adding a platform service, an ADR, a module, or a
  test category updates the corresponding register right then — the same
  rule Engineering Governance §6 already applies to the Academy and
  `Platform Service Map.md`, extended here.
- **Re-run the register's own Cross-Reference Check**, don't just append
  a row. Every register states what it was compared against when it was
  last reviewed; a later update should repeat that comparison, not assume
  it still holds.
- **Prefer Partial or Not Yet Applicable, with a stated Reason and Review
  Trigger, over inventing rows to look complete.** A register that
  honestly names what it doesn't yet cover, and what would change that,
  is more trustworthy than one padded to appear finished.

## Common Mistakes

**Filling an Unknown with a plausible guess.** The temptation is real —
an Unknown value looks unfinished next to a table full of confident
entries. Resist it anyway: a wrong answer that looks confident gets built
upon by every later reader, silently compounding the original error; a
labelled Unknown invites exactly the right next action (go find out, or
accept it may not be knowable) and nothing worse.

**Duplicating a source document instead of indexing it.** A register
that copies `Platform Service Map.md`'s own reasoning verbatim, rather
than summarising status and pointing back to it, will drift the moment
either one is updated and the other isn't — now there are two competing,
disagreeing "truths" instead of one authoritative source and one honest
index over it.

**Treating a stale register as harmless.** A register that hasn't been
updated in a while doesn't just become less useful — it becomes actively
misleading, because a reader has no way to tell, just by looking at it,
that it has drifted. This is the same "worse than no document at all"
principle Engineering Governance already applies to the Academy and the
Platform Service Map, and it applies here with the same force.

## Engineering Rationale

Every register in this suite marks each of its own entries **Verified**,
**Inferred**, or **Unknown** — never left ambiguous about which. This
single discipline is what makes the whole suite trustworthy rather than
merely tidy: a reader can tell, at a glance, whether a given claim was
read directly from a repository artifact, reasonably concluded from
available evidence, or honestly not established at all. See `Governance
Philosophy.md`'s own "Why Unknown Is Preferable to Invented Data" section
for the full reasoning, including a concrete example from this
repository's own history (`WP 4.4F`'s Academy audit) of this discipline
paying off.

## Related Documents

`docs/governance/Governance Index.md`; `docs/governance/Governance
Philosophy.md`; `docs/governance/Governance Audit Report.md`;
`docs/academy/06 Engineering Standards/Engineering Governance.md` (§6,
the maintenance obligation this article's own discipline extends).
