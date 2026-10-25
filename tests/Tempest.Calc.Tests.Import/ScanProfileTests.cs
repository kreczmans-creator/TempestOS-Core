using Tempest.Calc.Import;
using Tempest.Calc.Import.Scan;

namespace Tempest.Calc.Tests.Import;

/// <summary>Scan profiles: targets, validation, JSON.</summary>
public sealed class ScanProfileTests
{
    private static ScanProfile Valid() => SampleProfiles.Zeiss(new ScanFeatureEntry("D_g groove root", "contributors[0]"), new ScanFeatureEntry("D_s bore", "measured{D_s}", Required: true), new ScanFeatureEntry("b groove width", "B:sigma", Stage: "OP20"));

    [Theory]
    [InlineData("D", ScanTargetKind.Scalar, null, null, ScanStat.Mean)]
    [InlineData("D:sigma", ScanTargetKind.Scalar, null, null, ScanStat.Sigma)]
    [InlineData("D:NOMINAL", ScanTargetKind.Scalar, null, null, ScanStat.Nominal)]
    [InlineData("contributors[12]", ScanTargetKind.StackRow, 12, null, ScanStat.Mean)]
    [InlineData("measured{D_g}", ScanTargetKind.MeasuredKey, null, "D_g", ScanStat.Mean)]
    public void Targets_Parse(string text, ScanTargetKind kind, int? row, string? key, ScanStat stat)
    {
        Assert.True(ScanTarget.TryParse(text, out var t));
        Assert.Equal(kind, t.Kind);
        Assert.Equal(row, t.Row);
        Assert.Equal(key, t.Key);
        Assert.Equal(stat, t.Stat);
    }

    [Theory]
    [InlineData("")]
    [InlineData("1D")]
    [InlineData("D:")]
    [InlineData("D:median")]
    [InlineData("D:1")]
    [InlineData("D:99")]
    [InlineData("contributors[]")]
    [InlineData("contributors[-1]")]
    [InlineData("contributors[99999]")]
    [InlineData("contributors[1]x")]
    [InlineData("contributors[1].nominal")]
    [InlineData("measured{}")]
    [InlineData("measured{a b}")]
    [InlineData("measured{a}x")]
    [InlineData("D D")]
    [InlineData("D=1")]
    public void Malformed_Targets_Are_Refused(string text) => Assert.False(ScanTarget.TryParse(text, out _));

    [Fact]
    public void A_Sound_Profile_Validates_And_Its_Targets_Print_As_Written()
    {
        Assert.Empty(Valid().Validate());
        Assert.Equal("zeiss-groove@1", Valid().Label);
        Assert.Equal("ZEISS Inspect", Valid().SoftwareLabel);
        Assert.Equal("measured{D_s}", ScanTarget.TryParse("measured{D_s}", out var t) ? t.ToString() : null);
        Assert.Equal("contributors[3]", ScanTarget.TryParse("contributors[3]", out var r) ? r.ToString() : null);
        Assert.Equal("D:sigma", ScanTarget.TryParse("D:sigma", out var s) ? s.ToString() : null);
    }

    [Fact]
    public void A_Profile_May_Have_No_Features_And_Then_Only_Reads_And_Fits()
    {
        Assert.Empty(SampleProfiles.Zeiss().Validate());
    }

    public static TheoryData<string, ScanProfile> Unsound => new()
    {
        { "profile id", Valid() with { Id = "Bad Id" } },
        { "profile id", Valid() with { Id = string.Empty } },
        { "version", Valid() with { Version = 0 } },
        { "title", Valid() with { Title = " " } },
        { "unit", Valid() with { Unit = "furlongs" } },
        { "unit", Valid() with { Unit = "degC" } },
        { "unit", Valid() with { Unit = "%" } },
        { "header row", Valid() with { HeaderRow = 0 } },
        { "feature name", Valid() with { Columns = new ScanColumns(string.Empty, Actual: "Actual") } },
        { "actual value, or the deviation", Valid() with { Columns = new ScanColumns("Feature") } },
        { "position like #3", Valid() with { Columns = new ScanColumns("Feature", Actual: " ") } },
        { "#1 to #256", Valid() with { Columns = new ScanColumns("#1", Actual: "#300") } },
        { "also used for another quantity", Valid() with { Columns = new ScanColumns("Feature", Actual: "feature") } },
        { "appears twice", Valid() with { Features = [new ScanFeatureEntry("A", "D"), new ScanFeatureEntry("a", "F")], IgnoreCase = true } },
        { "appears twice", Valid() with { Features = [new ScanFeatureEntry("A", "D"), new ScanFeatureEntry("A", "F")] } },
        { "is not an input symbol", Valid() with { Features = [new ScanFeatureEntry("A", "D.x")] } },
        { "More than one feature is mapped to", Valid() with { Features = [new ScanFeatureEntry("A", "D"), new ScanFeatureEntry("B", "D")] } },
        { "number format", Valid() with { Number = new NumberProfile(DecimalMark.Comma, ',') } },
        { "fixed stage", Valid() with { FixedStage = new string('x', 41) } },
        { "more than 500", Valid() with { Features = Enumerable.Range(0, 501).Select(i => new ScanFeatureEntry($"F{i}", $"X{i}")).ToList() } },
    };

    [Theory]
    [MemberData(nameof(Unsound))]
    public void An_Unsound_Profile_Is_Refused_With_The_Reason(string reason, ScanProfile profile)
    {
        var problems = profile.Validate();

        Assert.NotEmpty(problems);
        Assert.Contains(problems, p => p.Contains(reason, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void A_Profile_Round_Trips_Through_Json_And_Is_Validated_On_Reading()
    {
        var profile = Valid() with { Number = NumberProfile.Comma, Delimiter = ScanDelimiter.Semicolon, Notes = "n" };

        var back = ScanProfileJson.Read(ScanProfileJson.Write(profile));

        Assert.Equal(profile.Id, back.Id);
        Assert.Equal(profile.Columns, back.Columns);
        Assert.Equal(profile.Features.Select(f => (f.Feature, f.Target, f.Stage, f.Required)), back.Features.Select(f => (f.Feature, f.Target, f.Stage, f.Required)));
        Assert.Equal(ScanDelimiter.Semicolon, back.Delimiter);
        Assert.Equal(DecimalMark.Comma, back.Number!.Decimal);
        Assert.Equal(ScanSoftware.ZeissInspect, back.Software);
    }

    [Theory]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{")]
    [InlineData("{\"id\":\"p\"}")]
    [InlineData("{\"id\":\"p\",\"version\":1,\"title\":\"t\",\"columns\":{\"feature\":\"F\",\"actual\":\"A\"},\"features\":[],\"software\":\"nonsense\"}")]
    [InlineData("{\"id\":\"BAD\",\"version\":1,\"title\":\"t\",\"columns\":{\"feature\":\"F\",\"actual\":\"A\"},\"features\":[]}")]
    public void Bad_Json_Is_A_Documented_Refusal(string json) =>
        Assert.Throws<ImportRefusedException>(() => ScanProfileJson.Read(json));

    [Fact]
    public void Json_With_A_Control_Character_Or_Over_The_Size_Cap_Or_Too_Deep_Is_Refused()
    {
        Samples.Refuses(ImportRefusalCode.ControlCharacter, () => ScanProfileJson.Read("{\"id\":\"p\u0001\"}"));
        Samples.Refuses(ImportRefusalCode.TooLarge, () => ScanProfileJson.Read(new string(' ', ScanProfileJson.MaxBytes + 1)));
        Assert.Throws<ImportRefusedException>(() => ScanProfileJson.Read(string.Concat(Enumerable.Repeat("[", 50)) + string.Concat(Enumerable.Repeat("]", 50))));
    }

    [Fact]
    public void Column_Positions_Are_Recognised()
    {
        Assert.True(ScanProfile.IsPosition("#3", out var p));
        Assert.Equal(3, p);
        Assert.False(ScanProfile.IsPosition("#", out _));
        Assert.False(ScanProfile.IsPosition("#x", out _));
        Assert.False(ScanProfile.IsPosition("3", out _));
        Assert.False(ScanProfile.IsPosition("#-1", out _));
    }
}
