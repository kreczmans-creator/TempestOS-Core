# Frozen Layers: Plugin Trust, REST, Licensing and P02–P07

**Release:** `v0.17.0`, `v0.18.0` (precursor: `v0.16.0`) · **Work
Package(s):** `WP 16.4B-1`, `WP 17.2A` part 1, `WP 18.0C` · **Decision:**
`D-024`, `ADR-0146`, `D-028` · **Debt:** `TD-62` (Resolved, `WP 16.4B`),
`TD-129` (Archived with the layer) · **Code:** `src/Frozen/`, `tests/Frozen/`

**In plain terms.** Three sizeable pieces of TempestOS — letting outside
plugins run inside it, answering requests from other programs over a network,
checking a paid licence — and six large areas of engineering and business
reasoning were built, tested, and then switched off so completely that they no
longer even compile. None of it was deleted; it sits in the repository, in a
folder called `src/Frozen/`, exactly as it was on the day it stopped shipping,
and nothing a user of TempestOS touches can reach it. It is kept rather than
thrown away because a paying client might one day ask for exactly one of these
things, and picking a reviewed design back up costs less than inventing it
again. This chapter is about why a team deliberately stops maintaining code
that already works.

## Three different things that sound like the same thing

"We got rid of it" can mean three different decisions. **Delete** means gone:
`ApiSampleModule` and `LicensingSampleModule` were deleted outright by `WP
17.2A`, because a sample demonstrates a capability, and a capability nothing
can reach has nothing left to demonstrate. **Archive** moves *documents*
somewhere clearly marked as history — `WP 17.0B` moved 807 governance files
into `archive/docs-2026-09/`, so a reader knows not to treat them as current.
**Freeze** is a third thing, for working, tested *code*: it stays exactly
where a reader would look for it, a sibling of `src/Tempest.Core/`, but cut
out of the build so it cannot rot what still ships. `src/Frozen/README.md`
states the test plainly — frozen code "compiles only against the
`Tempest.Core` of the commit it was frozen at… every later change… makes it
drift further, and that drift is expected, not a defect to fix." Nobody runs
its tests; until a client asks for what it does, maintaining it buys nothing.

## The precursor: turn it off before you take it out

`v0.16.0` did the cautious version of this first. The REST listener went from
"on whenever the Host starts" to "off unless an operator says otherwise"
(`D-024`, `WP 16.4B-1`, commit `d2f2de3`):

```csharp
var enabled = _configuration.TryGetValue(EnabledConfigurationKey, out var rawEnabled)
    && bool.TryParse(rawEnabled, out var parsedEnabled)
    && parsedEnabled;

if (!enabled)
{
    _logger?.Information($"REST API is disabled (default). ...");
    return;
}
```

The check runs before a single ASP.NET Core object exists — no port is ever
bound unless someone opts in. The same change closed `TD-62` by routing the
API's self-describing route list through the same identity and permission
check as every other command. `TD-129` — a wrong route and a right-but-refused
one still return different status codes, letting a caller map the API by trial
— was looked at and left open on purpose: changing a status code is a product
decision, and the exposure was already nil with the listener off. Reduce
exposure first, decide whether to keep the capability second — the shape the
freeze would repeat at scale.

## `WP 17.2A`: the freeze test, and what it cost elsewhere

`ADR-0146` names the test a capability fails before it is worth freezing:
**unreachable from any shipped surface.** `v1.0.0`'s own re-scope calls
TempestOS "a single-user, locally-trusted desktop system of record for a small
engineering consultancy." Nobody ships it a third-party plugin, nobody calls
its REST API from outside its own sample, nobody has ever been handed a
licence file. Plugin trust (signing, a certificate store, trust tiers,
capability enforcement — `Tempest.Core.Plugins`), the REST API
(`Tempest.Core.Api`) and Licensing (`Tempest.Core.Licensing`) had all been
properly built for requirements the product no longer has: 2,045 lines of
plugin-trust production code alone, and roughly 10.7K lines of tests,
adjudicating trust for plugins that do not exist.

The code was not the expensive part. Seven core types — `EventBus`,
`CommandRegistry`, `CommandHandlerTable`, `NavigationService`,
`IdentityService`, `ModuleLifecycleManager` and `HostedServiceManager`, plus
`ReflectionFrameworkDiscoveryService` — each carried an extra, optional
constructor parameter that silently reproduced the old, unguarded behaviour
whenever a caller left it out. `ADR-0146` names this plainly: "a constructor
with an optional trailing collaborator that silently no-ops when omitted is a
footgun." Commits `73dc2cb`/`9b5a120` removed all seven parameters and the
ownership dictionaries behind them — about 1,000 lines — into
`src/Frozen/Tempest.Core.{Api,Licensing,Plugins}` (41 production files) and
`tests/Frozen/` (37 test files). `TempestHost` stopped constructing a licence
validator before its own logger existed, stopped loading plugin assemblies,
and stopped registering `IApiEndpointRegistry`.

One thing was deliberately kept out of the freeze. **Plugin manifest
discovery** — reading what sits in the plugin drop folder, parsing and
validating its manifest — stays live in `src/Tempest.Core/Plugins`, because it
"has no attack surface of its own": nothing it finds is loaded, signed or
enforced, only recorded. A future plugin capability would be rebuilt on it
rather than rediscover it. `07-plugin-architecture.md` describes the trust
design as it stood before this freeze; this chapter is what happened to it
once the client it was built for never arrived.

## Then the freeze test found six more

The next day, `v0.18.0` asked the same question of a larger target. On the
second Windows smoke test of `v0.17.0`, the Product Owner asked whether
calculations belonged in TempestOS at all, "or should they be done in separate
software… and the evidence then loaded into Tempest as a record of that?" —
warning, the same day, "we need to be very careful here not to reinvent the
system as an ERP system or a PLM system." The answer, `D-028` (2026-09-09):
TempestOS is a client project system of record that evidence is tagged to;
nothing new computes inside it in `v1.0`. That re-scoped `v0.18.0` before the
abandoned plan was built, so `WP 18.0C` applied `ADR-0146`'s own test to six
programmes the new scope no longer needed, none of them read by any surface
that ships:

- **`EngineeringIntelligence` (P02)** — material selection, decision trees,
  design rules, review logic, trade-off reasoning. Froze in full.
- **`CommercialIntelligence` (P03)** — what a job costs, who could make it,
  how long it takes, which supplier to use. Froze in full.
- **`BusinessOperations` (P04)** — who the firm deals with, what it has
  committed to spend, ordered, and must keep. Kept `Organisation`, `Contact`,
  `Budget`; the rest froze.
- **`EngineeringAssets` (P05)** — the reusable artefacts engineering work
  produces: templates, calculation packs, verification, design reviews,
  technical documentation. Kept `Templates`, `CalculationPacks`,
  `Verification` (still read by the dormant calculation workbench); Design
  Reviews and Technical Documentation froze.
- **`Knowledge` (P06)** — what the organisation knows, cited on every entry.
  Froze in full.
- **`BusinessGovernance` (P07)** — obligations, risk, permitted use,
  pricing, financial position, opportunities, capacity. Kept `Money`,
  `CurrencyCode`, `EffectivePeriod`, `RateCard`, needed to price and tag
  evidence; the rest froze.

Six commits (`9d50bac`, `7c97bcd`, `26e3652`, `18091a6`, `1dc4f29`, `15721be`)
moved **32,308 lines out of `Tempest.Core`** — 85,151 down to 52,843 — into
`src/Frozen/Tempest.Core.<Namespace>`, tests the same way into
`tests/Frozen/`. A further 4,610 lines of host-registration tests and 1,572
lines of an integration/population test cluster moved too, not named after a
frozen namespace but built only to prove the reasoning chain now frozen.

## "Not optional" turned out to be a pattern

Three separate times, the same discovery repeated: a namespace's shared root
file — the governance or validation logic every kind in it calls — was not
something the kept kind could do without. `BusinessGovernance`'s kept
`RateCard` types its `Governance` as `BusinessGovernanceFacts`, reaching into
four root files nobody's brief had named, traced by hand, grep by grep. The
commit doing this (`1dc4f29`) calls it "the third time this exact shape has
appeared in this Work Package" — `EngineeringAssets` and `BusinessOperations`
needed the identical rescue first. Five files were split rather than moved
whole because one mixed a kept kind with an archived one (`Organisation.cs`,
`CrmCatalogs.cs`, `FinanceCatalogs.cs`, the `EngineeringAssetSeed.cs` seed
data); one check, `CrmValidationRules.SupplierRecordMustResolve`, was retired
rather than moved, since it checked a table gone once `CommercialIntelligence`
is frozen.

Every governing ADR from `ADR-0127` to `ADR-0142` was updated the same day:
ten (the three namespaces frozen in full) marked **Frozen** outright, four
**amended in place** because their subject only partly moved, and two more
needed no real change, since their whole subject was kept. `ADR-0146`'s own
answer to "how does this come back" is not "delete `src/Frozen/` and start
over" — that would throw away reviewed, working design. It is: re-add the
folder to `src/TempestOS.slnx` as a real project, re-point it at whatever
`Tempest.Core` has become by then, and re-review it against that — putting the
seven removed constructor seams back deliberately, rather than resurrecting
them as forgotten parameters nobody remembers the reason for.

## The honest trade-off

The design-freeze review behind both decisions puts the whole arc in one
sentence: TempestOS "was built as a platform first and became a product in its
fifth phase." Module discovery, a plugin system, a REST surface, a licence
gate, six programmes of reasoning — all "designed, implemented, tested and
documented" before there was a paying product to put any of it under. None of
that was wasted engineering; it is simply unreachable. `D-028` says plainly
what the product turned out to be instead: "TempestOS `v1.0` is a client
project system of record for an engineering consultancy, that evidence is
tagged to," not an ERP, not a PLM. `59-evidence.md` covers what `v0.18.0`
built in the space this freed; the case study `07-the-design-freeze-review.md`
covers the review itself in full.

Freezing did not claim more than it delivered — `BACKLOG.md` still lists
`TD-161`/`TD-162` as things `WP 18.0C` claims but does not close, since
`EngineeringTraceRegister` and `BracketEngineeringRecordService` still ship
outside `src/Frozen/`. Nor did it touch part 2 of `WP 17.2A` — Configuration,
Logging, Identity and Audit
(`55-configuration-logging-and-the-session-principal.md`), built *because* the
freeze made the platform simple enough to change safely.

## What to take away

- **Unreachable is not the same as wrong.** Code can be correctly designed,
  reviewed and fully tested, and still cost more to keep running than it
  returns, if nothing that ships ever calls it.
- **Freezing is a bet on the future, not a verdict on the past.** Keeping
  the code only pays off if someone re-reviews it against a live codebase
  before trusting it again — a frozen file is a starting point, not a finished
  product.
- **A capability's real cost is rarely the capability itself.** The
  plugin-trust code was cheap to remove; the seven optional constructor
  parameters it left scattered through the platform's core services were the
  part actually costing something, every day, to every engineer who read past
  them.
