# A6 Engineering Constants and Fundamentals

**Programme:** P01 — Engineering Reference Data
**Namespace:** `Tempest.Core.Constants`
**Governing ADR:** `ADR-0126`
**Status:** Implemented, `Group A`.

> The provenance model, lifecycle, catalogue mechanics, comparison
> semantics, data-quality principles and boundaries A6 shares with every
> other reference library are described once in
> `Group A Engineering Reference Data.md` and are not restated here.

---

## 1. Purpose

A6 holds engineering constants: physical constants, mathematical
constants, and the conventional reference values engineering works to.

A constant is the one kind of reference data that gets **used without
being looked at**. A bad bearing dimension is noticed when the bearing
does not fit; a bad constant propagates silently into every calculation
that consumed it. Everything below follows from that.

---

## 2. The seam built to refuse

`IReleasedConstantSource` — declared in the shared layer beside
`IStandardResolver`, so a future calculation capability can consume
constants without depending on A6 — is the only way anything should reach
a constant to calculate with.

```csharp
Task<ReleasedConstant?> FindReleasedAsync(string symbol, CancellationToken ct = default);
```

**It hands back nothing until a record is Released.** A `Draft` or
`Checked` constant is a value nobody has finished verifying, and a
calculation that silently used one would produce a result whose
trustworthiness nobody could later establish.

**It reports an unreleased constant exactly as it reports one that does
not exist.** Not "there is a value here you may not use" — that invites
using it anyway, with a caveat that gets dropped somewhere downstream.

**What it does hand back carries its own traceability.**
`ReleasedConstant` includes the record Id and the revision number
alongside the value. A calculation must be able to say afterwards
*which* constant, at *which* revision — and since a released record is
immutable and a corrected value becomes a new record that supersedes it,
that pair identifies the exact number used, permanently.

The seam is deliberately narrow: no enumeration, no search, no category
browsing. A consumer that wants to explore the library asks
`IConstantCatalog` instead, and gets records rather than values.

`IConstantCatalog.FindBySymbolAsync` is the librarian's lookup, returning
a record whatever its state. It is documented as *not* the one a consumer
of constants should use.

---

## 3. The value is always a dimensioned quantity

A constant recorded as a bare number is the most dangerous thing a
reference library can hold: it invites use in the wrong unit system and
offers nothing that could catch the mistake.

`ConstantDefinition.Value` is a `ReferenceQuantityValue` — a quantity of
whatever dimension the constant has. The dimension varies from record to
record, which is one of the two cases the shared
`ReferenceQuantityCodec` exists for; the other is A1's open property set.

Mathematical constants are **dimensionless quantities**, not bare
doubles, and `TEMPEST-CON-011` rejects a mathematical constant carrying
any other dimension.

---

## 4. Not recorded, zero, and exact are three different facts

`ConstantUncertaintyKind` has `NotRecorded`, `Exact`, `Standard`,
`Expanded` and `Tolerance` as separate members because they are separate
claims: nobody wrote the uncertainty down; the source stated one that
happens to be zero; the constant is exact by definition so no uncertainty
exists to state.

`ConstantUncertainty` carries an absolute figure (a quantity, in the
constant's own dimension), a relative figure (dimensionless), and a
coverage factor. Both figures may be present where the source gave both;
**neither is ever computed from the other**, because rounding either way
would invent precision the source did not publish.

A6 does not propagate uncertainty, does not combine it, and does not
convert between coverage factors. Those are measurement-analysis
operations belonging to whatever consumes the constant.

`TEMPEST-CON-004` warns when nothing is recorded — and still warns for a
category that is exact by nature, because "exact" is a claim the record
should make rather than one the reader should infer from the category.

---

## 5. Where a constant applies is part of the constant

`ConstantCategory` classifies where a constant's authority comes from,
which is what matters when deciding whether it may be relied on: a value
fixed by definition, a value measured with uncertainty, and a value
adopted by convention are three different kinds of fact.

A `ConventionalReference` value is exact within the convention that
adopted it and true of nowhere in particular. Recording the number
without the convention makes it look universal, so
`ConstantCategories.ExpectsApplicability` marks the categories where an
`Applicability` statement is expected and `TEMPEST-CON-012` warns when
one is missing.

---

## 6. Symbols

Uniqueness is enforced on the symbol alone, not on symbol and category
together: a calculation asking for a symbol must get exactly one answer,
and a library that could return two would be worse than one that returns
none. Where two constants genuinely share a symbol in the literature,
whoever records them disambiguates, and `AlternativeSymbols` keeps the
original wording of each.

The key trims whitespace but **does not fold case**: a constant's symbol
is case-significant, and merging an upper-case and a lower-case symbol
would silently merge two different constants. Search on a symbol is
case-sensitive for the same reason.

`TEMPEST-CON-014` catches what the index cannot: one record's own symbol
listed as another record's alternative symbol. An alternative symbol is
not a key, so both are legal — but a reader looking the symbol up finds
two claims to it.

---

## 7. Validation

`TEMPEST-CON-001`…`014`. Beyond the above: a constant with no value is
not a constant (001); an exact constant carrying an uncertainty figure is
a contradiction (005); an uncertainty in the wrong dimension for the value
it qualifies (007); a relative uncertainty of one or more, which says the
value is not known at all and is far more likely a percentage recorded as
a fraction (010).

---

## 8. Boundaries

No expression evaluation, no unit-system conversion policy, no
uncertainty propagation, no arithmetic on constants of any kind. A6
records constants; using them is somebody else's job.

---

## 9. Dataset

Empty. See `Group A Engineering Reference Data.md` §9. The fixtures use
fictional symbols and fictional digits deliberately: a fixture carrying a
real constant's digits would be exactly the fabricated reference data the
no-fabrication rule exists to prevent, and more dangerous here than
anywhere else in Group A.
