# WP 7.1B — Units & Quantities Framework — Lessons Learned

## Status

Complete.

## 1. `System.Text.Json`'s default constructor resolution silently prefers the implicit parameterless struct constructor over an explicit, validating one

Every struct in C# has an implicit public parameterless constructor,
even one that also declares its own explicit, parameterised constructor.
`System.Text.Json`, when given a type with more than one visible public
constructor and no `[JsonConstructor]` attribute, resolved to the
implicit parameterless one for deserialization — silently producing a
zero-valued, unvalidated `Unit<TDimension>`/`Quantity<TDimension>`
rather than routing through the explicit constructor's own validation.
This was caught immediately by the serialization round-trip tests
themselves (`Assert.Equal` failing with `Symbol = ""`/`ToBaseUnitFactor
= 0` instead of the expected values), not discovered later. The lesson
generalises: any value type with validating construction logic that
will ever be serialized needs `[JsonConstructor]` made explicit, not
assumed — record classes get this handled implicitly by their own
compiler-generated primary constructor, but a `record struct` with a
hand-written constructor does not automatically receive the same
treatment.

## 2. A framework with zero Platform Service dependency is a genuinely different implementation experience

Every prior Engineering Foundation Work Package (`WP 7.1A`) touched
`TempestHost.cs`, needed an `ILogger?` parameter, and needed a sample
module. This Work Package needed none of the three — not because
anything was skipped, but because the approved contract itself named
this as the one framework with zero Platform Service dependency, and
implementation confirmed that claim was accurate, not aspirational. This
is worth recording because it changes what "Platform Integration"
correctly looks like for a framework like this — a direct unit test
demonstrating the one real integration point (content stored via the
Engineering Data Model) is proportionate; building a `TempestHost.cs`
registration or a living-reference sample module purely for
consistency with prior Work Packages would have been unjustified
process overhead for a library with no lifecycle to demonstrate.

## 3. Choosing the starting catalogue's dimensions is where a real, unanticipated architectural gap surfaced

`WP7.0C`'s own contract review discussed Units & Quantities only at the
level of the generic `Quantity<TDimension>`/`Unit<TDimension>` shape —
no specific dimension was named. The moment this Work Package began
choosing concrete dimensions for the starting catalogue, Temperature's
affine-conversion incompatibility with `Unit<TDimension>`'s own
single-multiplicative-factor shape became apparent immediately — not a
subtle discovery, but one invisible from the contract review's own level
of abstraction. This generalises a lesson `WP 7.1A` already drew from a
different angle: a Contract Review conducted at the interface-signature
level cannot surface every gap a concrete-catalogue-level implementation
will find, and that is a property of doing contract review before any
concrete instantiation exists, not a quality failure of the review
itself.

## 4. The "same-`Unit`-only" arithmetic rule was easier to justify after implementing it than before

Before implementation, requiring `quantity1 + quantity2` to fail unless
both share the exact same `Unit` (not merely the same dimension) seemed
like it might be an inconvenient, overly strict design. After writing
the tests, it became clear this rule is what makes "never perform
implicit unit conversions" a real, checkable property rather than an
aspiration — a caller who wants to add 5 m and 500 cm must write
`.ConvertTo` explicitly, making the conversion visible in the code
itself, exactly the auditability this Work Package's own Design
Principles asked for.

## Recommendations

- **Candidate G (Materials) is the strongest next Work Package** — see
  `WP7.1B Engineering Foundation Impact Assessment.md` for the full
  reasoning.
- **Future Work Packages introducing a new `readonly record struct` with
  a hand-written (non-positional-record) constructor should add
  `[JsonConstructor]` proactively** if the type will ever be serialized
  — rather than discovering the gap the way this Work Package did, via
  a failing round-trip test.
- **A future Work Package resolving `FCR-0034` (affine unit conversion)
  should design it as an additive extension** (a second, parallel
  `AffineUnit<TDimension>` type, or an optional offset field defaulting
  to zero for every existing dimension) rather than retrofitting an
  offset into `Unit<TDimension>`'s own existing shape without
  confirming every existing dimension's own conversion arithmetic still
  behaves identically.

## Related Documents

`WP7.1B Implementation Report.md`; `WP7.1B Engineering Review
Report.md`; `ADR-0054`; `docs/academy/03 Work Packages/
WP7.1B-units-and-quantities-framework-implementation.md`.
