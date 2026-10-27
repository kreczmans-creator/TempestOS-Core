using System.Text;
using Tempest.Calc.Library;
using Tempest.Calc.Sheets;
using Tempest.Calc.Structural;
using Tempest.Calc.Structural.Frame;

namespace Tempest.Calc.Tests.Library;

/// <summary>
/// DC-15 and DC-21: the frame table caps (<see cref="FrameLimits"/>) are what the store's 4 MB document cap holds with margin. This test builds the
/// largest frame within the caps in the worst form the store can write it (every column filled, the longest number the serialiser writes, a
/// <c>large-deflection</c> warning on every member, the detailed outputs) and holds its stored size under 85 % of <see cref="CalcInstanceStore.MaxDocumentBytes"/>.
/// The per-row sizes it measures are the ones the spec page and the <see cref="FrameLimits"/> summary quote.
/// </summary>
public sealed class FrameSizeCapTests
{
    /// <summary>The longest text the serialiser writes for a double: 17 significant figures, a sign, a point and an exponent (24 characters).</summary>
    private const double Longest = -1.2345678901234567E-300;

    private static CalcCell N(string unit) => new(Longest, unit);

    private static CalcCell N() => new(Longest);

    private static CalcRow NodeRow() => CalcRow.Of(("id", N()), ("x", N("m")), ("y", N("m")), ("support", new CalcCell(Text: FrameCalculator.SupportRollerX)), ("k_x", N("kN/m")), ("k_y", N("kN/m")), ("k_r", N("kN*m")));

    private static CalcRow ElementRow() => CalcRow.Of(("id", N()), ("i", N()), ("j", N()), ("A", N("cm^2")), ("I", N("cm^4")), ("E", N("GPa")), ("rel_i", new CalcCell(Flag: false)), ("rel_j", new CalcCell(Flag: false)));

    private static CalcRow LoadRow() => CalcRow.Of(("case", N()), ("kind", new CalcCell(Text: "trapezoid")), ("target", N()), ("a", N("m")), ("b", N("m")), ("w1", N("kN/m")), ("w2", N("kN/m")), ("F_x", N("kN")), ("F_y", N("kN")), ("M", N("kN*m")), ("gravity", new CalcCell(Flag: true)));

    private static CalcRow CombinationRow() => CalcRow.Of(("name", new CalcCell(Text: new string('x', 40))), ("case", N()), ("factor", N()));

    private static int Bytes<T>(T record) => Encoding.UTF8.GetByteCount(CalcInstanceStore.Serialise(record));

    /// <summary>The stored bytes of one row: the table of n rows less the table of n − 1 rows.</summary>
    private static int RowBytes(Func<CalcRow> row) =>
        Bytes(CalcInputValue.Table("t", [row(), row()])) - Bytes(CalcInputValue.Table("t", [row()]));

    [Fact]
    public void A_Node_Row_Stores_In_364_Bytes_An_Element_Row_In_340_And_A_Load_Row_In_526_At_The_Longest_Number()
    {
        Assert.Equal(24, CalcInstanceStore.Serialise(Longest).Length);
        Assert.Equal(364, RowBytes(NodeRow));
        Assert.Equal(340, RowBytes(ElementRow));
        Assert.Equal(526, RowBytes(LoadRow));
        Assert.Equal(159, RowBytes(CombinationRow));
    }

    [Fact]
    public void The_Largest_Frame_Within_The_Caps_Stores_Under_The_Document_Cap_With_Margin()
    {
        var template = new FrameCalculator().Template;
        var provenance = CalcProvenance.Drawing("DRG-FRAME-001", "C");
        var inputs = new List<CalcInputValue>
        {
            CalcInputValue.Table("nodes", [.. Enumerable.Range(0, template.FindInput("nodes")!.MaxRows).Select(_ => NodeRow())], provenance),
            CalcInputValue.Table("elements", [.. Enumerable.Range(0, template.FindInput("elements")!.MaxRows).Select(_ => ElementRow())], provenance),
            CalcInputValue.Table("loads", [.. Enumerable.Range(0, template.FindInput("loads")!.MaxRows).Select(_ => LoadRow())], provenance),
            CalcInputValue.Table("combinations", [.. Enumerable.Range(0, template.FindInput("combinations")!.MaxRows).Select(_ => CombinationRow())], provenance),
            CalcInputValue.Quantity("E", 210, "GPa", provenance),
            CalcInputValue.Of("n_div", FrameLimits.MaxStations, provenance),
            new CalcInputValue("report", Text: new string('r', 40), Provenance: provenance),
        };

        // The detailed outputs (50 members × 6, 50 supports × 5, the extremes and the envelope) and a large-deflection warning on every member.
        var outputs = Enumerable.Range(1, 567).Select(i => new CalcOutputSnapshot($"M_abs_e{i}", $"Member {i} greatest moment magnitude (sagging or hogging)", "-123456.78 kN·m", Longest, "kN*m")).ToList();
        var warnings = Enumerable.Range(1, FrameLimits.MaxElements).Select(i => new CalcNoticeSnapshot("large-deflection", "Warning", $"elements: member {i} deflects 123456.78 mm, more than L/50 of the 12345.678 m straight chain of members it belongs to.")).ToList();
        var now = DateTimeOffset.UtcNow;
        var instance = new CalcInstance(Guid.NewGuid(), template.Reference, new string('a', 64), Guid.NewGuid(), "Frame at its caps", "A", inputs, outputs, [], warnings, [], CalcInstanceStatus.Draft, Guid.NewGuid(), Guid.NewGuid(), "test.engineer", now, now);

        var bytes = Bytes(instance);
        var tables = FrameLimits.MaxNodes * 364 + FrameLimits.MaxElements * 340 + FrameLimits.MaxLoads * 526;
        Assert.Equal(2_515_200, tables);
        Assert.True(bytes > tables, $"the document ({bytes:N0} bytes) holds at least its tables ({tables:N0})");
        Assert.True(bytes <= 0.85 * CalcInstanceStore.MaxDocumentBytes, $"the largest frame within the caps stores in {bytes:N0} bytes, above 85 % of the {CalcInstanceStore.MaxDocumentBytes:N0}-byte cap");
        Assert.Equal((1_200, 2_400, 2_400), (FrameLimits.MaxNodes, FrameLimits.MaxElements, FrameLimits.MaxLoads));
    }
}
