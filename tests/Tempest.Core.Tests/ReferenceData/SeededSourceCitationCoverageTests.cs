using Tempest.Core.ReferenceData.Seeding.Datasets;

namespace Tempest.Core.Tests.ReferenceData;

/// <summary>
/// Pins the per-library count of seeded records with and without a
/// <see cref="Tempest.Core.ReferenceData.SourceCitation"/> — the table
/// `WP 18.0B`'s own closing report names. Adding a seed record legitimately
/// changes these numbers; a change here is expected to accompany a dataset
/// change, not a signal something broke, exactly as
/// `Population/SeedDatasetTests.EveryDataset_RegistersEveryRecordItOffers`'s
/// own comment already explains for record counts generally.
/// </summary>
public class SeededSourceCitationCoverageTests
{
    [Fact]
    public void Materials_EveryRecord_CarriesACitation()
    {
        AssertCoverage(MaterialSeed.Instance.Records, withSource: 6, withoutSource: 0);
    }

    [Fact]
    public void Fasteners_EveryRecord_CarriesACitation()
    {
        AssertCoverage(FastenerSeed.Instance.Records, withSource: 7, withoutSource: 0);
    }

    [Fact]
    public void Bearings_EveryRecord_CarriesACitation()
    {
        AssertCoverage(BearingSeed.Instance.Records, withSource: 2, withoutSource: 0);
    }

    [Fact]
    public void Standards_EveryRecord_CarriesACitation()
    {
        AssertCoverage(StandardSeed.Instance.Records, withSource: 14, withoutSource: 0);
    }

    [Fact]
    public void Constants_EveryRecord_CarriesACitation()
    {
        AssertCoverage(ConstantSeed.Instance.Records, withSource: 12, withoutSource: 0);
    }

    private static void AssertCoverage<TDefinition>(
        IReadOnlyList<Tempest.Core.ReferenceData.Seeding.ReferenceSeedRecord<TDefinition>> records,
        int withSource,
        int withoutSource)
        where TDefinition : class
    {
        var actualWithSource = records.Count(r => r.Source is not null);
        var actualWithoutSource = records.Count(r => r.Source is null);

        Assert.Equal(withSource, actualWithSource);
        Assert.Equal(withoutSource, actualWithoutSource);
        Assert.Equal(withSource + withoutSource, records.Count);
    }
}
