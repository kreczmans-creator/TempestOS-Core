using Tempest.Calc.Library;
using Tempest.Calc.Sheets;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Evidence;
using Tempest.Core.Persistence;

namespace Tempest.Calc.Tests.Library;

public sealed class CalcInstanceServiceTests
{
    [Fact]
    public async Task Create_Makes_A_Draft_With_Evidence_Register_Entry_And_Outputs()
    {
        var host = new LibraryTestHost();
        var project = await host.ProjectAsync();
        var instance = await host.Service.CreateAsync("test.direct-stress", project, "CALC-001 Hanger", LibraryTestHost.ProvenancedInputs());

        Assert.Equal(CalcInstanceStatus.Draft, instance.Status);
        Assert.Equal("test.direct-stress@1.0.0", instance.TemplateReference);
        Assert.NotNull(instance.EvidenceId);
        Assert.NotNull(instance.RegisterObjectId);
        Assert.Contains(instance.Outputs, o => o.Symbol == "sigma" && o.Text == "60 MPa");
        Assert.NotNull(instance.OutputsHash);

        var evidence = await host.Context.Repository.FindAsync(instance.EvidenceId!.Value) as Evidence;
        Assert.NotNull(evidence);
        Assert.Equal(EvidenceStatus.Draft, evidence!.Status);
        Assert.Equal(EvidenceClassification.Calculation, evidence.Classification);
        Assert.Equal(instance.RegisterObjectId, evidence.SubjectId);

        var register = await host.Context.Repository.FindAsync(instance.RegisterObjectId!.Value) as Calculation;
        Assert.NotNull(register);
        Assert.Equal("CALC-001 Hanger", register!.DisplayName);
        Assert.Equal(project, (register as IHasParent)!.ParentId);
        Assert.Contains(await register.GetRelationshipsAsync(), r => r.TargetId == instance.EvidenceId && r.RelationshipKind == CalcInstanceService.DocumentedByRelationship);

        var stored = await host.Store.FindInstanceAsync(instance.Id);
        Assert.NotNull(stored);
        Assert.Equal(instance.Name, stored!.Name);
        Assert.Single(await host.Store.ListInstancesAsync(project));
    }

    [Fact]
    public async Task Create_Without_A_Project_Has_No_Register_Entry()
    {
        var host = new LibraryTestHost(withProject: false);
        var instance = await host.Service.CreateAsync("test.direct-stress", null, "Loose calc");
        Assert.Null(instance.RegisterObjectId);
        Assert.NotNull(instance.EvidenceId);
    }

    [Fact]
    public async Task Create_Refuses_An_Unknown_Template()
    {
        var host = new LibraryTestHost();
        await Assert.ThrowsAsync<KeyNotFoundException>(() => host.Service.CreateAsync("nope", null, "x"));
    }

    [Fact]
    public async Task Update_Reruns_And_Refuses_After_Check()
    {
        var host = new LibraryTestHost();
        var instance = await host.Service.CreateAsync("test.direct-stress", await host.ProjectAsync(), "C1", LibraryTestHost.ProvenancedInputs());
        var updated = await host.Service.UpdateInputsAsync(instance.Id, LibraryTestHost.ProvenancedInputs(f: 24));
        Assert.True(updated.Succeeded);
        Assert.Contains(updated.Instance!.Outputs, o => o.Symbol == "sigma" && o.Text == "120 MPa");
        Assert.NotEqual(instance.OutputsHash, updated.Instance.OutputsHash);

        var check = await host.Service.CheckAsync(instance.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted);
        Assert.True(check.Succeeded, check.Reason);
        Assert.Equal(CalcInstanceStatus.Checked, check.Instance!.Status);

        var refused = await host.Service.UpdateInputsAsync(instance.Id, LibraryTestHost.ProvenancedInputs(f: 1));
        Assert.False(refused.Succeeded);
        Assert.Contains("only a draft takes new inputs", refused.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Check_Is_Refused_While_Blocked()
    {
        var host = new LibraryTestHost();
        var inputs = LibraryTestHost.ProvenancedInputs(f: -5).Select(i => i.Symbol == "slender" ? i with { Flag = true } : i).ToList();
        var instance = await host.Service.CreateAsync("test.direct-stress", await host.ProjectAsync(), "C-blocked", inputs);
        Assert.NotEmpty(instance.Blocks);
        var check = await host.Service.CheckAsync(instance.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted);
        Assert.False(check.Succeeded);
        Assert.Contains("blocked", check.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Issue_Requires_Check_Then_Snapshots_Everything_In_One_Commit()
    {
        var host = new LibraryTestHost();
        var project = await host.ProjectAsync();
        var instance = await host.Service.CreateAsync("test.direct-stress", project, "C2", LibraryTestHost.ProvenancedInputs());

        var early = await host.Service.IssueAsync(instance.Id, "ISS-1", "Client Co");
        Assert.False(early.Succeeded);
        Assert.Contains("check it before issuing", early.Reason, StringComparison.Ordinal);

        Assert.True((await host.Service.CheckAsync(instance.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted)).Succeeded);
        var commits = host.Backing.CommitCount;
        var issued = await host.Service.IssueAsync(instance.Id, "ISS-1", "Client Co");
        Assert.True(issued.Succeeded, issued.Reason);

        // One commit for the issue itself; the attachment-id touch-up and the register completion follow it.
        var stored = await host.Store.FindInstanceAsync(instance.Id);
        Assert.Equal(CalcInstanceStatus.Issued, stored!.Status);
        Assert.Equal("ISS-1", stored.IssueReference);
        Assert.NotNull(stored.Issue);
        Assert.Contains("CALCULATION SHEET — ISSUED", stored.Issue!.SheetText, StringComparison.Ordinal);
        Assert.Contains("formula=sigma = F / A", stored.Issue.TemplateText, StringComparison.Ordinal);
        Assert.Equal("calc-engine/1.0.0", stored.Issue.EngineVersion);
        Assert.Equal(2, stored.Issue.KnownAnswerTotal);
        Assert.Equal(2, stored.Issue.KnownAnswerPassed);
        Assert.NotNull(stored.Issue.SheetAttachmentId);
        Assert.True(host.Backing.CommitCount - commits <= 3);

        var evidence = (Evidence)(await host.Context.Repository.FindAsync(instance.EvidenceId!.Value))!;
        Assert.Equal(EvidenceStatus.Issued, evidence.Status);
        Assert.Equal("ISS-1", evidence.Issue!.IssueReference);
        var attachment = Assert.Single(await evidence.GetAttachmentsAsync());
        Assert.Equal("text/plain", attachment.ContentType);
        var content = await evidence.ReadAttachmentContentAsync(attachment.Id);
        Assert.Contains("4. WORKING", System.Text.Encoding.UTF8.GetString(content.Bytes), StringComparison.Ordinal);

        var register = (Calculation)(await host.Context.Repository.FindAsync(instance.RegisterObjectId!.Value))!;
        Assert.True(register.Completed);
    }

    [Fact]
    public async Task Check_And_Issue_Are_Refused_Without_Provenance()
    {
        // The check demands what the issue will demand, so a checked calculation is never a dead end (ADR-0166, Black B-13).
        var host = new LibraryTestHost();
        var inputs = LibraryTestHost.ProvenancedInputs().Select(i => i.Symbol == "A" ? i with { Provenance = CalcProvenance.Manual } : i).ToList();
        var instance = await host.Service.CreateAsync("test.direct-stress", await host.ProjectAsync(), "C3", inputs);
        var checkResult = await host.Service.CheckAsync(instance.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted);
        Assert.False(checkResult.Succeeded);
        Assert.StartsWith("A needs a source", checkResult.Reason, StringComparison.Ordinal);
        Assert.Equal(CalcInstanceStatus.Draft, (await host.Store.FindInstanceAsync(instance.Id))!.Status);

        var issued = await host.Service.IssueAsync(instance.Id, "ISS-2", "Client Co");
        Assert.False(issued.Succeeded);
        Assert.Contains("check it before issuing", issued.Reason, StringComparison.Ordinal);

        // With the source supplied the same draft checks and issues.
        Assert.True((await host.Service.UpdateInputsAsync(instance.Id, LibraryTestHost.ProvenancedInputs())).Succeeded);
        Assert.True((await host.Service.CheckAsync(instance.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted)).Succeeded);
        Assert.True((await host.Service.IssueAsync(instance.Id, "ISS-2", "Client Co")).Succeeded);
    }

    [Fact]
    public async Task Rejected_Check_Leaves_Instance_And_Evidence_Draft_And_Can_Be_Checked_Again()
    {
        // ADR-0166, Black B-12: a rejection is kept on the instance; the Evidence stays Draft.
        var host = new LibraryTestHost();
        var instance = await host.Service.CreateAsync("test.direct-stress", await host.ProjectAsync(), "C3R", LibraryTestHost.ProvenancedInputs());
        var rejected = await host.Service.CheckAsync(instance.Id, "J. Reviewer", "Client Co", "Section too small", CheckOutcome.Rejected);
        Assert.False(rejected.Succeeded);
        Assert.Contains("rejected", rejected.Reason, StringComparison.Ordinal);

        var stored = (await host.Store.FindInstanceAsync(instance.Id))!;
        Assert.Equal(CalcInstanceStatus.Draft, stored.Status);
        Assert.Contains("Section too small", stored.LastRejection, StringComparison.Ordinal);
        Assert.Equal(EvidenceStatus.Draft, ((Evidence)(await host.Context.Repository.FindAsync(instance.EvidenceId!.Value))!).Status);

        Assert.True((await host.Service.UpdateInputsAsync(instance.Id, LibraryTestHost.ProvenancedInputs())).Succeeded);
        Assert.True((await host.Service.CheckAsync(instance.Id, "J. Reviewer", "Client Co", "OK now", CheckOutcome.Accepted)).Succeeded);
        Assert.True((await host.Service.IssueAsync(instance.Id, "ISS-3", "Client Co")).Succeeded);
    }

    [Fact]
    public async Task Empty_Checker_Or_Issue_Fields_Are_Refused_Not_Thrown()
    {
        // Black B-14.
        var host = new LibraryTestHost();
        var instance = await host.Service.CreateAsync("test.direct-stress", await host.ProjectAsync(), "C3E", LibraryTestHost.ProvenancedInputs());
        Assert.Equal("A checker name is needed.", (await host.Service.CheckAsync(instance.Id, " ", "Client Co", "OK", CheckOutcome.Accepted)).Reason);
        Assert.Equal("The checker's organisation is needed.", (await host.Service.CheckAsync(instance.Id, "J. Reviewer", "", "OK", CheckOutcome.Accepted)).Reason);
        Assert.True((await host.Service.CheckAsync(instance.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted)).Succeeded);
        Assert.Equal("An issue reference is needed.", (await host.Service.IssueAsync(instance.Id, "", "Client Co")).Reason);
        Assert.Equal("A client is needed.", (await host.Service.IssueAsync(instance.Id, "ISS-4", " ")).Reason);
    }

    [Fact]
    public async Task Crash_Mid_Issue_Leaves_Instance_Checked_And_Evidence_Checked()
    {
        var host = new LibraryTestHost();
        var instance = await host.Service.CreateAsync("test.direct-stress", await host.ProjectAsync(), "C4", LibraryTestHost.ProvenancedInputs());
        Assert.True((await host.Service.CheckAsync(instance.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted)).Succeeded);

        host.Failing.FailNextCommit = true;
        await Assert.ThrowsAsync<PersistenceStoreUnavailableException>(() => host.Service.IssueAsync(instance.Id, "ISS-3", "Client Co"));

        var stored = await host.Store.FindInstanceAsync(instance.Id);
        Assert.Equal(CalcInstanceStatus.Checked, stored!.Status);
        Assert.Null(stored.Issue);
        var evidence = (Evidence)(await host.Context.Repository.FindAsync(instance.EvidenceId!.Value))!;
        Assert.Equal(EvidenceStatus.Checked, evidence.Status);
        Assert.Empty(await evidence.GetAttachmentsAsync());
        Assert.Empty(host.Backing.CommittedKeys(AttachmentContentStore.ContentCollectionName));

        // And the retry succeeds cleanly.
        var retry = await host.Service.IssueAsync(instance.Id, "ISS-3", "Client Co");
        Assert.True(retry.Succeeded, retry.Reason);
    }

    [Fact]
    public async Task Issued_Instance_Is_Immutable_In_The_Store()
    {
        var host = new LibraryTestHost();
        var instance = await host.Service.CreateAsync("test.direct-stress", await host.ProjectAsync(), "C5", LibraryTestHost.ProvenancedInputs());
        Assert.True((await host.Service.CheckAsync(instance.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted)).Succeeded);
        Assert.True((await host.Service.IssueAsync(instance.Id, "ISS-4", "Client Co")).Succeeded);

        var stored = (await host.Store.FindInstanceAsync(instance.Id))!;
        await Assert.ThrowsAsync<CalcInstanceImmutableException>(() => host.Store.SaveInstanceAsync(stored with { Name = "tampered" }));
        await Assert.ThrowsAsync<CalcInstanceImmutableException>(() => host.Store.SaveInstanceAsync(stored with { Status = CalcInstanceStatus.Draft }));
        await Assert.ThrowsAsync<CalcInstanceImmutableException>(() => host.Store.DeleteDraftInstanceAsync(instance.Id));
        Assert.False((await host.Service.UpdateInputsAsync(instance.Id, LibraryTestHost.ProvenancedInputs(f: 1))).Succeeded);
    }

    [Fact]
    public async Task Revise_Supersedes_And_Starts_Revision_B()
    {
        var host = new LibraryTestHost();
        var project = await host.ProjectAsync();
        var instance = await host.Service.CreateAsync("test.direct-stress", project, "C6", LibraryTestHost.ProvenancedInputs());
        Assert.True((await host.Service.CheckAsync(instance.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted)).Succeeded);
        Assert.True((await host.Service.IssueAsync(instance.Id, "ISS-5", "Client Co")).Succeeded);

        var revised = await host.Service.ReviseAsync(instance.Id);
        Assert.True(revised.Succeeded, revised.Reason);
        Assert.Equal("B", revised.Instance!.Revision);
        Assert.Equal(CalcInstanceStatus.Draft, revised.Instance.Status);
        Assert.Equal(instance.Id, revised.Instance.PredecessorId);
        Assert.NotEqual(instance.EvidenceId, revised.Instance.EvidenceId);

        var old = (await host.Store.FindInstanceAsync(instance.Id))!;
        Assert.Equal(CalcInstanceStatus.Superseded, old.Status);
        Assert.NotNull(old.Issue);
        Assert.Equal(2, (await host.Store.ListInstancesAsync(project)).Count);
    }

    [Fact]
    public async Task Staleness_Flags_Template_Change_And_Upstream_Change()
    {
        var host = new LibraryTestHost();
        var project = await host.ProjectAsync();
        var upstream = await host.Service.CreateAsync("test.direct-stress", project, "UP", LibraryTestHost.ProvenancedInputs());
        var inputs = LibraryTestHost.ProvenancedInputs().Select(i => i.Symbol == "F" ? i with { Provenance = CalcProvenance.FromReference(CalcInstance.UpstreamPrefix + upstream.Id.ToString("N")) } : i).ToList();
        var downstream = await host.Service.CreateAsync("test.direct-stress", project, "DOWN", inputs);

        Assert.False((await host.Service.StalenessAsync(downstream)).IsStale);

        Assert.True((await host.Service.UpdateInputsAsync(upstream.Id, LibraryTestHost.ProvenancedInputs(f: 99))).Succeeded);
        var stale = await host.Service.StalenessAsync(downstream);
        Assert.True(stale.IsStale);
        Assert.Contains(stale.Reasons, r => r.Contains("'UP' has changed its outputs", StringComparison.Ordinal));

        var hashChanged = downstream with { TemplateHash = "0000" };
        Assert.Contains((await host.Service.StalenessAsync(hashChanged)).Reasons, r => r.Contains("content changed", StringComparison.Ordinal));

        var versionChanged = downstream with { TemplateReference = "test.direct-stress@0.9.0" };
        Assert.Contains((await host.Service.StalenessAsync(versionChanged)).Reasons, r => r.Contains("newer version", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Stale_Instance_Cannot_Be_Issued()
    {
        var host = new LibraryTestHost();
        var project = await host.ProjectAsync();
        var upstream = await host.Service.CreateAsync("test.direct-stress", project, "UP2", LibraryTestHost.ProvenancedInputs());
        var inputs = LibraryTestHost.ProvenancedInputs().Select(i => i.Symbol == "F" ? i with { Provenance = CalcProvenance.FromReference(CalcInstance.UpstreamPrefix + upstream.Id.ToString("N")) } : i).ToList();
        var downstream = await host.Service.CreateAsync("test.direct-stress", project, "DOWN2", inputs);
        Assert.True((await host.Service.CheckAsync(downstream.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted)).Succeeded);
        Assert.True((await host.Service.UpdateInputsAsync(upstream.Id, LibraryTestHost.ProvenancedInputs(f: 50))).Succeeded);

        var issued = await host.Service.IssueAsync(downstream.Id, "ISS-6", "Client Co");
        Assert.False(issued.Succeeded);
        Assert.Contains("is stale", issued.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Pack_Issues_Every_Member_Under_One_Sheet_In_One_Commit()
    {
        var host = new LibraryTestHost();
        var project = await host.ProjectAsync();
        var a = await host.Service.CreateAsync("test.direct-stress", project, "PA", LibraryTestHost.ProvenancedInputs());
        var b = await host.Service.CreateAsync("test.direct-stress", project, "PB", LibraryTestHost.ProvenancedInputs(f: 20));
        var pack = await host.Service.CreatePackAsync(project, "Bracket pack", [a.Id, b.Id]);
        Assert.Equal(CalcInstanceStatus.Draft, pack.Status);
        Assert.Equal(pack.Id, (await host.Store.FindInstanceAsync(a.Id))!.PackId);

        var check = await host.Service.CheckPackAsync(pack.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted);
        Assert.True(check.Succeeded, check.Reason);

        var commits = host.Backing.CommitCount;
        var issued = await host.Service.IssuePackAsync(pack.Id, "ISS-PACK", "Client Co");
        Assert.True(issued.Succeeded, issued.Reason);

        var storedPack = (await host.Store.FindPackAsync(pack.Id))!;
        Assert.Equal(CalcInstanceStatus.Issued, storedPack.Status);
        Assert.Equal(CalcInstanceStatus.Issued, (await host.Store.FindInstanceAsync(a.Id))!.Status);
        Assert.Equal(CalcInstanceStatus.Issued, (await host.Store.FindInstanceAsync(b.Id))!.Status);
        Assert.Equal("ISS-PACK", (await host.Store.FindInstanceAsync(b.Id))!.IssueReference);

        var evidence = (Evidence)(await host.Context.Repository.FindAsync(pack.EvidenceId!.Value))!;
        Assert.Equal(EvidenceStatus.Issued, evidence.Status);
        var attachment = Assert.Single(await evidence.GetAttachmentsAsync());
        var text = System.Text.Encoding.UTF8.GetString((await evidence.ReadAttachmentContentAsync(attachment.Id)).Bytes);
        Assert.Contains("PACK INDEX", text, StringComparison.Ordinal);
        Assert.Contains("1. PA", text, StringComparison.Ordinal);
        Assert.Contains("2. PB", text, StringComparison.Ordinal);
        Assert.Contains("REVISIONS", text, StringComparison.Ordinal);
        Assert.True(host.Backing.CommitCount - commits <= 1 + 2);
    }

    [Fact]
    public async Task Pack_Crash_Mid_Issue_Leaves_Every_Member_Unissued()
    {
        var host = new LibraryTestHost();
        var project = await host.ProjectAsync();
        var a = await host.Service.CreateAsync("test.direct-stress", project, "PA2", LibraryTestHost.ProvenancedInputs());
        var pack = await host.Service.CreatePackAsync(project, "Pack 2", [a.Id]);
        Assert.True((await host.Service.CheckPackAsync(pack.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted)).Succeeded);
        host.Failing.FailNextCommit = true;
        await Assert.ThrowsAsync<PersistenceStoreUnavailableException>(() => host.Service.IssuePackAsync(pack.Id, "ISS-P2", "Client Co"));
        Assert.Equal(CalcInstanceStatus.Draft, (await host.Store.FindInstanceAsync(a.Id))!.Status);
        Assert.Equal(CalcInstanceStatus.Checked, (await host.Store.FindPackAsync(pack.Id))!.Status);
    }

    [Fact]
    public async Task Sheet_Of_A_Draft_Shows_Draft_Watermark_And_Live_Footer()
    {
        var host = new LibraryTestHost();
        var instance = await host.Service.CreateAsync("test.direct-stress", await host.ProjectAsync(), "C7", LibraryTestHost.ProvenancedInputs());
        var run = await host.Service.RunAsync(instance.Id);
        Assert.NotNull(run);
        Assert.StartsWith("CALCULATION SHEET — DRAFT", run!.SheetText, StringComparison.Ordinal);
        Assert.Contains("Calc test project", run.SheetText, StringComparison.Ordinal);
        Assert.Contains("Known-answer cases passed: 2/2", run.SheetText, StringComparison.Ordinal);
        Assert.Contains("Application: TempestOS test", run.SheetText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Preview_Runs_Without_Saving()
    {
        var host = new LibraryTestHost();
        var result = host.Service.Preview("test.direct-stress", new CalcInputSet(LibraryTestHost.ProvenancedInputs(f: 6)));
        Assert.Equal(30e6, result.OutputSi("sigma"), 3);
        Assert.Empty(await host.Store.ListInstancesAsync());
    }

    [Fact]
    public async Task Instances_Are_Indexed_For_Search()
    {
        var host = new LibraryTestHost();
        await host.Service.CreateAsync("test.direct-stress", await host.ProjectAsync(), "Hanger bracket HB-7", LibraryTestHost.ProvenancedInputs());
        var hits = await host.Backing.SearchAsync("HB-7", 10);
        Assert.Contains(hits, h => h.Kind == CalcInstanceStore.InstanceKind);
    }

    [Fact]
    public void Catalogue_Resolves_Current_Version_And_Refuses_Duplicates()
    {
        var catalogue = new CalcCatalogue();
        var v1 = new VersionedCalculator("1.0.0");
        var v2 = new VersionedCalculator("1.2.0");
        catalogue.Register(v1);
        catalogue.Register(v2);
        Assert.Equal("1.2.0", catalogue.Find("test.versioned")!.Template.Version);
        Assert.Equal("1.0.0", catalogue.Find("test.versioned@1.0.0")!.Template.Version);
        Assert.Throws<InvalidOperationException>(() => catalogue.Register(new VersionedCalculator("1.2.0")));
        Assert.Null(catalogue.Find("zz"));
        Assert.Single(catalogue.Current());
        Assert.Equal(2, catalogue.All().Count);
    }

    [Theory]
    [InlineData("bad id!", "1.0.0")]
    [InlineData("ok", "1.0.0-beta")]
    [InlineData("", "1")]
    public void Catalogue_Refuses_Bad_Ids_And_Versions(string id, string version) =>
        Assert.Throws<ArgumentException>(() => new CalcCatalogue().Register(new VersionedCalculator(version, id)));

    [Theory]
    [InlineData("A", "B")]
    [InlineData("Y", "Z")]
    [InlineData("3", "4")]
    [InlineData("Z", "Z+")]
    public void Next_Revision(string from, string to) => Assert.Equal(to, CalcInstanceService.NextRevision(from));

    [Theory]
    [InlineData("../../etc/passwd", "----etcpasswd")]
    [InlineData("C:\\x\\y", "Cxy")]
    [InlineData("", "calc")]
    [InlineData("Hanger bracket HB-7", "Hanger-bracket-HB-7")]
    public void Safe_File_Names(string input, string expected) => Assert.Equal(expected, CalcInstanceService.Safe(input));

    private sealed class VersionedCalculator(string version, string id = "test.versioned") : ICalcCalculator
    {
        public CalcTemplate Template { get; } = new(id, version, CalcDiscipline.Mechanical, "Versioned", "", "", [new CalcInputSpec("x", "x", CalcInputKind.Number, Default: 1)], [], [], [], []);

        public void Run(CalcRunContext context)
        {
            context.Input("x");
            context.Assign("y", "x * 2");
            context.Output("y", "double");
        }
    }
}
