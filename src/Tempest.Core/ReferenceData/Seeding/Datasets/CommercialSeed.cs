using Tempest.Core.BusinessGovernance;
using Tempest.Core.CommercialIntelligence;
using Tempest.Core.CommercialIntelligence.Costs;
using Tempest.Core.CommercialIntelligence.LeadTimes;
using Tempest.Core.CommercialIntelligence.Suppliers;

namespace Tempest.Core.ReferenceData.Seeding.Datasets;

/// <summary>
/// Seed commercial records for the three suppliers this corpus was
/// actually sourced from.
/// </summary>
/// <remarks>
/// <para>
/// <b>Real suppliers, because they are the ones that supplied the data.</b>
/// Aalco, Proto Labs and RHD Bearings are here not because somebody went
/// looking for suppliers to list, but because the material, capability and
/// bearing records already cite their documents. That makes the supplier
/// records verifiable in the same breath as everything else, and it means
/// the commercial layer and the engineering layer are describing the same
/// world rather than two invented ones.
/// </para>
/// <para>
/// <b>No price is fabricated, and the one real price is dated.</b> The
/// only cost figure in this dataset is the injection-moulding tool price
/// the supplier publishes, and it is recorded as
/// <see cref="CostCertainty.Quoted"/> with the date it was observed and an
/// effective period that has already been stated to expire. A cost record
/// that cannot say when it was true is worse than no cost record, because
/// somebody will quote from it.
/// </para>
/// <para>
/// <b>Capability assurance stays at <see cref="CapabilityAssurance.Offered"/>.</b>
/// These suppliers say they can do these things. Nobody at Tempest has
/// bought anything from them, audited them, or seen a part. "Offered" is
/// the whole of what is known, and the enumeration exists precisely so
/// that it cannot quietly become "Proven".
/// </para>
/// </remarks>
public sealed class CommercialSeed
{
    /// <summary>The supplier reference for the metals stockholder.</summary>
    public const string AalcoReference = "SUP-AALCO";

    /// <summary>The supplier reference for the digital manufacturing service.</summary>
    public const string ProtolabsReference = "SUP-PROTOLABS";

    /// <summary>The supplier reference for the bearing manufacturer.</summary>
    public const string RhdReference = "SUP-RHD";

    /// <summary>The identity of the injection moulding tooling cost record.</summary>
    public const string MouldToolingCost = "cost-protolabs-mould-tooling";

    /// <summary>The identity of the machining lead time record.</summary>
    public const string MachiningLeadTime = "lead-protolabs-cnc-machining";

    /// <summary>The identity of the sheet metal lead time record.</summary>
    public const string SheetMetalLeadTime = "lead-protolabs-sheet-metal";

    /// <summary>The suppliers.</summary>
    public static IReferenceSeed<SupplierRecord> Suppliers { get; } = new SupplierSeed();

    /// <summary>The process costs.</summary>
    public static IReferenceSeed<ProcessCostRecord> Costs { get; } = new CostSeed();

    /// <summary>The lead times.</summary>
    public static IReferenceSeed<LeadTimeRecord> LeadTimes { get; } = new LeadTimeSeed();

    private static CommercialSource ObservedAtRetrieval() =>
        new(ObservedOn: SeedSources.RetrievedOn);

    private sealed class SupplierSeed : IReferenceSeed<SupplierRecord>
    {
        public string DatasetName => "Suppliers the seed corpus was sourced from";

        public int DatasetRevision => 1;

        public IReadOnlyList<ReferenceSeedRecord<SupplierRecord>> Records { get; } =
        [
            new("sup-aalco",
                new SupplierRecord
                {
                    Identity = new SupplierIdentity
                    {
                        Reference = AalcoReference,
                        LegalName = "Aalco Metals Limited",
                        RegistrationCountry = "GB",
                        Confidence = IdentityConfidence.NotAssessed,
                    },
                    Status = SupplierStatus.Prospective,
                    StatusReason = "Named as the publisher of datasheets this corpus cites. No trading "
                        + "relationship exists and none is implied.",
                    Category = "Metals stockholder",
                    Capabilities =
                    [
                        new SupplierCapability(
                            "CAP-AALCO-STOCK",
                            "Stockholds and supplies stainless steel, aluminium and copper alloy bar, sheet "
                            + "and plate, and publishes technical datasheets for the grades it carries.",
                            CapabilityAssurance.Offered,
                            MaterialRecordIds:
                            [
                                MaterialSeed.Stainless1Point4301,
                                MaterialSeed.Stainless1Point4404,
                                MaterialSeed.Aluminium6082T6,
                                MaterialSeed.Aluminium5083OH111,
                                MaterialSeed.CopperCw004A,
                            ],
                            Limits: ["Product forms and size ranges are those the cited datasheets state."]),
                    ],
                    Source = new CommercialSource(ObservedOn: SeedSources.RetrievedOn),
                    Notes = "Recorded because five material records cite this supplier's datasheets. Nothing "
                        + "about pricing, availability, terms or performance is known.",
                },
                SeedSources.Aalco("Company and datasheet library", "Publisher attribution on the datasheets cited")),

            new("sup-protolabs",
                new SupplierRecord
                {
                    Identity = new SupplierIdentity
                    {
                        Reference = ProtolabsReference,
                        LegalName = "Proto Labs, Inc.",
                        RegistrationCountry = "US",
                        Confidence = IdentityConfidence.NotAssessed,
                    },
                    Status = SupplierStatus.Prospective,
                    StatusReason = "Named as the publisher of the manufacturing capability data this corpus "
                        + "cites. No trading relationship exists.",
                    Category = "Digital manufacturing service",
                    Capabilities =
                    [
                        new SupplierCapability(
                            "CAP-PL-MILLING",
                            "CNC milling to a 559 mm x 356 mm x 95.3 mm factory envelope.",
                            CapabilityAssurance.Offered,
                            ProcessRecordId: ProcessSeed.CncMilling,
                            Limits: ["Unmarked dimensions held to ISO 2768-1-1989-f."]),
                        new SupplierCapability(
                            "CAP-PL-TURNING",
                            "CNC turning to 100.33 mm diameter and 228.6 mm length in the factory route.",
                            CapabilityAssurance.Offered,
                            ProcessRecordId: ProcessSeed.CncTurning),
                        new SupplierCapability(
                            "CAP-PL-SHEET",
                            "Sheet metal fabrication in 0.61 mm to 6.35 mm material.",
                            CapabilityAssurance.Offered,
                            ProcessRecordId: ProcessSeed.SheetMetalFabrication),
                        new SupplierCapability(
                            "CAP-PL-MOULDING",
                            "Plastic injection moulding to a 480 mm x 751 mm x 203 mm envelope, with no "
                            + "minimum order quantity.",
                            CapabilityAssurance.Offered,
                            ProcessRecordId: ProcessSeed.InjectionMoulding),
                    ],
                    TradingCurrency = new CurrencyCode("USD"),
                    Source = new CommercialSource(ObservedOn: SeedSources.RetrievedOn),
                    Notes = "The trading currency is inferred from the supplier quoting its tooling price in "
                        + "US dollars, and from nothing else; it is not a statement that this is the currency "
                        + "a UK buyer would transact in.",
                },
                SeedSources.Protolabs("Service capability pages", "Company attribution and service listings")),

            new("sup-rhd",
                new SupplierRecord
                {
                    Identity = new SupplierIdentity
                    {
                        Reference = RhdReference,
                        LegalName = "RHD Bearings",
                        RegistrationCountry = "IN",
                        Confidence = IdentityConfidence.NotAssessed,
                    },
                    Status = SupplierStatus.Prospective,
                    StatusReason = "Named as the publisher of the bearing specifications this corpus cites.",
                    Category = "Bearing manufacturer",
                    Capabilities =
                    [
                        new SupplierCapability(
                            "CAP-RHD-DGBB",
                            "Manufactures single-row deep groove ball bearings in SAE52100 steel to ISO 15 "
                            + "boundary dimensions.",
                            CapabilityAssurance.Offered,
                            Limits: ["Only the 6205 and 6305 sizes are recorded in this corpus."]),
                    ],
                    Source = new CommercialSource(ObservedOn: SeedSources.RetrievedOn),
                    Notes = "The registration country is taken from the supplier's own stated location and is "
                        + "not corroborated by a company register.",
                },
                SeedSources.RhdBearings("Company and product specification pages", "Publisher attribution")),
        ];
    }

    private sealed class CostSeed : IReferenceSeed<ProcessCostRecord>
    {
        public string DatasetName => "Published process costs";

        public int DatasetRevision => 1;

        public IReadOnlyList<ReferenceSeedRecord<ProcessCostRecord>> Records { get; } =
        [
            new(MouldToolingCost,
                new ProcessCostRecord
                {
                    Reference = "COST-PL-MOULD-TOOL",
                    Description = "Starting price for an injection moulding tool, as the supplier publishes it.",
                    Basis = CostBasis.OneOff,
                    Cost = CostFigure.Quoted(new Money(1495m, new CurrencyCode("USD"))),
                    ToolingCost = CostFigure.Quoted(new Money(1495m, new CurrencyCode("USD"))),
                    Applicability = new CommercialApplicability
                    {
                        ProcessRecordId = ProcessSeed.InjectionMoulding,
                        SupplierReference = ProtolabsReference,

                        // A tool is bought once, whatever the run length,
                        // and this supplier imposes no minimum order — so
                        // the band is genuinely open above one rather than
                        // a placeholder chosen to satisfy the rule.
                        Quantities = QuantityBand.From(1),
                        Conditions =
                        [
                            "The cost is per tool and does not vary with the number of parts moulded from it, "
                            + "which is exactly why it dominates the economics of a short run.",
                            "A starting price, not a quotation. The tool for any particular part costs more "
                            + "than this unless that part is at the simplest end of the supplier's range.",
                            "Published as a US dollar figure with no stated validity period.",
                        ],
                    },
                    Source = new CommercialSource(ObservedOn: SeedSources.RetrievedOn),
                    Notes = "The only price in this corpus. It is time-sensitive and carries the date it was "
                        + "observed; a consumer must treat it as evidence of the order of magnitude of tooling "
                        + "cost and must re-obtain a quotation before relying on it commercially. Nothing here "
                        + "asserts the figure is still current.",
                },
                SeedSources.Protolabs("Injection Molding Services for Custom Parts", "Stated mould cost")),
        ];
    }

    private sealed class LeadTimeSeed : IReferenceSeed<LeadTimeRecord>
    {
        public string DatasetName => "Published manufacturing lead times";

        public int DatasetRevision => 1;

        public IReadOnlyList<ReferenceSeedRecord<LeadTimeRecord>> Records { get; } =
        [
            new(MachiningLeadTime,
                new LeadTimeRecord
                {
                    Reference = "LEAD-PL-CNC",
                    Description = "Quoted lead time for CNC machined prototypes and production parts.",
                    Kind = LeadTimeKind.Estimated,
                    Typical = LeadTimeDuration.WorkingDays(4m),
                    Minimum = LeadTimeDuration.WorkingDays(1m),
                    Applicability = new CommercialApplicability
                    {
                        ProcessRecordId = ProcessSeed.CncMilling,
                        SupplierReference = ProtolabsReference,
                        Conditions = ["The one-day figure is the supplier's fastest advertised turnaround, not "
                            + "a commitment for an arbitrary part."],
                    },
                    Source = new CommercialSource(ObservedOn: SeedSources.RetrievedOn),
                    Assumptions = ["The part falls within the published machining envelope."],
                    Excludes = ["Carriage.", "Any post-processing or coating operation."],
                    Commentary = "Recorded as Estimated rather than Historical because Tempest has placed no "
                        + "orders with this supplier and has observed no actual delivery.",
                    SourceDocumentReference = "Proto Labs CNC machining service page",
                },
                SeedSources.Protolabs("Online CNC Machining Service | Get a Quote", "Stated lead times")),

            new(SheetMetalLeadTime,
                new LeadTimeRecord
                {
                    Reference = "LEAD-PL-SHEET",
                    Description = "Quoted lead time for sheet metal components and assemblies.",
                    Kind = LeadTimeKind.Estimated,
                    Typical = LeadTimeDuration.WorkingDays(5m),
                    Minimum = LeadTimeDuration.WorkingDays(1m),
                    Maximum = LeadTimeDuration.WorkingDays(5m),
                    Applicability = new CommercialApplicability
                    {
                        ProcessRecordId = ProcessSeed.SheetMetalFabrication,
                        SupplierReference = ProtolabsReference,
                        Conditions =
                        [
                            "The three-day route is limited to material 3.175 mm and under and to no more "
                            + "than 12 bends; the five-day route removes both limits.",
                        ],
                    },
                    Source = new CommercialSource(ObservedOn: SeedSources.RetrievedOn),
                    Assumptions = ["The design falls within the published thickness range."],
                    Excludes = ["Carriage.", "Finishing."],
                    Commentary = "The lead time and the geometry limits move together, which is the point of "
                        + "recording them against the same process: choosing the faster route constrains the "
                        + "design, and the design constrains the achievable route.",
                    SourceDocumentReference = "Proto Labs sheet metal fabrication service page",
                },
                SeedSources.Protolabs("Online Custom Sheet Metal Fabrication Service", "Stated lead times and route limits")),
        ];
    }
}
