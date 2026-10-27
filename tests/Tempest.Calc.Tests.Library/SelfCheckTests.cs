using Tempest.Calc.Library;
using Tempest.Calc.Sheets;
using Tempest.Core.Audit;
using Tempest.Core.Evidence;

namespace Tempest.Calc.Tests.Library;

/// <summary>Every audit row the service writes, in order.</summary>
internal sealed class RecordingAuditRecorder : IAuditRecorder
{
    public List<(string Action, IReadOnlyDictionary<string, string> Detail)> Rows { get; } = [];

    public Task RecordAsync(string action, IReadOnlyDictionary<string, string>? detail = null, CancellationToken cancellationToken = default)
    {
        Rows.Add((action, detail ?? new Dictionary<string, string>()));
        return Task.CompletedTask;
    }
}

/// <summary>An audit store that cannot be written (D4).</summary>
internal sealed class FailingAuditRecorder : IAuditRecorder
{
    public Task RecordAsync(string action, IReadOnlyDictionary<string, string>? detail = null, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("the audit store is read-only tonight");
}

/// <summary>
/// DA-12 as the owner settled it (DC-21): a template that demands an independent check (E4, E5, S6) is checked by a different person as
/// before, or by the preparer alone with a statement of why they stand by it, which is kept on the calculation (or pack), printed on the
/// issued sheet as "Self-checked by the preparer: …" and written to the audit trail. S8 lifting keeps its multi-person rule (DB-13).
/// </summary>
public sealed class SelfCheckTests
{
    private const string Statement = "No second engineer is available this week; I re-derived the loop impedance by hand and it agrees.";

    private static (LibraryTestHost Host, RecordingAuditRecorder Audit) HostWithAudit(params ICalcCalculator[] calculators)
    {
        var audit = new RecordingAuditRecorder();
        return (new LibraryTestHost(calculators: [new IndependentStressCalculator(), .. calculators], audit: audit), audit);
    }

    [Fact]
    public async Task The_Preparer_May_Check_Their_Own_Calculation_With_A_Statement_That_Is_Kept_Printed_And_Audited()
    {
        var (host, audit) = HostWithAudit();
        var instance = await host.Service.CreateAsync("test.independent", await host.ProjectAsync(), "E5 circuit 1", LibraryTestHost.ProvenancedInputs());

        var check = await host.Service.CheckAsync(instance.Id, "Sam Preparer", "Tempest Engineering", "Checked in the worksheet form.", CheckOutcome.Accepted, selfCheckStatement: "  " + Statement + "  ");
        Assert.True(check.Succeeded, check.Reason);

        // Kept on the calculation: who, what they said (trimmed) and when.
        var checkedInstance = (await host.Store.FindInstanceAsync(instance.Id))!;
        Assert.Equal(CalcInstanceStatus.Checked, checkedInstance.Status);
        var selfCheck = Assert.IsType<CalcSelfCheck>(checkedInstance.SelfCheck);
        Assert.Equal("test.engineer", selfCheck.Principal);
        Assert.Equal("Sam Preparer", selfCheck.Name);
        Assert.Equal(Statement, selfCheck.Statement);
        Assert.Equal(checkedInstance.CheckedAt, selfCheck.At);

        // On the Evidence's own check record too: the statement, and the record's own self-check flag (D3), whatever the global rule says.
        var evidence = Assert.IsType<Evidence>(await host.Context.Repository.FindAsync(instance.EvidenceId!.Value));
        Assert.Contains(CalcSheetStandard.SelfCheckPrefix + Statement, evidence.Check!.Statement, StringComparison.Ordinal);
        Assert.True(evidence.Check.SelfCheck);

        // Printed on the sheet, drafted and issued alike.
        var sheet = await host.Service.SheetAsync(checkedInstance);
        Assert.Equal(Statement, sheet.Header.SelfCheck);
        Assert.Contains(CalcSheetStandard.SelfCheckPrefix + Statement, CalcSheetTextRenderer.Render(sheet), StringComparison.Ordinal);

        var issued = await host.Service.IssueAsync(instance.Id, "ISS-1", "Client Co");
        Assert.True(issued.Succeeded, issued.Reason);
        Assert.Contains("Self-checked by the preparer: " + Statement, issued.Instance!.Issue!.SheetText, StringComparison.Ordinal);

        // Written to the audit trail, under its own action, naming the calculation and the statement.
        var row = Assert.Single(audit.Rows, r => r.Action == CalcInstanceService.SelfCheckedActionName);
        Assert.Equal("E5 circuit 1", row.Detail["Calculation"]);
        Assert.Equal(instance.Id.ToString("N"), row.Detail["CalculationId"]);
        Assert.Equal("test.engineer", row.Detail["CheckerPrincipal"]);
        Assert.Equal("Sam Preparer", row.Detail["Checker"]);
        Assert.Equal(Statement, row.Detail["Statement"]);
        Assert.Equal(instance.Revision, row.Detail["Revision"]);
    }

    [Fact]
    public async Task A_Different_Person_Who_Ticks_The_Box_Is_Told_And_Checks_As_Before_Without_It()
    {
        var (host, audit) = HostWithAudit();
        var instance = await host.Service.CreateAsync("test.independent", await host.ProjectAsync(), "E5 circuit 2", LibraryTestHost.ProvenancedInputs());

        // D2: a ticked box by someone who is not an author is refused in a sentence, not silently discarded.
        host.Principal = "another.engineer";
        var ticked = await host.Service.CheckAsync(instance.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted, selfCheckStatement: Statement);
        Assert.False(ticked.Succeeded);
        Assert.Equal($"'E5 circuit 2': you are not its preparer; untick \"{CalcInstanceService.SelfCheckCaption}\" to record your check as an independent check.", ticked.Reason);
        Assert.Equal(CalcInstanceStatus.Draft, (await host.Store.FindInstanceAsync(instance.Id))!.Status);

        // Without it, an independent check as before: nothing self-checked is recorded, printed or audited, and the Evidence record says so.
        var check = await host.Service.CheckAsync(instance.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted);
        Assert.True(check.Succeeded, check.Reason);
        var checkedInstance = (await host.Store.FindInstanceAsync(instance.Id))!;
        Assert.Null(checkedInstance.SelfCheck);
        Assert.False(Assert.IsType<Evidence>(await host.Context.Repository.FindAsync(instance.EvidenceId!.Value)).Check!.SelfCheck);
        Assert.DoesNotContain(audit.Rows, r => r.Action == CalcInstanceService.SelfCheckedActionName);
        Assert.Null((await host.Service.SheetAsync(checkedInstance)).Header.SelfCheck);
        Assert.DoesNotContain(CalcSheetStandard.SelfCheckPrefix, CalcSheetTextRenderer.Render(await host.Service.SheetAsync(checkedInstance)), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_Recorder_That_Cannot_Write_The_Audit_Row_Leaves_The_Check_Standing_With_A_Warning()
    {
        // D4: the row is written after the check has committed; a failure is a sentence on the result, not an exception out of a check that stands.
        var host = new LibraryTestHost(calculators: [new IndependentStressCalculator()], audit: new FailingAuditRecorder());
        var instance = await host.Service.CreateAsync("test.independent", await host.ProjectAsync(), "E5 circuit 7", LibraryTestHost.ProvenancedInputs());

        var check = await host.Service.CheckAsync(instance.Id, "Sam Preparer", "Tempest Engineering", "OK", CheckOutcome.Accepted, selfCheckStatement: Statement);
        Assert.True(check.Succeeded, check.Reason);
        Assert.Equal("The self-check was recorded, but its audit row could not be written: the audit store is read-only tonight", check.Reason);
        Assert.Equal(CalcInstanceStatus.Checked, (await host.Store.FindInstanceAsync(instance.Id))!.Status);
        Assert.Equal(Statement, (await host.Store.FindInstanceAsync(instance.Id))!.SelfCheck!.Statement);

        var pack = await host.Service.CreatePackAsync(instance.ProjectId, "Pack 7", [(await host.Service.CreateAsync("test.independent", instance.ProjectId, "E5 circuit 8", LibraryTestHost.ProvenancedInputs())).Id]);
        var packCheck = await host.Service.CheckPackAsync(pack.Id, "Sam Preparer", "Tempest Engineering", "OK", CheckOutcome.Accepted, selfCheckStatement: Statement);
        Assert.True(packCheck.Succeeded, packCheck.Reason);
        Assert.StartsWith("The self-check was recorded, but its audit row could not be written", packCheck.Reason, StringComparison.Ordinal);
        Assert.Equal(CalcInstanceStatus.Checked, (await host.Store.FindPackAsync(pack.Id))!.Status);
    }

    [Fact]
    public async Task Without_The_Box_The_Preparer_Is_Still_Refused_And_Told_About_The_Self_Check()
    {
        var (host, _) = HostWithAudit();
        var instance = await host.Service.CreateAsync("test.independent", await host.ProjectAsync(), "E5 circuit 3", LibraryTestHost.ProvenancedInputs());

        var refused = await host.Service.CheckAsync(instance.Id, "Sam Preparer", "Tempest Engineering", "OK", CheckOutcome.Accepted);
        Assert.False(refused.Succeeded);
        Assert.Contains("needs an independent check: sign in as someone other than its author (test.engineer) to check it", refused.Reason, StringComparison.Ordinal);
        Assert.Contains($"tick \"{CalcInstanceService.SelfCheckCaption}\"", refused.Reason, StringComparison.Ordinal);

        // A ticked box with nothing written is the same refusal.
        var blank = await host.Service.CheckAsync(instance.Id, "Sam Preparer", "Tempest Engineering", "OK", CheckOutcome.Accepted, selfCheckStatement: "   ");
        Assert.False(blank.Succeeded);
        Assert.Contains(CalcInstanceService.SelfCheckCaption, blank.Reason, StringComparison.Ordinal);
        Assert.Equal(CalcInstanceStatus.Draft, (await host.Store.FindInstanceAsync(instance.Id))!.Status);
    }

    [Theory]
    [InlineData(19, "at least 20 characters")]
    [InlineData(501, "limited to 500 characters")]
    public async Task The_Statement_Is_At_Least_20_Characters_And_At_Most_500(int length, string refusal)
    {
        var (host, audit) = HostWithAudit();
        var instance = await host.Service.CreateAsync("test.independent", await host.ProjectAsync(), "E5 circuit 4", LibraryTestHost.ProvenancedInputs());

        var refused = await host.Service.CheckAsync(instance.Id, "Sam Preparer", "Tempest Engineering", "OK", CheckOutcome.Accepted, selfCheckStatement: new string('x', length));
        Assert.False(refused.Succeeded);
        Assert.Contains(refusal, refused.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain(audit.Rows, r => r.Action == CalcInstanceService.SelfCheckedActionName);

        // The limits themselves are taken.
        var atLimit = await host.Service.CheckAsync(instance.Id, "Sam Preparer", "Tempest Engineering", "OK", CheckOutcome.Accepted, selfCheckStatement: new string('y', length == 19 ? 20 : 500));
        Assert.True(atLimit.Succeeded, atLimit.Reason);
        Assert.Equal(length == 19 ? 20 : 500, (await host.Store.FindInstanceAsync(instance.Id))!.SelfCheck!.Statement.Length);
    }

    [Fact]
    public async Task A_Template_That_Demands_No_Independent_Check_Takes_No_Self_Check()
    {
        var (host, audit) = HostWithAudit();
        var instance = await host.Service.CreateAsync("test.direct-stress", await host.ProjectAsync(), "Plain", LibraryTestHost.ProvenancedInputs());

        var check = await host.Service.CheckAsync(instance.Id, "Sam Preparer", "Tempest Engineering", "OK", CheckOutcome.Accepted, selfCheckStatement: Statement);
        Assert.True(check.Succeeded, check.Reason);
        Assert.Null((await host.Store.FindInstanceAsync(instance.Id))!.SelfCheck);
        Assert.DoesNotContain(audit.Rows, r => r.Action == CalcInstanceService.SelfCheckedActionName);
    }

    [Fact]
    public async Task A_Rejection_By_The_Preparer_Records_No_Self_Check()
    {
        var (host, audit) = HostWithAudit();
        var instance = await host.Service.CreateAsync("test.independent", await host.ProjectAsync(), "E5 circuit 5", LibraryTestHost.ProvenancedInputs());

        var rejected = await host.Service.CheckAsync(instance.Id, "Sam Preparer", "Tempest Engineering", "The cable size is wrong.", CheckOutcome.Rejected, selfCheckStatement: Statement);
        Assert.False(rejected.Succeeded);
        Assert.Contains("rejected the calculation", rejected.Reason, StringComparison.Ordinal);
        var draft = (await host.Store.FindInstanceAsync(instance.Id))!;
        Assert.Equal(CalcInstanceStatus.Draft, draft.Status);
        Assert.Null(draft.SelfCheck);
        Assert.DoesNotContain(audit.Rows, r => r.Action == CalcInstanceService.SelfCheckedActionName);
    }

    [Fact]
    public async Task A_Revision_Starts_Without_The_Self_Check()
    {
        var (host, _) = HostWithAudit();
        var instance = await host.Service.CreateAsync("test.independent", await host.ProjectAsync(), "E5 circuit 6", LibraryTestHost.ProvenancedInputs());
        Assert.True((await host.Service.CheckAsync(instance.Id, "Sam Preparer", "Tempest Engineering", "OK", CheckOutcome.Accepted, selfCheckStatement: Statement)).Succeeded);
        Assert.True((await host.Service.IssueAsync(instance.Id, "ISS-1", "Client Co")).Succeeded);

        var revised = await host.Service.ReviseAsync(instance.Id);
        Assert.True(revised.Succeeded, revised.Reason);
        Assert.Null(revised.Instance!.SelfCheck);
        Assert.Null((await host.Service.SheetAsync(revised.Instance)).Header.SelfCheck);
    }

    [Fact]
    public async Task A_Pack_Holding_Such_A_Calculation_Takes_A_Self_Check_Which_Its_Sheets_Print_And_Reopening_Clears()
    {
        var (host, audit) = HostWithAudit();
        var project = await host.ProjectAsync();
        var plain = await host.Service.CreateAsync("test.direct-stress", project, "Plain", LibraryTestHost.ProvenancedInputs());
        var strict = await host.Service.CreateAsync("test.independent", project, "Strict", LibraryTestHost.ProvenancedInputs());
        var pack = await host.Service.CreatePackAsync(project, "Circuit pack", [plain.Id, strict.Id]);

        var refused = await host.Service.CheckPackAsync(pack.Id, "Sam Preparer", "Tempest Engineering", "OK", CheckOutcome.Accepted);
        Assert.False(refused.Succeeded);
        Assert.Contains("'Strict' needs an independent check", refused.Reason, StringComparison.Ordinal);
        Assert.Contains(CalcInstanceService.SelfCheckCaption, refused.Reason, StringComparison.Ordinal);

        var tooShort = await host.Service.CheckPackAsync(pack.Id, "Sam Preparer", "Tempest Engineering", "OK", CheckOutcome.Accepted, selfCheckStatement: "too short");
        Assert.False(tooShort.Succeeded);
        Assert.Contains("at least 20 characters", tooShort.Reason, StringComparison.Ordinal);

        var check = await host.Service.CheckPackAsync(pack.Id, "Sam Preparer", "Tempest Engineering", "OK", CheckOutcome.Accepted, selfCheckStatement: Statement);
        Assert.True(check.Succeeded, check.Reason);

        var checkedPack = (await host.Store.FindPackAsync(pack.Id))!;
        Assert.Equal(CalcInstanceStatus.Checked, checkedPack.Status);
        Assert.Equal(Statement, checkedPack.SelfCheck!.Statement);
        Assert.Equal("test.engineer", checkedPack.SelfCheck.Principal);
        Assert.Equal("Sam Preparer", checkedPack.SelfCheck.Name);

        var row = Assert.Single(audit.Rows, r => r.Action == CalcInstanceService.PackSelfCheckedActionName);
        Assert.Equal("Circuit pack", row.Detail["Pack"]);
        Assert.Equal(pack.Id.ToString("N"), row.Detail["PackId"]);
        Assert.Equal("2", row.Detail["Members"]);
        Assert.Equal(Statement, row.Detail["Statement"]);
        Assert.DoesNotContain(audit.Rows, r => r.Action == CalcInstanceService.SelfCheckedActionName);

        // A member's sheet carries the pack's self-check while the pack is checked — the member it is true of (D1), not the plain one.
        Assert.Equal(Statement, (await host.Service.SheetAsync((await host.Store.FindInstanceAsync(strict.Id))!)).Header.SelfCheck);
        Assert.Null((await host.Service.SheetAsync((await host.Store.FindInstanceAsync(plain.Id))!)).Header.SelfCheck);
        Assert.Equal([strict.Id], checkedPack.SelfCheck.MemberIds);
        Assert.Equal(strict.Id.ToString("N"), row.Detail["SelfCheckedMembers"]);

        // Reopened, the pack starts its next revision with no check and no self-check; checked again, it takes a new one.
        var reopened = await host.Service.ReopenPackAsync(pack.Id);
        Assert.True(reopened.Succeeded, reopened.Reason);
        Assert.Null((await host.Store.FindPackAsync(pack.Id))!.SelfCheck);
        Assert.Null((await host.Service.SheetAsync((await host.Store.FindInstanceAsync(strict.Id))!)).Header.SelfCheck);
        Assert.True((await host.Service.CheckPackAsync(pack.Id, "Sam Preparer", "Tempest Engineering", "OK", CheckOutcome.Accepted, selfCheckStatement: Statement)).Succeeded);
        Assert.Equal(2, audit.Rows.Count(r => r.Action == CalcInstanceService.PackSelfCheckedActionName));

        // The issued sheet of the member the self-check is true of prints it; the plain member's sheet, whose template never asked for an
        // independent check, does not (D1: the sentence would be false there).
        var issued = await host.Service.IssuePackAsync(pack.Id, "PACK-1", "Client Co");
        Assert.True(issued.Succeeded, issued.Reason);

        var issuedStrict = (await host.Store.FindInstanceAsync(strict.Id))!;
        Assert.Equal(CalcInstanceStatus.Issued, issuedStrict.Status);
        Assert.Contains("Self-checked by the preparer: " + Statement, issuedStrict.Issue!.SheetText, StringComparison.Ordinal);

        var issuedPlain = (await host.Store.FindInstanceAsync(plain.Id))!;
        Assert.Equal(CalcInstanceStatus.Issued, issuedPlain.Status);
        Assert.DoesNotContain("Self-checked by the preparer", issuedPlain.Issue!.SheetText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_Pack_Self_Check_Is_Printed_Only_On_The_Members_The_Checker_Prepared()
    {
        // D1: Plain-by-B and Strict-by-A and Strict-by-B in one pack; A self-checks it. Only Strict-by-A's sheet reads "Self-checked by the preparer".
        var (host, audit) = HostWithAudit();
        var project = await host.ProjectAsync();
        host.Principal = "engineer.b";
        var plainByB = await host.Service.CreateAsync("test.direct-stress", project, "Plain by B", LibraryTestHost.ProvenancedInputs());
        var strictByB = await host.Service.CreateAsync("test.independent", project, "Strict by B", LibraryTestHost.ProvenancedInputs());
        host.Principal = "engineer.a";
        var strictByA = await host.Service.CreateAsync("test.independent", project, "Strict by A", LibraryTestHost.ProvenancedInputs());
        var pack = await host.Service.CreatePackAsync(project, "Mixed pack", [plainByB.Id, strictByA.Id, strictByB.Id]);

        var check = await host.Service.CheckPackAsync(pack.Id, "A. Preparer", "Tempest Engineering", "OK", CheckOutcome.Accepted, selfCheckStatement: Statement);
        Assert.True(check.Succeeded, check.Reason);
        var checkedPack = (await host.Store.FindPackAsync(pack.Id))!;
        Assert.Equal([strictByA.Id], checkedPack.SelfCheck!.MemberIds);
        Assert.True(checkedPack.SelfCheck.AppliesTo(strictByA.Id));
        Assert.False(checkedPack.SelfCheck.AppliesTo(strictByB.Id));
        Assert.Equal(strictByA.Id.ToString("N"), Assert.Single(audit.Rows, r => r.Action == CalcInstanceService.PackSelfCheckedActionName).Detail["SelfCheckedMembers"]);

        Assert.Equal(Statement, (await host.Service.SheetAsync((await host.Store.FindInstanceAsync(strictByA.Id))!)).Header.SelfCheck);
        Assert.Null((await host.Service.SheetAsync((await host.Store.FindInstanceAsync(strictByB.Id))!)).Header.SelfCheck);
        Assert.Null((await host.Service.SheetAsync((await host.Store.FindInstanceAsync(plainByB.Id))!)).Header.SelfCheck);

        var issued = await host.Service.IssuePackAsync(pack.Id, "PACK-2", "Client Co");
        Assert.True(issued.Succeeded, issued.Reason);
        Assert.Contains("Self-checked by the preparer: " + Statement, (await host.Store.FindInstanceAsync(strictByA.Id))!.Issue!.SheetText, StringComparison.Ordinal);
        Assert.DoesNotContain("Self-checked by the preparer", (await host.Store.FindInstanceAsync(strictByB.Id))!.Issue!.SheetText, StringComparison.Ordinal);
        Assert.DoesNotContain("Self-checked by the preparer", (await host.Store.FindInstanceAsync(plainByB.Id))!.Issue!.SheetText, StringComparison.Ordinal);

        // A record kept before the member list existed applies to nobody rather than to everybody.
        Assert.False(new CalcSelfCheck("p", "n", Statement, DateTimeOffset.UnixEpoch).AppliesTo(strictByA.Id));
    }

    [Fact]
    public async Task A_Pack_Checker_Who_Prepared_None_Of_Its_Independent_Members_Is_Told_When_They_Tick_The_Box()
    {
        // D2 for a pack: the tick is judged over the whole pack — one self-checkable member the checker prepared is enough to take it.
        var (host, _) = HostWithAudit();
        var project = await host.ProjectAsync();
        host.Principal = "engineer.b";
        var strictByB = await host.Service.CreateAsync("test.independent", project, "Strict by B", LibraryTestHost.ProvenancedInputs());
        var pack = await host.Service.CreatePackAsync(project, "B's pack", [strictByB.Id]);

        host.Principal = "engineer.c";
        var ticked = await host.Service.CheckPackAsync(pack.Id, "C. Checker", "Check Co", "OK", CheckOutcome.Accepted, selfCheckStatement: Statement);
        Assert.False(ticked.Succeeded);
        Assert.Equal($"Pack 'B's pack': you are not the preparer of any calculation in it that needs an independent check; untick \"{CalcInstanceService.SelfCheckCaption}\" to record your check as an independent check.", ticked.Reason);
        Assert.Equal(CalcInstanceStatus.Draft, (await host.Store.FindPackAsync(pack.Id))!.Status);

        // Without the box, C checks the pack independently; a plain pack ignores a stray tick as before.
        var check = await host.Service.CheckPackAsync(pack.Id, "C. Checker", "Check Co", "OK", CheckOutcome.Accepted);
        Assert.True(check.Succeeded, check.Reason);
        Assert.Null((await host.Store.FindPackAsync(pack.Id))!.SelfCheck);

        var plain = await host.Service.CreateAsync("test.direct-stress", project, "Plain", LibraryTestHost.ProvenancedInputs());
        var plainPack = await host.Service.CreatePackAsync(project, "Plain pack", [plain.Id]);
        Assert.True((await host.Service.CheckPackAsync(plainPack.Id, "C. Checker", "Check Co", "OK", CheckOutcome.Accepted, selfCheckStatement: Statement)).Succeeded);
        Assert.Null((await host.Store.FindPackAsync(plainPack.Id))!.SelfCheck);
    }

    [Theory]
    [InlineData("struct.lifting")]
    [InlineData("mech.lifting-lug")]
    public async Task S8_Lifting_Keeps_Its_Multi_Person_Rule_And_Takes_No_Self_Check(string id)
    {
        ICalcCalculator calculator = id == "struct.lifting" ? new Tempest.Calc.Structural.LiftingLugCalculator() : new Tempest.Calc.Mechanical.LiftingLugCalculator();
        var audit = new RecordingAuditRecorder();
        var host = new LibraryTestHost(calculators: [calculator], features: new CalcFeatureFlags([CalcFeatures.Lifting]), audit: audit);
        host.Principal = "designer";
        var basis = CalcProvenance.Assumed($"Worked inputs of the {id} test case.");
        var instance = await host.Service.CreateAsync(id, await host.ProjectAsync(), "Lug", [.. calculator.Template.TestCases[0].Inputs.Select(v => v with { Provenance = basis })]);

        host.Principal = "competent.person";
        Assert.True((await host.Service.AttestAsync(instance.Id, "Pat Competent", "CEng MIMechE", "Reviewed the lug geometry, the material certificate and this calculation.")).Succeeded);

        // The designer cannot check their own lifting calculation, statement or not: the refusal is the plain one, with no self-check offered.
        host.Principal = "designer";
        var refused = await host.Service.CheckAsync(instance.Id, "Chris Checker", "Check Co", "ok", CheckOutcome.Accepted, selfCheckStatement: Statement);
        Assert.False(refused.Succeeded);
        Assert.Contains("needs an independent check: sign in as someone other than", refused.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain(CalcInstanceService.SelfCheckCaption, refused.Reason, StringComparison.Ordinal);
        Assert.Equal(CalcInstanceStatus.Draft, (await host.Store.FindInstanceAsync(instance.Id))!.Status);
        Assert.Empty(audit.Rows);

        // A pack holding it is refused in the same words.
        var pack = await host.Service.CreatePackAsync(instance.ProjectId, "Lifting pack", [instance.Id]);
        var packRefused = await host.Service.CheckPackAsync(pack.Id, "Chris Checker", "Check Co", "ok", CheckOutcome.Accepted, selfCheckStatement: Statement);
        Assert.False(packRefused.Succeeded);
        Assert.DoesNotContain(CalcInstanceService.SelfCheckCaption, packRefused.Reason, StringComparison.Ordinal);

        // A different person checks, as the rule always said; nothing self-checked is recorded.
        host.Principal = "checker.one";
        var check = await host.Service.CheckAsync(instance.Id, "Chris Checker", "Check Co", "ok", CheckOutcome.Accepted, selfCheckStatement: Statement);
        Assert.True(check.Succeeded, check.Reason);
        Assert.Null((await host.Store.FindInstanceAsync(instance.Id))!.SelfCheck);
        Assert.Empty(audit.Rows);
    }

    [Fact]
    public void The_Caption_And_Limits_Are_The_Ones_The_Owner_Set()
    {
        Assert.Equal("Self-check (no second person available)", CalcInstanceService.SelfCheckCaption);
        Assert.Equal(20, CalcInstanceService.MinSelfCheckStatementLength);
        Assert.Equal(500, CalcInstanceService.MaxSelfCheckStatementLength);
        Assert.Equal("Self-checked by the preparer: ", CalcSheetStandard.SelfCheckPrefix);
    }
}
