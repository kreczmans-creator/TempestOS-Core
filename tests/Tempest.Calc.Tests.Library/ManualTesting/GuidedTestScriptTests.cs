using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Tempest.Calc.Library.ManualTesting;

namespace Tempest.Calc.Tests.Library.ManualTesting;

/// <summary>The embedded guided test script: it loads, is versioned, names real calculators, and refuses damaged or oversize content.</summary>
public sealed class GuidedTestScriptTests
{
    [Fact]
    public void The_Embedded_Script_Loads_And_Is_Versioned_By_Release_And_Content()
    {
        var script = GuidedTestScript.Current;
        Assert.Matches(new Regex(@"^\d+\.\d+\.\d+\+[0-9a-f]{8}$"), script.ScriptVersion);
        Assert.Equal(File.ReadAllText(Path.Combine(GuidedTestHelpers.RepositoryRoot, "VERSION")).Trim(), script.Release);
        Assert.StartsWith(script.Release + "+", script.ScriptVersion, StringComparison.Ordinal);
        Assert.Equal(script.Steps.Count, script.Steps.Select(s => s.Id).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void The_Script_Holds_The_Runbooks_Manual_Steps_And_Every_Worked_Example()
    {
        var steps = GuidedTestScript.Current.Steps;

        foreach (var kind in new[] { GuidedStepKind.Smoke, GuidedStepKind.Pack, GuidedStepKind.Import, GuidedStepKind.Lifting, GuidedStepKind.Legacy })
            Assert.Contains(steps, s => s.Kind == kind);

        Assert.Equal(87, steps.Count(s => s.Kind == GuidedStepKind.Example));
        Assert.Equal(54, GuidedTestScript.Current.TemplateIds.Count);
        Assert.All(steps.Where(s => s.Kind == GuidedStepKind.Example), s =>
        {
            Assert.False(string.IsNullOrWhiteSpace(s.InputsJson));
            Assert.NotEmpty(s.Expected!);
            Assert.NotEmpty(s.Inputs!);
        });
    }

    [Fact]
    public void Every_Example_Names_A_Shipped_Template_At_The_Version_The_Script_Was_Written_For()
    {
        var shipped = ShippedCalculators.All.ToDictionary(c => c.Template.Id, c => c.Template.Version, StringComparer.Ordinal);

        foreach (var step in GuidedTestScript.Current.Steps.Where(s => s.Kind == GuidedStepKind.Example))
        {
            Assert.True(shipped.TryGetValue(step.TemplateId!, out var version), $"{step.Id}: no shipped template '{step.TemplateId}'.");
            Assert.True(version == step.TemplateVersion, $"{step.Id}: the script was written for {step.TemplateVersion} but the build ships {version}; run scripts/generate-guided-test-script.py.");
        }
    }

    [Fact]
    public void The_Smoke_Test_Step_That_Pastes_An_Example_Opens_That_Example()
    {
        var script = GuidedTestScript.Current;
        var smoke = script.Find("smoke-3")!;
        var example = script.ExampleOf(smoke)!;
        Assert.Equal("fp.accumulator", example.TemplateId);
        Assert.Equal("clamp-circuit-bladder-accumulator", example.ExampleId);
        Assert.Same(example, script.ExampleOf(example));
        Assert.Null(script.ExampleOf(script.Find("lifting-1")!));
    }

    [Fact]
    public void A_Small_Script_Parses()
    {
        var script = GuidedTestHelpers.SmallScript();
        Assert.Equal("9.9.9+aaaaaaaa", script.ScriptVersion);
        Assert.Equal(4, script.Steps.Count);
        Assert.Equal(["t.a", "t.b"], script.TemplateIds);
        Assert.Null(script.Find("nope"));
    }

    [Fact]
    public void A_Script_Over_The_Step_Cap_Is_Refused()
    {
        var json = GuidedTestHelpers.SmallScriptJson(exampleCount: GuidedTestScript.MaxSteps);
        var refusal = Assert.Throws<InvalidDataException>(() => GuidedTestScript.Parse(json));
        Assert.Contains("between 1 and", refusal.Message, StringComparison.Ordinal);
        Assert.Contains(GuidedTestScript.MaxSteps.ToString(System.Globalization.CultureInfo.InvariantCulture), refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_Script_Over_The_Size_Cap_Is_Refused_Without_Being_Read()
    {
        var refusal = Assert.Throws<InvalidDataException>(() => GuidedTestScript.Parse(new string(' ', GuidedTestScript.MaxScriptBytes + 1)));
        Assert.Contains("larger than", refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_Field_Over_The_Length_Cap_Is_Refused()
    {
        var node = JsonNode.Parse(GuidedTestHelpers.SmallScriptJson())!;
        node["steps"]![0]!["title"] = new string('x', GuidedTestScript.MaxTextLength + 1);
        Assert.Contains("longer than", Assert.Throws<InvalidDataException>(() => GuidedTestScript.Parse(node.ToJsonString())).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Too_Many_Expected_Outputs_Instructions_Or_Input_Rows_Are_Refused()
    {
        var expected = JsonNode.Parse(GuidedTestHelpers.SmallScriptJson())!;
        expected["steps"]![1]!["expected"] = new JsonArray([.. Enumerable.Range(0, GuidedTestScript.MaxExpectedPerStep + 1).Select(i => (JsonNode)new JsonObject { ["symbol"] = $"s{i}", ["value"] = "1", ["unit"] = "" })]);
        Assert.Contains("expected outputs", Assert.Throws<InvalidDataException>(() => GuidedTestScript.Parse(expected.ToJsonString())).Message, StringComparison.Ordinal);

        var instructions = JsonNode.Parse(GuidedTestHelpers.SmallScriptJson())!;
        instructions["steps"]![0]!["instructions"] = new JsonArray([.. Enumerable.Range(0, GuidedTestScript.MaxInstructionLines + 1).Select(i => (JsonNode)$"line {i}")]);
        Assert.Contains("instruction lines", Assert.Throws<InvalidDataException>(() => GuidedTestScript.Parse(instructions.ToJsonString())).Message, StringComparison.Ordinal);

        var inputs = JsonNode.Parse(GuidedTestHelpers.SmallScriptJson())!;
        inputs["steps"]![1]!["inputs"] = new JsonArray([.. Enumerable.Range(0, GuidedTestScript.MaxInputRowsPerStep + 1).Select(i => (JsonNode)new JsonObject { ["symbol"] = $"s{i}", ["description"] = "d", ["value"] = "1", ["unit"] = "" })]);
        Assert.Contains("inputs", Assert.Throws<InvalidDataException>(() => GuidedTestScript.Parse(inputs.ToJsonString())).Message, StringComparison.Ordinal);

        var inputsJson = JsonNode.Parse(GuidedTestHelpers.SmallScriptJson())!;
        inputsJson["steps"]![1]!["inputsJson"] = "[" + new string(' ', GuidedTestScript.MaxInputsJsonChars) + "]";
        Assert.Contains("characters of inputs", Assert.Throws<InvalidDataException>(() => GuidedTestScript.Parse(inputsJson.ToJsonString())).Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("format")]
    [InlineData("duplicate")]
    [InlineData("noExpected")]
    [InlineData("badRef")]
    [InlineData("noVersion")]
    [InlineData("notJson")]
    public void A_Damaged_Script_Is_Refused_With_A_Reason(string damage)
    {
        var node = JsonNode.Parse(GuidedTestHelpers.SmallScriptJson())!;
        var text = node.ToJsonString();

        switch (damage)
        {
            case "format": node["format"] = 2; break;
            case "duplicate": node["steps"]![2]!["id"] = "smoke-1"; break;
            case "noExpected": node["steps"]![1]!["expected"] = new JsonArray(); break;
            case "badRef": node["steps"]![0]!["exampleRef"] = "example:missing"; break;
            case "noVersion": node["scriptVersion"] = " "; break;
            default: text = "{ not json"; break;
        }

        if (damage != "notJson")
            text = node.ToJsonString();

        var refusal = Assert.Throws<InvalidDataException>(() => GuidedTestScript.Parse(text));
        Assert.False(string.IsNullOrWhiteSpace(refusal.Message));
    }
}
