# ADR-0149: A Reference Record Carries a Structured Source Citation, and Supersession Keeps Every Pin Valid

## Status

Accepted — `WP 18.0B` (Reference libraries reach the product, with citation), 2026-09-09.

## Context

`ReferenceProvenance` (`ADR-0124`) answers "where did this record's own
values come from, and has anyone checked them back against it", in
free-text fields (`SourceOrganisation`, `SourceDocument`,
`SourceLocation`). `D-028` makes evidence the product: an issue sheet
lists, for every citation, "library, record, revision and source". A
free-text `SourceLocation` such as `"Table 1"` is not a structured field a
renderer can lay out consistently across five libraries' worth of prose.

`ADR-0149` was originally reserved for interpolation and structured value
bands over reference data — a calculation-authoring concern `D-028`
withdrew from `v1.0`; nothing in the product would consume either. This
decision re-scopes the number to what the product does consume: Evidence's
own citation.

## Decision

**1. `SourceCitation(Publisher, Work, Edition?, Page?, TableOrFigure?,
RowOrEntry?)`** — `Publisher`/`Work` required, the rest `null` where the
source does not state it, never guessed. `ToString()` is one issue-sheet
line: `Publisher, Work, ed. Edition, p. Page, TableOrFigure, row
RowOrEntry`, absent parts omitted.

**2. `Source` is optional on every `IReferenceRecord<TDefinition>`**,
persisted in the same envelope as `Definition`/`Provenance`.
`IReferenceDataCatalog<T>` gains a citation-accepting overload of
`RegisterAsync` and `ReviseAsync`; the pre-existing overloads' signatures
and every caller written against them are unchanged. `RegisterAsync`'s
pre-existing overload registers with no citation. `ReviseAsync`'s
pre-existing overload carries the record's own **current** citation
forward untouched rather than overwriting it with `null`:
`ReferenceReviewService.VerifyAsync`/`ReleaseAsync` revise a record's
provenance through exactly this overload, and a citation set at
registration must still be there once the record is Released and cited
from Evidence — the point of this decision. The new overload always
writes `source` as given, `null` included: withdrawing a citation is a
real, distinct act from never having mentioned one.

**3. Forty-one seeded records, across the five governed libraries, carry a
citation** — every one whose dataset already names a publisher and a
work: the datasheet actually read (Materials, Bearings), the tertiary
index (Fasteners, some Standards), the standards body's own catalogue
entry (the rest of Standards), the CODATA listing (Constants). No
citation is invented past what the dataset already stated; the
per-library count is in `WP 18.0B`'s closing report. `WorkPackages.md`'s
"79 seeded records" figure predates this population and is superseded.

**4. The supersession invariant is a tested property, not an assumption.**
Nothing is deleted or overwritten across a revise, a lifecycle transition
or a supersession — `SupersedeAsync` writes a new revision of the
superseded record's own state; `GetRevisionAsync` reads any past revision
back exactly — but nothing had proved this under an arbitrary sequence of
such acts. A CsCheck property, against all five real libraries, drives a
random sequence of them against one record and asserts every pin minted
along the way still resolves to its exact content, and a superseded
record still reports itself as `Superseded` through `FindAsync` rather
than vanishing.

**5. Interpolation and structured value bands remain deferred.** Nothing
in `v1.0` computes from reference data; a value band's own consumer, if
ever built, gets its own ADR when it exists to read.

## Consequences

**Positive.** Evidence's issue sheet (`WP 18.2B`) renders a citation line
for every reference without reaching into five differently-shaped
free-text fields. The citation survives seed, verify and release — every
record's own pipeline before evidence may cite it — proved by test.

**Negative.** `SourceCitation` and `ReferenceProvenance` both name a
publisher and a document, in different shapes, because they answer
different questions sharing underlying facts; no attempt derives one from
the other, so each seed record states both by hand. Not every seeded
record earns a citation this precise — the gap is reported, not papered
over.

## Alternatives Considered

**Deriving `SourceCitation` from `ReferenceProvenance` automatically.**
Rejected: `SourceLocation` is one free-text field where a citation needs
up to four independent ones; guessing a split would be the fabrication
this platform's provenance discipline (`ADR-0124`) refuses.

**Requiring a citation on every record.** Rejected: some seed records (a
tertiary placeholder, an unresolved standard) have nothing this precise
to state.

## Related Documents

`ADR-0124` (`ReferenceProvenance`, `ReferenceDataCatalog<TDefinition>`);
`ADR-0143` (`ReferencePin`); `D-028`; `docs/releases/v1.0.0/WorkPackages.md`
(`WP 18.0B` row).
