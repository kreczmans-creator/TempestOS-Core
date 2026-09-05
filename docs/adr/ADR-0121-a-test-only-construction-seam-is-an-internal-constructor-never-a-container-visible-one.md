# ADR-0121: A Test-Only Construction Seam Is an `internal` Constructor, Never a Container-Visible One

## Status

Accepted — `WP 16.4B-R1` (Architecture), 2026-09-05. Records a decision
taken during `WP 16.3B` and established under Technical Review at that
time; written here after the `v0.16.0` review board found it undocumented,
which Engineering Governance §5 criterion 5 does not permit. Extends
`ADR-0120` (durable state carries a schema version) without reopening it,
and depends on `ADR-0009`'s composition-root discipline. Serves the
`v1.0.0` scope `D-021` (**Proposed**, `WP 16.0A`) governs.

## Context

`ADR-0120` requires `EngineeringObjectStateStore`'s read path to migrate a
record until it reaches the current schema version, and to log and skip
any record that cannot get there. Testing that behaviour honestly requires
a store whose read path targets a schema version **other** than this
build's `CurrentSchemaVersion` — otherwise the only way to exercise a
multi-step chain is to bump a production constant from a test, which makes
the constant meaningless.

So the store needs a construction seam that production does not use.

`WP 16.3B`'s first attempt at that seam was rejected at Technical Review.
It had satisfied the new optional `targetSchemaVersion` parameter by
registering it in the container:

```csharp
services.AddInstance(typeof(int?), someValue);
```

That is a container-wide registration of `int?`. Any other type that ever
declared an `int?` constructor parameter would silently receive this
value. It solved a test's problem by making a global change to the
platform's own dependency graph, and it was rejected on that basis.

The constraint that shapes the alternative is specific to this project's
own container: `TempestServiceProvider.Construct` calls
`Type.GetConstructors()`, which returns **public** constructors only, and
requires **exactly one** — more than one throws
`AmbiguousConstructorException`. So a second public constructor is not a
neutral addition; it breaks resolution of the type entirely.

## Decision

**A construction seam that exists for tests is declared `internal`, and
the single public constructor delegates to it.**

Concretely, for `EngineeringObjectStateStore`:

- one **public** constructor carrying the production parameters, which
  delegates to the internal one passing `CurrentSchemaVersion`;
- one **`internal`** constructor taking the additional
  `targetSchemaVersion`, reachable from `Tempest.Core.Tests` through the
  existing `InternalsVisibleTo` and from nowhere else.

The container sees exactly one public constructor and resolves the type
normally. The test seam exists, is typed, and is invisible to the
container by construction rather than by convention.

**The general rule this sets:** where a type needs a construction path
that only tests use, express it as an `internal` constructor plus
`InternalsVisibleTo`. Do not add a second public constructor (the
container refuses the type), and do not register a test's value in the
container to reach it (it changes the platform's dependency graph for
every other consumer of that type).

## Consequences

- The seam is compile-time typed and scoped to one assembly. It cannot be
  reached from `Tempest.App`, `Tempest.Desktop`, a sample, or a plugin.
- The container's one-public-constructor contract is preserved, so no
  change to `TempestServiceProvider` was needed to support this.
- `InternalsVisibleTo` is load-bearing. If it were ever removed, this
  pattern's tests stop compiling — a loud failure, not a silent one, which
  is the correct direction.
- The pattern costs one extra constructor and a delegation. That is the
  whole cost, and it is paid once per type that needs a seam.
- **This decision has a foreseeable end.** `WP 16.4B` (the same release)
  taught `TempestServiceProvider.Construct` to honour
  `ParameterInfo.HasDefaultValue`, so a single public constructor with a
  defaulted `int targetSchemaVersion = CurrentSchemaVersion` would now
  resolve correctly and would need no seam at all. That was not true when
  `WP 16.3B` made this decision, and it is recorded here rather than left
  implicit: a future Work Package may collapse the two constructors into
  one, and should, if nothing else has come to depend on the internal one.
  This ADR is then superseded rather than violated.

## Alternatives Considered

**Register the value in the container** (`services.AddInstance(typeof(int?), …)`).
Rejected at Technical Review. It is a global change to satisfy a local
need: the registration is keyed on `int?`, matches any consumer declaring
that parameter type, and would be found only by whoever eventually
debugged the collision.

**A second public constructor.** Rejected on a hard technical constraint,
not taste: `TempestServiceProvider.Construct` requires exactly one public
constructor and throws `AmbiguousConstructorException` otherwise, so this
would have made the type unresolvable.

**A settable property or a static mutable default.** Rejected. Both make
the store's target version mutable after construction, introduce shared
state between tests, and would have to be reset between them — the exact
class of hidden coupling `WP 16.4A` spent a Work Package removing from
this test suite.

**Bump `CurrentSchemaVersion` in tests.** Rejected: it makes the
production constant meaningless and couples every test to a value the
production read path also depends on.

**No seam; test through a real multi-version migration.** Rejected as
premature — it requires inventing a second real schema version purely to
have something to migrate to, committing the production model to a shape
no product need has yet asked for.

## Future Considerations

- Collapse to one public constructor with a defaulted parameter once
  `WP 16.4B`'s `HasDefaultValue` support is settled, superseding this ADR.
- If a second type needs the same seam before then, apply this pattern
  rather than inventing a third approach, and cite this ADR.

## Related Documents

`docs/adr/ADR-0120-durable-state-carries-a-schema-version-and-migrations-apply-only-on-read.md`;
`docs/adr/ADR-0009` (composition root);
`docs/architecture/State Schema Versioning Architecture.md`;
`docs/releases/v0.16.0/WP16.3B Schema Versioning Implementation Report.md`;
`docs/releases/v0.16.0/WP16.4B-2 Platform Hygiene.md`
(`TempestServiceProvider`'s `HasDefaultValue` support);
`src/Tempest.Core/EngineeringDomain/Implementation/EngineeringObjectStateStore.cs`;
`src/Tempest.Core/DependencyInjection/TempestServiceProvider.cs`;
`docs/academy/06 Engineering Standards/Engineering Governance.md` §5, §9.
