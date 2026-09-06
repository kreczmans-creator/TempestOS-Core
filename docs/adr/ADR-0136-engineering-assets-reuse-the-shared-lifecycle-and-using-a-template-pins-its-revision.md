# ADR-0136: Engineering Assets Reuse the Shared Lifecycle, and Using a Template Pins Its Revision

## Status

Accepted — `Group E` (P05 Engineering Assets), 2026-09-06.

## Context

`P05` holds five kinds of reusable engineering artefact: templates,
calculation packs, verification artefacts, design review packs and
technical documents. Each is authored by somebody, reviewed, released,
and eventually replaced — the lifecycle `ADR-0126` built for `Group A`,
reused by `ADR-0129` for `Group C` and `ADR-0132` for `Group D`. A fourth
implementation would be the fourth place a supersession bug has to be
fixed.

The harder question is templates. A template exists to be used many times,
and the thing produced from it outlives the template. When a template is
revised — a section added, a field made mandatory — what happens to work
done from the old one?

Three answers are available and two are wrong. **Migrate silently**: the
old work now claims a structure it never had. **Freeze the template**: the
organisation can never improve it. **Record what was used**: the work
keeps pointing at the revision it was actually produced from, and
improving the template affects only what comes next.

There is also a question of what ships. It is tempting to populate the
template library with the consultancy's own document structures, so the
platform arrives useful. Those structures are the organisation's own
intellectual property and change with its practice; baking them into a
platform release makes them a thing to be upgraded rather than edited.

## Decision

**One lifecycle.** Every `P05` library derives from
`ReferenceDataCatalog<TDefinition>` and uses `ReferenceValidationState`
unchanged. Released assets are immutable; corrections are revisions and
replacements are supersessions.

**A second, orthogonal axis: `AssetStanding`** — `Invalid`, `Incomplete`,
`Unverified`, `Stale`, `Verified`, `NotApplicable`, `Superseded`. The
lifecycle says how far the record got through governance; this says
whether the asset is fit to be used. A Released template whose effective
period ended last year is governed impeccably and stale in substance.

**Shared governance facts, composed not inherited.**
`AssetGovernanceFacts` carries ownership, authorship, reviews, approval,
classification and evidence. The five asset kinds share these facts and
share no hierarchy, so a base class would impose a structure the domain
does not have.

**Ownership is separate from authorship**, on `WP 9.1A`'s reasoning for
requirements: ownership changes, authorship never does. **Review is
separate from approval**: a reviewer says the work is sound, an approver
commits the organisation, and the second is a
`BusinessAuthorisation` a named person constructs.

**Using a template pins it.** `TemplateUsage` records a `ReferencePin` —
library, record, revision — naming what was actually worked from.
`ITemplateCatalog.PinAsync` takes the revision from the record rather than
from the caller, so a pin naming a revision nobody read is not
constructible. `TemplateUsage.IsBehind` reports that a usage is older than
the current revision and does nothing about it: whether to redo the work
is an engineering judgement.

**`AssetApplicability` reads absence as "unrestricted"** — the opposite of
`P03`'s `CommercialApplicability`, deliberately. A price that names no
supplier applies to no supplier in particular; a review checklist that
names no discipline applies to all of them. Absence means "unknown" in one
domain and "unrestricted" in the other, and one convention for both would
be wrong somewhere.

**No template ships.** `P05` provides the mechanism; the templates an
organisation uses are its own.

## Consequences

**Work done from an old template stays correct about what it used**, and
the library can be improved without rewriting history. The cost is that
somebody must ask which work is now behind, which
`TemplateUsage.IsBehind` makes answerable and does not answer.

**A `P05` asset can be Released and unusable**, and the model can say so
without contradiction.

**The template library is empty on delivery.** A user's first task is to
author their own structures, which is more work than a shipped set and
produces templates that are theirs.

**Two applicability conventions exist in the platform**, which is a real
cost. It is paid because the alternative — one convention, wrong in one
domain — costs more, and the asymmetry is documented on both types.
