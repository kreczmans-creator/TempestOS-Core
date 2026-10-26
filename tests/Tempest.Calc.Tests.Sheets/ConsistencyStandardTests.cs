using Tempest.Calc.Engine.Units;
using Tempest.Calc.Engine.Values;
using Tempest.Calc.Sheets;
using Tempest.Calc.Verification;

namespace Tempest.Calc.Tests.Sheets;

/// <summary>
/// A small calculator whose input and output port were renamed (C-2): <c>F_axial</c> was <c>F</c>, and the port <c>stress.axial</c> was the
/// output symbol <c>sigma_old</c> before the output was renamed <c>sigma</c>. It also raises one yes/no check, a warning and a block.
/// </summary>
public sealed class RenamedCalculator : ICalcCalculator
{
    public CalcTemplate Template { get; } = new(
        "test.renamed", "1.1.0", CalcDiscipline.Structural, "Renamed stress (test)",
        "Axial stress after an input and an output were renamed.",
        "Stress: sigma = F_axial / A.",
        [
            new CalcInputSpec("F_axial", "Axial load", CalcInputKind.Quantity, "kN", Default: 12, Min: 0, Max: 10_000, HardMin: -1e6, HardMax: 1e6, Aliases: ["F", "F_old"]),
            new CalcInputSpec("A", "Cross-section area", CalcInputKind.Quantity, "mm^2", Default: 200, Min: 1, HardMin: 1e-9),
            new CalcInputSpec("sigma_allow", "Allowable stress", CalcInputKind.Quantity, "MPa", Default: 160, Min: 1, HardMin: 1e-9),
        ],
        [new CalcAssumption("The load acts through the centroid.", "No bending from eccentricity.")],
        [new CalcClauseRef("EN 1993-1-1", "6.2.3", "Tension")],
        [new CalcApplicabilityRule("high-stress", "Stress above 80 % of allowable is flagged.", CalcNoticeSeverity.Warning)],
        [
            new CalcTestCase("ka-1", "12 kN on 200 mm²", new CalcReferenceSource("Hand calculation", null, "σ = 12 000 / 200 = 60 MPa"),
                [CalcInputValue.Quantity("F_axial", 12, "kN"), CalcInputValue.Quantity("A", 200, "mm^2")],
                [new CalcExpectedValue("sigma", 60, "MPa", 1e-9)]),
        ],
        Category: "Members",
        OutputPorts:
        [
            new CalcOutputPort("sigma", "MPa", "Direct stress", "stress.axial", ["sigma_old"]),
            new CalcOutputPort("A_req", "mm^2", "Required area", "stress.area_required"),
        ]);

    public void Run(CalcRunContext c)
    {
        c.Input("F_axial");
        c.Input("A");
        c.Input("sigma_allow");
        c.Assign("sigma", "F_axial / A", "MPa", "Direct stress");
        c.Assign("u", "sigma / sigma_allow", null, "Utilisation");
        c.Assign("A_req", "F_axial / sigma_allow", "mm^2", "Required area");
        c.Assign("margin", "sigma_allow - sigma", "MPa", "Stress reserve");
        c.Output("margin", "Stress reserve");
        c.Output("u", "Utilisation");
        c.Output("A_req", "Required area");
        c.Output("sigma", "Direct stress");
        c.CheckUtilisation("Direct stress against allowable", "u");
        c.CheckCondition("Tension only", c.Si("sigma") >= 0, "tension", "tension", "sigma");

        if (c.Si("u") > 0.8)
            c.Warn("high-stress", "u: the stress is above 80 % of the allowable.");
    }
}

/// <summary>C-2, rows 1 to 4, 27, 29 to 31, 43 to 45: the standard layout, the display precision, the message shape, and the port and input aliases.</summary>
public sealed class ConsistencyStandardTests
{
    private static readonly RenamedCalculator Calculator = new();

    private static CalcSheet Sheet(CalcInputSet? inputs = null, CalcTemplate? template = null)
    {
        template ??= Calculator.Template;
        var set = inputs ?? new CalcInputSet([CalcInputValue.Quantity("F_axial", 12, "kN", CalcProvenance.Manual), CalcInputValue.Quantity("A", 200, "mm^2", CalcProvenance.Manual), CalcInputValue.Quantity("sigma_allow", 160, "MPa", CalcProvenance.Manual)]);
        var result = CalcRunner.Run(Calculator, set);
        return CalcSheetBuilder.Build(template, set, result, GoldenFiles.FixedHeader(template.Id, template.Title), GoldenFiles.FixedFooter(template, 1, 1));
    }

    // ---- rows 1 to 4: layout ----------------------------------------

    [Fact]
    public void The_Text_Sheet_Prints_Every_Section_In_The_Standard_Order_Even_When_It_Is_Empty()
    {
        var empty = new CalcSheet(
            GoldenFiles.FixedHeader("x", "x"), string.Empty, string.Empty, [], [], [], [], [], [], [], [],
            new CalcSheetFooter("x@1.0.0", "0000000000000000", "engine", 0, 0, null));
        var text = CalcSheetTextRenderer.Render(empty);
        var last = -1;

        foreach (var heading in CalcSheetStandard.Headings)
        {
            var at = text.IndexOf("\n" + heading + "\n", StringComparison.Ordinal);
            Assert.True(at > last, $"'{heading}' is missing or out of order");
            last = at;
        }

        Assert.Equal(["1. Scope and method", "2. Inputs", "3. Assumptions", "4. Working", "5. Results", "6. Checks (governing first)", "7. Applicability", "8. Verification", "9. References"], CalcSheetStandard.Headings);

        // Inputs, assumptions, working, results, checks, applicability and references say "None." when they hold nothing.
        Assert.Equal(7, text.Split('\n').Count(l => l == "  " + CalcSheetStandard.Empty));
    }

    [Fact]
    public void A_Notice_Is_One_Line_In_The_Standard_Form_With_The_Rule_Id_Last()
    {
        Assert.Equal("BLOCK  A: rating needs the heat transfer area.  [area-missing]", CalcSheetStandard.NoticeLine(new CalcNotice("area-missing", CalcNoticeSeverity.Block, "A: rating needs the heat transfer area.")));
        Assert.Equal("WARNING  u: the stress is above 80 % of the allowable.  [high-stress]", CalcSheetStandard.NoticeLine(new CalcNotice("high-stress", CalcNoticeSeverity.Warning, "u: the stress is above 80 % of the allowable.")));

        var text = CalcSheetTextRenderer.Render(Sheet(new CalcInputSet([CalcInputValue.Quantity("F_axial", 30, "kN", CalcProvenance.Manual), CalcInputValue.Quantity("A", 200, "mm^2", CalcProvenance.Manual), CalcInputValue.Quantity("sigma_allow", 160, "MPa", CalcProvenance.Manual)])));
        Assert.Contains("  WARNING  u: the stress is above 80 % of the allowable.  [high-stress]", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Results_List_The_Ports_First_Then_The_Supporting_Values_Then_The_Utilisations()
    {
        var sheet = Sheet();
        // The template lists its ports as sigma, A_req; the run produced margin, u, A_req, sigma.
        Assert.Equal(["sigma", "A_req", "margin", "u"], sheet.Outputs.Select(o => o.Symbol));

        var text = CalcSheetTextRenderer.Render(sheet);
        Assert.True(text.IndexOf("  sigma = ", StringComparison.Ordinal) < text.IndexOf("  margin = ", StringComparison.Ordinal));
        Assert.True(text.IndexOf("  margin = ", StringComparison.Ordinal) < text.IndexOf("  u = ", StringComparison.Ordinal));
    }

    [Fact]
    public void A_Template_With_No_Ports_Still_Puts_The_Utilisations_Last()
    {
        var sheet = Sheet(template: Calculator.Template with { OutputPorts = null });
        Assert.Equal(["margin", "A_req", "sigma", "u"], sheet.Outputs.Select(o => o.Symbol));
    }

    [Fact]
    public void The_Governing_Result_Is_Marked_And_A_Yes_No_Check_Prints_No_Utilisation()
    {
        var text = CalcSheetTextRenderer.Render(Sheet());
        Assert.Contains("u = 0.38    Utilisation (governing)", text, StringComparison.Ordinal);
        Assert.Contains("  PASS  Tension only: tension vs tension\n", text, StringComparison.Ordinal);
        Assert.DoesNotContain("utilisation 0\n", text, StringComparison.Ordinal);
        Assert.Contains("  PASS  Direct stress against allowable: u = 0.38 vs u ≤ 1  utilisation 0.38\n", text, StringComparison.Ordinal);

        var result = CalcRunner.Run(Calculator, new CalcInputSet([CalcInputValue.Quantity("F_axial", -12, "kN", CalcProvenance.Manual)]));
        var yesNo = result.Checks.Single(c => c.Name == "Tension only");
        Assert.False(yesNo.HasUtilisation);
        Assert.Equal("—", yesNo.UtilisationText);
        Assert.Equal("FAIL  Tension only: tension vs tension", CalcSheetStandard.CheckLine(yesNo));
    }

    [Fact]
    public void Default_Notes_Are_Listed_Apart_From_The_Warnings_In_Plain_Words_With_Unit_Symbols()
    {
        var set = new CalcInputSet([CalcInputValue.Quantity("F_axial", 30, "kN", CalcProvenance.Manual)]);
        var result = CalcRunner.Run(Calculator, set);

        var area = Assert.Single(result.DefaultNotices, n => n.RuleId == "input.A");
        Assert.Equal("A: not entered, so the default of 200 mm² is used.", area.Message);
        Assert.DoesNotContain("template", area.Message, StringComparison.OrdinalIgnoreCase);
        Assert.All(result.DefaultNotices, n => Assert.True(n.IsDefaultUsed));
        Assert.DoesNotContain(result.EngineeringWarnings, n => n.IsDefaultUsed);

        var sheet = CalcSheetBuilder.Build(Calculator.Template, set, result, GoldenFiles.FixedHeader("x", "x"), GoldenFiles.FixedFooter(Calculator.Template, 1, 1));
        Assert.DoesNotContain(sheet.Warnings, n => n.IsDefaultUsed);
        Assert.Equal(2, sheet.DefaultNotices.Count);

        var text = CalcSheetTextRenderer.Render(sheet);
        Assert.Contains("  Defaults used (inputs left empty):\n    A: not entered, so the default of 200 mm² is used.\n", text, StringComparison.Ordinal);
        Assert.Contains("7. Applicability\n  WARNING  u: ", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Units_Are_Written_As_Symbols_On_The_Sheet()
    {
        Assert.Equal("kN·m", CalcUnitText.Pretty("kN*m"));
        Assert.Equal("m³/h", CalcUnitText.Pretty("m^3/h"));
        Assert.Equal("J/(kg·K)", CalcUnitText.Pretty("J/(kg*K)"));
        Assert.Equal("W/(m²·K)", CalcUnitText.Pretty("W/(m^2*K)"));
        Assert.Equal("°C", CalcUnitText.Pretty("degC"));
        Assert.Equal("mΩ", CalcUnitText.Pretty("mohm"));
        Assert.Equal("Ω·mm²/m", CalcUnitText.Pretty("ohm*mm^2/m"));
        Assert.Equal("°", CalcUnitText.Pretty("deg"));
        Assert.Equal("mm⁻¹", CalcUnitText.Pretty("mm^-1"));
        Assert.Equal(string.Empty, CalcUnitText.Pretty(null));

        var text = CalcSheetTextRenderer.Render(Sheet());
        Assert.Contains("A | Cross-section area | 200 | mm² | manual | ≥ 1 mm²", text, StringComparison.Ordinal);
    }

    // ---- rows 29 and 30: display precision -------------------------

    [Theory]
    [InlineData(144.93187, "MPa", "144.9 MPa")]
    [InlineData(2194.8, "N*m", "2194.8 N·m")]
    [InlineData(2194.8000001, "N*m", "2194.8 N·m")]
    [InlineData(2.0, "kN", "2.00 kN")]
    [InlineData(321190.4, "kN/m", "321190 kN/m")]
    [InlineData(12.34, "mm", "12.3 mm")]
    [InlineData(0.01234, "mm", "0.012 mm")]
    [InlineData(20.918, "µm", "20.9 µm")]
    [InlineData(57.947, "kPa", "57.9 kPa")]
    [InlineData(57947.0, "Pa", "57947 Pa")]
    [InlineData(16667.0, "h", "16667 h")]
    [InlineData(4.4, "m^3/h", "4.4 m³/h")]
    [InlineData(61.5, "degC", "61.5 °C")]
    [InlineData(0.0, "kN", "0 kN")]
    [InlineData(1.5, "furlong/fortnight", "1.5 furlong/fortnight")]
    public void A_Scalar_Is_Shown_At_The_Decimals_Of_Its_Quantity_Type(double displayed, string unit, string expected)
    {
        var shown = UnitCatalog.TryParse(unit, out _, out _) ? ScalarValue.Of(displayed, unit) : new ScalarValue(displayed, Dimension.None, new Unit(unit, Dimension.None, 1.0));
        Assert.Equal(expected, CalcDisplay.Format(shown));
    }

    [Fact]
    public void Display_Rounds_The_Text_Only_And_Never_The_Value()
    {
        var value = ScalarValue.Of(144.93187, "MPa");
        var output = new CalcOutput("sigma", "Stress", value);
        var before = value.Si;
        Assert.Equal("144.9 MPa", output.ValueText);
        Assert.Equal(before, ((ScalarValue)output.Value).Si);
        Assert.Equal(144.93187e6, ((ScalarValue)output.Value).Si, 3);

        // An override of the decimals is for one output.
        Assert.Equal("144.93 MPa", new CalcOutput("sigma", "Stress", value, 2).ValueText);
        Assert.Equal("145 MPa", new CalcOutput("sigma", "Stress", value, 0).ValueText);
    }

    [Fact]
    public void A_Value_Below_A_Billionth_Of_The_Largest_Of_Its_Dimension_Is_Shown_As_Zero()
    {
        var outputs = new[]
        {
            new CalcOutput("sigma_t_a", "Hoop stress, bore", ScalarValue.Of(144.9, "MPa")),
            new CalcOutput("sigma_r_b", "Radial stress, outside", ScalarValue.Of(1.8626e-15, "MPa")),
            new CalcOutput("d", "Diameter", ScalarValue.Of(1.8626e-15, "mm")),
        };

        Assert.Equal(["144.9 MPa", "0 MPa", "1.8626e-15 mm"], CalcDisplay.Texts(outputs));
        Assert.Equal(1.8626e-9, ((ScalarValue)outputs[1].Value).Si, 12);
    }

    [Theory]
    [InlineData(0.94057, "0.94")]
    [InlineData(0.375, "0.38")]
    [InlineData(1.0, "1.00")]
    [InlineData(1.004, "1.01")]
    [InlineData(1.2481, "1.25")]
    [InlineData(0.0, "0.00")]
    [InlineData(double.PositiveInfinity, "∞")]
    public void A_Utilisation_Is_Shown_To_Two_Decimals_And_A_Failing_One_Is_Rounded_Up(double u, string expected) => Assert.Equal(expected, CalcDisplay.Utilisation(u));

    [Fact]
    public void A_Utilisation_Output_And_A_Safety_Factor_Output_Use_Two_Decimals_And_Other_Plain_Numbers_Keep_Five_Figures()
    {
        Assert.Equal("0.94", new CalcOutput("u_life", "Life utilisation", new ScalarValue(0.94057)).ValueText);
        Assert.Equal("0.18", new CalcOutput("x", "Static utilisation", new ScalarValue(0.18182)).ValueText);
        Assert.Equal("2.31", new CalcOutput("n_f_min", "Lowest fatigue safety factor", new ScalarValue(2.3086)).ValueText);
        Assert.Equal("7.8516", new CalcOutput("Re_s1", "Reynolds number", new ScalarValue(7.8516)).ValueText);
        Assert.Equal("84.1 %", new CalcOutput("yield", "Yield", ScalarValue.Of(84.134, "%")).ValueText);
        Assert.Equal("30.00°", new CalcOutput("theta", "Angle", ScalarValue.Of(30, "deg")).ValueText);
    }

    [Fact]
    public void Every_Rule_Of_The_Table_Names_A_Unit_The_Engine_Parses_In_Its_Dimension_And_Is_Listed_Once()
    {
        var seen = new HashSet<(Dimension, string)>();

        foreach (var rule in CalcDisplay.Rules)
        {
            Assert.True(seen.Add((rule.Dimension, rule.Unit)), $"{rule.Unit} is listed twice");
            Assert.True(UnitCatalog.TryParse(rule.Unit.Replace("·", "*", StringComparison.Ordinal), out var unit, out var error), $"{rule.Unit}: {error}");
            Assert.True(unit.Dimension == rule.Dimension, $"{rule.Unit} is not a unit of {rule.Quantity}");
            Assert.InRange(rule.Decimals, 0, 8);
            Assert.Equal(rule.Decimals, CalcDisplay.DecimalsFor(rule.Dimension, rule.Unit));
        }
    }

    // ---- row 31: message shape -------------------------------------

    [Theory]
    [InlineData("A: rating needs the heat transfer area.", true)]
    [InlineData("F_r: the load is too small against C.", true)]
    [InlineData("segments[2].d: enter at least 5 mm.", true)]
    [InlineData("{missing}: the shape needs this dimension.", true)]
    [InlineData("F_r/F_a: the load is too small.", false)]
    [InlineData("sections (Duct sections) needs a value.", false)]
    [InlineData("A: rating needs the area", false)]
    [InlineData("the shaft model could not be solved.", false)]
    public void A_Message_Is_A_Symbol_A_Colon_And_One_Sentence(string message, bool conforms) => Assert.Equal(conforms, CalcMessageShape.Problems(message).Count == 0);

    [Fact]
    public void An_Engine_Failure_Is_Wrapped_In_The_Same_Shape()
    {
        Assert.Equal("calculation: the solver did not converge.", CalcMessageShape.Engine("the solver did not converge"));
        Assert.Equal("F: enter a number.", CalcMessageShape.Engine("F: enter a number."));
        Assert.Equal("calculation: the calculation could not be completed.", CalcMessageShape.Engine(""));
        Assert.Equal("segments", CalcMessageShape.SymbolOf("segments: the shaft model could not be solved."));
    }

    [Fact]
    public void The_Validator_Messages_Follow_The_Shape()
    {
        var template = Calculator.Template with { Inputs = [.. Calculator.Template.Inputs, new CalcInputSpec("mode", "Mode", CalcInputKind.Choice, Choices: ["a", "b"], DefaultChoice: "a")] };
        var set = new CalcInputSet([CalcInputValue.Quantity("F_axial", -5e9, "kN"), CalcInputValue.Quantity("A", 0, "mm^2"), CalcInputValue.Quantity("sigma_allow", 5, "mm"), CalcInputValue.Choice("mode", "z"), CalcInputValue.Of("zz", 1)]);
        var notices = CalcInputValidator.Validate(template, set);

        Assert.True(notices.Count >= 5);
        Assert.All(notices, n => Assert.Empty(CalcMessageShape.Problems(n.Message)));
    }

    // ---- rows 43 to 45: port and input aliases ----------------------

    [Fact]
    public void A_Port_Is_Found_By_Its_Name_Its_Symbol_Or_An_Alias()
    {
        var template = Calculator.Template;
        Assert.Equal("sigma", template.FindPort("stress.axial")!.Symbol);
        Assert.Equal("sigma", template.FindPort("sigma")!.Symbol);
        Assert.Equal("sigma", template.FindPort("sigma_old")!.Symbol);
        Assert.Equal("A_req", template.FindPort("stress.area_required")!.Symbol);
        Assert.Null(template.FindPort("stress.nothing"));
        Assert.Equal("stress.axial", template.FindPort("sigma_old")!.PublicName);
        Assert.Equal("x", new CalcOutputPort("x", "", "No name yet").PublicName);
    }

    [Fact]
    public void An_Input_Is_Found_By_Its_Symbol_Or_By_A_Symbol_It_Had_Before()
    {
        var template = Calculator.Template;
        Assert.Equal("F_axial", template.ResolveInput("F")!.Symbol);
        Assert.Equal("F_axial", template.ResolveInput("F_old")!.Symbol);
        Assert.Equal("F_axial", template.ResolveInput("F_axial")!.Symbol);
        Assert.Null(template.FindInput("F"));
        Assert.Null(template.ResolveInput("G"));
        Assert.Equal("F_axial", CalcInputAliases.Canonical(template, "F"));
        Assert.Equal("G", CalcInputAliases.Canonical(template, "G"));
    }

    [Fact]
    public void An_Input_Given_Under_An_Old_Symbol_Runs_Exactly_As_Under_The_New_One()
    {
        var oldForm = new CalcInputSet([CalcInputValue.Quantity("F", 12, "kN", CalcProvenance.Drawing("DRG-1", "A")), CalcInputValue.Quantity("A", 200, "mm^2")]);
        var newForm = new CalcInputSet([CalcInputValue.Quantity("F_axial", 12, "kN", CalcProvenance.Drawing("DRG-1", "A")), CalcInputValue.Quantity("A", 200, "mm^2")]);

        var a = CalcRunner.Run(Calculator, oldForm);
        var b = CalcRunner.Run(Calculator, newForm);

        Assert.False(a.IsBlocked);
        Assert.Equal(b.OutputSi("sigma"), a.OutputSi("sigma"));
        Assert.Equal(60e6, a.OutputSi("sigma"), 3);
        Assert.Equal(b.Notices.Select(n => n.Message), a.Notices.Select(n => n.Message));
        Assert.DoesNotContain(a.Notices, n => n.Message.Contains("not an input", StringComparison.Ordinal));
    }

    [Fact]
    public void When_Both_Symbols_Are_Given_The_Current_One_Wins()
    {
        var both = new CalcInputSet([CalcInputValue.Quantity("F", 99, "kN"), CalcInputValue.Quantity("F_axial", 12, "kN")]);
        var set = CalcInputAliases.Canonicalise(Calculator.Template, both);
        Assert.Equal(12, set.Find("F_axial")!.Number);
        Assert.Null(set.Find("F"));
    }

    [Fact]
    public void An_Old_Inputs_Json_Still_Loads_With_The_Template()
    {
        var json = CalcInputJson.Write(new CalcInputSet([CalcInputValue.Quantity("F", 12, "kN", CalcProvenance.Manual), CalcInputValue.Quantity("A", 200, "mm^2")]));
        Assert.Contains("\"Symbol\": \"F\"", json, StringComparison.Ordinal);

        var (inputs, notices) = CalcInputJson.Read(json, Calculator.Template);
        Assert.Empty(notices);
        Assert.Equal(12, inputs.Find("F_axial")!.Number);
        Assert.Null(inputs.Find("F"));

        // Without a template the file is read as it is written.
        Assert.NotNull(CalcInputJson.Read(json).Inputs.Find("F"));
    }

    [Fact]
    public void An_Old_Csv_Still_Imports_And_The_Export_Writes_The_Current_Symbol()
    {
        var (inputs, notices) = CalcCsv.ImportInputs(Calculator.Template, "symbol,value,unit\nF,12,kN\nA,200,mm^2\n");
        Assert.DoesNotContain(notices, n => n.Severity == CalcNoticeSeverity.Block);
        Assert.DoesNotContain(notices, n => n.Message.Contains("not an input", StringComparison.Ordinal));
        Assert.Equal(12, inputs.Find("F_axial")!.Number);

        var csv = CalcCsv.ExportInputs(Calculator.Template, inputs);
        Assert.Contains("F_axial,12,kN", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("\nF,12", csv, StringComparison.Ordinal);
    }

    [Fact]
    public void The_Sheet_Shows_The_Current_Symbol_Of_An_Input_Given_Under_An_Old_One()
    {
        var set = new CalcInputSet([CalcInputValue.Quantity("F", 12, "kN", CalcProvenance.Manual)]);
        var text = CalcSheetTextRenderer.Render(CalcSheetBuilder.Build(Calculator.Template, set, CalcRunner.Run(Calculator, set), GoldenFiles.FixedHeader("x", "x"), GoldenFiles.FixedFooter(Calculator.Template, 1, 1)));
        Assert.Contains("  F_axial | Axial load | 12 | kN | manual |", text, StringComparison.Ordinal);
        Assert.DoesNotContain("  F | Axial load", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Names_And_Aliases_Change_The_Hash_Only_Of_A_Template_That_Has_Them()
    {
        var plain = DirectStressCalculator.TemplateDefinition;
        var text = CalcTemplateHasher.CanonicalText(plain, []);
        Assert.DoesNotContain("aliases=", text, StringComparison.Ordinal);
        Assert.DoesNotContain("|name=", text, StringComparison.Ordinal);

        var hashed = CalcTemplateHasher.CanonicalText(Calculator.Template, []);
        Assert.Contains("  aliases=F,F_old", hashed, StringComparison.Ordinal);
        Assert.Contains("port=sigma|MPa|Direct stress|name=stress.axial|aliases=sigma_old", hashed, StringComparison.Ordinal);
        Assert.Contains("port=A_req|mm^2|Required area|name=stress.area_required\n", hashed, StringComparison.Ordinal);

        // A port that names nothing hashes as it always did.
        var oldPort = plain with { OutputPorts = [new CalcOutputPort("sigma", "MPa", "Stress")] };
        Assert.Contains("port=sigma|MPa|Stress\n", CalcTemplateHasher.CanonicalText(oldPort, []), StringComparison.Ordinal);
    }
}
