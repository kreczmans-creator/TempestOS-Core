using Tempest.Calc.Import.Scan;

namespace Tempest.Calc.Tests.Import;

/// <summary>
/// W2 statistics against figures computed independently in python3 (the standard library only: math.fsum, math.erfc, the published
/// Anderson–Darling formula and its D'Agostino–Stephens p-value) and, for the first case, by hand: the ten values below sum to 100.03, so the
/// mean is 10.003, the squared deviations sum to 0.00321, s = √(0.00321/9) = 0.0188856…, Cp = Pp = 0.1/(6s) = 0.8825…, Cpk = Ppk = 0.077/(3s) = 0.8295….
/// </summary>
public sealed class ScanStatisticsTests
{
    private static readonly double[] Ten = [10.02, 9.98, 10.01, 10.03, 9.97, 10.00, 10.02, 9.99, 10.01, 10.00];

    private static IReadOnlyList<ScanObservation> Obs(IEnumerable<double> values, double? nominal = null, double? lsl = null, double? usl = null, string stage = "", bool serials = true, int file = 0) =>
        values.Select((v, i) => new ScanObservation(file, i + 2, "F", stage, serials ? $"S{i + 1:000}" : string.Empty, nominal, v, lsl, usl, false)).ToList();

    private static readonly IReadOnlyList<ScanFileInfo> OneFile = [new("a.csv", new string('a', 64), null, null, 10, 10)];

    private static ScanFeatureStats Compute(IEnumerable<double> values, ScanSettings? settings = null, double? nominal = 10, double? lsl = 9.95, double? usl = 10.05) =>
        ScanStatistics.Compute("F", string.Empty, Obs(values, nominal, lsl, usl), OneFile, settings ?? new ScanSettings());

    private static void Close(double expected, double? actual, double relative = 1e-12)
    {
        Assert.NotNull(actual);
        Assert.True(Math.Abs(expected - actual.Value) <= relative * Math.Max(1, Math.Abs(expected)), $"expected {expected:R}, got {actual:R}");
    }

    [Fact]
    public void Ten_Values_Match_The_Hand_Computed_Mean_Sigma_Cp_Cpk_Pp_Ppk()
    {
        var s = Compute(Ten);

        Assert.True(s.Computable);
        Assert.Equal(10, s.Count);
        Close(10.003, s.Mean);
        Close(0.018885620632286656, s.Sigma);
        Close(0.018885620632286656, s.SigmaWithin);
        Close(0.8825056370227913, s.Cp);
        Close(0.8295552988014225, s.Cpk);
        Close(0.8825056370227913, s.Pp);
        Close(0.8295552988014225, s.Ppk);
        Assert.Equal(9.97, s.Min);
        Assert.Equal(10.03, s.Max);
    }

    [Fact]
    public void With_No_Subgrouping_Cp_Equals_Pp_And_Cpk_Equals_Ppk()
    {
        var s = Compute(Ten);

        Assert.Equal(s.Cp, s.Pp);
        Assert.Equal(s.Cpk, s.Ppk);
        Assert.Equal(s.Sigma, s.SigmaWithin);

        // Nothing is pooled, so nothing is c4-corrected, and the output says so.
        Assert.Null(s.WithinC4);
        Assert.Equal("the overall σ (no subgrouping)", s.WithinBasis);
    }

    [Fact]
    public void C4_Matches_The_Published_Table_And_The_Gamma_Function()
    {
        // Published c4 (Montgomery, Introduction to Statistical Quality Control, appendix table): n = 2 0.7979, 5 0.9400, 10 0.9727; python3 math.lgamma to 10 places.
        Close(0.7978845608028651, ScanStatistics.C4(2), 1e-12);
        Close(0.9399856029866257, ScanStatistics.C4(5), 1e-12);
        Close(0.9726592741215869, ScanStatistics.C4(10), 1e-12);
        Close(0.9914180532926701, ScanStatistics.C4(30), 1e-12);
        Close(0.9997497811016857, ScanStatistics.C4(1000), 1e-11);
        Close(0.9999975000276928, ScanStatistics.C4(1e5), 1e-9);

        // ln Γ: Γ(1/2) = √π, Γ(5) = 24, Γ(20.5) from the recurrence and the series agree at the join.
        Close(0.5 * Math.Log(Math.PI), ScanStatistics.LogGamma(0.5), 1e-13);
        Close(Math.Log(24), ScanStatistics.LogGamma(5), 1e-13);
        Close(ScanStatistics.LogGamma(19.999999) + Math.Log(19.999999), ScanStatistics.LogGamma(20.999999), 1e-12);

        // c4 rises towards 1 and a pooled sigma of more degrees of freedom is corrected less.
        Assert.True(ScanStatistics.C4(9) < ScanStatistics.C4(25));
        Assert.True(ScanStatistics.C4(25) < 1);
        Assert.Throws<ArgumentOutOfRangeException>(() => ScanStatistics.C4(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => ScanStatistics.LogGamma(0));
    }

    [Fact]
    public void The_Statistics_Csv_Carries_The_C4_Used_For_The_Within_Sigma()
    {
        var pooled = ScanImportReport.StatisticsCsv([Compute(Ten, new ScanSettings { SubgroupSize = 5 })], "mm");
        var plain = ScanImportReport.StatisticsCsv([Compute(Ten)], "mm");

        Assert.Contains("sigma_within,within_c4,min", pooled, StringComparison.Ordinal);
        Assert.Contains("0.02063321905546", pooled, StringComparison.Ordinal);
        Assert.Contains("0.969310699713", pooled, StringComparison.Ordinal);
        Assert.Contains(",,9.97,", plain, StringComparison.Ordinal);
    }

    [Fact]
    public void Subgroups_Pool_The_Within_Sigma_And_Leave_The_Overall_Sigma_Alone()
    {
        // Two subgroups of five: pooled variance (0.00268 + 0.00052)/8 = 0.0004, pooled sigma exactly 0.02 on 8 degrees of freedom. It is divided by c4(d) with
        // d = 8 + 1 = 9 (python3, math.lgamma: c4(9) = 0.969310699713954), so the within sigma is 0.02063321905546036 and Cp = 0.2/(6 x 0.0206332) = 0.8077589164283239,
        // Cpk = 0.077/(3 x 0.0206332) = 0.7592933814426233 (uncorrected 0.8333 and 0.7833, 3.2 % optimistic).
        var s = Compute(Ten, new ScanSettings { SubgroupSize = 5 });

        Close(0.02063321905546036, s.SigmaWithin, 1e-12);
        Close(0.969310699713954, s.WithinC4, 1e-12);
        Close(0.8077589164283239, s.Cp, 1e-10);
        Close(0.7592933814426233, s.Cpk, 1e-10);
        Assert.Contains("c4 = 0.96931", s.WithinBasis, StringComparison.Ordinal);
        Close(0.8825056370227913, s.Pp);
        Close(0.8295552988014225, s.Ppk);
        Assert.NotEqual(s.Cp, s.Pp);

        // Subgroups of three: the last subgroup holds one value and is not used for the within sigma (and the warning says so).
        var three = Compute(Ten, new ScanSettings { SubgroupSize = 3 });
        // python3: pooled 0.022852182001336326 on 6 degrees of freedom, c4(7) = 0.959368788699833, within 0.023820018193740002.
        Close(0.023820018193740002, three.SigmaWithin, 1e-12);
        Close(0.959368788699833, three.WithinC4, 1e-12);
        Assert.Contains(three.Warnings, w => w.Contains("incomplete final subgroup", StringComparison.Ordinal));
    }

    [Fact]
    public void Anderson_Darling_Matches_The_Published_Formula_And_P_Value_Approximation()
    {
        var s = Compute(Ten);

        Assert.Equal(NormalityVerdict.Consistent, s.Normality.Verdict);
        Close(0.2084628110016915, s.Normality.A2, 1e-9);
        Close(0.2287879350743564, s.Normality.AStar2, 1e-9);
        Close(0.8106543998873604, s.Normality.PValue, 1e-9);
    }

    [Fact]
    public void A_Bimodal_Sample_Is_Rejected_As_Normal_With_A_Warning()
    {
        var values = Enumerable.Repeat(9.90, 10).Concat(Enumerable.Repeat(10.10, 10)).ToArray();
        var s = Compute(values, lsl: 9.8, usl: 10.2);

        Assert.Equal(NormalityVerdict.Rejected, s.Normality.Verdict);
        Close(3.4312722970674585, s.Normality.A2, 1e-9);
        Close(3.5792459148784928, s.Normality.AStar2, 1e-9);
        Close(6.1801066799085835e-09, s.Normality.PValue, 1e-6);
        Assert.Contains(s.Warnings, w => w.Contains("normal distribution is rejected", StringComparison.Ordinal));
    }

    [Fact]
    public void The_Far_Tail_Of_The_Normal_Is_Accurate_So_A_Gross_Outlier_Gives_The_Published_Statistic()
    {
        // One value 8 against a spread of about 0.1: z ≈ 3.3 at the top, and the ln Φ terms live in the tail.
        double[] values = [0.0, 0.1, -0.1, 0.05, -0.05, 0.02, -0.02, 0.08, -0.08, 0.0, 0.03, 8.0];
        var normality = ScanStatistics.AndersonDarling(values);

        Close(3.7735817577274062, normality.A2, 1e-9);
        Close(4.0683928325498595, normality.AStar2, 1e-9);
        Assert.Equal(NormalityVerdict.Rejected, normality.Verdict);
    }

    [Theory]
    [InlineData(-8.0, -35.013437159914545)]
    [InlineData(-5.0, -15.064998393988724)]
    [InlineData(-3.0, -6.607726221510348)]
    [InlineData(-2.9999, -6.607397916292054)]
    [InlineData(0.0, -0.6931471805599453)]
    [InlineData(2.5, -0.006229025485860027)]
    [InlineData(3.0, -0.0013508099647482027)]
    [InlineData(5.0, -2.866516130081049e-07)]
    public void The_Log_Normal_Cdf_Agrees_With_The_Reference_To_Nine_Digits_In_The_Body_And_The_Tails(double z, double expected)
    {
        var actual = NormalMath.LogCdf(z);
        Assert.True(Math.Abs(actual - expected) <= 1e-9 * Math.Max(1e-6, Math.Abs(expected)) + 1e-15, $"z = {z}: expected {expected:R}, got {actual:R}");
    }

    [Fact]
    public void The_Normal_Cdf_Is_Symmetric_Monotone_And_Never_Leaves_The_Unit_Interval_Even_Far_Out()
    {
        double previous = -1;

        for (var z = -45.0; z <= 45.0; z += 0.25)
        {
            var p = NormalMath.Cdf(z);
            Assert.InRange(p, 0.0, 1.0);
            Assert.True(p >= previous);
            Assert.InRange(NormalMath.Cdf(z) + NormalMath.Cdf(-z), 1 - 1e-12, 1 + 1e-12);
            Assert.True(double.IsFinite(NormalMath.LogCdf(z)));
            previous = p;
        }
    }

    [Fact]
    public void Fences_Report_The_Outlier_Keep_It_By_Default_And_Leave_It_Out_On_Request()
    {
        double[] values = [10.00, 10.01, 9.99, 10.02, 9.98, 10.01, 10.00, 9.99, 10.02, 10.00, 9.98, 10.50];

        // Quartiles by linear interpolation: Q1 = 9.99, Q3 = 10.0125, IQR = 0.0225, upper fence 10.04625, far fence 10.08.
        var kept = Compute(values, lsl: 9.9, usl: 10.1);
        var outlier = Assert.Single(kept.Outliers);
        Assert.Equal(10.50, outlier.Value);
        Assert.True(outlier.Far);
        Assert.Contains("Q3 + 1.5·IQR", outlier.Rule, StringComparison.Ordinal);
        Assert.Equal(12, kept.Count);
        Assert.False(kept.OutliersExcluded);
        Close(10.041666666666666, kept.Mean);
        Close(0.14496603572958566, kept.Sigma);
        Close(0.13413103522203973, kept.Cpk);
        Assert.Contains(kept.Warnings, w => w.Contains("kept in the statistics", StringComparison.Ordinal));

        var excluded = Compute(values, new ScanSettings { ExcludeOutliers = true }, lsl: 9.9, usl: 10.1);
        Assert.Equal(11, excluded.Count);
        Assert.Equal(12, excluded.Total);
        Assert.True(excluded.OutliersExcluded);
        Assert.Single(excluded.Outliers);
        Close(10.0, excluded.Mean);
        Close(0.014142135623730649, excluded.Sigma);
        Close(2.3570226039552002, excluded.Cpk);
        Assert.Contains(excluded.Warnings, w => w.Contains("left out of the statistics at your request", StringComparison.Ordinal));
    }

    [Fact]
    public void Fewer_Than_Thirty_Parts_Warns_That_The_Capability_Is_Uncertain()
    {
        var s = Compute(Ten);
        Assert.Contains(s.Warnings, w => w.Contains("fewer than 30 parts", StringComparison.Ordinal));

        var relaxed = Compute(Ten, new ScanSettings { MinParts = 10 });
        Assert.DoesNotContain(relaxed.Warnings, w => w.Contains("fewer than", StringComparison.Ordinal));
    }

    [Fact]
    public void Below_Eight_Parts_Normality_Is_Not_Assessed_And_Says_Why()
    {
        var s = Compute(Ten.Take(6));

        Assert.True(s.Computable);
        Assert.Equal(NormalityVerdict.NotAssessed, s.Normality.Verdict);
        Assert.Null(s.Normality.PValue);
        Assert.Contains(s.Warnings, w => w.Contains("Normality not assessed", StringComparison.Ordinal) && w.Contains("at least 8", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Fewer_Than_Two_Parts_Is_Not_Computable_And_Is_Said(int count)
    {
        var s = Compute(Ten.Take(count));

        Assert.False(s.Computable);
        Assert.Null(s.Cp);
        Assert.Null(s.Cpk);
        Assert.Contains(s.Warnings, w => w.Contains("at least 2 parts", StringComparison.Ordinal) || w.Contains("No usable observation", StringComparison.Ordinal));
        Assert.True(double.IsFinite(s.Mean));
    }

    [Fact]
    public void Identical_Values_Give_Zero_Sigma_And_No_Capability_Index_Rather_Than_Infinity()
    {
        var s = Compute(Enumerable.Repeat(10.0, 12));

        Assert.True(s.Computable);
        Assert.Equal(0, s.Sigma);
        Assert.Null(s.Cp);
        Assert.Null(s.Cpk);
        Assert.Equal(NormalityVerdict.NotAssessed, s.Normality.Verdict);
        Assert.Contains(s.Warnings, w => w.Contains("standard deviation is zero", StringComparison.Ordinal));
    }

    [Fact]
    public void One_Sided_Tolerance_Gives_Cpk_And_Ppk_But_Not_Cp()
    {
        var s = Compute(Ten, lsl: null, usl: 10.05);

        Assert.Null(s.Cp);
        Assert.Null(s.Pp);
        Close(0.047 / (3 * 0.018885620632286656), s.Cpk);
        Assert.Contains(s.Warnings, w => w.Contains("Only one limit", StringComparison.Ordinal));
    }

    [Fact]
    public void No_Tolerance_Gives_No_Index_And_Says_So()
    {
        var s = Compute(Ten, lsl: null, usl: null);

        Assert.Null(s.Cpk);
        Assert.Contains(s.Warnings, w => w.Contains("No tolerance is mapped", StringComparison.Ordinal));
    }

    [Fact]
    public void Rows_That_Disagree_About_The_Tolerance_Use_The_Most_Common_And_Warn()
    {
        var obs = Obs(Ten, 10, 9.95, 10.05).ToList();
        obs[0] = obs[0] with { Usl = 10.06 };
        var s = ScanStatistics.Compute("F", string.Empty, obs, OneFile, new ScanSettings());

        Assert.Equal(10.05, s.Usl);
        Assert.Contains(s.Warnings, w => w.Contains("upper limit differs between rows", StringComparison.Ordinal));
    }

    [Fact]
    public void Rows_Mixing_Absolute_Values_And_Deviation_Only_Are_Not_One_Population()
    {
        var obs = Obs(Ten, 10, 9.95, 10.05).ToList();
        obs[3] = obs[3] with { DeviationOnly = true };
        var s = ScanStatistics.Compute("F", string.Empty, obs, OneFile, new ScanSettings());

        Assert.False(s.Computable);
        Assert.Contains(s.Warnings, w => w.Contains("not one population", StringComparison.Ordinal));
    }

    [Fact]
    public void Repeated_Serials_Are_Counted_As_Measurements_And_Reported()
    {
        var obs = Obs(Ten, 10, 9.95, 10.05).ToList();
        obs[1] = obs[1] with { Serial = obs[0].Serial };
        var s = ScanStatistics.Compute("F", string.Empty, obs, OneFile, new ScanSettings());

        Assert.Equal(10, s.Count);
        Assert.Equal(9, s.Parts);
        Assert.Contains(s.Warnings, w => w.Contains("appear more than once", StringComparison.Ordinal));
    }

    [Fact]
    public void Values_Too_Large_To_Square_Are_Reported_Not_Turned_Into_NaN()
    {
        var obs = new[] { 1e200, 2e200, 3e200, 4e200 }.Select((v, i) => new ScanObservation(0, i + 2, "F", string.Empty, string.Empty, null, v, null, null, false)).ToList();
        var s = ScanStatistics.Compute("F", string.Empty, obs, OneFile, new ScanSettings());

        Assert.False(s.Computable);
        Assert.Contains(s.Warnings, w => w.Contains("overflow", StringComparison.Ordinal));
    }

    [Fact]
    public void A_Set_Of_Several_Files_Has_A_Set_Hash_And_A_Single_File_Keeps_Its_Own()
    {
        var files = new List<ScanFileInfo> { new("a.csv", new string('a', 64), null, null, 5, 5), new("b.csv", new string('b', 64), null, null, 5, 5) };
        var first = Obs(Ten.Take(5)).ToList();
        var second = Obs(Ten.Skip(5), file: 1).ToList();

        var single = ScanStatistics.Compute("F", string.Empty, first, files, new ScanSettings());
        Assert.Equal(new string('a', 64), single.SetSha256);

        var both = ScanStatistics.Compute("F", string.Empty, first.Concat(second).ToList(), files, new ScanSettings());
        Assert.Equal(64, both.SetSha256.Length);
        Assert.NotEqual(single.SetSha256, both.SetSha256);
        Assert.Equal(new[] { 0, 1 }, both.Files);
    }

    [Fact]
    public void Quantiles_Use_Linear_Interpolation_Of_The_Order_Statistics()
    {
        double[] sorted = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];
        Assert.Equal(3.25, ScanStatistics.Quantile(sorted, 0.25), 12);
        Assert.Equal(5.5, ScanStatistics.Quantile(sorted, 0.5), 12);
        Assert.Equal(7.75, ScanStatistics.Quantile(sorted, 0.75), 12);
        Assert.Equal(10, ScanStatistics.Quantile(sorted, 1.0), 12);
    }

    [Theory]
    [InlineData(0.752, 0.0503)]
    [InlineData(0.631, 0.0988)]
    [InlineData(1.035, 0.01)]
    [InlineData(0.15, 0.96)]
    public void The_P_Value_Approximation_Reproduces_The_Published_Critical_Points_To_Rounding(double aStar, double p)
    {
        // The usual critical values of A*² for the normal case with estimated mean and variance (10 %, 5 %, 1 %) give p close to their level.
        Assert.InRange(ScanStatistics.PValue(aStar), p - 0.01, p + 0.01);
    }

    [Fact]
    public void The_P_Value_Is_Continuous_Across_Its_Four_Pieces()
    {
        foreach (var edge in new[] { 0.2, 0.34, 0.6 })
        {
            var below = ScanStatistics.PValue(edge - 1e-9);
            var above = ScanStatistics.PValue(edge + 1e-9);
            Assert.InRange(Math.Abs(below - above), 0, 0.01);
        }

        Assert.InRange(ScanStatistics.PValue(50), 0, 1e-20);
        Assert.InRange(ScanStatistics.PValue(0), 0.0, 1.0);
    }

    [Fact]
    public void The_Reference_N30_Sample_Statistics_Are_Reproduced()
    {
        var file = Samples.Load("scan/zeiss-groove.csv");
        var profile = SampleProfiles.Zeiss();
        var analysis = ScanImporter.Analyse(file, profile);

        var dg = analysis.Stats.Single(s => s.Feature == "D_g groove root");
        Assert.Equal(30, dg.Count);
        Close(51.48546666666667, dg.Mean);
        Close(0.009926395787663347, dg.Sigma);
        Close(1.6790249979130663, dg.Cp);
        Close(1.1909883985196645, dg.Cpk);
        Close(1.6790249979130663, dg.Pp);
        Close(1.1909883985196645, dg.Ppk);
        Close(0.7326379429316354, dg.Normality.A2, 1e-9);
        Close(0.7527854863622553, dg.Normality.AStar2, 1e-9);
        Close(0.050116149262830034, dg.Normality.PValue, 1e-9);

        var b = analysis.Stats.Single(s => s.Feature == "b groove width");
        Close(4.836600000000001, b.Mean);
        Close(0.015867120634865606, b.Sigma);
        Close(1.0503901148923087, b.Cp);
        Close(0.7688855641011906, b.Cpk);
        Close(0.39726867203716765, b.Normality.A2, 1e-9);
        Close(0.34683761329287904, b.Normality.PValue, 1e-9);

        var ds = analysis.Stats.Single(s => s.Feature == "D_s bore");
        Close(56.9205, ds.Mean);
        Close(0.009694470517810312, ds.Sigma);
        Close(0.7908288186117699, ds.Cp);
        Close(0.7048691644147933, ds.Cpk);

        // Subgroups of five parts, in file order per feature: the within sigma and so Cp, Cpk move; Pp and Ppk do not.
        var grouped = ScanImporter.Analyse(file, profile, new ScanSettings { SubgroupSize = 5 });
        var dgm = grouped.Stats.Single(s => s.Feature == "D_g groove root");
        // python3: pooled 0.0090295810164887965 on 24 degrees of freedom (six subgroups of five), c4(25) = 0.989640375585705, within 0.0091241032997918699;
        // Cp = 0.1/(6 x 0.0091241) = 1.826663521778179, Cpk = 0.0645333/(3 x 0.0091241) = 1.295713324781317 (uncorrected 1.84579 and 1.30928).
        Close(0.0091241032997918699, dgm.SigmaWithin);
        Close(0.989640375585705, dgm.WithinC4, 1e-12);
        Close(1.826663521778179, dgm.Cp);
        Close(1.295713324781317, dgm.Cpk);
        Close(1.6790249979130663, dgm.Pp);
        Close(1.1909883985196645, dgm.Ppk);
    }
}
