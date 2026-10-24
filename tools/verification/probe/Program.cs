// Probe: run a TempestOS calculator from the command line (Addendum C Track P aid).
//   probe list                      ids and versions
//   probe kat <id>                  run the declared known-answer cases
//   probe run <id> <inputs.json>    run one input set (CalcInputJson list); prints outputs, checks, notices
//   probe steps <id> <inputs.json>  as run, plus every worksheet step
//   probe template <id>             inputs, rules and output ports as JSON
using System.Text.Json;
using Tempest.Calc.Engine.Values;
using Tempest.Calc.Sheets;
using Tempest.Calc.Verification;

var all = Tempest.Calc.Mechanical.MechanicalCalculators.All
    .Concat(Tempest.Calc.Structural.StructuralCalculators.All)
    .Concat(Tempest.Calc.Electrical.ElectricalCalculators.All)
    .Concat(Tempest.Calc.BuildingServices.BuildingServicesCalculators.All)
    .ToDictionary(c => c.Template.Id, StringComparer.Ordinal);

if (args.Length == 0 || args[0] == "list")
{
    foreach (var c in all.Values) Console.WriteLine($"{c.Template.Id}\t{c.Template.Version}\t{c.Template.Title}");
    return 0;
}

if (args.Length < 2 || !all.TryGetValue(args[1], out var calc))
{
    Console.Error.WriteLine("Unknown calculator. Use: probe list");
    return 2;
}

switch (args[0])
{
    case "kat":
        var report = KnownAnswerRunner.Run(calc);
        foreach (var o in report.Outcomes)
        {
            Console.WriteLine($"{o.Case.Id} [{o.Case.Status}] {(o.Passed ? "PASS" : "FAIL")} {o.Problem}");
            foreach (var k in o.Checks)
                Console.WriteLine($"  {k.Symbol,-12} expected {k.Expected,14:G8} {k.Unit,-8} actual {k.Actual,14:G8} relerr {k.RelativeError:E2} tol {k.RelativeTolerance:E1} {(k.Passed ? "ok" : "FAIL " + k.Problem)}");
        }
        Console.WriteLine($"{report.Passed}/{report.Total} passed");
        return report.AllPassed ? 0 : 1;

    case "template":
        var t = calc.Template;
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            t.Id, t.Version, t.Title, t.Method,
            Inputs = t.Inputs.Select(i => new { i.Symbol, i.Description, Kind = i.Kind.ToString(), i.DefaultUnit, i.Default, i.Min, i.Max, i.HardMin, i.HardMax, i.Choices, i.IsOptional }),
            Rules = t.ApplicabilityRules.Select(r => new { r.Id, r.Description, Severity = r.Severity.ToString() }),
            Ports = t.OutputPorts?.Select(p => new { p.Symbol, p.Unit, p.Description }),
        }, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
        return 0;

    case "run":
    case "steps":
        if (args.Length < 3) { Console.Error.WriteLine("Give an inputs JSON file."); return 2; }
        var (inputs, readNotices) = CalcInputJson.Read(File.ReadAllText(args[2]));
        foreach (var n in readNotices) Console.WriteLine($"NOTICE {n.Severity} {n.RuleId}: {n.Message}");
        var r = CalcRunner.Run(calc, inputs);
        if (args[0] == "steps")
            foreach (var s in r.Steps) Console.WriteLine($"STEP {s.Symbol} = {s.Formula} | {s.Substitution} | {s.ResultText}");
        foreach (var o in r.Outputs)
            Console.WriteLine(o.Value is ScalarValue sv ? $"OUT {o.Symbol}\t{sv.Si:G10}\t(SI)\t{o.ValueText}" : $"OUT {o.Symbol}\t{o.ValueText}");
        foreach (var c in r.Checks) Console.WriteLine($"CHECK {c.Name}\tu={c.Utilisation:G6}\t{(c.Passed ? "pass" : "FAIL")}\t{c.DemandText} vs {c.CapacityText}");
        foreach (var n in r.Notices) Console.WriteLine($"NOTICE {n.Severity} {n.RuleId}: {n.Message}");
        return r.IsBlocked ? 3 : 0;

    default:
        Console.Error.WriteLine("Commands: list | kat <id> | run <id> <json> | steps <id> <json> | template <id>");
        return 2;
}
