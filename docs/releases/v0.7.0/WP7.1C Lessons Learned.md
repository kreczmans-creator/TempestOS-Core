# WP 7.1C — Materials Framework — Lessons Learned

## Status

Complete.

## 1. A provenance requirement can resolve a reserved property-typing question more decisively than the question alone

`ADR-0055`'s own reserved question ("confirm the open, boxed-`object`
shape, or design a stronger alternative") could have been argued either
way in isolation — the bare `object` shape is simpler, and no discipline
requirement yet forces a richer type. What actually settled it was this
Work Package's own separate, explicit provenance requirement: a bare
`object` has nowhere to attach a `MaterialPropertyProvenance` to. The
lesson generalises: a reserved architectural question, argued abstractly
at contract-review time, sometimes only becomes answerable once a
second, seemingly unrelated requirement forces a concrete design.

## 2. A "thin index over an existing store" can still need its own storage dependency

`WP7.1A Future Capability Recommendations.md`'s own Recommendation 1
said `IMaterialCatalog` should be a thin, typed index over
`IEngineeringDocumentStore` — true, and followed here. What that
recommendation did not anticipate is that `IEngineeringDocumentStore`
itself has no lookup-by-arbitrary-string capability, so a "thin index"
still needs its own small index of its own. This was only visible once
`FindAsync`/`ListAsync` were actually implemented against the real
`IEngineeringDocumentStore` contract, not from reading the approved
interface signatures alone.

## 3. Bounding a heterogeneous property value to an already-established, small set avoids two failure modes at once

A fully general, reflection-based polymorphic value type would have
solved "support any dimension" at the cost of a real
deserialization-safety concern (constructing arbitrary types by name
from stored data). A closed, hand-invented property-name taxonomy would
have avoided that risk but repeated exactly the invention this project's
own governance discipline has repeatedly declined (`WP 7.0A`/`WP 7.0B`
both explicitly refused to invent discipline-specific capability without
evidence). Bounding `MaterialPropertyValueCodec` to the seven dimensions
`Tempest.Core.UnitsAndQuantities` already defines — reused, not
reinvented — avoided both failure modes simultaneously, at the cost of
Temperature (`FCR-0034`) not yet being representable.

## 4. "Do not invent values" is a discipline worth naming explicitly, not just following implicitly

Writing a living-reference sample module for an engineering-data
framework creates a real, specific temptation: picking a real-sounding
material name and a real-sounding property value makes for a more
convincing demonstration. This Work Package's own controlling
instruction named the discipline explicitly ("do not invent values"),
which made the right choice (a clearly-labelled "Fictional Test Alloy"
with values explicitly disclosed as invented) the obvious one rather
than a judgment call made silently. Worth naming as a standing
discipline for any future Work Package populating engineering-domain
sample data, not just this one.

## Recommendations

- **Candidate F (Calculation) is the strongest next Work Package** — see
  `WP7.1C Engineering Foundation Impact Assessment.md` for the full
  reasoning.
- **Future Work Packages adding a new bounded, boxed-value codec should
  follow `MaterialPropertyValueCodec`'s own pattern** (ordinary
  type-pattern matching over an explicit, small, already-established
  set) rather than reaching for reflection-based polymorphic
  serialization the first time a heterogeneous value type appears.
- **Any future framework populating sample or test engineering data
  should state its own "do not invent values" discipline explicitly**,
  mirroring this Work Package's own — a good practice worth carrying
  forward as a standing Academy note, not re-deriving per Work Package.

## Related Documents

`WP7.1C Implementation Report.md`; `WP7.1C Engineering Review
Report.md`; `ADR-0055`; `docs/academy/03 Work Packages/
WP7.1C-materials-framework-implementation.md`.
