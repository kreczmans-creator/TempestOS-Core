using System.Globalization;
using System.Text.RegularExpressions;
using Tempest.Calc.Sheets;
using Tempest.Calc.Verification;

namespace Tempest.Calc.Tests.Calculators;

/// <summary>
/// C2-20, catalogue-wide: no sheet prints the digits of round-off. A number whose magnitude is below 1e-12 and is not exactly 0 is floating-point noise
/// (a residual, a computed zero), and its digits depend on the platform's maths library, so a golden or a worked example that printed one could not match
/// on both Linux and Windows (the Windows CI run of 9c92e672 printed 2.2428e-16 where Linux printed 1.9375e-16). Such a value is shown as 0 when its
/// template flags it noise-prone, or as "below" its stated floor when it is a residual; this test keeps the digits from coming back. Checked: the sheet of
/// every calculator on its defaults and on every known-answer case, the sheet of every worked example, and every committed golden and worked-example
/// sheet text (the pack sheets included).
/// </summary>
public sealed class RoundOffDigitsContractTests
{
    /// <summary>The smallest magnitude a sheet may print, other than an exact 0.</summary>
    public const double Smallest = 1e-12;

    // A number standing on its own: not part of a word, a version or a longer number; a sign written against it; a decimal part; an exponent.
    private static readonly Regex Number = new(@"(?<![\w.])[-−]?\d+(?:\.\d+)?(?:e[-+]?\d+)?(?![\d.])", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, TimeSpan.FromSeconds(5));

    /// <summary>The numbers in <paramref name="text"/> that are not 0 and whose magnitude is below <see cref="Smallest"/>, each with the line it is on.</summary>
    public static IReadOnlyList<string> RoundOffNumbers(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var found = new List<string>();

        foreach (var line in text.ReplaceLineEndings("\n").Split('\n'))
        {
            foreach (Match m in Number.Matches(line))
            {
                if (double.TryParse(m.Value.Replace('−', '-'), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && value != 0 && Math.Abs(value) < Smallest)
                    found.Add($"{m.Value} in \"{line.Trim()}\"");
            }
        }

        return found;
    }

    private static void AssertNone(string where, string text)
    {
        var found = RoundOffNumbers(text);
        Assert.True(found.Count == 0, $"{where} prints round-off digits (a number below {Smallest} that is not 0; C2-20): {string.Join("; ", found.Take(5))}");
    }

    private static string Render(ICalcCalculator calculator, CalcInputSet inputs)
    {
        var template = calculator.Template;
        var result = CalcRunner.Run(calculator, inputs);
        var sheet = CalcSheetBuilder.Build(template, inputs, result, GoldenFiles.FixedHeader(template.Id, template.Title), GoldenFiles.FixedFooter(template, 0, 0));
        return CalcSheetTextRenderer.Render(sheet);
    }

    [Theory]
    [MemberData(nameof(AllCalculators.Ids), MemberType = typeof(AllCalculators))]
    public void No_Sheet_Of_A_Calculator_On_Its_Defaults_Or_A_Known_Answer_Case_Prints_Round_Off_Digits(string id)
    {
        var calculator = AllCalculators.Get(id);
        var template = calculator.Template;
        AssertNone($"{id} on its defaults", Render(calculator, CalcTemplateHasher.DefaultInputs(template)));

        foreach (var testCase in template.TestCases)
        {
            var inputs = CalcTemplateHasher.NewSheetInputs(template);

            foreach (var value in testCase.Inputs)
                inputs.Set(value);

            AssertNone($"{id} / {testCase.Id}", Render(calculator, inputs));
        }
    }

    [Theory]
    [MemberData(nameof(WorkedExamples.Keys), MemberType = typeof(WorkedExamples))]
    public void No_Worked_Example_Sheet_Prints_Round_Off_Digits(string key)
    {
        var example = WorkedExamples.Get(key);
        AssertNone(key, Render(AllCalculators.Get(example.TemplateId), example.Inputs));
    }

    [Fact]
    public void No_Committed_Golden_Or_Worked_Example_Sheet_Prints_Round_Off_Digits()
    {
        var root = WorkedExamples.RepositoryRoot;
        var golden = Path.Combine(root, "tests", "Tempest.Calc.Tests.Calculators", "Golden");
        var examples = Path.Combine(root, "docs", "worked-examples");
        var files = Directory.EnumerateFiles(golden, "*.txt", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(examples, "*.txt", SearchOption.AllDirectories))
            .Order(StringComparer.Ordinal)
            .ToList();

        // The goldens and the sheets of the worked examples and the packs are all there to be read.
        Assert.True(files.Count >= 100, $"Only {files.Count} sheet texts were found under {golden} and {examples}.");
        var offenders = files.Select(f => (File: Path.GetRelativePath(root, f), Found: RoundOffNumbers(File.ReadAllText(f)))).Where(x => x.Found.Count > 0).ToList();
        Assert.True(offenders.Count == 0, "Sheets that print round-off digits (C2-20): " + string.Join("; ", offenders.Select(o => $"{o.File}: {string.Join(", ", o.Found.Take(3))}")));
    }

    [Theory]
    [InlineData("      = 1.9375e-16", "1.9375e-16")]
    [InlineData("      = |-1.4552e-14| kN·m", "-1.4552e-14")]
    [InlineData("      = sqrt(((33.333 MPa − 1.8626e-15 MPa)^2", "1.8626e-15")]
    [InlineData("  j_sw = [170.55, 44.141, 1.0443e-14, -44.141] m/s³", "1.0443e-14")]
    [InlineData("  P_mean = −4.375e-13 kW", "−4.375e-13")]
    [InlineData("  x = 0.0000000000001 mm", "0.0000000000001")]
    public void The_Scan_Finds_A_Round_Off_Number_Wherever_A_Sheet_Prints_It(string line, string number)
    {
        var found = Assert.Single(RoundOffNumbers(line));
        Assert.StartsWith(number + " ", found, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("  res_max = 0 mm    Largest loop-closure residual")]
    [InlineData("  res_vel_max = below 1e-12    Largest velocity-loop residual")]
    [InlineData("  PASS  Loop-closure residual against its limit: max residual below 1e-10 mm vs < 1e-9 mm")]
    [InlineData("  [2] p_atm = 101.325 (1 − 2.25577e-5 z)^5.2559")]
    [InlineData("  J_motor | Candidate motor rotor inertia | 0.0002 | kg·m² | manual | 1e-7 … 10 kg·m²")]
    [InlineData("Template mech.mechanism@1.3.1 · 2026-10-27 · v0.26.0 · S235 · 3e5")]
    public void The_Scan_Leaves_Zero_A_Stated_Floor_And_Ordinary_Numbers_Alone(string line) => Assert.Empty(RoundOffNumbers(line));
}
