using System.Text;
using Tempest.Calc.Library.Dashboard;

namespace Tempest.Calc.Tests.Library.Dashboard;

/// <summary>Addendum C, C-4: reading the embedded snapshot is bounded: a file or a caller cannot make it read without limit.</summary>
public sealed class VerificationSnapshotTests
{
    [Fact]
    public void The_Embedded_Snapshot_Loads_With_Findings_Results_And_Examples()
    {
        var snapshot = VerificationSnapshot.LoadEmbedded();

        Assert.Null(snapshot.Problem);
        var shaft = snapshot.Items["mech.shaft"];
        Assert.NotNull(shaft.Kat);
        Assert.True(shaft.WorkedExamples > 0);
        Assert.Equal("match-with-findings", shaft.Rederivation!.Result);
        Assert.Contains(shaft.Rederivation.Findings, f => f.Id == "mech.shaft-01" && f.Defect is not null);
        Assert.StartsWith("MATCH WITH FINDINGS", shaft.Rederivation.Verdict, StringComparison.Ordinal);
    }

    [Fact]
    public void Text_Over_The_Cap_Is_Refused_Not_Parsed()
    {
        var snapshot = VerificationSnapshot.Parse(new string(' ', VerificationSnapshot.MaxJsonChars + 1));

        Assert.Empty(snapshot.Items);
        Assert.Equal("The verification data is larger than this program reads.", snapshot.Problem);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{\"schema\": 99, \"items\": {}}")]
    [InlineData("null")]
    [InlineData("{\"schema\": 1, \"items\": {\"a\": {\"rederivation\": {\"result\": \"match\", \"verdict\": \"\", \"findings\": [{\"id\": \"a-01\", \"severity\": \"High\", \"defect\": \"maybe\"}]}}}}")]
    public void A_File_That_Is_Not_Valid_Is_Unavailable_With_A_Plain_Reason(string json)
    {
        var snapshot = VerificationSnapshot.Parse(json);

        Assert.Empty(snapshot.Items);
        Assert.False(string.IsNullOrEmpty(snapshot.Problem));
        Assert.DoesNotContain("Exception", snapshot.Problem, StringComparison.Ordinal);
    }

    [Fact]
    public void A_Null_Item_Is_Skipped_And_The_Other_Items_Are_Kept()
    {
        var snapshot = VerificationSnapshot.Parse("{\"schema\":1,\"items\":{\"a\":null,\"b\":{\"workedExamples\":2}}}");

        Assert.Null(snapshot.Problem);
        Assert.DoesNotContain("a", snapshot.Items.Keys);
        Assert.Equal(2, snapshot.Items["b"].WorkedExamples);
    }

    [Fact]
    public void A_Null_Finding_Is_Skipped_And_The_Other_Findings_Are_Kept()
    {
        var snapshot = VerificationSnapshot.Parse("{\"schema\":1,\"items\":{\"a\":{\"rederivation\":{\"result\":\"match\",\"verdict\":\"v\",\"findings\":[null,{\"id\":\"a-01\",\"severity\":\"High\",\"defect\":\"open\"},null]}}}}");

        Assert.Null(snapshot.Problem);
        var finding = Assert.Single(snapshot.Items["a"].Rederivation!.Findings);
        Assert.Equal("a-01", finding.Id);
    }

    [Fact]
    public void A_Null_Findings_List_Reads_As_No_Findings()
    {
        var snapshot = VerificationSnapshot.Parse("{\"schema\":1,\"items\":{\"a\":{\"rederivation\":{\"result\":\"match\",\"verdict\":null,\"findings\":null}}}}");

        Assert.Null(snapshot.Problem);
        Assert.Empty(snapshot.Items["a"].Rederivation!.Findings);
    }

    [Theory]
    [InlineData("{\"schema\":1,\"items\":{\"a\":{\"rederivation\":{\"result\":\"match\",\"verdict\":\"\",\"findings\":[{\"id\":\"a-01\",\"severity\":\"High\",\"defect\":5}]}}}}")]
    [InlineData("{\"schema\":1,\"items\":[1,2]}")]
    [InlineData("{\"schema\":1,\"sources\":{\"a\":1},\"items\":{}}")]
    [InlineData("{\"schema\":\"one\",\"items\":{}}")]
    public void A_Fault_The_Reader_Does_Not_Foresee_Reads_As_Unavailable_And_Never_Throws(string json)
    {
        var snapshot = VerificationSnapshot.Parse(json);

        Assert.Empty(snapshot.Items);
        Assert.False(string.IsNullOrEmpty(snapshot.Problem));
    }

    [Fact]
    public void Items_Findings_And_Strings_Are_Capped()
    {
        var json = new StringBuilder("{\"schema\": 1, \"items\": {\"big\": {\"rederivation\": {\"result\": \"match\", \"verdict\": \"").Append(new string('v', 5000)).Append("\", \"findings\": [");

        for (var i = 0; i < VerificationSnapshot.MaxFindingsPerItem + 40; i++)
            json.Append(i > 0 ? "," : "").Append("{\"id\": \"").Append(new string('i', i == 0 ? 5000 : 3)).Append("\", \"severity\": \"Low\", \"defect\": null}");

        json.Append("]}}");
        json.Append(",\"").Append(new string('k', 65)).Append("\": {}");

        for (var i = 0; i < VerificationSnapshot.MaxItems + 25; i++)
            json.Append($",\"t.{i}\": {{\"workedExamples\": 999999}}");

        json.Append("}}");
        var snapshot = VerificationSnapshot.Parse(json.ToString());

        Assert.Null(snapshot.Problem);
        Assert.True(snapshot.Items.Count <= VerificationSnapshot.MaxItems);
        Assert.DoesNotContain(new string('k', 65), snapshot.Items.Keys);
        Assert.All(snapshot.Items.Where(kv => kv.Key.StartsWith("t.", StringComparison.Ordinal)), kv => Assert.Equal(10_000, kv.Value.WorkedExamples));
        var big = snapshot.Items["big"];
        Assert.Equal(VerificationSnapshot.MaxFindingsPerItem, big.Rederivation!.Findings.Count);
        Assert.Equal(VerificationSnapshot.MaxTextLength, big.Rederivation.Verdict.Length);
        Assert.Equal(VerificationSnapshot.MaxTextLength, big.Rederivation.Findings[0].Id.Length);
    }

    [Fact]
    public void Items_Past_The_Cap_Are_Dropped()
    {
        var json = new StringBuilder("{\"schema\": 1, \"items\": {\"first\": {\"workedExamples\": 2}");

        for (var i = 0; i < VerificationSnapshot.MaxItems; i++)
            json.Append($",\"t.{i}\": {{}}");

        json.Append("}}");
        var snapshot = VerificationSnapshot.Parse(json.ToString());

        Assert.Equal(VerificationSnapshot.MaxItems, snapshot.Items.Count);
        Assert.Equal(2, snapshot.Items["first"].WorkedExamples);
    }

    [Fact]
    public void Negative_Or_Huge_Counts_Are_Clamped()
    {
        var snapshot = VerificationSnapshot.Parse("{\"schema\": 1, \"items\": {\"a\": {\"workedExamples\": -5, \"exampleChecks\": 2000000000, \"kat\": {\"version\": \"1.0.0\", \"kats\": -1, \"katsPassed\": 2000000000, \"failedKats\": [], \"examplesPassed\": -4, \"failedExamples\": []}}}}");
        var a = snapshot.Items["a"];

        Assert.Equal(0, a.WorkedExamples);
        Assert.Equal(100_000, a.ExampleChecks);
        Assert.Equal(0, a.Kat!.Kats);
        Assert.Equal(100_000, a.Kat.KatsPassed);
        Assert.Equal(0, a.Kat.ExamplesPassed);
    }
}
