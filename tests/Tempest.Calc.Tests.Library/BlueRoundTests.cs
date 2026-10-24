using System.Reflection;
using System.Text;
using Tempest.Calc.Library;
using Tempest.Calc.Sheets;
using Tempest.Core.Evidence;
using Tempest.Core.Persistence;

namespace Tempest.Calc.Tests.Library;

/// <summary>A calculator with one declared output port and one output that is not a port (a factor it echoes).</summary>
internal sealed class PortedCalculator : ICalcCalculator
{
    public CalcTemplate Template { get; } = new(
        "test.ported", "1.0.0", CalcDiscipline.Mechanical, "Ported",
        "Gives one output to link to and echoes a factor it does not offer.",
        "Output: a = 2·P; echoed factor b = P.",
        [new CalcInputSpec("P", "Input force", CalcInputKind.Quantity, "kN", Default: 5, Min: 0, Max: 1e9, HardMin: -1e9, HardMax: 1e9)],
        [new CalcAssumption("Test only.", "—")],
        [new CalcClauseRef("Test", "—", "Test")],
        [],
        [
            new CalcTestCase("ka-1", "5 kN", new CalcReferenceSource("Hand", null, "2 × 5"), [CalcInputValue.Quantity("P", 5, "kN")], [new CalcExpectedValue("a", 10, "kN", 1e-9)]),
            new CalcTestCase("ka-2", "1 kN", new CalcReferenceSource("Hand", null, "2 × 1"), [CalcInputValue.Quantity("P", 1, "kN")], [new CalcExpectedValue("a", 2, "kN", 1e-9)]),
        ],
        OutputPorts: [new CalcOutputPort("a", "kN", "Offered output")]);

    public void Run(CalcRunContext c)
    {
        c.Input("P");
        c.Assign("a", "2 * P", "kN", "Offered output");
        c.Assign("b", "P", "kN", "Echoed factor");
        c.Output("a", "Offered output");
        c.Output("b", "Echoed factor");
    }
}

/// <summary>Counts the calls a store receives, by method name.</summary>
public class CountingProxy : DispatchProxy
{
    public IQueryablePersistenceStore Target { get; set; } = null!;

    public Dictionary<string, int> Counts { get; } = [];

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(targetMethod);
        Counts[targetMethod.Name] = Counts.GetValueOrDefault(targetMethod.Name) + 1;

        try
        {
            return targetMethod.Invoke(Target, args);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw ex.InnerException;
        }
    }
}

/// <summary>Blue round (Addendum A): the fixes to the Red, Security, Green and Black findings on linking, records, packs and sheets.</summary>
public sealed class BlueRoundTests
{
    private static LibraryTestHost RecordedHost() => new(calculators: [new RecordedCalculator()]);

    private static CalcInputValue Ratings(params double[] amps) =>
        CalcInputValue.Table("ratings", [.. amps.Select((a, i) => CalcRow.Of(("S", new CalcCell(2.5 * (i + 1), "mm^2")), ("I_t", new CalcCell(a, "A"))))]);

    private static CalcDataRecord Draft(bool licensed, double k = 0.8, params double[] amps) => new(
        Guid.Empty, "test.factors", "Grouping factors", 0, "Test Wiring Rules", "2018+A2:2022", "Table 4C1", "IET (own copy)", "s.kreczman",
        licensed, CalcDataOrigin.UserEntered,
        [new CalcDataValue("k_1", k), new CalcDataValue("ratings", Rows: Ratings(amps.Length == 0 ? [13.7, 21.9, 27.7] : amps).Rows)], DateTimeOffset.UnixEpoch);

    private static readonly CalcProvenance Typed = CalcProvenance.Specification("typed");

    private static async Task<CalcDataRecord> SaveAsync(LibraryTestHost host, CalcDataRecord draft)
    {
        var (record, refusal) = await host.Service.SaveRecordAsync(draft);
        Assert.True(record is not null, refusal);
        return record!;
    }

    private static Task<CalcInstance> TypedCalculationAsync(LibraryTestHost host, Guid? project, string name = "Feeder") =>
        host.Service.CreateAsync("test.recorded", project, name, [CalcInputValue.Of("k_1", 1, Typed), Ratings(20) with { Provenance = Typed }]);

    private static async Task<string> PackSheetAsync(LibraryTestHost host, CalcPack pack)
    {
        var stored = (await host.Store.FindPackAsync(pack.Id))!;
        var evidence = (Evidence)(await host.Context.Repository.FindAsync(stored.EvidenceId!.Value))!;
        var attachment = Assert.Single(await evidence.GetAttachmentsAsync());
        return Encoding.UTF8.GetString((await evidence.ReadAttachmentContentAsync(attachment.Id)).Bytes);
    }

    // ------------------------------------------------------------------ R-A-01 / SEC-A-01

    [Fact]
    public async Task R_A_01_An_Issued_Sheet_Withholds_The_Rows_Of_A_Licensed_Table_And_Shows_The_Value_Used()
    {
        var host = RecordedHost();
        var project = await host.ProjectAsync();
        var record = await SaveAsync(host, Draft(licensed: true));
        var calc = await TypedCalculationAsync(host, project);
        Assert.True((await host.Service.ApplyRecordAsync(calc.Id, record.Id)).Succeeded);
        Assert.True((await host.Service.CheckAsync(calc.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted)).Succeeded);
        Assert.True((await host.Service.IssueAsync(calc.Id, "ISS-1", "Client Co")).Succeeded);

        var sheet = (await host.Store.FindInstanceAsync(calc.Id))!.Issue!.SheetText;

        Assert.Contains("rows withheld (licensed record); the values used appear in the working", sheet, StringComparison.Ordinal);
        Assert.Contains(record.Pin, sheet, StringComparison.Ordinal);
        Assert.Contains("Grouping factors — Test Wiring Rules, 2018+A2:2022, Table 4C1 (licensed source) rev 1", sheet, StringComparison.Ordinal);
        Assert.DoesNotContain("13.7", sheet, StringComparison.Ordinal);
        Assert.DoesNotContain("21.9", sheet, StringComparison.Ordinal);

        // The value used (the largest rating the calculation took) and the scalar from the licensed record stay: they are the calculation's own.
        Assert.Contains("27.7", sheet, StringComparison.Ordinal);
        Assert.Contains("0.8", sheet, StringComparison.Ordinal);
    }

    [Fact]
    public async Task R_A_01_An_Issued_Pack_Sheet_And_A_Preview_Withhold_The_Licensed_Rows_Too()
    {
        var host = RecordedHost();
        var project = await host.ProjectAsync();
        var record = await SaveAsync(host, Draft(licensed: true));
        var calc = await TypedCalculationAsync(host, project);
        Assert.True((await host.Service.ApplyRecordAsync(calc.Id, record.Id)).Succeeded);

        // The preview of a draft is withheld as well: it is what the PDF is made from.
        var preview = (await host.Service.RunAsync(calc.Id))!.SheetText;
        Assert.DoesNotContain("13.7", preview, StringComparison.Ordinal);
        Assert.Contains("rows withheld (licensed record)", preview, StringComparison.Ordinal);

        var pack = await host.Service.CreatePackAsync(project, "Feeder pack", [calc.Id]);
        Assert.True((await host.Service.CheckPackAsync(pack.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted)).Succeeded);
        Assert.True((await host.Service.IssuePackAsync(pack.Id, "PK-1", "Client Co")).Succeeded);

        var sheet = await PackSheetAsync(host, pack);
        Assert.Contains("rows withheld (licensed record)", sheet, StringComparison.Ordinal);
        Assert.DoesNotContain("13.7", sheet, StringComparison.Ordinal);
        Assert.DoesNotContain("21.9", sheet, StringComparison.Ordinal);
        Assert.Contains("27.7", sheet, StringComparison.Ordinal);
    }

    [Fact]
    public async Task R_A_01_An_Unlicensed_Record_Table_Is_Still_Printed()
    {
        var host = RecordedHost();
        var record = await SaveAsync(host, Draft(licensed: false));
        var calc = await TypedCalculationAsync(host, await host.ProjectAsync());
        Assert.True((await host.Service.ApplyRecordAsync(calc.Id, record.Id)).Succeeded);

        var sheet = (await host.Service.RunAsync(calc.Id))!.SheetText;

        Assert.Contains("I_t = 13.7 A", sheet, StringComparison.Ordinal);
        Assert.DoesNotContain("withheld", sheet, StringComparison.Ordinal);
    }

    [Fact]
    public async Task B_A_05_A_Licensed_Table_Stays_Withheld_Through_The_CSV_Round_Trip_And_An_Unlicensed_One_Comes_Back()
    {
        var host = RecordedHost();
        var template = new RecordedCalculator().Template;
        var project = await host.ProjectAsync();
        var licensed = await SaveAsync(host, Draft(licensed: true));
        var open = await SaveAsync(host, Draft(licensed: false));
        var calc = await TypedCalculationAsync(host, project);

        // Unlicensed: the table is written and read back whole.
        var applied = (await host.Service.ApplyRecordAsync(calc.Id, open.Id)).Instance!;
        var openCsv = CalcCsv.ExportInputs(template, await host.Service.WithholdLicensedAsync(new CalcInputSet(applied.Inputs)));
        var (back, openNotices) = CalcCsv.ImportInputs(template, openCsv);
        Assert.Empty(openNotices);
        Assert.Equal(3, back.Find("ratings")!.Rows!.Count);
        Assert.Equal(21.9, back.Find("ratings")!.Rows![1].Get("I_t")!.Number);

        // Licensed: no value of the table, or the factor, is in the file, and none comes back.
        applied = (await host.Service.ApplyRecordAsync(calc.Id, licensed.Id)).Instance!;
        var csv = CalcCsv.ExportInputs(template, await host.Service.WithholdLicensedAsync(new CalcInputSet(applied.Inputs)));
        Assert.DoesNotContain("13.7", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("21.9", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("27.7", csv, StringComparison.Ordinal);
        Assert.Contains("ratings,,,Reference,record:", csv, StringComparison.Ordinal);

        var (withheld, notices) = CalcCsv.ImportInputs(template, csv);
        Assert.Null(withheld.Find("ratings")!.Rows);
        Assert.True(CalcCsv.IsWithheld(withheld.Find("ratings")!.Provenance));
        Assert.Null(withheld.Find("k_1"));
        Assert.Equal(2, notices.Count(n => n.Message.Contains("withheld", StringComparison.Ordinal)));
        Assert.Contains(notices, n => n.Severity == CalcNoticeSeverity.Block && n.Message.Contains("k_1: this value was withheld", StringComparison.Ordinal));
    }

    // ------------------------------------------------------------------ R-A-02 / SEC-A-02

    [Fact]
    public async Task R_A_02_A_Calculation_Is_Not_Checked_Or_Issued_On_A_Draft_Upstream_Unless_They_Go_In_One_Pack()
    {
        var host = new LibraryTestHost(calculators: [new ForceSourceCalculator()]);
        var project = await host.ProjectAsync();
        var motor = await host.Service.CreateAsync("test.force-source", project, "Motor torque", [CalcInputValue.Quantity("P", 5, "kN", CalcProvenance.Drawing("DRG-9", "A"))]);
        var bracket = await host.Service.CreateAsync("test.direct-stress", project, "Bracket", LibraryTestHost.ProvenancedInputs());
        Assert.True((await host.Service.BindAsync(bracket.Id, "F", motor.Id, "F_out")).Succeeded);

        var check = await host.Service.CheckAsync(bracket.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted);
        Assert.False(check.Succeeded);
        Assert.Contains("'Motor torque' rev A, which is still a draft", check.Reason, StringComparison.Ordinal);

        // Issue holds the same line: a checked calculation whose upstream is a draft (forced here through the store) is not issued.
        var forced = (await host.Store.FindInstanceAsync(bracket.Id))!;
        await host.Store.SaveInstanceAsync(forced with { Status = CalcInstanceStatus.Checked }, forced.UpdatedAt);
        var issue = await host.Service.IssueAsync(bracket.Id, "ISS-1", "Client Co");
        Assert.False(issue.Succeeded);
        Assert.Contains("still a draft", issue.Reason, StringComparison.Ordinal);
        await host.Store.SaveInstanceAsync((await host.Store.FindInstanceAsync(bracket.Id))! with { Status = CalcInstanceStatus.Draft }, null);

        // Once the upstream is checked it is fixed and the downstream checks.
        Assert.True((await host.Service.CheckAsync(motor.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted)).Succeeded);
        Assert.True((await host.Service.CheckAsync(bracket.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted)).Succeeded);
    }

    [Fact]
    public async Task R_A_02_Both_Drafts_Are_Checked_And_Issued_Together_In_One_Pack()
    {
        var host = new LibraryTestHost(calculators: [new ForceSourceCalculator()]);
        var project = await host.ProjectAsync();
        var motor = await host.Service.CreateAsync("test.force-source", project, "Motor torque", [CalcInputValue.Quantity("P", 5, "kN", CalcProvenance.Drawing("DRG-9", "A"))]);
        var bracket = await host.Service.CreateAsync("test.direct-stress", project, "Bracket", LibraryTestHost.ProvenancedInputs());
        Assert.True((await host.Service.BindAsync(bracket.Id, "F", motor.Id, "F_out")).Succeeded);

        var pack = await host.Service.CreatePackAsync(project, "Bracket pack", [motor.Id, bracket.Id]);

        Assert.True((await host.Service.CheckPackAsync(pack.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted)).Succeeded);
        Assert.True((await host.Service.IssuePackAsync(pack.Id, "PK-1", "Client Co")).Succeeded);
    }

    // ------------------------------------------------------------------ R-A-03 / SEC-A-03

    [Fact]
    public async Task R_A_03_A_Record_Revision_Upstream_Makes_The_Downstream_Stale()
    {
        var host = RecordedHost();
        var project = await host.ProjectAsync();
        var record = await SaveAsync(host, Draft(licensed: true, amps: [27]));
        var cable = await TypedCalculationAsync(host, project, "Cable");
        Assert.True((await host.Service.ApplyRecordAsync(cable.Id, record.Id)).Succeeded);
        var fault = await host.Service.CreateAsync("test.recorded", project, "Fault", [CalcInputValue.Of("k_1", 1, Typed), Ratings(5) with { Provenance = Typed }]);
        Assert.True((await host.Service.BindCellAsync(fault.Id, "ratings", 0, "I_t", cable.Id, "I")).Succeeded);
        Assert.False((await host.Service.StalenessAsync((await host.Store.FindInstanceAsync(fault.Id))!)).IsStale);

        await SaveAsync(host, Draft(licensed: true, k: 0.5, amps: [27]) with { Id = record.Id });

        Assert.True((await host.Service.StalenessAsync((await host.Store.FindInstanceAsync(cable.Id))!)).IsStale);
        var downstream = await host.Service.StalenessAsync((await host.Store.FindInstanceAsync(fault.Id))!);
        Assert.True(downstream.IsStale);
        Assert.Contains("is itself stale", downstream.Reasons[0], StringComparison.Ordinal);
        Assert.False((await host.Service.CheckAsync(fault.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted)).Succeeded);
    }

    [Fact]
    public async Task R_A_03_A_Template_Change_Upstream_Makes_The_Downstream_Stale()
    {
        var host = new LibraryTestHost(calculators: [new ForceSourceCalculator()]);
        var project = await host.ProjectAsync();
        var motor = await host.Service.CreateAsync("test.force-source", project, "Motor torque", [CalcInputValue.Quantity("P", 5, "kN", CalcProvenance.Drawing("DRG-9", "A"))]);
        var bracket = await host.Service.CreateAsync("test.direct-stress", project, "Bracket", LibraryTestHost.ProvenancedInputs());
        Assert.True((await host.Service.BindAsync(bracket.Id, "F", motor.Id, "F_out")).Succeeded);
        Assert.False((await host.Service.StalenessAsync((await host.Store.FindInstanceAsync(bracket.Id))!)).IsStale);

        // An application upgrade changes the upstream template's content hash.
        var upstream = (await host.Store.FindInstanceAsync(motor.Id))!;
        await host.Store.SaveInstanceAsync(upstream with { TemplateHash = "0000" }, upstream.UpdatedAt);

        Assert.True((await host.Service.StalenessAsync((await host.Store.FindInstanceAsync(bracket.Id))!)).IsStale);
    }

    // ------------------------------------------------------------------ R-A-04 / SEC-A-04

    [Fact]
    public async Task R_A_04_A_Checked_Calculation_Gone_Stale_Is_Revised_Not_Stranded()
    {
        var host = RecordedHost();
        var project = await host.ProjectAsync();
        var record = await SaveAsync(host, Draft(licensed: true));
        var calc = await TypedCalculationAsync(host, project);
        Assert.True((await host.Service.ApplyRecordAsync(calc.Id, record.Id)).Succeeded);
        Assert.True((await host.Service.CheckAsync(calc.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted)).Succeeded);

        // A checked calculation that is still current cannot be revised.
        Assert.False((await host.Service.ReviseAsync(calc.Id)).Succeeded);

        await SaveAsync(host, Draft(licensed: true, k: 0.7) with { Id = record.Id });
        Assert.False((await host.Service.IssueAsync(calc.Id, "ISS-1", "Client Co")).Succeeded);

        var revised = await host.Service.ReviseAsync(calc.Id);
        Assert.True(revised.Succeeded, revised.Reason);
        Assert.Equal("B", revised.Instance!.Revision);
        Assert.Equal(CalcInstanceStatus.Superseded, (await host.Store.FindInstanceAsync(calc.Id))!.Status);

        Assert.True((await host.Service.ApplyRecordAsync(revised.Instance.Id, record.Id)).Succeeded);
        Assert.True((await host.Service.CheckAsync(revised.Instance.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted)).Succeeded);
        Assert.True((await host.Service.IssueAsync(revised.Instance.Id, "ISS-2", "Client Co")).Succeeded);
    }

    [Fact]
    public async Task R_A_04_A_Checked_Pack_Gone_Stale_Is_Reopened_Corrected_And_Issued()
    {
        var host = RecordedHost();
        var project = await host.ProjectAsync();
        var record = await SaveAsync(host, Draft(licensed: true));
        var calc = await TypedCalculationAsync(host, project);
        Assert.True((await host.Service.ApplyRecordAsync(calc.Id, record.Id)).Succeeded);
        var pack = await host.Service.CreatePackAsync(project, "Feeder pack", [calc.Id]);
        Assert.False((await host.Service.ReopenPackAsync(pack.Id)).Succeeded);   // a draft is already open
        Assert.True((await host.Service.CheckPackAsync(pack.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted)).Succeeded);
        var firstEvidence = (await host.Store.FindPackAsync(pack.Id))!.EvidenceId;

        await SaveAsync(host, Draft(licensed: true, k: 0.7) with { Id = record.Id });

        // Stranded before: issue refused (stale), the member frozen, the pack no longer a draft.
        Assert.False((await host.Service.IssuePackAsync(pack.Id, "PK-1", "Client Co")).Succeeded);
        Assert.False((await host.Service.ApplyRecordAsync(calc.Id, record.Id)).Succeeded);

        var reopened = await host.Service.ReopenPackAsync(pack.Id);
        Assert.True(reopened.Succeeded, reopened.Reason);
        var stored = (await host.Store.FindPackAsync(pack.Id))!;
        Assert.Equal((CalcInstanceStatus.Draft, "B", 2), (stored.Status, stored.Revision, stored.Revisions.Count));
        Assert.NotEqual(firstEvidence, stored.EvidenceId);

        Assert.True((await host.Service.ApplyRecordAsync(calc.Id, record.Id)).Succeeded);
        Assert.True((await host.Service.CheckPackAsync(pack.Id, "J. Reviewer", "Client Co", "OK again", CheckOutcome.Accepted)).Succeeded);
        Assert.True((await host.Service.IssuePackAsync(pack.Id, "PK-1", "Client Co")).Succeeded);
    }

    // ------------------------------------------------------------------ R-A-05 / SEC-A-05, R-A-11

    [Fact]
    public async Task R_A_05_Whoever_Changed_The_Inputs_Cannot_Check_Them_And_The_Set_Travels_With_Each_Revision()
    {
        var host = new LibraryTestHost(calculators: [new IndependentStressCalculator()]);
        var project = await host.ProjectAsync();

        // Bob sets the calculation up, Alice designs it.
        host.Principal = "bob";
        var draft = await host.Service.CreateAsync("test.independent", project, "E4 circuit 1", LibraryTestHost.ProvenancedInputs());
        host.Principal = "alice";
        Assert.True((await host.Service.UpdateInputsAsync(draft.Id, LibraryTestHost.ProvenancedInputs(f: 30))).Succeeded);

        var aliceChecks = await host.Service.CheckAsync(draft.Id, "Alice", "Client Co", "OK", CheckOutcome.Accepted);
        Assert.False(aliceChecks.Succeeded);
        Assert.Contains("someone other than anyone who set up or changed its inputs (bob, alice)", aliceChecks.Reason, StringComparison.Ordinal);
        host.Principal = "bob";
        Assert.False((await host.Service.CheckAsync(draft.Id, "Bob", "Client Co", "OK", CheckOutcome.Accepted)).Succeeded);

        host.Principal = "carol";
        Assert.True((await host.Service.CheckAsync(draft.Id, "Carol", "Client Co", "OK", CheckOutcome.Accepted)).Succeeded);
        Assert.True((await host.Service.IssueAsync(draft.Id, "ISS-1", "Client Co")).Succeeded);

        // Dave presses "revise": revision B is his, and Bob and Alice, who designed what it was made from, still cannot check it.
        host.Principal = "dave";
        var revised = (await host.Service.ReviseAsync(draft.Id)).Instance!;
        Assert.Contains("alice", revised.Authors);
        Assert.Contains("dave", revised.Authors);
        host.Principal = "alice";
        Assert.False((await host.Service.CheckAsync(revised.Id, "Alice", "Client Co", "OK", CheckOutcome.Accepted)).Succeeded);
        host.Principal = "carol";
        Assert.True((await host.Service.CheckAsync(revised.Id, "Carol", "Client Co", "OK", CheckOutcome.Accepted)).Succeeded);
    }

    [Fact]
    public async Task R_A_05_The_Pack_Check_Refuses_Whoever_Changed_A_Members_Inputs()
    {
        var host = new LibraryTestHost(calculators: [new IndependentStressCalculator()]);
        var project = await host.ProjectAsync();
        host.Principal = "bob";
        var member = await host.Service.CreateAsync("test.independent", project, "Strict", LibraryTestHost.ProvenancedInputs());
        var pack = await host.Service.CreatePackAsync(project, "Circuit pack", [member.Id]);
        host.Principal = "alice";
        Assert.True((await host.Service.UpdateInputsAsync(member.Id, LibraryTestHost.ProvenancedInputs(f: 30))).Succeeded);

        Assert.False((await host.Service.CheckPackAsync(pack.Id, "Alice", "Client Co", "OK", CheckOutcome.Accepted)).Succeeded);
        host.Principal = "carol";
        Assert.True((await host.Service.CheckPackAsync(pack.Id, "Carol", "Client Co", "OK", CheckOutcome.Accepted)).Succeeded);
    }

    [Fact]
    public async Task P_A_03_The_Checker_Who_Re_Pins_A_Link_Is_Not_Thereby_An_Author()
    {
        var host = new LibraryTestHost(calculators: [new ForceSourceCalculator(), new IndependentStressCalculator()]);
        var project = await host.ProjectAsync();

        // Alice designs a motor and an independent-check bracket linked to it; the motor is checked and issued by Carol.
        host.Principal = "alice";
        var motor = await host.Service.CreateAsync("test.force-source", project, "Motor torque", [CalcInputValue.Quantity("P", 5, "kN", CalcProvenance.Drawing("DRG-9", "A"))]);
        var bracket = await host.Service.CreateAsync("test.independent", project, "Bracket", LibraryTestHost.ProvenancedInputs());
        Assert.True((await host.Service.BindAsync(bracket.Id, "F", motor.Id, "F_out")).Succeeded);
        host.Principal = "carol";
        Assert.True((await host.Service.CheckAsync(motor.Id, "Carol", "Client Co", "OK", CheckOutcome.Accepted)).Succeeded);
        Assert.True((await host.Service.IssueAsync(motor.Id, "ISS-M", "Client Co")).Succeeded);

        // The motor moves on to revision B, so the bracket is stale.
        host.Principal = "alice";
        var motorB = (await host.Service.ReviseAsync(motor.Id)).Instance!;
        Assert.True((await host.Service.UpdateInputsAsync(motorB.Id, [CalcInputValue.Quantity("P", 6, "kN", CalcProvenance.Drawing("DRG-9", "B"))])).Succeeded);
        host.Principal = "carol";
        Assert.True((await host.Service.CheckAsync(motorB.Id, "Carol", "Client Co", "OK", CheckOutcome.Accepted)).Succeeded);
        Assert.True((await host.Service.IssueAsync(motorB.Id, "ISS-M2", "Client Co")).Succeeded);
        Assert.True((await host.Service.StalenessAsync((await host.Store.FindInstanceAsync(bracket.Id))!)).IsStale);

        // Bob, the only other engineer, presses "Update to revision B" and then checks it: pressing the button did not make him its designer.
        host.Principal = "bob";
        var updated = await host.Service.UpdateLinksAsync(bracket.Id);
        Assert.True(updated.Succeeded, updated.Reason);
        Assert.DoesNotContain("bob", updated.Instance!.Authors);
        Assert.Contains("bob", updated.Instance.RepinnedBy!);
        var check = await host.Service.CheckAsync(bracket.Id, "Bob", "Client Co", "OK", CheckOutcome.Accepted);
        Assert.True(check.Succeeded, check.Reason);
    }

    [Fact]
    public async Task P_A_03_Re_Pinning_A_Checked_Calculation_Makes_A_Revision_Whose_Author_Is_Still_The_Original_Designer()
    {
        var host = new LibraryTestHost(calculators: [new ForceSourceCalculator(), new IndependentStressCalculator()]);
        var project = await host.ProjectAsync();
        host.Principal = "alice";
        var motor = await host.Service.CreateAsync("test.force-source", project, "Motor torque", [CalcInputValue.Quantity("P", 5, "kN", CalcProvenance.Drawing("DRG-9", "A"))]);
        var bracket = await host.Service.CreateAsync("test.independent", project, "Bracket", LibraryTestHost.ProvenancedInputs());
        Assert.True((await host.Service.BindAsync(bracket.Id, "F", motor.Id, "F_out")).Succeeded);
        host.Principal = "carol";
        Assert.True((await host.Service.CheckAsync(motor.Id, "Carol", "Client Co", "OK", CheckOutcome.Accepted)).Succeeded);
        Assert.True((await host.Service.IssueAsync(motor.Id, "ISS-M", "Client Co")).Succeeded);
        Assert.True((await host.Service.CheckAsync(bracket.Id, "Carol", "Client Co", "OK", CheckOutcome.Accepted)).Succeeded);
        host.Principal = "alice";
        var motorB = (await host.Service.ReviseAsync(motor.Id)).Instance!;
        Assert.True((await host.Service.UpdateInputsAsync(motorB.Id, [CalcInputValue.Quantity("P", 6, "kN", CalcProvenance.Drawing("DRG-9", "B"))])).Succeeded);
        host.Principal = "carol";
        Assert.True((await host.Service.CheckAsync(motorB.Id, "Carol", "Client Co", "OK", CheckOutcome.Accepted)).Succeeded);
        Assert.True((await host.Service.IssueAsync(motorB.Id, "ISS-M2", "Client Co")).Succeeded);

        // Bob updates the checked, now stale, bracket: a new draft revision, which he can still check.
        host.Principal = "bob";
        var revised = (await host.Service.UpdateLinksAsync(bracket.Id)).Instance!;
        Assert.Equal("B", revised.Revision);
        Assert.Equal(["alice"], revised.Authors);
        Assert.Contains("bob", revised.RepinnedBy!);
        var check = await host.Service.CheckAsync(revised.Id, "Bob", "Client Co", "OK", CheckOutcome.Accepted);
        Assert.True(check.Succeeded, check.Reason);
    }

    [Fact]
    public async Task R_A_11_Nobody_Signed_In_Is_Not_An_Independent_Checker()
    {
        var host = new LibraryTestHost(calculators: [new IndependentStressCalculator()]);
        host.Principal = "alice";
        var instance = await host.Service.CreateAsync("test.independent", await host.ProjectAsync(), "E5 circuit 1", LibraryTestHost.ProvenancedInputs());

        host.Principal = Tempest.Core.EngineeringData.EngineeringDocumentStore.UnknownAuthorPrincipalId;
        var refused = await host.Service.CheckAsync(instance.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted);

        Assert.False(refused.Succeeded);
        Assert.Contains("nobody is signed in", refused.Reason, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------ R-A-06

    [Fact]
    public async Task R_A_06_A_Value_That_Is_Not_The_Records_Cannot_Cite_It()
    {
        var host = RecordedHost();
        var project = await host.ProjectAsync();
        var record = await SaveAsync(host, Draft(licensed: true));
        var calc = await TypedCalculationAsync(host, project);
        var applied = (await host.Service.ApplyRecordAsync(calc.Id, record.Id)).Instance!;
        Assert.False((await host.Service.StalenessAsync(applied)).IsStale);

        // (a) the user edits k_1 in the form; the source column still reads the pin
        var edited = applied.Inputs.Select(i => i.Symbol == "k_1" ? i with { Number = 1.9 } : i).ToList();
        Assert.True((await host.Service.UpdateInputsAsync(calc.Id, edited)).Succeeded);
        var staleness = await host.Service.StalenessAsync((await host.Store.FindInstanceAsync(calc.Id))!);
        Assert.True(staleness.IsStale);
        Assert.Contains("no longer matches", staleness.Reasons[0], StringComparison.Ordinal);
        Assert.False((await host.Service.CheckAsync(calc.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted)).Succeeded);

        // ... and a table cell edited the same way.
        Assert.True((await host.Service.ApplyRecordAsync(calc.Id, record.Id)).Succeeded);
        var cellEdited = (await host.Store.FindInstanceAsync(calc.Id))!.Inputs.Select(i => i.Symbol == "ratings" ? Ratings(13.7, 21.9, 99) with { Provenance = i.Provenance } : i).ToList();
        Assert.True((await host.Service.UpdateInputsAsync(calc.Id, cellEdited)).Succeeded);
        Assert.True((await host.Service.StalenessAsync((await host.Store.FindInstanceAsync(calc.Id))!)).IsStale);
    }

    [Fact]
    public async Task R_A_06_The_Bare_Prefix_Record_Is_Not_A_Pin()
    {
        var host = RecordedHost();
        var fake = await host.Service.CreateAsync("test.recorded", await host.ProjectAsync(), "Fake", [new CalcInputValue("k_1", Number: 1.9, Provenance: CalcProvenance.FromReference("record: BS 7671 Table 4C1")), Ratings(20) with { Provenance = CalcProvenance.FromReference("record: owner's table") }]);

        Assert.Contains(fake.Warnings, w => w.RuleId == "record.k_1");
        Assert.Contains(fake.Warnings, w => w.RuleId == "record.ratings");
        Assert.False(CalcInputValidator.IsRecordPin(CalcProvenance.FromReference("record:not-a-guid@1")));
        Assert.True(CalcInputValidator.IsRecordPin(CalcProvenance.FromReference($"record:{Guid.NewGuid():N}@2 · Table 4C1")));
    }

    [Fact]
    public async Task R_A_06_A_Pin_To_A_Revision_That_Does_Not_Exist_Is_Stale_And_Withheld_From_Every_Export()
    {
        var host = RecordedHost();
        var project = await host.ProjectAsync();
        var record = await SaveAsync(host, Draft(licensed: true));
        var future = new CalcInputValue("k_1", Number: 0.8, Provenance: CalcProvenance.FromReference($"record:{record.Id:N}@9 · Table 4C1"));
        var table = Ratings(11.3, 13.7) with { Provenance = CalcProvenance.FromReference($"record:{record.Id:N}@9 · Table 4C1") };
        var calc = await host.Service.CreateAsync("test.recorded", project, "Future", [future, table]);

        var staleness = await host.Service.StalenessAsync(calc);
        Assert.True(staleness.IsStale);
        Assert.Contains("does not exist", staleness.Reasons[0], StringComparison.Ordinal);

        var withheld = await host.Service.WithholdLicensedAsync(new CalcInputSet([future, table]));
        Assert.Null(withheld.Find("k_1")!.Number);
        Assert.Null(withheld.Find("ratings")!.Rows);

        // The sheet withholds the rows as well (the largest, 13.7 A, is the value used and stays in the working).
        var sheet = (await host.Service.RunAsync(calc.Id))!.SheetText;
        Assert.DoesNotContain("11.3", sheet, StringComparison.Ordinal);
        Assert.Contains("rows withheld (licensed record)", sheet, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------ R-A-07

    [Fact]
    public async Task R_A_07_A_Relist_Reads_The_Projects_Instances_Not_The_Whole_Collection_And_Not_Once_Per_Row()
    {
        var host = new LibraryTestHost();
        var project = await host.ProjectAsync();

        for (var i = 0; i < 30; i++)
            await host.Service.CreateAsync("test.direct-stress", project, $"Calc {i}", LibraryTestHost.ProvenancedInputs());

        var proxy = DispatchProxy.Create<IQueryablePersistenceStore, CountingProxy>();
        var counting = (CountingProxy)(object)proxy;
        counting.Target = host.Backing;
        var service = new CalcInstanceService(host.Catalogue, new CalcInstanceStore(proxy), host.Evidence, host.Context);

        // The old Desktop relist: one staleness question per listed calculation, each of which read every document of every project.
        foreach (var instance in await service.Store.ListInstancesAsync(project))
            await service.StalenessAsync(instance);

        Assert.Equal(0, counting.Counts.GetValueOrDefault("ReadAllAsync"));

        // The new relist: one listing and one read for the whole project.
        counting.Counts.Clear();
        var all = await service.StalenessForProjectAsync(project);

        Assert.Equal(30, all.Count);
        Assert.All(all.Values, s => Assert.False(s.IsStale));
        Assert.Equal(0, counting.Counts.GetValueOrDefault("ReadAllAsync"));
        Assert.True(counting.Counts.GetValueOrDefault("ReadManyAsync") <= 1, $"{counting.Counts.GetValueOrDefault("ReadManyAsync")} reads of the instances");
    }

    [Fact]
    public async Task R_A_07_The_Whole_Project_Answer_Agrees_With_Asking_Row_By_Row()
    {
        var host = new LibraryTestHost(calculators: [new ForceSourceCalculator()]);
        var project = await host.ProjectAsync();
        var motor = await host.Service.CreateAsync("test.force-source", project, "Motor torque", [CalcInputValue.Quantity("P", 5, "kN", CalcProvenance.Drawing("DRG-9", "A"))]);
        var bracket = await host.Service.CreateAsync("test.direct-stress", project, "Bracket", LibraryTestHost.ProvenancedInputs());
        Assert.True((await host.Service.BindAsync(bracket.Id, "F", motor.Id, "F_out")).Succeeded);
        Assert.True((await host.Service.UpdateInputsAsync(motor.Id, [CalcInputValue.Quantity("P", 9, "kN", CalcProvenance.Drawing("DRG-9", "A"))])).Succeeded);

        var all = await host.Service.StalenessForProjectAsync(project);

        foreach (var instance in await host.Store.ListInstancesAsync(project))
            Assert.Equal((await host.Service.StalenessAsync(instance)).Reasons, all[instance.Id].Reasons);

        Assert.True(all[bracket.Id].IsStale);
        Assert.False(all[motor.Id].IsStale);
    }

    // ------------------------------------------------------------------ R-A-08, R-A-09, R-A-15

    [Fact]
    public async Task R_A_08_A_Link_Cannot_Name_A_Revision_The_Upstream_Does_Not_Have()
    {
        var host = new LibraryTestHost(calculators: [new ForceSourceCalculator()]);
        var project = await host.ProjectAsync();
        var motor = await host.Service.CreateAsync("test.force-source", project, "Motor torque", [CalcInputValue.Quantity("P", 5, "kN", CalcProvenance.Drawing("DRG-9", "A"))]);
        var forged = new CalcProvenance(CalcProvenanceKind.Link, $"calc:{motor.Id:N}#F_out", "D");
        var inputs = LibraryTestHost.ProvenancedInputs().Select(i => i.Symbol == "F" ? CalcInputValue.Quantity("F", 10, "kN", forged) : i).ToList();

        var bracket = await host.Service.CreateAsync("test.direct-stress", project, "Bracket", LibraryTestHost.ProvenancedInputs());
        var refused = await host.Service.UpdateInputsAsync(bracket.Id, inputs);

        Assert.False(refused.Succeeded);
        Assert.Contains("rev D, but that calculation is rev A", refused.Reason, StringComparison.Ordinal);

        // A true link prints the upstream's own revision.
        Assert.True((await host.Service.BindAsync(bracket.Id, "F", motor.Id, "F_out")).Succeeded);
        Assert.Contains("link: Motor torque rev A · F_out", (await host.Service.RunAsync(bracket.Id))!.SheetText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task R_A_09_Only_Declared_Output_Ports_Can_Be_Linked()
    {
        var host = new LibraryTestHost(calculators: [new PortedCalculator()]);
        var project = await host.ProjectAsync();
        var source = await host.Service.CreateAsync("test.ported", project, "Source", [CalcInputValue.Quantity("P", 5, "kN", CalcProvenance.Drawing("DRG-9", "A"))]);
        var sink = await host.Service.CreateAsync("test.direct-stress", project, "Sink", LibraryTestHost.ProvenancedInputs());

        var echoed = await host.Service.BindAsync(sink.Id, "F", source.Id, "b");
        Assert.False(echoed.Succeeded);
        Assert.Contains("not an output Ported offers for linking; link to one of: a", echoed.Reason, StringComparison.Ordinal);

        // The same from an import that states the link.
        var forged = LibraryTestHost.ProvenancedInputs().Select(i => i.Symbol == "F" ? CalcInputValue.Quantity("F", 5, "kN", CalcProvenance.Link(source.Id, "b", "A")) : i).ToList();
        Assert.False((await host.Service.UpdateInputsAsync(sink.Id, forged)).Succeeded);

        Assert.True((await host.Service.BindAsync(sink.Id, "F", source.Id, "a")).Succeeded);
    }

    [Fact]
    public async Task R_A_15_A_Legacy_Reference_Meets_The_Cycle_And_Project_Rules()
    {
        var host = new LibraryTestHost(calculators: [new ForceSourceCalculator()]);
        var project = await host.ProjectAsync();
        var a = await host.Service.CreateAsync("test.force-source", project, "A", [CalcInputValue.Quantity("P", 5, "kN", CalcProvenance.Drawing("DRG-9", "A"))]);
        var b = await host.Service.CreateAsync("test.direct-stress", project, "B", LibraryTestHost.ProvenancedInputs());
        Assert.True((await host.Service.BindAsync(b.Id, "F", a.Id, "F_out")).Succeeded);

        // A takes a value from B by the legacy reference: that would close A → B → A.
        var cycle = await host.Service.UpdateInputsAsync(a.Id, [CalcInputValue.Quantity("P", 5, "kN", CalcProvenance.FromReference(CalcInstance.UpstreamPrefix + b.Id.ToString("N")))]);
        Assert.False(cycle.Succeeded);
        Assert.Contains("cycle", cycle.Reason, StringComparison.Ordinal);

        // ... and one that does not exist is refused when it is entered.
        var missing = await host.Service.UpdateInputsAsync(b.Id, LibraryTestHost.ProvenancedInputs().Select(i => i.Symbol == "A" ? i with { Provenance = CalcProvenance.FromReference(CalcInstance.UpstreamPrefix + Guid.NewGuid().ToString("N")) } : i).ToList());
        Assert.False(missing.Succeeded);
        Assert.Contains("does not exist", missing.Reason, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------ R-A-10, R-A-12, R-A-13, R-A-14, R-A-16

    [Fact]
    public async Task R_A_10_Nothing_Is_Attached_To_A_Member_Of_A_Checked_Pack_Or_For_A_Refused_Citation()
    {
        var host = new LibraryTestHost();
        var project = await host.ProjectAsync();
        var calc = await host.Service.CreateAsync("test.direct-stress", project, "Base", LibraryTestHost.ProvenancedInputs());

        // A citing input that is not a text input is refused before the file is attached.
        var refused = await host.Service.AttachExternalEvidenceAsync(calc.Id, "profis.pdf", "application/pdf", new byte[] { 1 }, "F");
        Assert.False(refused.Succeeded);
        var evidence = (Evidence)(await host.Context.Repository.FindAsync(calc.EvidenceId!.Value))!;
        Assert.Empty(await evidence.GetAttachmentsAsync());

        var pack = await host.Service.CreatePackAsync(project, "P", [calc.Id]);
        Assert.True((await host.Service.CheckPackAsync(pack.Id, "J. Reviewer", "Co", "OK", CheckOutcome.Accepted)).Succeeded);
        var attach = await host.Service.AttachExternalEvidenceAsync(calc.Id, "profis.pdf", "application/pdf", new byte[] { 1 });
        Assert.False(attach.Succeeded);
        Assert.Empty(await evidence.GetAttachmentsAsync());
    }

    [Fact]
    public async Task R_A_12_A_Pack_Holds_The_Calculations_Of_One_Project_And_The_No_Project_List_Is_Its_Own()
    {
        var host = new LibraryTestHost();
        var project = await host.ProjectAsync();
        var loose = await host.Service.CreateAsync("test.direct-stress", null, "Loose", LibraryTestHost.ProvenancedInputs());
        var inProject = await host.Service.CreateAsync("test.direct-stress", project, "Member", LibraryTestHost.ProvenancedInputs());

        await Assert.ThrowsAsync<ArgumentException>(() => host.Service.CreatePackAsync(project, "P", [inProject.Id, loose.Id]));
        var (template, refusal) = await host.Service.PackTemplateFromChainAsync("Chain", [inProject.Id, loose.Id]);
        Assert.Null(template);
        Assert.Contains("another project", refusal, StringComparison.Ordinal);

        // "No project" lists the calculations saved without one, not every project's.
        Assert.Equal([loose.Id], (await host.Store.ListInstancesAsync(null)).Select(i => i.Id));
        Assert.Equal([inProject.Id], (await host.Store.ListInstancesAsync(project)).Select(i => i.Id));
    }

    [Fact]
    public async Task R_A_13_Instantiating_A_Template_Twice_In_One_Project_Is_A_Refusal_That_Leaves_Nothing_Behind()
    {
        var host = new LibraryTestHost(calculators: [.. Tempest.Calc.Electrical.ElectricalCalculators.All, .. Tempest.Calc.Mechanical.MechanicalCalculators.All], packTemplates: CalcPackTemplates.All);
        var project = await host.ProjectAsync();
        var (first, firstRefusal) = await host.Service.InstantiatePackTemplateAsync(CalcPackTemplates.DriveTrain, project, "Conveyor drive", Chains.ChainInputs.DriveTrain(host));
        Assert.True(firstRefusal is null, firstRefusal);
        Assert.NotNull(first);
        var instances = (await host.Store.ListInstancesAsync(project)).Count;

        // The steps' names are taken: a refusal now, not a DuplicateBusinessIdentifierException half way through.
        var (second, refusal) = await host.Service.InstantiatePackTemplateAsync(CalcPackTemplates.DriveTrain, project, "Second drive", Chains.ChainInputs.DriveTrain(host));

        Assert.Null(second);
        Assert.Contains("already holds a calculation called", refusal, StringComparison.Ordinal);
        Assert.Equal(instances, (await host.Store.ListInstancesAsync(project)).Count);
        Assert.Single(await host.Store.ListPacksAsync(project));
    }

    [Fact]
    public async Task R_A_14_The_Pack_Check_Is_One_Transaction()
    {
        var host = new LibraryTestHost();
        var project = await host.ProjectAsync();
        var calc = await host.Service.CreateAsync("test.direct-stress", project, "Base", LibraryTestHost.ProvenancedInputs());
        var pack = await host.Service.CreatePackAsync(project, "P", [calc.Id]);

        // A failed commit leaves the pack and its Evidence both as they were; the check can be tried again.
        host.Failing.FailNextCommit = true;
        await Assert.ThrowsAsync<PersistenceStoreUnavailableException>(() => host.Service.CheckPackAsync(pack.Id, "J. Reviewer", "Co", "OK", CheckOutcome.Accepted));
        Assert.Equal(CalcInstanceStatus.Draft, (await host.Store.FindPackAsync(pack.Id))!.Status);

        var before = host.Failing.BodiesCompleted;
        Assert.True((await host.Service.CheckPackAsync(pack.Id, "J. Reviewer", "Co", "OK", CheckOutcome.Accepted)).Succeeded);
        Assert.Equal(1, host.Failing.BodiesCompleted - before);
        Assert.Equal(CalcInstanceStatus.Checked, (await host.Store.FindPackAsync(pack.Id))!.Status);
    }

    [Fact]
    public async Task R_A_14_An_Issue_Is_Refused_When_A_Calculation_It_Relied_On_Changed_Before_The_Commit()
    {
        var host = new LibraryTestHost();
        var calc = await host.Service.CreateAsync("test.direct-stress", await host.ProjectAsync(), "Base", LibraryTestHost.ProvenancedInputs());
        var staleCopy = calc.UpdatedAt;
        Assert.True((await host.Service.UpdateInputsAsync(calc.Id, LibraryTestHost.ProvenancedInputs(f: 30))).Succeeded);

        await Assert.ThrowsAsync<CalcConcurrencyException>(() => host.Failing.ExecuteInTransactionAsync(
            (tx, token) => CalcInstanceStore.AssertUnchangedAsync(tx, calc.ProjectId, calc.Id, calc.Name, staleCopy, token)));
    }

    [Fact]
    public async Task R_A_16_A_Records_Citation_Is_Exported_As_One_Quoted_Cell_And_Is_Entered_By_The_Signed_In_Person()
    {
        var host = RecordedHost();
        host.Principal = "j.engineer";
        var record = await SaveAsync(host, Draft(licensed: false) with { Edition = "2018,=cmd|' /C calc'!A0", EnteredBy = "someone.else" });

        Assert.Equal("j.engineer", record.EnteredBy);
        var (csv, refusal) = await host.Service.ExportRecordAsync(record.Id);
        Assert.Null(refusal);
        var first = csv!.Split('\n')[0];
        Assert.StartsWith("\"# ", first, StringComparison.Ordinal);
        Assert.EndsWith("\"", first, StringComparison.Ordinal);

        var (tooWide, why) = await host.Service.SaveRecordAsync(Draft(licensed: false) with
        {
            Values = [new CalcDataValue("ratings", Rows: [CalcRow.Of(Enumerable.Range(0, CalcDataRecord.MaxCellsPerRow + 1).Select(i => ($"c{i}", new CalcCell(1))).ToArray())])],
        });
        Assert.Null(tooWide);
        Assert.Contains("at most 50 cells", why, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------ G-A-02 / G-B-06, B-A-13, B-A-18

    [Fact]
    public void G_A_02_A_New_Sheet_Opens_On_The_Declared_Defaults_Only()
    {
        foreach (var calculator in ShippedCalculators.All)
        {
            var template = calculator.Template;
            var opened = CalcTemplateHasher.NewSheetInputs(template);

            foreach (var spec in template.Inputs)
            {
                var hasDefault = spec.Kind switch
                {
                    CalcInputKind.Quantity or CalcInputKind.Number => spec.Default is not null,
                    CalcInputKind.Choice => spec.DefaultChoice is not null,
                    CalcInputKind.Boolean => true,
                    _ => false,
                };

                Assert.True(hasDefault == (opened.Find(spec.Symbol) is not null), $"{template.Id}.{spec.Symbol}: opens {(opened.Find(spec.Symbol) is null ? "blank" : "filled")} with {(hasDefault ? "a" : "no")} declared default");

                if (opened.Find(spec.Symbol) is { Number: { } number } && spec.Default is { } declared)
                    Assert.Equal(declared, number);
            }
        }

        // The case that showed it: a shaft opens on the declared 7850 kg/m^3, not the first known-answer case's 0.
        var shaft = CalcTemplateHasher.NewSheetInputs(new Tempest.Calc.Mechanical.ShaftCalculator().Template);
        Assert.Equal(7850, shaft.Find("rho")!.Number);
        Assert.Null(shaft.Find("masses"));
    }

    [Fact]
    public async Task B_A_13_A_Pack_Members_Own_Sheet_Shows_The_Packs_Checker_And_Client()
    {
        var host = new LibraryTestHost();
        var project = await host.ProjectAsync();
        var calc = await host.Service.CreateAsync("test.direct-stress", project, "Base", LibraryTestHost.ProvenancedInputs());
        var pack = await host.Service.CreatePackAsync(project, "P", [calc.Id]);
        Assert.True((await host.Service.CheckPackAsync(pack.Id, "J. Reviewer", "Co", "OK", CheckOutcome.Accepted)).Succeeded);
        Assert.True((await host.Service.IssuePackAsync(pack.Id, "PK-7", "Client Co")).Succeeded);

        var sheet = (await host.Service.RunAsync(calc.Id))!.SheetText;

        Assert.Contains("Checker: J. Reviewer", sheet, StringComparison.Ordinal);
        Assert.Contains("Client: Client Co", sheet, StringComparison.Ordinal);
        Assert.Contains("Issue: PK-7", sheet, StringComparison.Ordinal);
    }

    [Fact]
    public async Task B_A_18_Repeated_Unsourced_Table_Warnings_Are_One_Line_Listing_The_Symbols()
    {
        var host = RecordedHost();
        var calc = await TypedCalculationAsync(host, await host.ProjectAsync());
        Assert.Equal(2, calc.Warnings.Count(w => w.RuleId.StartsWith("record.", StringComparison.Ordinal)));

        var sheet = (await host.Service.RunAsync(calc.Id))!.SheetText;

        Assert.Equal(1, CountOf(sheet, "not taken from a reference record"));
        Assert.Contains("Tabulated values not taken from a reference record: k_1, ratings (Test factors).", sheet, StringComparison.Ordinal);
    }

    private static int CountOf(string text, string part)
    {
        var count = 0;

        for (var at = text.IndexOf(part, StringComparison.Ordinal); at >= 0; at = text.IndexOf(part, at + part.Length, StringComparison.Ordinal))
            count++;

        return count;
    }
}
