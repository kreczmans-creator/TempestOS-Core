using System.Runtime.CompilerServices;
using System.Text.Json;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators.Consistency;

/// <summary>
/// C-2 renamed output ports (<c>Q</c> → <c>Phi</c>, <c>t_m</c> → <c>T_m</c>, <c>I_xx</c> → <c>I_y</c> ...) and gave every port a public name. A link,
/// a pack binding or an inputs file written before the rename names the old symbol or the old public name, and must still resolve
/// (<see cref="CalcOutputPort.Aliases"/>, <see cref="CalcTemplate.FindPort"/>). <c>PortsBaseline.json</c> holds every declared port of the catalogue at
/// 02260766 (the base of the C-2 branches), by template, symbol and public name; this contract holds every one of them to resolve on the current
/// catalogue, by its symbol, its public name or an alias of the port it became. A template that declared no ports on the base offered every output
/// and now offers only its declared ports: a stored link to another output keeps resolving, a new one needs a declared port (C-2 review, §5).
/// </summary>
public sealed class RenamedPortContractTests
{
    private sealed record BasePort(string symbol, string? name);

    private static string BaselinePath([CallerFilePath] string callerFilePath = "") => Path.Combine(Path.GetDirectoryName(callerFilePath)!, "PortsBaseline.json");

    private static readonly Lazy<Dictionary<string, List<BasePort>>> Base = new(() => JsonSerializer.Deserialize<Dictionary<string, List<BasePort>>>(File.ReadAllText(BaselinePath()))!);

    public static TheoryData<string> TemplatesWithPortsOnTheBase => [.. Base.Value.Keys.Order(StringComparer.Ordinal)];

    [Theory]
    [MemberData(nameof(TemplatesWithPortsOnTheBase))]
    public void Every_Port_The_Base_Declared_Still_Resolves_By_Its_Old_Symbol_And_Its_Old_Public_Name(string id)
    {
        var template = AllCalculators.Get(id).Template;
        var problems = new List<string>();

        foreach (var port in Base.Value[id])
        {
            var bySymbol = template.FindPort(port.symbol);
            var byName = port.name is { Length: > 0 } ? template.FindPort(port.name) : bySymbol;

            if (bySymbol is null)
                problems.Add($"{port.symbol} ({port.name}) was a port on 02260766 and resolves to none now: the port it became must list '{port.symbol}' in its Aliases");
            else if (byName is null)
                problems.Add($"{port.name} was the public name of {port.symbol} on 02260766 and resolves to none now: the port it became must list it in its Aliases");
            else if (!ReferenceEquals(bySymbol, byName))
                problems.Add($"{port.symbol} resolves to {bySymbol.Symbol} but its old public name {port.name} resolves to {byName.Symbol}");
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void Every_Alias_Of_Every_Port_Is_A_Symbol_Or_Name_No_Other_Port_Of_The_Template_Has()
    {
        var problems = new List<string>();

        foreach (var calculator in AllCalculators.All)
        {
            var ports = calculator.Template.OutputPorts ?? [];

            foreach (var port in ports)
            {
                foreach (var alias in port.Aliases ?? [])
                {
                    var others = ports.Where(p => !ReferenceEquals(p, port) && p.Matches(alias)).Select(p => p.Symbol).ToList();

                    if (others.Count > 0)
                        problems.Add($"{calculator.Template.Id}: the alias {alias} of {port.Symbol} is also {string.Join(", ", others)}");
                }
            }
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }
}
